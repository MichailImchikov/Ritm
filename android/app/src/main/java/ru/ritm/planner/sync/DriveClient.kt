package ru.ritm.planner.sync

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put
import kotlinx.serialization.json.putJsonArray
import kotlinx.serialization.json.JsonPrimitive
import ru.ritm.planner.data.RitmException
import ru.ritm.planner.data.RitmJson
import ru.ritm.planner.data.Store
import ru.ritm.planner.data.TaskItem
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder

class AuthExpired : Exception("Доступ Google истёк.")

/**
 * Протокол облака как в GoogleDriveClient.cs: каждая версия записи — неизменяемый файл
 * ritm-v2-<id>-<sha256 содержимого>.json в скрытой папке приложения (appDataFolder).
 * Файлы видны десктопу, если OAuth-клиенты Android и Desktop созданы в одном проекте Google Cloud.
 */
class DriveClient(
    private val cacheDir: File,
    private val token: suspend () -> String,
    private val transport: Transport = HttpTransport,
) {
    data class Pulled(val items: List<TaskItem>, val names: Set<String>)

    fun interface Transport {
        /** Возвращает (код ответа, тело). */
        fun send(method: String, url: String, token: String, contentType: String?, body: ByteArray?): Pair<Int, ByteArray>
    }

    suspend fun pull(): Pulled = withContext(Dispatchers.IO) {
        cacheDir.mkdirs()
        val result = mutableListOf<TaskItem>()
        val seen = mutableSetOf<String>()
        var page: String? = null
        do {
            val url = "$API/files?spaces=appDataFolder&pageSize=1000&fields=nextPageToken,files(id,name)&q=" +
                enc("trashed = false and name contains 'ritm-v2-'") + (page?.let { "&pageToken=" + enc(it) } ?: "")
            val listing = Json.parseToJsonElement(String(call("GET", url), Charsets.UTF_8)).jsonObject
            val files = listing["files"] as? JsonArray ?: throw RitmException("Не удалось прочитать список файлов Ритма.")
            for (f in files) {
                val o = f.jsonObject
                val id = o["id"]?.jsonPrimitive?.content ?: continue
                val name = o["name"]?.jsonPrimitive?.content ?: continue
                if (!name.startsWith("ritm-v2-") || !name.endsWith(".json")) continue
                if (id.length > 200 || id.any { !it.isLetterOrDigit() && it != '_' && it != '-' })
                    throw RitmException("Некорректный идентификатор файла Google.")
                val cached = File(cacheDir, "$id.json")
                val bytes = if (cached.exists()) cached.readBytes() else call("GET", "$API/files/${enc(id)}?alt=media").also {
                    if (it.size > 2_000_000) throw RitmException("Слишком большой файл синхронизации.")
                }
                val item = runCatching { RitmJson.decodeFromString(TaskItem.serializer(), String(bytes, Charsets.UTF_8)).validated() }
                    .getOrElse { throw RitmException("Не удалось прочитать облачную задачу.") }
                if (eventName(item.id, bytes) != name) throw RitmException("Облачный файл Ритма не прошёл проверку целостности.")
                if (!cached.exists()) cached.writeBytes(bytes)
                seen += name
                result += item
            }
            page = (listing["nextPageToken"] as? JsonPrimitive)?.content
        } while (!page.isNullOrEmpty())
        Pulled(result, seen)
    }

    /** Загружает версии, которых ещё нет в облаке. Возвращает число загруженных файлов. */
    suspend fun push(items: List<TaskItem>, cloud: Pulled): Int = withContext(Dispatchers.IO) {
        val present = cloud.items.map { key(it) }.toSet()
        var uploaded = 0
        for (item in items) {
            if (key(item) in present) continue
            val bytes = RitmJson.encodeToString(TaskItem.serializer(), item).toByteArray(Charsets.UTF_8)
            val name = eventName(item.id, bytes)
            if (name in cloud.names) continue
            val meta = buildJsonObject {
                put("name", name)
                putJsonArray("parents") { add(JsonPrimitive("appDataFolder")) }
                put("mimeType", "application/json")
            }.toString()
            val boundary = "ritm_" + java.util.UUID.randomUUID().toString().replace("-", "")
            val body = buildString {
                append("--$boundary\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n").append(meta).append("\r\n")
                append("--$boundary\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n")
            }.toByteArray(Charsets.UTF_8) + bytes + "\r\n--$boundary--\r\n".toByteArray(Charsets.UTF_8)
            val saved = Json.parseToJsonElement(
                String(call("POST", "$UPLOAD/files?uploadType=multipart&fields=id", "multipart/related; boundary=$boundary", body), Charsets.UTF_8),
            ) as? JsonObject
            if (saved?.get("id") == null) throw RitmException("Google не подтвердил сохранение.")
            uploaded++
        }
        uploaded
    }

    private suspend fun call(method: String, url: String, type: String? = null, body: ByteArray? = null): ByteArray {
        val (code, bytes) = transport.send(method, url, token(), type, body)
        when {
            code in 200..299 -> return bytes
            code == 401 -> throw AuthExpired()
            code == 403 -> throw RitmException("Google отклонил запрос. Проверьте, что Drive API включён в проекте Google Cloud.")
            code == 429 -> throw RitmException("Google просит подождать. Синхронизация повторится автоматически.")
            else -> throw RitmException("Google Диск временно недоступен ($code).")
        }
    }

    companion object {
        const val SCOPE = "https://www.googleapis.com/auth/drive.appdata"
        private const val API = "https://www.googleapis.com/drive/v3"
        private const val UPLOAD = "https://www.googleapis.com/upload/drive/v3"
        private fun enc(s: String) = URLEncoder.encode(s, "UTF-8").replace("+", "%20")
        private fun key(t: TaskItem) = listOf(t.id, t.version, t.updatedUtc, t.deviceId, t.deleted).joinToString("|")
        fun eventName(id: String, bytes: ByteArray) = "ritm-v2-$id-${Store.sha256(bytes)}.json"
    }
}

object HttpTransport : DriveClient.Transport {
    override fun send(method: String, url: String, token: String, contentType: String?, body: ByteArray?): Pair<Int, ByteArray> {
        val c = URL(url).openConnection() as HttpURLConnection
        try {
            c.requestMethod = method
            c.connectTimeout = 25_000
            c.readTimeout = 25_000
            c.setRequestProperty("Authorization", "Bearer $token")
            if (body != null) {
                c.doOutput = true
                c.setRequestProperty("Content-Type", contentType)
                c.setFixedLengthStreamingMode(body.size)
                c.outputStream.use { it.write(body) }
            }
            val code = c.responseCode
            val stream = if (code in 200..299) c.inputStream else c.errorStream
            return code to (stream?.use { it.readBytes() } ?: ByteArray(0))
        } finally {
            c.disconnect()
        }
    }
}
