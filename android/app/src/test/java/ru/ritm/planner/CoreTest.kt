package ru.ritm.planner

import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.ritm.planner.data.FocusState
import ru.ritm.planner.data.FocusTimer
import ru.ritm.planner.data.NO_PROJECT
import ru.ritm.planner.data.RitmException
import ru.ritm.planner.data.RitmJson
import ru.ritm.planner.data.SmartInput
import ru.ritm.planner.data.Store
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.sync.DriveClient
import java.io.File
import java.nio.file.Files
import java.time.Instant
import java.time.LocalDate

class CoreTest {
    private fun tempDir(): File = Files.createTempDirectory("ritm").toFile()

    @Test fun smartInputParsesDatePriorityProject() {
        val today = LocalDate.of(2026, 10, 29) // четверг
        val t = SmartInput.parse("Позвонить маме завтра !1 #личное", today, listOf("Личное"))
        assertEquals("Позвонить маме", t.title)
        assertEquals("2026-10-30", t.date)
        assertEquals(1, t.priority)
        assertEquals("Личное", t.project)
        assertEquals("2026-10-30", SmartInput.parse("Отчёт в пятницу", today, emptyList()).date)
        assertEquals("2026-11-05", SmartInput.parse("Встреча чт", today, emptyList()).date)
        assertEquals("2026-11-15", SmartInput.parse("Сдать 15.11", today, emptyList()).date)
        assertEquals("2027-01-03", SmartInput.parse("Ёлка 3.1", today, emptyList()).date)
        assertEquals("Мой проект", SmartInput.parse("x #Мой_проект", today, emptyList()).project)
        assertEquals("сегодня", SmartInput.parse("сегодня", today, emptyList()).title)
    }

    @Test fun storeVersionsAndConflicts() {
        var now = Instant.parse("2026-10-29T08:00:00Z")
        val store = Store(tempDir()) { now }
        val saved = store.save(TaskItem(title = "  Купить хлеб  "))
        assertEquals("Купить хлеб", saved.title)
        assertEquals(1, saved.version)
        assertEquals(NO_PROJECT, saved.category)
        now = now.plusSeconds(5)
        val second = store.save(saved.copy(title = "Купить батон"))
        assertEquals(2, second.version)
        try { store.save(saved.copy(title = "Старая версия")); throw AssertionError("conflict expected") } catch (_: RitmException) {}
        store.delete(second)
        assertTrue(store.tasks.isEmpty())
        // Переоткрытие: данные читаются с диска.
        val again = Store(store.root)
        assertTrue(again.tasks.isEmpty())
        assertEquals(1, again.records.value.size)
    }

    @Test fun completedForcesDoneStatus() {
        val t = TaskItem(title = "a", completed = true, status = "todo").validated()
        assertEquals("done", t.status)
        assertEquals("todo", TaskItem(title = "a", completed = false, status = "done").validated().status)
    }

    @Test fun projectsRenameAndDelete() {
        val store = Store(tempDir())
        store.createProject("Работа")
        store.save(TaskItem(title = "Отчёт", category = "Работа"))
        store.renameProject("Работа", "Офис")
        assertEquals(listOf("Офис"), store.projectNames)
        assertEquals("Офис", store.tasks.single().category)
        store.deleteProject("Офис")
        assertTrue(store.projectNames.isEmpty())
        assertEquals(NO_PROJECT, store.tasks.single().category)
    }

    /** Файл в формате десктопного JavaScriptSerializer читается без потерь. */
    @Test fun readsDesktopJson() {
        val json = """{"id":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","title":"Подвести итоги месяца","description":"","date":"2026-10-28","priority":2,"category":"Личное","completed":true,"subtasks":[{"id":"a1","title":"Шаг","done":true}],"version":3,"createdAt":1761638400000,"updatedUtc":"2026-10-28T09:15:42.1234567Z","deviceId":"desk","deleted":false,"status":"done","recordType":"task"}"""
        val t = RitmJson.decodeFromString(TaskItem.serializer(), json).validated()
        assertEquals("Личное", t.category)
        assertEquals(Instant.parse("2026-10-28T09:15:42.1234567Z"), Store.parse(t.updatedUtc))
        val store = Store(tempDir())
        assertTrue(store.merge(listOf(t)))
        assertFalse(store.merge(listOf(t)))
    }

