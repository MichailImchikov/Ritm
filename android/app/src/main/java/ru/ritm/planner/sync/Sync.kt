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
import ru.ritm.planner.data.ROUTE_DRIVE
import ru.ritm.planner.data.ROUTE_SERVER
import ru.ritm.planner.data.RitmException
import ru.ritm.planner.data.Store
import ru.ritm.planner.data.TeamChange
import ru.ritm.planner.data.TeamLink
import ru.ritm.planner.notify.Notifications
import java.io.File
import java.time.Instant
import java.time.LocalTime
import java.time.temporal.ChronoUnit
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

    /** Личные задачи — через Google Диск; общие проекты — напрямую с сервера, а без него тоже через Диск. */
    suspend fun run(context: Context): Boolean = mutex.withLock {
        val app = context.applicationContext as RitmApp
        val store = app.store
        if (!store.settings.driveConnected && store.teams.isEmpty()) return false
        _busy.value = true
        try {
            val changes = mutableListOf<TeamChange>()
            var ok = true
            var client: DriveClient? = null
            if (store.settings.driveConnected) {
                var token = GoogleAuth.silentToken(app)
                try {
                    if (token == null) throw RitmException("Войдите в Google снова в настройках.")
                    client = DriveClient(File(app.filesDir, "google/cache"), { token!! })
                    var retried = false
                    while (true) {
                        try {
                            val pulled = client.pull()
                            store.merge(pulled.items)
                            client.push(store.personalRecords, pulled)
                            client.teams()?.let(store::applyTeams)
                            break
                        } catch (e: AuthExpired) {
                            if (retried) throw e
                            retried = true
                            GoogleAuth.clear(app, token!!)
                            token = GoogleAuth.silentToken(app) ?: throw e
                        }
                    }
                    val time = LocalTime.now().format(DateTimeFormatter.ofPattern("HH:mm"))
                    store.updatePrefs { it.copy(syncStatus = "Google Диск · синхронизировано в $time", lastSync = java.time.Instant.now().toString()) }
                } catch (e: Exception) {
                    client = null
                    ok = false
                    fail(app, "Сохранено на телефоне. Синхронизация: " + (e.message ?: "ошибка сети"))
                }
            }
            changes += syncTeams(app, client)
            client?.let { runCatching { showNotices(app, it) } }
            Notifications.showTeamChanges(app, changes)
            Notifications.checkMeetings(app)
            return ok
        } finally {
            _busy.value = false
        }
    }

    /** Сначала сервер; у кого он недоступен — обмен через Google Диск, откуда записи заберёт компьютер. */
    private suspend fun syncTeams(app: RitmApp, client: DriveClient?): List<TeamChange> = withContext(Dispatchers.IO) {
        val store = app.store
        val changes = mutableListOf<TeamChange>()
        val prefs = store.settings
        val lan = ServerAccess.onLocalNetwork(app)
        val password = if (lan) app.serverPassword.read() else ""
        val offServer = mutableListOf<Pair<TeamLink, String?>>()
        for (link in store.teams) {
            if (!lan) { offServer += link to null; continue }
            try {
                ServerAccess.open(link.path, prefs.serverLogin, password, prefs.serverDomain).use { folder ->
                    changes += TeamSync.exchange(store, link, folder)
                }
                store.updateTeam(link.name) { it.copy(synced = true, route = ROUTE_SERVER, status = "Сервер · ${clock()}") }
            } catch (e: Exception) {
                offServer += link to serverError(e)
            }
        }
        if (offServer.isEmpty()) return@withContext changes
        try {
            if (client == null) throw RitmException("Google Диск не подключён")
            val pulled = client.pull(DriveClient.TEAM)
            for ((link, reason) in offServer) {
                changes += store.mergeTeam(link, pulled.items.filter { it.category == link.name })
                client.push(store.teamRecords(link.name), pulled, DriveClient.TEAM)
                store.updateTeam(link.name) { it.copy(synced = true, route = ROUTE_DRIVE, status = listOfNotNull("Google Диск · ${clock()}", reason).joinToString(" · ")) }
            }
        } catch (e: Exception) {
            for ((link, reason) in offServer) store.updateTeam(link.name) { it.copy(route = null, status = "Нет связи: " + (reason ?: e.message ?: "ошибка сети")) }
        }
        changes
    }

    private fun serverError(e: Exception) = when {
        e is java.net.SocketTimeoutException || e is java.net.ConnectException || e is java.net.NoRouteToHostException -> "сервер не отвечает"
        e.javaClass.simpleName == "SMBApiException" && e.message.orEmpty().contains("LOGON_FAILURE") -> "неверный логин или пароль сервера"
        e.javaClass.simpleName == "SMBApiException" && e.message.orEmpty().contains("ACCESS_DENIED") -> "нет доступа к папке"
        else -> e.message ?: "ошибка сервера"
    }

    private suspend fun showNotices(app: RitmApp, client: DriveClient) {
        val store = app.store
        val (fresh, names) = client.notices(store.settings.noticesSeen)
        if (fresh.isEmpty() && names.toSet() == store.settings.noticesSeen.toSet()) return
        val limit = Instant.now().minus(1, ChronoUnit.DAYS)
        fresh.forEach { n ->
            val time = Store.parse(n.createdUtc)
            if (time != null && time >= limit) Notifications.showNotice(app, n)
        }
        store.updatePrefs { it.copy(noticesSeen = names) }
    }

    private fun clock() = LocalTime.now().format(DateTimeFormatter.ofPattern("HH:mm"))

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
