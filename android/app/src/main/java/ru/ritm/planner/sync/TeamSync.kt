package ru.ritm.planner.sync

import ru.ritm.planner.data.RitmJson
import ru.ritm.planner.data.Store
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.data.TeamChange
import ru.ritm.planner.data.TeamLink
import ru.ritm.planner.data.TeamManifest
import ru.ritm.planner.data.TeamMember
import java.io.Closeable
import java.util.concurrent.ConcurrentHashMap

/** Папка общего проекта. Пути — относительно папки проекта, через обратную косую черту. */
interface TeamFolder : Closeable {
    data class Entry(val name: String, val stamp: Long)

    fun list(dir: String): List<Entry>
    fun read(path: String): ByteArray
    /** Запись через временный файл, как Store.Atomic на компьютере: остальные не увидят половину файла. */
    fun write(path: String, bytes: ByteArray)
}

/** Обмен с папкой проекта: повторяет Store.SyncTeam с компьютера. */
object TeamSync {
    private class Seen(val stamp: Long, val item: TaskItem)

    // Файлы перечитываются только после изменения — по Wi-Fi это заметно быстрее.
    private val seen = ConcurrentHashMap<String, Seen>()

    fun forget(path: String) = seen.keys.removeAll { it.startsWith("$path|") }

    fun manifest(folder: TeamFolder): TeamManifest? =
        runCatching { RitmJson.decodeFromString(TeamManifest.serializer(), String(folder.read("ritm-project.json"), Charsets.UTF_8)) }.getOrNull()

    fun exchange(store: Store, link: TeamLink, folder: TeamFolder): List<TeamChange> {
        val remote = folder.list("tasks").filter { it.name.endsWith(".json") }
        val present = remote.map { it.name.removeSuffix(".json") }.toSet()
        val incoming = mutableListOf<TaskItem>()
        for (file in remote) {
            val key = link.path + "|" + file.name
            var known = seen[key]
            if (known == null || known.stamp != file.stamp) {
                // Повреждённый файл не должен останавливать обмен остальными задачами — как на компьютере.
                val item = runCatching {
                    val bytes = folder.read("tasks\\" + file.name)
                    if (bytes.size > 2_000_000) null
                    else RitmJson.decodeFromString(TaskItem.serializer(), String(bytes, Charsets.UTF_8)).validated().takeIf { it.id == file.name.removeSuffix(".json") }
                }.getOrNull() ?: continue
                known = Seen(file.stamp, item)
                seen[key] = known
            }
            incoming += known.item
        }
        val changes = store.mergeTeam(link, incoming)
        for (t in store.teamRecords(link.name)) {
            val key = link.path + "|" + t.id + ".json"
            val server = seen[key]
            if (t.id in present && (server == null || Store.compare(t, server.item) <= 0)) continue
            folder.write("tasks\\" + t.id + ".json", RitmJson.encodeToString(TaskItem.serializer(), t).toByteArray(Charsets.UTF_8))
            seen.remove(key) // Отметку времени назначил сервер: файл перечитается один раз и совпадёт с локальной записью.
        }
        val members = runCatching { folder.list("members") }.getOrDefault(emptyList()).filter { it.name.endsWith(".json") }.mapNotNull { f ->
            runCatching { RitmJson.decodeFromString(TeamMember.serializer(), String(folder.read("members\\" + f.name), Charsets.UTF_8)).name }.getOrNull()?.takeIf { it.isNotBlank() }
        }
        store.updateTeam(link.name) { it.copy(members = members) }
        return changes
    }
}
