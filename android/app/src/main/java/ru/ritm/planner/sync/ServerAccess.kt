package ru.ritm.planner.sync

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import com.hierynomus.msdtyp.AccessMask
import com.hierynomus.msfscc.FileAttributes
import com.hierynomus.mssmb2.SMB2CreateDisposition
import com.hierynomus.mssmb2.SMB2CreateOptions
import com.hierynomus.mssmb2.SMB2ShareAccess
import com.hierynomus.smbj.SMBClient
import com.hierynomus.smbj.SmbConfig
import com.hierynomus.smbj.auth.AuthenticationContext
import com.hierynomus.smbj.share.DiskShare
import ru.ritm.planner.data.RitmException
import ru.ritm.planner.data.TeamPath
import ru.ritm.planner.data.newId
import java.io.File
import java.net.InetSocketAddress
import java.net.Socket
import java.security.KeyStore
import java.util.EnumSet
import java.util.concurrent.TimeUnit
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Прямой доступ к папке на сервере. Сервер виден только из рабочей сети, поэтому в мобильной сети попыток нет. */
object ServerAccess {
    fun onLocalNetwork(context: Context): Boolean {
        val cm = context.getSystemService(ConnectivityManager::class.java)
        val caps = cm.getNetworkCapabilities(cm.activeNetwork) ?: return false
        return caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) || caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) ||
            caps.hasTransport(NetworkCapabilities.TRANSPORT_VPN)
    }

    fun open(path: String, login: String, password: String, domain: String): TeamFolder {
        val target = TeamPath.parse(path) ?: throw RitmException("Это не сетевой путь: $path")
        // Недоступный сервер отвечает десятками секунд — проверяем порт быстро и сразу уходим на резервный канал.
        Socket().use { it.connect(InetSocketAddress(target.host, 445), 3000) }
        return SmbFolder(target, login, password, domain)
    }
}

private class SmbFolder(private val target: TeamPath, login: String, password: String, domain: String) : TeamFolder {
    private val client = SMBClient(SmbConfig.builder().withTimeout(20, TimeUnit.SECONDS).withSoTimeout(20, TimeUnit.SECONDS).build())
    private val connection = client.connect(target.host)
    private val session = connection.authenticate(
        if (login.isBlank()) AuthenticationContext.guest() else AuthenticationContext(login, password.toCharArray(), domain.ifBlank { null }),
    )
    private val share = session.connectShare(target.share) as? DiskShare ?: throw RitmException("«${target.share}» — не общая папка.")

    override fun list(dir: String) = share.list(target.join(dir))
        .filter { it.fileName != "." && it.fileName != ".." }
        .map { TeamFolder.Entry(it.fileName, it.lastWriteTime.toEpochMillis()) }

    override fun read(path: String): ByteArray = share.openFile(
        target.join(path), EnumSet.of(AccessMask.GENERIC_READ), null, SMB2ShareAccess.ALL, SMB2CreateDisposition.FILE_OPEN, null,
    ).use { file -> file.inputStream.use { it.readBytes() } }

    override fun write(path: String, bytes: ByteArray) {
        val final = target.join(path)
        val temp = final + "." + newId().replace("-", "") + ".tmp"
        share.openFile(
            temp, EnumSet.of(AccessMask.GENERIC_WRITE, AccessMask.DELETE), EnumSet.of(FileAttributes.FILE_ATTRIBUTE_NORMAL),
            SMB2ShareAccess.ALL, SMB2CreateDisposition.FILE_OVERWRITE_IF, EnumSet.of(SMB2CreateOptions.FILE_NON_DIRECTORY_FILE),
        ).use { file ->
            file.outputStream.use { it.write(bytes) }
            file.rename(final, true)
        }
    }

    override fun close() {
        runCatching { session.close() }
        runCatching { connection.close() }
        runCatching { client.close() }
    }
}

/** Пароль сервера лежит только на телефоне, зашифрованный ключом из аппаратного хранилища Android. */
class ServerPassword(context: Context) {
    private val file = File(context.filesDir, "server.bin")

    private fun key(): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey(ALIAS, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())
        }.generateKey()
    }

    fun save(password: String) {
        if (password.isEmpty()) { file.delete(); return }
        val cipher = Cipher.getInstance(MODE).apply { init(Cipher.ENCRYPT_MODE, key()) }
        file.writeBytes(cipher.iv + cipher.doFinal(password.toByteArray(Charsets.UTF_8)))
    }

    /** Если копию данных перенесли на другой телефон, ключа там нет — пароль придётся ввести заново. */
    fun read(): String = runCatching {
        val bytes = file.readBytes()
        val cipher = Cipher.getInstance(MODE).apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, bytes, 0, IV)) }
        String(cipher.doFinal(bytes, IV, bytes.size - IV), Charsets.UTF_8)
    }.getOrDefault("")

    private companion object {
        const val ALIAS = "ritm-server"
        const val MODE = "AES/GCM/NoPadding"
        const val IV = 12
    }
}
