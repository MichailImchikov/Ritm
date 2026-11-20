package ru.ritm.planner

import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.ritm.planner.data.FocusState
import ru.ritm.planner.data.FocusTimer
import ru.ritm.planner.data.MeetingSchedule
import ru.ritm.planner.data.NO_PROJECT
import ru.ritm.planner.data.NotificationKinds
import ru.ritm.planner.data.PhoneTeam
import ru.ritm.planner.data.PhoneTeams
import ru.ritm.planner.data.Preferences
import ru.ritm.planner.data.RitmException
import ru.ritm.planner.data.RitmJson
import ru.ritm.planner.data.SmartInput
import ru.ritm.planner.data.Store
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.data.TeamLink
import ru.ritm.planner.data.TeamPath
import ru.ritm.planner.sync.DriveClient
import ru.ritm.planner.sync.TeamFolder
import ru.ritm.planner.sync.TeamSync
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

    /** Запись с компьютера с исполнителем, встречей и вложением проходит через телефон без потерь. */
    @Test fun keepsDesktopFieldsOnEdit() {
        val json = """{"id":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","title":"Созвон","description":"","date":"2026-11-02","priority":3,"category":"Команда","completed":false,"subtasks":[],"version":1,"createdAt":1,"updatedUtc":"2026-11-02T09:00:00Z","deviceId":"desk","deleted":false,"status":"todo","recordType":"task","isMeeting":true,"meetingHue":120,"meetingTransparency":10,"meetingColor":"#112233","meetingTime":"10:30","meetingDays":[1,3],"assignee":"Анна","createdBy":"Борис","attachments":[{"id":"3f2504e0-4f89-11d3-9a0c-0305e82c3302","name":"план.pdf","file":"a.pdf","size":10,"addedUtc":"2026-11-02T09:00:00Z","addedBy":"Анна","IsImage":false,"Extension":".pdf"}]}"""
        val store = Store(tempDir())
        store.merge(listOf(RitmJson.decodeFromString(TaskItem.serializer(), json)))
        val edited = store.save(store.records.value.single().copy(title = "Созвон 2"))
        val back = RitmJson.encodeToString(TaskItem.serializer(), edited)
        for (part in listOf("\"meetingTime\":\"10:30\"", "\"meetingDays\":[1,3]", "\"assignee\":\"Анна\"", "\"createdBy\":\"Борис\"", "\"file\":\"a.pdf\"", "\"meetingHue\":120"))
            assertTrue(part, back.contains(part))
    }

    private fun team(store: Store, name: String = "Команда", user: String = "Анна", synced: Boolean = true): TeamLink {
        store.addTeam(TeamLink("\\\\srv\\work\\$name", name, user, synced = synced))
        return store.team(name)!!
    }

    private class MemoryFolder : TeamFolder {
        val files = LinkedHashMap<String, Pair<Long, ByteArray>>()
        var clock = 1L
        override fun list(dir: String) = files.filterKeys { it.startsWith("$dir\\") }.map { TeamFolder.Entry(it.key.substringAfter("\\"), it.value.first) }
        override fun read(path: String) = files[path]?.second ?: throw java.io.FileNotFoundException(path)
        override fun write(path: String, bytes: ByteArray) { files[path] = ++clock to bytes }
        override fun close() {}
        fun put(t: TaskItem) = write("tasks\\${t.id}.json", RitmJson.encodeToString(TaskItem.serializer(), t).toByteArray())
    }

    private fun remote(title: String, createdBy: String? = "Борис", assignee: String? = null) = TaskItem(
        title = title, category = "Команда", createdBy = createdBy, assignee = assignee, version = 1, deviceId = "desk", updatedUtc = "2026-01-02T09:00:00Z",
    )

    /** Обмен с папкой сервера: чужие задачи приходят, свои правки уходят, а лишних записей нет. */
    @Test fun serverFolderExchange() {
        val store = Store(tempDir()); val link = team(store); val folder = MemoryFolder()
        folder.put(remote("С компьютера"))
        folder.write("tasks\\broken.json", "{не json".toByteArray())
        folder.write("members\\a.json", """{"name":"Борис","lastSeenUtc":"2026-11-02T09:00:00Z"}""".toByteArray())
        val changes = TeamSync.exchange(store, link, folder)
        assertEquals(1, changes.size); assertTrue(changes.single().added)
        assertEquals(listOf("Борис"), store.team("Команда")!!.members)
        assertEquals(listOf("Анна", "Борис"), store.members("Команда"))
        val writes = folder.clock
        assertTrue("без изменений ничего не пишется", TeamSync.exchange(store, store.team("Команда")!!, folder).isEmpty())
        assertEquals(writes, folder.clock)
        val mine = store.save(TaskItem(title = "С телефона", category = "Команда"))
        assertEquals("автор подставляется из проекта", "Анна", mine.createdBy)
        TeamSync.exchange(store, store.team("Команда")!!, folder)
        val written = RitmJson.decodeFromString(TaskItem.serializer(), String(folder.read("tasks\\${mine.id}.json")))
        assertEquals("С телефона", written.title)
        Thread.sleep(5)
        val task = store.tasks.first { it.title == "С компьютера" }
        val assigned = store.save(task.copy(status = "doing"))
        TeamSync.exchange(store, store.team("Команда")!!, folder)
        assertEquals("doing", RitmJson.decodeFromString(TaskItem.serializer(), String(folder.read("tasks\\${assigned.id}.json"))).status)
        try { store.save(store.tasks.first().copy(category = NO_PROJECT)); throw AssertionError("guard expected") } catch (_: RitmException) {}
    }

    @Test fun teamChangesLikeDesktop() {
        val store = Store(tempDir()); val link = team(store)
        val first = remote("Для меня", assignee = "анна")
        val change = store.mergeTeam(link, listOf(first)).single()
        assertTrue(change.added && change.assignedToMe)
        assertTrue("моя задача с компьютера не новость", store.mergeTeam(link, listOf(remote("Своя", createdBy = "Анна"))).isEmpty())
        val silent = team(store, "Тихий", synced = false)
        assertTrue("первая синхронизация молчит", store.mergeTeam(silent, listOf(remote("x").copy(category = "Тихий"))).isEmpty())
        assertTrue("задача чужого проекта не применяется", store.mergeTeam(link, listOf(remote("y").copy(category = "Другой"))).isEmpty())
        assertFalse(store.isMine(remote("Чужая", assignee = "Борис")))
        assertTrue(store.isMine(TaskItem(title = "Личная")))
    }

    /** Через Диск записи общего проекта идут отдельно; список проектов приходит с компьютера. */
    @Test fun teamsOverDrive() = runBlocking {
        val cloud = FakeDrive(); val store = Store(tempDir()); val link = team(store); val client = DriveClient(tempDir(), { "t" }, cloud)
        store.save(TaskItem(title = "Общая", category = "Команда"))
        store.save(TaskItem(title = "Личная"))
        client.push(store.personalRecords, client.pull())
        client.push(store.teamRecords(link.name), client.pull(DriveClient.TEAM), DriveClient.TEAM)
        assertEquals("Личная", client.pull().items.single().title)
        assertEquals("Общая", client.pull(DriveClient.TEAM).items.single().title)
        assertNull(client.teams())
        cloud.files["t"] = DriveClient.TEAMS to """{"app":"ritm-teams","version":1,"teams":[{"name":"Команда","path":"\\\\srv\\new","user":"Анна Б","members":["Борис"]},{"name":"Новый","path":"\\\\srv\\n","user":"Анна"}]}""".toByteArray()
        store.applyTeams(client.teams()!!)
        assertEquals(listOf("Команда", "Новый"), store.teams.map { it.name })
        assertEquals("\\\\srv\\new", store.team("Команда")!!.path)
        assertTrue("синхронизированный проект не начинается заново", store.team("Команда")!!.synced)
        store.applyTeams(PhoneTeams(teams = listOf(PhoneTeam("Новый", "\\\\srv\\n", "Анна"))))
        assertEquals(listOf("Новый"), store.teams.map { it.name })
        assertTrue("задачи отключённого проекта убираются", store.tasks.none { it.category == "Команда" })
        cloud.files["n"] = "${DriveClient.NOTICE}1.json" to """{"id":"1","kind":"build.done","title":"Сборка","text":"Готово","createdUtc":"2026-11-02T09:00:00Z"}""".toByteArray()
        val (fresh, names) = client.notices(emptyList())
        assertEquals("build.done", fresh.single().kind)
        assertTrue(client.notices(names).first.isEmpty())
    }

    @Test fun meetingSchedule() {
        val monday = LocalDate.of(2026, 11, 2)
        val weekly = TaskItem(title = "Планёрка", isMeeting = true, date = "2026-11-02", meetingTime = "10:30", meetingDays = listOf(1, 0))
        assertTrue(MeetingSchedule.occursOn(weekly, monday))
        assertTrue("воскресенье — 0", MeetingSchedule.occursOn(weekly, LocalDate.of(2026, 11, 8)))
        assertFalse(MeetingSchedule.occursOn(weekly, monday.plusDays(1)))
        assertFalse("до даты начала встречи нет", MeetingSchedule.occursOn(weekly, monday.minusDays(6)))
        assertTrue(MeetingSchedule.due(weekly, monday, monday.atTime(10, 20), 10))
        assertTrue(MeetingSchedule.due(weekly, monday, monday.atTime(10, 33), 0))
        assertFalse(MeetingSchedule.due(weekly, monday, monday.atTime(10, 36), 0))
        assertEquals(monday.atTime(10, 20), MeetingSchedule.next(listOf(weekly), monday.atTime(9, 0)))
        assertEquals(monday.atTime(10, 30), MeetingSchedule.next(listOf(weekly), monday.atTime(10, 20)))
        assertEquals(LocalDate.of(2026, 11, 8).atTime(10, 20), MeetingSchedule.next(listOf(weekly), monday.atTime(11, 0)))
        assertEquals("10:30 · пн, вс", MeetingSchedule.label(weekly))
    }

    @Test fun notificationChoices() {
        val p = Preferences()
        assertEquals(6, NotificationKinds.all.size)
        assertTrue(NotificationKinds.isOn(p, NotificationKinds.BuildDone))
        assertFalse(NotificationKinds.isOn(p.copy(notificationsMuted = true), NotificationKinds.BuildDone))
        assertFalse(NotificationKinds.isOn(p.copy(notifications = mapOf("build.done" to false)), NotificationKinds.BuildDone))
        assertTrue(NotificationKinds.isOn(p.copy(notifications = mapOf("build.done" to false)), NotificationKinds.Meeting))
    }

    @Test fun serverPathParsing() {
        val p = TeamPath.parse("\\\\192.168.1.97\\work\\ПРОЕКТЫ\\2026\\Программисты")!!
        assertEquals("192.168.1.97", p.host); assertEquals("work", p.share)
        assertEquals("ПРОЕКТЫ\\2026\\Программисты\\tasks", p.join("tasks"))
        assertEquals("tasks", TeamPath.parse("\\\\srv\\share")!!.join("tasks"))
        assertNull(TeamPath.parse("Z:\\Проект"))
        assertNull(TeamPath.parse("\\\\srv"))
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
