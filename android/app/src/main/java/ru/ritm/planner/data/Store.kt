package ru.ritm.planner.data

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.io.File
import java.security.MessageDigest
import java.time.Instant

/**
 * Локальное хранилище: по файлу на запись в tasks/, прежние версии в history/,
 * настройки в settings.json. Правила версий и разрешения конфликтов — как в Store.cs.
 */
class Store(val root: File, private val clock: () -> Instant = Instant::now) {
    private val local = File(root, "tasks")
    private val history = File(root, "history")
    private val items = LinkedHashMap<String, TaskItem>()
    private val _records = MutableStateFlow<List<TaskItem>>(emptyList())
    private val _prefs: MutableStateFlow<Preferences>

    /** Все записи, включая проекты и удалённые (надгробия) — для синхронизации. */
    val records: StateFlow<List<TaskItem>> = _records.asStateFlow()
    val prefs: StateFlow<Preferences>
    var onChange: (() -> Unit)? = null

    init {
        local.mkdirs(); history.mkdirs()
        val config = File(root, "settings.json")
        val loaded = if (config.exists()) runCatching { RitmJson.decodeFromString<Preferences>(config.readText()) }.getOrNull() else null
        _prefs = MutableStateFlow(loaded ?: Preferences())
        prefs = _prefs.asStateFlow()
        if (loaded == null) savePrefs()
        reload()
    }

    val settings get() = _prefs.value
    val tasks get() = items.values.filter { !it.deleted && !it.isProject }
    val projectNames: List<String>
        get() = items.values.filter { !it.deleted }.map { if (it.isProject) it.title else it.category }
            .filter { it != NO_PROJECT }.distinct().sortedWith(String.CASE_INSENSITIVE_ORDER)

    fun find(id: String) = items[id]?.takeIf { !it.deleted }

    @Synchronized
    fun reload() {
        local.listFiles { f -> f.extension == "json" }?.forEach { file ->
            val t = runCatching { readRecord(file) }.getOrNull() ?: return@forEach
            val old = items[t.id]
            if (old == null || compare(t, old) > 0) items[t.id] = t
        }
        publish(false)
    }

    @Synchronized
    fun updatePrefs(change: (Preferences) -> Preferences) {
        _prefs.value = change(_prefs.value)
        savePrefs()
    }

    @Synchronized
    fun save(value: TaskItem): TaskItem {
        val checked = value.validated()
        val current = items[checked.id]
        if (current != null) {
            if (current.version != value.version || current.deleted)
                throw RitmException("Задача изменилась на другом устройстве. Откройте свежую версию задачи.")
        } else if (value.version != 0) throw RitmException("Исходная задача не найдена.")
        val saved = checked.copy(
            version = value.version + 1,
            updatedUtc = clock().toString(),
            deviceId = settings.deviceId,
            createdAt = if (checked.createdAt == 0L) clock().toEpochMilli() else checked.createdAt,
        )
        if (current != null) archive(current)
        atomic(File(local, saved.id + ".json"), RitmJson.encodeToString(TaskItem.serializer(), saved))
        items[saved.id] = saved
        publish(true)
        return saved
    }

    fun delete(value: TaskItem) = save(value.copy(deleted = true))

    fun createProject(name: String) {
        val n = name.trim()
        if (n == NO_PROJECT || n.isEmpty() || n.length > 50) throw RitmException("Название проекта: от 1 до 50 символов.")
        if (projectNames.any { it.equals(n, ignoreCase = true) }) throw RitmException("Проект с таким названием уже есть.")
        save(TaskItem(title = n, category = n, recordType = "project"))
    }

    fun renameProject(old: String, new: String) {
        val n = new.trim()
        if (old == n) return
        if (n.isEmpty() || n.length > 50 || n == NO_PROJECT || projectNames.any { it.equals(n, ignoreCase = true) })
            throw RitmException("Выберите другое название проекта (до 50 символов).")
        items.values.filter { !it.deleted && it.category == old }.toList().forEach {
            save(it.copy(category = n, title = if (it.isProject) n else it.title))
        }
        settings.taskLayouts["project:$old"]?.let { layout ->
            updatePrefs { p -> p.copy(taskLayouts = p.taskLayouts - "project:$old" + ("project:$n" to layout)) }
        }
    }

