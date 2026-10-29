package ru.ritm.planner.sync

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.google.android.gms.auth.GoogleAuthUtil
import com.google.android.gms.auth.api.identity.AuthorizationRequest
import com.google.android.gms.auth.api.identity.AuthorizationResult
import com.google.android.gms.auth.api.identity.Identity
import com.google.android.gms.common.api.Scope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.tasks.await
import kotlinx.coroutines.withContext
import ru.ritm.planner.RitmApp
import java.io.File
import java.time.LocalTime
import java.time.format.DateTimeFormatter
import java.util.concurrent.TimeUnit

object GoogleAuth {
    val request: AuthorizationRequest =
        AuthorizationRequest.builder().setRequestedScopes(listOf(Scope(DriveClient.SCOPE))).build()

    suspend fun authorize(context: Context): AuthorizationResult =
        Identity.getAuthorizationClient(context).authorize(request).await()

    /** Токен без показа окон; null — нужно войти заново из настроек. */
    suspend fun silentToken(context: Context): String? {
        val result = runCatching { authorize(context) }.getOrNull() ?: return null
        return if (result.hasResolution()) null else result.accessToken
    }

    suspend fun clear(context: Context, token: String) = withContext(Dispatchers.IO) {
        runCatching { GoogleAuthUtil.clearToken(context, token) }
    }
}

object Sync {
    private val mutex = Mutex()
    private val _busy = MutableStateFlow(false)
    val busy = _busy.asStateFlow()

    suspend fun run(context: Context): Boolean = mutex.withLock {
        val app = context.applicationContext as RitmApp
        val store = app.store
        if (!store.settings.driveConnected) return false
        _busy.value = true
        try {
            var token = GoogleAuth.silentToken(app)
                ?: return fail(app, "Войдите в Google снова в настройках.")
            var retried = false
            val client = DriveClient(File(app.filesDir, "google/cache"), { token })
            while (true) {
                try {
                    val pulled = client.pull()
                    store.merge(pulled.items)
                    client.push(store.records.value, pulled)
                    break
                } catch (e: AuthExpired) {
                    if (retried) throw e
                    retried = true
                    GoogleAuth.clear(app, token)
                    token = GoogleAuth.silentToken(app) ?: throw e
                }
            }
            val time = LocalTime.now().format(DateTimeFormatter.ofPattern("HH:mm"))
            store.updatePrefs { it.copy(syncStatus = "Google Диск · синхронизировано в $time", lastSync = java.time.Instant.now().toString()) }
            return true
        } catch (e: Exception) {
            return fail(app, "Сохранено на телефоне. Синхронизация: " + (e.message ?: "ошибка сети"))
        } finally {
            _busy.value = false
        }
    }

    private fun fail(app: RitmApp, message: String): Boolean {
        app.store.updatePrefs { it.copy(syncStatus = message) }
        return false
    }

    /** Отложенная синхронизация после локального изменения (сливает частые правки в одну). */
    fun soon(context: Context) {
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setInitialDelay(3, TimeUnit.SECONDS)
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork("ritm-sync-soon", ExistingWorkPolicy.REPLACE, request)
    }

    fun schedulePeriodic(context: Context, enabled: Boolean) {
        val wm = WorkManager.getInstance(context)
        if (!enabled) { wm.cancelUniqueWork("ritm-sync"); return }
        val request = PeriodicWorkRequestBuilder<SyncWorker>(15, TimeUnit.MINUTES)
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
            .build()
        wm.enqueueUniquePeriodicWork("ritm-sync", ExistingPeriodicWorkPolicy.KEEP, request)
    }
}

class SyncWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        Sync.run(applicationContext)
        return Result.success()
    }
}
