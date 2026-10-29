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
) {
    val isProject get() = recordType == "project"
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
