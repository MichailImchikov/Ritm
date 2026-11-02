package ru.ritm.planner.data

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.util.UUID

class RitmException(message: String) : Exception(message)

const val NO_PROJECT = "Без проекта"

fun newId(): String = UUID.randomUUID().toString()

// Формат записей совпадает с десктопным Ритмом (Model.cs): те же поля, имена и правила проверки.
@Serializable
data class Step(val id: String? = null, val title: String = "", val done: Boolean = false)

@Serializable
data class Attachment(
    val id: String = "",
    val name: String = "",
    val file: String = "",
    val size: Long = 0,
    val addedUtc: String? = null,
    val addedBy: String? = null,
)

@Serializable
data class TaskItem(
    val id: String = newId(),
    val title: String = "",
    val description: String? = "",
    val date: String? = null,
    val priority: Int = 3,
    val category: String = NO_PROJECT,
    val completed: Boolean = false,
    val subtasks: List<Step>? = emptyList(),
    val version: Int = 0,
    val createdAt: Long = 0,
    val updatedUtc: String? = null,
    val deviceId: String? = null,
    val deleted: Boolean = false,
    val status: String? = "todo",
    val recordType: String? = "task",
    val isMeeting: Boolean = false,
    val meetingHue: Int? = null,
    val meetingTransparency: Int? = null,
    val meetingColor: String? = null,
    val meetingTime: String? = null,
    val meetingDays: List<Int>? = null,
    val assignee: String? = null,
    val createdBy: String? = null,
    val attachments: List<Attachment>? = emptyList(),
) {
    val isProject get() = recordType == "project"
    /** Обычная задача: не проект, не встреча и не удалена. */
    val isTask get() = !deleted && !isProject && !isMeeting
    val steps get() = subtasks.orEmpty()
    val localDate: LocalDate? get() = date?.let { runCatching { LocalDate.parse(it) }.getOrNull() }

    fun validated(): TaskItem {
        val uuid = runCatching { UUID.fromString(id).toString() }.getOrNull()
        if (uuid == null || uuid != id) throw RitmException("Некорректный идентификатор задачи.")
        if (title.isBlank() || title.length > 200) throw RitmException("Введите название задачи — до 200 символов.")
        val text = description ?: ""
        if (text.length > 10000) throw RitmException("Описание слишком длинное. Максимум — 10 000 символов.")
        if (category.isBlank() || category.length > 50) throw RitmException("Введите категорию — до 50 символов.")
        if (date != null) {
            val d = runCatching { LocalDate.parse(date, DateTimeFormatter.ISO_LOCAL_DATE) }.getOrNull()
            if (d == null || d.year < 1900 || date.length != 10) throw RitmException("Проверьте дату задачи.")
        }
        if (isMeeting) {
            if (meetingHue != null && meetingHue !in 0..360 || meetingTransparency != null && meetingTransparency !in 0..100)
                throw RitmException("Проверьте цвет и прозрачность встречи.")
            if (date == null || runCatching { java.time.LocalTime.parse(meetingTime ?: "") }.isFailure)
                throw RitmException("Укажите дату и время встречи (чч:мм).")
            val days = meetingDays.orEmpty()
            if (days.size > 7 || days.any { it !in 0..6 } || days.distinct().size != days.size) throw RitmException("Проверьте дни встреч.")
        }
        if (priority !in 1..4) throw RitmException("Некорректный приоритет.")
        val type = if (recordType.isNullOrEmpty()) "task" else recordType
        if (type != "task" && type != "project") throw RitmException("Некорректный тип записи.")
        val st = when {
            completed -> "done"
            status.isNullOrEmpty() || status == "done" -> "todo"
            else -> status
        }
        if (st != "todo" && st != "doing" && st != "done") throw RitmException("Некорректный статус.")
        val list = subtasks.orEmpty()
        if (list.size > 50 || list.any { it.title.isBlank() || it.title.length > 200 })
            throw RitmException("В задаче может быть до 50 подзадач с названиями до 200 символов.")
        return copy(
            title = title.trim(), category = category.trim(), description = text, recordType = type, status = st,
            subtasks = list.map { if (it.id.isNullOrEmpty()) it.copy(id = newId()) else it },
            assignee = TeamNames.person(assignee), createdBy = TeamNames.person(createdBy), attachments = attachments.orEmpty(),
        )
    }
}

@Serializable
data class FocusState(
    val mode: String = "focus",
    val running: Boolean = false,
    val remainingSeconds: Int = 1500,
    val deadlineUtc: String? = null,
    val sessions: Int = 0,
)

@Serializable
data class Preferences(
    val deviceId: String = newId(),
    val theme: String = "system",
    val palette: String = "violet",
    val reduceMotion: Boolean = false,
    val focus: FocusState = FocusState(),
    val focusTaskId: String? = null,
    val taskLayouts: Map<String, String> = emptyMap(),
    val driveConnected: Boolean = false,
    val lastSync: String? = null,
    val syncStatus: String? = null,
    val digestEnabled: Boolean = true,
    val digestMinutes: Int = 9 * 60,
    val teams: List<TeamLink> = emptyList(),
    val serverLogin: String = "",
    val serverDomain: String = "",
    val notificationsMuted: Boolean = false,
    val notifications: Map<String, Boolean> = emptyMap(),
    val meetingNotified: Map<String, String> = emptyMap(),
    val noticesSeen: List<String> = emptyList(),
)

@Serializable
data class BackupDocument(
    val app: String = "ritm-android",
    val version: Int = 2,
    val tasks: List<TaskItem> = emptyList(),
    val projects: List<TaskItem>? = emptyList(),
)

val RitmJson = Json {
    ignoreUnknownKeys = true
    encodeDefaults = true
    explicitNulls = true
    coerceInputValues = true
}