    fun deleteProject(name: String) {
        items.values.filter { !it.deleted && it.category == name }.toList().forEach {
            if (it.isProject) delete(it) else save(it.copy(category = NO_PROJECT))
        }
    }

    /** Слияние записей из облака: побеждает более свежая (updatedUtc, затем deviceId). */
    @Synchronized
    fun merge(incoming: List<TaskItem>): Boolean {
        var changed = false
        for (raw in incoming) {
            val t = raw.validated()
            if (t.version < 1 || t.deviceId.isNullOrEmpty() || parse(t.updatedUtc) == null)
                throw RitmException("Некорректная облачная запись.")
            val old = items[t.id]
            if (old == null || compare(t, old) > 0) {
                if (old != null) archive(old)
                atomic(File(local, t.id + ".json"), RitmJson.encodeToString(TaskItem.serializer(), t))
                items[t.id] = t
                changed = true
            }
        }
        if (changed) publish(false)
        return changed
    }

    fun exportJson(): String = RitmJson.encodeToString(
        BackupDocument.serializer(),
        BackupDocument(tasks = tasks, projects = items.values.filter { !it.deleted && it.isProject }),
    )

    /** Импорт добавляет копии записей (новые id), как на компьютере. */
    fun importJson(text: String): Int {
        if (text.length > 10_000_000) throw RitmException("Размер резервной копии не должен превышать 10 МБ.")
        val backup = runCatching { RitmJson.decodeFromString(BackupDocument.serializer(), text) }.getOrNull()
            ?: throw RitmException("Некорректная резервная копия.")
        val list = backup.tasks + backup.projects.orEmpty()
        if (list.size > 10000) throw RitmException("В копии слишком много записей.")
        val prepared = list.map { it.copy(id = newId(), version = 0, createdAt = 0, deleted = false).validated() }
        prepared.forEach { save(it) }
        return prepared.size
    }

    private fun publish(local: Boolean) {
        _records.value = items.values.toList()
        if (local) onChange?.invoke()
    }

    private fun savePrefs() = atomic(File(root, "settings.json"), RitmJson.encodeToString(Preferences.serializer(), _prefs.value))

    private fun readRecord(file: File): TaskItem {
        if (file.length() > 2_000_000) throw RitmException("Слишком большой файл задачи: ${file.name}")
        val t = RitmJson.decodeFromString(TaskItem.serializer(), file.readText()).validated()
        if (t.id != file.nameWithoutExtension || t.version < 1 || parse(t.updatedUtc) == null || t.deviceId.isNullOrEmpty())
            throw RitmException("Повреждён файл задачи: ${file.name}")
        return t
    }

    private fun archive(t: TaskItem) {
        val raw = RitmJson.encodeToString(TaskItem.serializer(), t)
        val file = File(history, t.id + "-" + sha256(raw).substring(0, 16) + ".json")
        if (!file.exists()) atomic(file, raw)
    }

    companion object {
        fun parse(s: String?): Instant? = s?.let {
            runCatching { Instant.parse(it) }.getOrNull()
                ?: runCatching { java.time.OffsetDateTime.parse(it).toInstant() }.getOrNull()
        }

        fun compare(a: TaskItem, b: TaskItem): Int {
            val c = compareValues(parse(a.updatedUtc), parse(b.updatedUtc))
            return if (c != 0) c else (a.deviceId ?: "").compareTo(b.deviceId ?: "")
        }

        fun sha256(text: String) = sha256(text.toByteArray(Charsets.UTF_8))
        fun sha256(bytes: ByteArray): String =
            MessageDigest.getInstance("SHA-256").digest(bytes).joinToString("") { "%02x".format(it) }

        fun atomic(file: File, content: String) {
            file.parentFile?.mkdirs()
            val temp = File(file.parentFile, file.name + "." + newId() + ".tmp")
            try {
                temp.writeText(content, Charsets.UTF_8)
                if (!temp.renameTo(file)) {
                    file.delete()
                    if (!temp.renameTo(file)) throw RitmException("Не удалось сохранить ${file.name}")
                }
            } finally {
                temp.delete()
            }
        }
    }
}
