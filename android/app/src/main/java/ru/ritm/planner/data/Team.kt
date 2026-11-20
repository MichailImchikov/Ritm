package ru.ritm.planner.data

import kotlinx.serialization.Serializable
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime

/** Общий проект на телефоне: папка на сервере и как до неё дошла последняя синхронизация. */
@Serializable
data class TeamLink(
    val path: String = "",
    val name: String = "",
    val user: String = "",
    val manual: Boolean = false,
    val synced: Boolean = false,
    val route: String? = null,
    val status: String? = null,
    val members: List<String> = emptyList(),
)

const val ROUTE_SERVER = "server"
const val ROUTE_DRIVE = "drive"

// Форматы файлов компьютера: ritm-project.json и members\*.json в папке проекта, ritm-teams-v1.json на Google Диске.
@Serializable data class TeamManifest(val name: String = "")
@Serializable data class TeamMember(val name: String = "", val lastSeenUtc: String? = null)
@Serializable data class PhoneTeam(val name: String = "", val path: String = "", val user: String = "", val members: List<String>? = null)
@Serializable data class PhoneTeams(val app: String = "", val version: Int = 0, val teams: List<PhoneTeam> = emptyList())
@Serializable data class PhoneNotice(val id: String = "", val kind: String = "", val title: String = "", val text: String = "", val createdUtc: String? = null)

/** Что нового пришло от других участников — для уведомлений. */
data class TeamChange(val task: TaskItem, val project: String, val added: Boolean, val assignedToMe: Boolean)

object TeamNames {
    fun person(name: String?): String? {
        val n = (name ?: "").trim().split(Regex("\\s+")).filter { it.isNotEmpty() }.joinToString(" ")
        if (n.isEmpty()) return null
        if (n.length > 50) throw RitmException("Имя: до 50 символов.")
        return n
    }

    fun same(a: String?, b: String?) = a != null && b != null && a.equals(b, ignoreCase = true)
}

/** Сетевой путь \\сервер\шаре\папка, разобранный для подключения по SMB. */
data class TeamPath(val host: String, val share: String, val folder: String) {
    companion object {
        fun parse(path: String): TeamPath? {
            val parts = path.trim().replace('/', '\\').removePrefix("\\\\").split('\\').filter { it.isNotEmpty() }
            if (!path.trim().replace('/', '\\').startsWith("\\\\") || parts.size < 2) return null
            return TeamPath(parts[0], parts[1], parts.drop(2).joinToString("\\"))
        }
    }

    fun join(vararg names: String) = (listOf(folder) + names).filter { it.isNotEmpty() }.joinToString("\\")
}

/** Расписание встреч — правила как в MeetingNotifications.cs. */
object MeetingSchedule {
    val offsets = listOf(10, 0)

    private fun time(m: TaskItem) = runCatching { LocalTime.parse(m.meetingTime ?: "") }.getOrNull()

    /** В TaskItem.meetingDays дни как в .NET: 0 — воскресенье, 6 — суббота. */
    fun occursOn(m: TaskItem, day: LocalDate): Boolean {
        val start = m.localDate
        if (!m.isMeeting || start == null || day < start) return false
        val days = m.meetingDays.orEmpty()
        return if (days.isEmpty()) day == start else (day.dayOfWeek.value % 7) in days
    }

    fun at(m: TaskItem, day: LocalDate, offset: Int): LocalDateTime? = time(m)?.let { day.atTime(it).minusMinutes(offset.toLong()) }

    /** Телефон может проснуться с опозданием, поэтому сигнал действует несколько минут, а не одну, как на компьютере. */
    fun due(m: TaskItem, day: LocalDate, now: LocalDateTime, offset: Int, graceMinutes: Long = 5): Boolean {
        if (!occursOn(m, day)) return false
        val at = at(m, day, offset) ?: return false
        return now >= at && now < at.plusMinutes(graceMinutes)
    }

    fun next(meetings: List<TaskItem>, now: LocalDateTime): LocalDateTime? =
        meetings.flatMap { m ->
            (0L..8L).map { now.toLocalDate().plusDays(it) }.filter { occursOn(m, it) }
                .flatMap { day -> offsets.mapNotNull { at(m, day, it) } }
        }.filter { it > now }.minOrNull()

    fun label(m: TaskItem): String {
        val days = m.meetingDays.orEmpty()
        val names = listOf(DayOfWeek.SUNDAY to "вс", DayOfWeek.MONDAY to "пн", DayOfWeek.TUESDAY to "вт", DayOfWeek.WEDNESDAY to "ср",
            DayOfWeek.THURSDAY to "чт", DayOfWeek.FRIDAY to "пт", DayOfWeek.SATURDAY to "сб")
        val week = (1..7).map { it % 7 }.filter { it in days }.joinToString(", ") { names[it].second }
        return listOfNotNull(m.meetingTime, if (days.isEmpty()) m.date?.let { Dates.friendly(it) } else week).joinToString(" · ")
    }
}

/**
 * Виды уведомлений — те же, что на компьютере (Notifications.cs), с теми же кодами.
 * Настройки строятся по этому списку, каждому виду соответствует свой канал Android.
 */
class NotificationKind(val id: String, val title: String, val description: String, val defaultOn: Boolean = true, val urgent: Boolean = false)

object NotificationKinds {
    val Meeting = NotificationKind("meetings.reminder", "Уведомления о встречах", "За 10 минут до начала встречи и в момент её начала.", urgent = true)
    val NewTask = NotificationKind("tasks.new", "Новые задачи в общих проектах", "Другой участник добавил задачу в общий проект.")
    val Assigned = NotificationKind("tasks.assigned", "Задача назначена на вас", "Вас сделали исполнителем задачи в общем проекте.", urgent = true)
    val MainUpdated = NotificationKind("git.main", "Обновление main", "Компьютер заметил новый коммит на сервере origin.")
    val FolderChanged = NotificationKind("folder.changed", "Изменения в папке", "Новые и изменённые файлы в папке, которую отслеживает компьютер.")
    val BuildDone = NotificationKind("build.done", "Сборка Unity завершена", "Сборка Unity на стенде закончилась.")
    val all = listOf(Meeting, NewTask, Assigned, MainUpdated, FolderChanged, BuildDone)

    fun find(id: String) = all.firstOrNull { it.id == id }
    fun choice(p: Preferences, kind: NotificationKind) = p.notifications[kind.id] ?: kind.defaultOn
    fun isOn(p: Preferences, kind: NotificationKind) = !p.notificationsMuted && choice(p, kind)
}