    @Test fun focusTimerCycle() {
        val t0 = Instant.parse("2026-10-29T08:00:00Z")
        var s = FocusTimer.start(FocusState(), t0)
        assertEquals(1500, FocusTimer.remaining(s, t0))
        assertNull(FocusTimer.tick(s, t0.plusSeconds(100)))
        s = FocusTimer.pause(s, t0.plusSeconds(100))
        assertEquals(1400, FocusTimer.remaining(s, t0.plusSeconds(9999)))
        s = FocusTimer.start(s, t0.plusSeconds(200))
        val next = FocusTimer.tick(s, t0.plusSeconds(1600))!!
        assertEquals("break", next.mode)
        assertEquals(1, next.sessions)
        assertEquals(300, next.remainingSeconds)
    }

    /** Две «машины» обмениваются через поддельный Google Диск: создание, правка, удаление, целостность. */
    @Test fun driveProtocolRoundTrip() = runBlocking {
        val cloud = FakeDrive()
        val a = Store(tempDir()); val b = Store(tempDir())
        val ca = DriveClient(tempDir(), { "t" }, cloud); val cb = DriveClient(tempDir(), { "t" }, cloud)
        a.createProject("Облако")
        a.save(TaskItem(title = "Облачная задача", category = "Облако", status = "doing"))
        ca.push(a.records.value, ca.pull())
        val count = cloud.files.size
        ca.push(a.records.value, ca.pull())
        assertEquals("повторная отправка не создаёт файлов", count, cloud.files.size)
        b.merge(cb.pull().items)
        assertEquals("doing", b.tasks.single().status)
        assertTrue("Облако" in b.projectNames)
        Thread.sleep(5)
        val edited = b.save(b.tasks.single().copy(title = "Версия B"))
        cb.push(b.records.value, cb.pull())
        a.merge(ca.pull().items)
        assertEquals("Версия B", a.tasks.single().title)
        Thread.sleep(5)
        a.delete(a.tasks.single())
        ca.push(a.records.value, ca.pull())
        b.merge(cb.pull().items)
        assertTrue(b.tasks.isEmpty())
        assertEquals(edited.id, b.records.value.first { it.deleted }.id)
        // Подмена содержимого файла обнаруживается.
        val key = cloud.files.keys.first()
        cloud.files[key] = cloud.files[key]!!.first to cloud.files[key]!!.second.toString(Charsets.UTF_8).replace("Облако", "Подмена").toByteArray()
        try { DriveClient(tempDir(), { "t" }, cloud).pull(); throw AssertionError("integrity check expected") } catch (_: RitmException) {}
    }

    private class FakeDrive : DriveClient.Transport {
        val files = LinkedHashMap<String, Pair<String, ByteArray>>() // id → (name, content)
        override fun send(method: String, url: String, token: String, contentType: String?, body: ByteArray?): Pair<Int, ByteArray> {
            if (method == "GET" && url.contains("alt=media")) {
                val id = url.substringAfter("/files/").substringBefore("?")
                return 200 to files[id]!!.second
            }
            if (method == "GET") {
                val list = files.entries.joinToString(",") { """{"id":"${it.key}","name":"${it.value.first}"}""" }
                return 200 to """{"files":[$list]}""".toByteArray()
            }
            val boundary = contentType!!.substringAfter("boundary=")
            val parts = String(body!!, Charsets.UTF_8).split("--$boundary").map { it.substringAfter("\r\n\r\n").removeSuffix("\r\n") }
            val name = Regex("\"name\":\"([^\"]+)\"").find(parts[1])!!.groupValues[1]
            val id = "f" + files.size
            files[id] = name to parts[2].toByteArray(Charsets.UTF_8)
            return 200 to """{"id":"$id"}""".toByteArray()
        }
    }
}
