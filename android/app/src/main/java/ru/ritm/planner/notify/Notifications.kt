package ru.ritm.planner.notify

import android.Manifest
import android.app.AlarmManager
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import ru.ritm.planner.MainActivity
import ru.ritm.planner.R
import ru.ritm.planner.RitmApp
import ru.ritm.planner.data.FocusTimer
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.ZoneId

object Notifications {
    const val CH_FOCUS = "focus"
    const val CH_TIMER = "timer"
    const val CH_DIGEST = "digest"
    private const val ID_TIMER = 10
    private const val ID_FOCUS_DONE = 11
    private const val ID_DIGEST = 12

    fun createChannels(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val nm = context.getSystemService(NotificationManager::class.java)
        nm.createNotificationChannels(listOf(
            NotificationChannel(CH_FOCUS, "Окончание фокуса", NotificationManager.IMPORTANCE_HIGH).apply { description = "Сигнал, когда закончился фокус или перерыв" },
            NotificationChannel(CH_TIMER, "Идёт таймер", NotificationManager.IMPORTANCE_LOW).apply { description = "Обратный отсчёт в шторке"; setShowBadge(false) },
            NotificationChannel(CH_DIGEST, "План на день", NotificationManager.IMPORTANCE_DEFAULT).apply { description = "Утреннее напоминание о задачах на сегодня" },
        ))
    }

    private fun allowed(context: Context) = Build.VERSION.SDK_INT < 33 ||
        ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED

    fun openApp(context: Context, request: Int = 0, tab: String? = null): PendingIntent {
        val intent = Intent(context, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        if (tab != null) intent.putExtra(MainActivity.EXTRA_TAB, tab)
        return PendingIntent.getActivity(context, request, intent, PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
    }

    private fun alarmIntent(context: Context, action: String, request: Int) = PendingIntent.getBroadcast(
        context, request, Intent(context, AlarmReceiver::class.java).setAction(action),
        PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
    )

    private fun schedule(context: Context, at: Long, pending: PendingIntent, exact: Boolean) {
        val am = context.getSystemService(AlarmManager::class.java)
        val canExact = Build.VERSION.SDK_INT < 31 || am.canScheduleExactAlarms()
        if (exact && canExact) am.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, pending)
        else am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, pending)
    }

    /** Синхронизирует будильник и уведомление-отсчёт с текущим состоянием таймера. */
    fun syncFocus(context: Context) {
        val app = context.applicationContext as RitmApp
        val state = app.store.settings.focus
        val pending = alarmIntent(context, AlarmReceiver.FOCUS, 1)
        val nm = NotificationManagerCompat.from(context)
        val deadline = FocusTimer.deadline(state)
        if (deadline == null) {
            context.getSystemService(AlarmManager::class.java).cancel(pending)
            nm.cancel(ID_TIMER)
            return
        }
        schedule(context, deadline.toEpochMilli(), pending, exact = true)
        if (!allowed(context)) return
        val task = app.store.settings.focusTaskId?.let { app.store.find(it)?.title }
        val n = NotificationCompat.Builder(context, CH_TIMER)
            .setSmallIcon(R.drawable.ic_stat_ritm)
            .setContentTitle(if (state.mode == "break") "Перерыв" else "Фокус")
            .setContentText(task ?: "Ритм")
            .setWhen(deadline.toEpochMilli())
            .setUsesChronometer(true)
            .setChronometerCountDown(true)
            .setShowWhen(true)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setSilent(true)
            .setCategory(NotificationCompat.CATEGORY_STOPWATCH)
            .setContentIntent(openApp(context, 1, "today"))
            .build()
        nm.notify(ID_TIMER, n)
    }

    fun focusFinished(context: Context, nextMode: String) {
        NotificationManagerCompat.from(context).cancel(ID_TIMER)
        if (!allowed(context)) return
        val (title, text) = if (nextMode == "break") "Фокус завершён" to "Самое время сделать перерыв 5 минут."
        else "Перерыв окончен" to "Можно начинать следующий фокус."
        val n = NotificationCompat.Builder(context, CH_FOCUS)
            .setSmallIcon(R.drawable.ic_stat_ritm)
            .setContentTitle(title).setContentText(text)
            .setCategory(NotificationCompat.CATEGORY_ALARM)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(openApp(context, 2, "today"))
            .build()
        NotificationManagerCompat.from(context).notify(ID_FOCUS_DONE, n)
    }

    fun scheduleDigest(context: Context) {
        val prefs = (context.applicationContext as RitmApp).store.settings
        val pending = alarmIntent(context, AlarmReceiver.DIGEST, 2)
        if (!prefs.digestEnabled) { context.getSystemService(AlarmManager::class.java).cancel(pending); return }
        val now = LocalDateTime.now()
        var at = now.toLocalDate().atTime(prefs.digestMinutes / 60, prefs.digestMinutes % 60)
        if (!at.isAfter(now)) at = at.plusDays(1)
        schedule(context, at.atZone(ZoneId.systemDefault()).toInstant().toEpochMilli(), pending, exact = false)
    }

    fun showDigest(context: Context) {
        val store = (context.applicationContext as RitmApp).store
        val today = LocalDate.now().toString()
        val open = store.tasks.filter { !it.completed && it.date != null }
        val due = open.filter { it.date == today }.sortedBy { it.priority }
        val overdue = open.filter { it.date!! < today }
        if (due.isEmpty() && overdue.isEmpty() || !allowed(context)) return
        val title = "Сегодня ${due.size} ${ru.ritm.planner.data.Dates.plural(due.size, "задача", "задачи", "задач")}" +
            if (overdue.isNotEmpty()) " · просрочено ${overdue.size}" else ""
        val style = NotificationCompat.InboxStyle()
        (overdue.sortedBy { it.priority } + due).take(6).forEach { style.addLine(it.title) }
        val n = NotificationCompat.Builder(context, CH_DIGEST)
            .setSmallIcon(R.drawable.ic_stat_ritm)
            .setContentTitle(title)
            .setContentText((overdue + due).take(3).joinToString(" · ") { it.title })
            .setStyle(style)
            .setAutoCancel(true)
            .setContentIntent(openApp(context, 3, "today"))
            .build()
        NotificationManagerCompat.from(context).notify(ID_DIGEST, n)
    }
}

class AlarmReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val app = context.applicationContext as RitmApp
        when (intent.action) {
            FOCUS -> app.completeFocusIfDue()
            DIGEST -> { Notifications.showDigest(context); Notifications.scheduleDigest(context) }
            Intent.ACTION_BOOT_COMPLETED, Intent.ACTION_MY_PACKAGE_REPLACED, AlarmManager.ACTION_SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED -> {
                Notifications.scheduleDigest(context)
                app.completeFocusIfDue()
                Notifications.syncFocus(context)
            }
        }
    }

    companion object {
        const val FOCUS = "ru.ritm.planner.FOCUS_DONE"
        const val DIGEST = "ru.ritm.planner.DIGEST"
    }
}
