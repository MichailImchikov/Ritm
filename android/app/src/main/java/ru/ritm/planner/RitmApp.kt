package ru.ritm.planner

import android.app.Application
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import ru.ritm.planner.data.FocusTimer
import ru.ritm.planner.data.Store
import ru.ritm.planner.notify.Notifications
import ru.ritm.planner.sync.Sync
import ru.ritm.planner.widget.TodayWidget
import java.io.File

class RitmApp : Application() {
    val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    lateinit var store: Store
        private set

    override fun onCreate() {
        super.onCreate()
        store = Store(File(filesDir, "data"))
        store.onChange = {
            if (store.settings.driveConnected) Sync.soon(this)
            refreshWidgets()
        }
        scope.launch { store.records.collect { refreshWidgets() } }
        Notifications.createChannels(this)
        Notifications.scheduleDigest(this)
        Sync.schedulePeriodic(this, store.settings.driveConnected)
        completeFocusIfDue()
    }

    fun refreshWidgets() {
        scope.launch { runCatching { TodayWidget.refresh(this@RitmApp) } }
    }

    /** Если отрезок таймера закончился (в том числе пока приложение было закрыто) — переключает фокус/перерыв. */
    fun completeFocusIfDue(): Boolean {
        val next = FocusTimer.tick(store.settings.focus) ?: return false
        store.updatePrefs { it.copy(focus = next) }
        Notifications.focusFinished(this, next.mode)
        Notifications.syncFocus(this)
        return true
    }
}
