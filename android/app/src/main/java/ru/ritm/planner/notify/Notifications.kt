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
import ru.ritm.planner.data.Dates
import ru.ritm.planner.data.FocusTimer
import ru.ritm.planner.data.MeetingSchedule
import ru.ritm.planner.data.NotificationKind
import ru.ritm.planner.data.NotificationKinds
import ru.ritm.planner.data.PhoneNotice
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.data.TeamChange
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
        nm.createNotificationChannels(NotificationKinds.all.map { kind ->
            NotificationChannel(kind.id, kind.title, if (kind.urgent) NotificationManager.IMPORTANCE_HIGH else NotificationManager.IMPORTANCE_DEFAULT).apply { description = kind.description }
        })
        nm.createNotificationChannels(listOf(
            NotificationChannel(CH_FOCUS, "Окончание фокуса", NotificationManager.IMPORTANCE_HIGH).apply { description = "Сигнал, когда закончился фокус или перерыв" },
            NotificationChannel(CH_TIMER, "Идёт таймер", NotificationManager.IMPORTANCE_LOW).apply { description = "Обратный отсчёт в шторке"; setShowBadge(false) },
            NotificationChannel(CH_DIGEST, "План на день", NotificationManager.IMPORTANCE_DEFAULT).apply { description = "Утреннее напоминание о задачах на сегодня" },
        ))
    }

    private fun allowed(context: Context) = Build.VERSION.SDK_INT < 33 ||
        ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED

    fun openApp(context: Context, request: Int = 0, tab: String? = null, action: String? = null): PendingIntent {
        val intent = Intent(context, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        if (tab != null) intent.putExtra(MainActivity.EXTRA_TAB, tab)
        if (action != null) intent.putExtra(MainActivity.EXTRA_ACTION, action)
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

    /** Показать можно только вид из списка: выбор пользователя проверяется здесь, как в Notifier.Show. */
    private fun show(context: Context, kind: NotificationKind, id: Int, title: String, text: String, tab: String = "shared", action: String? = null) {
        val prefs = (context.applicationContext as RitmApp).store.settings
        if (!NotificationKinds.isOn(prefs, kind) || !allowed(context)) return
        val n = NotificationCompat.Builder(context, kind.id)
            .setSmallIcon(R.drawable.ic_stat_ritm)
            .setContentTitle(title).setContentText(text)
            .setStyle(NotificationCompat.BigTextStyle().bigText(text))
            .setPriority(if (kind.urgent) NotificationCompat.PRIORITY_HIGH else NotificationCompat.PRIORITY_DEFAULT)
            .setAutoCancel(true)
            .setContentIntent(openApp(context, id, tab, action))
            .build()
        NotificationManagerCompat.from(context).notify(id, n)
    }

    private fun short(text: String, limit: Int = 60) = if (text.length <= limit) text else text.take(limit - 1) + "…"
    private fun author(t: TaskItem) = t.createdBy?.let { " · $it" } ?: ""
    private fun tasks(n: Int) = "$n ${Dates.plural(n, "задача", "задачи", "задач")}"

    /** Те же сообщения, что на компьютере; новая задача, назначенная на вас, приходит одним уведомлением. */
    fun showTeamChanges(context: Context, changes: List<TeamChange>) {
        val prefs = (context.applicationContext as RitmApp).store.settings
        val assignedShown = NotificationKinds.isOn(prefs, NotificationKinds.Assigned)
        changes.groupBy { it.project }.forEach { (project, group) ->
            val name = short(project, 40)
            val mine = group.filter { it.assignedToMe }
            val added = group.filter { it.added && !(it.assignedToMe && assignedShown) }
            if (mine.size == 1) show(context, NotificationKinds.Assigned, mine[0].task.id.hashCode(), "Задача для вас · $name", "«${short(mine[0].task.title)}»${author(mine[0].task)}", action = "task:${mine[0].task.id}")
            else if (mine.size > 1) show(context, NotificationKinds.Assigned, "assigned:$project".hashCode(), "Задачи для вас · $name", "На вас назначено ${tasks(mine.size)}.", action = "project:$project")
            if (added.size == 1) show(context, NotificationKinds.NewTask, added[0].task.id.hashCode(), "Новая задача · $name", "«${short(added[0].task.title)}»${author(added[0].task)}", action = "task:${added[0].task.id}")
            else if (added.size > 1) show(context, NotificationKinds.NewTask, "new:$project".hashCode(), "Новые задачи · $name", "Добавлено ${tasks(added.size)}.", action = "project:$project")
        }
    }

    /** События, которые видит только компьютер: git, папки, сборки. */
    fun showNotice(context: Context, notice: PhoneNotice) {
        val kind = NotificationKinds.find(notice.kind) ?: return
        show(context, kind, notice.id.hashCode(), notice.title, notice.text)
    }

    fun checkMeetings(context: Context) {
        val app = context.applicationContext as RitmApp
        val store = app.store
        val now = LocalDateTime.now()
        val today = now.toLocalDate()
        val yesterday = today.minusDays(1).toString()
        val sent = store.settings.meetingNotified.filterValues { it >= yesterday }.toMutableMap()
        if (NotificationKinds.isOn(store.settings, NotificationKinds.Meeting)) {
            for (offset in MeetingSchedule.offsets) {
                val due = store.meetings.flatMap { m -> listOf(today, today.plusDays(1)).filter { MeetingSchedule.due(m, it, now, offset) }.map { m to it } }
                    .filter { (m, day) -> key(m, day, offset) !in sent }
                if (due.isEmpty()) continue
                val first = due[0].first
                val title = if (offset == 10) "Встреча через 10 минут" else "Встреча начинается"
                val text = if (due.size == 1) "«${first.title}» · ${first.meetingTime} · ${first.category}"
                else "${due.size} встреч: " + due.joinToString(", ") { "${it.first.title} (${it.first.meetingTime})" }
                show(context, NotificationKinds.Meeting, key(first, due[0].second, offset).hashCode(), title, text, action = "project:${first.category}")
                due.forEach { (m, day) -> sent[key(m, day, offset)] = day.toString() }
            }
        }
        if (sent != store.settings.meetingNotified) store.updatePrefs { it.copy(meetingNotified = sent) }
        scheduleMeetings(context)
    }

    private fun key(m: TaskItem, day: LocalDate, offset: Int) = "${m.id}|$day|${m.meetingTime}|$offset"

    fun scheduleMeetings(context: Context) {
        val store = (context.applicationContext as RitmApp).store
        val pending = alarmIntent(context, AlarmReceiver.MEETING, 3)
        val next = MeetingSchedule.next(store.meetings, LocalDateTime.now())
        if (next == null || !NotificationKinds.isOn(store.settings, NotificationKinds.Meeting)) {
            context.getSystemService(AlarmManager::class.java).cancel(pending)
            return
        }
        schedule(context, next.atZone(ZoneId.systemDefault()).toInstant().toEpochMilli(), pending, exact = true)
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
        val open = store.tasks.filter { !it.completed && it.date != null && store.isMine(it) }
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
            MEETING -> Notifications.checkMeetings(context)
            Intent.ACTION_BOOT_COMPLETED, Intent.ACTION_MY_PACKAGE_REPLACED, AlarmManager.ACTION_SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED -> {
                Notifications.scheduleDigest(context)
                Notifications.checkMeetings(context)
                app.completeFocusIfDue()
                Notifications.syncFocus(context)
            }
        }
    }

    companion object {
        const val FOCUS = "ru.ritm.planner.FOCUS_DONE"
        const val DIGEST = "ru.ritm.planner.DIGEST"
        const val MEETING = "ru.ritm.planner.MEETING"
    }
}
