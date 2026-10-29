package ru.ritm.planner.ui

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import ru.ritm.planner.RitmApp
import ru.ritm.planner.data.FocusState
import ru.ritm.planner.data.FocusTimer
import ru.ritm.planner.data.NO_PROJECT
import ru.ritm.planner.data.SmartInput
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.data.newId
import ru.ritm.planner.notify.Notifications
import ru.ritm.planner.sync.Sync
import java.time.LocalDate

data class UiMessage(val text: String, val undo: (() -> Unit)? = null)

class AppViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application as RitmApp
    val store = app.store
    val records = store.records
    val prefs = store.prefs
    val syncBusy = Sync.busy

    private val _messages = MutableSharedFlow<UiMessage>(extraBufferCapacity = 8)
    val messages: SharedFlow<UiMessage> = _messages

    fun projects() = store.projectNames

    private fun say(text: String, undo: (() -> Unit)? = null) { _messages.tryEmit(UiMessage(text, undo)) }

    private inline fun attempt(block: () -> Unit) {
        try { block() } catch (e: Exception) { say(e.message ?: "Не удалось сохранить.") }
    }

    fun quickAdd(text: String, status: String = "todo", project: String? = null, date: String? = null): Boolean {
        if (text.isBlank()) return false
        val parsed = SmartInput.parse(text, LocalDate.now(), store.projectNames)
        var ok = false
        attempt {
            store.save(TaskItem(
                title = parsed.title, priority = parsed.priority,
                date = parsed.date ?: date,
                category = parsed.project ?: project ?: NO_PROJECT,
                status = status, completed = status == "done",
            ))
            ok = true
        }
        return ok
    }

    fun save(task: TaskItem): Boolean {
        var ok = false
        attempt { store.save(task); ok = true }
        return ok
    }

    fun toggleComplete(task: TaskItem) {
        val current = store.find(task.id) ?: return
        attempt {
            if (current.completed) {
                store.save(current.copy(completed = false, status = "todo"))
            } else {
                val saved = store.save(current.copy(completed = true, status = "done"))
                say("Задача выполнена") { attempt { store.save(saved.copy(completed = false, status = current.status ?: "todo")) } }
            }
        }
    }

    fun setStatus(task: TaskItem, status: String) {
        val current = store.find(task.id) ?: return
        if (current.status == status) return
        attempt { store.save(current.copy(status = status, completed = status == "done")) }
    }

    fun setDate(task: TaskItem, date: LocalDate?) {
        val current = store.find(task.id) ?: return
        attempt { store.save(current.copy(date = date?.toString())) }
    }

    fun toggleStep(task: TaskItem, stepId: String?) {
        val current = store.find(task.id) ?: return
        attempt { store.save(current.copy(subtasks = current.steps.map { if (it.id == stepId) it.copy(done = !it.done) else it })) }
    }

    fun duplicate(task: TaskItem) {
        attempt {
            store.save(task.copy(id = newId(), version = 0, createdAt = 0, completed = false, status = "todo",
                subtasks = task.steps.map { it.copy(id = newId(), done = false) }))
            say("Создана копия задачи")
        }
    }

    fun delete(task: TaskItem) {
        val current = store.find(task.id) ?: return
        attempt {
            val tomb = store.delete(current)
            say("Задача удалена") { attempt { store.save(tomb.copy(deleted = false)) } }
        }
    }

    fun createProject(name: String) = attempt { store.createProject(name) }
    fun renameProject(old: String, new: String) = attempt { store.renameProject(old, new) }
    fun deleteProject(name: String) = attempt { store.deleteProject(name); say("Проект удалён, задачи остались без проекта") }

    fun setTheme(theme: String) = store.updatePrefs { it.copy(theme = theme) }
    fun setPalette(id: String) = store.updatePrefs { it.copy(palette = id) }
    fun setReduceMotion(v: Boolean) = store.updatePrefs { it.copy(reduceMotion = v) }
    fun layout(context: String) = store.settings.taskLayouts[context]?.takeIf { it == "board" || it == "list" } ?: "board"
    fun setLayout(context: String, layout: String) = store.updatePrefs { it.copy(taskLayouts = it.taskLayouts + (context to layout)) }

    fun setDigest(enabled: Boolean, minutes: Int = store.settings.digestMinutes) {
        store.updatePrefs { it.copy(digestEnabled = enabled, digestMinutes = minutes) }
        Notifications.scheduleDigest(app)
    }

    // Фокус
    private fun updateFocus(change: (FocusState) -> FocusState) {
        store.updatePrefs { it.copy(focus = change(FocusTimer.normalize(it.focus))) }
        Notifications.syncFocus(app)
    }
    fun focusStart() = updateFocus { FocusTimer.start(it) }
    fun focusPause() = updateFocus { FocusTimer.pause(it) }
    fun focusReset() = updateFocus { FocusTimer.reset(it) }
    fun focusMode(mode: String) = updateFocus { FocusTimer.setMode(it, mode) }
    fun focusTask(id: String?) = store.updatePrefs { it.copy(focusTaskId = id) }
    fun focusTick() = app.completeFocusIfDue()

    // Google Диск
    fun driveConnected() {
        store.updatePrefs { it.copy(driveConnected = true, syncStatus = "Google Диск подключён. Синхронизация…") }
        Sync.schedulePeriodic(app, true)
        syncNow()
    }

    fun driveDisconnect() {
        store.updatePrefs { it.copy(driveConnected = false, syncStatus = null) }
        Sync.schedulePeriodic(app, false)
        say("Google Диск отключён. Задачи остались на телефоне.")
    }

    fun syncNow() {
        if (!store.settings.driveConnected) return
        viewModelScope.launch { Sync.run(app) }
    }

    fun driveError(message: String) {
        store.updatePrefs { it.copy(syncStatus = message) }
        say(message)
    }

    fun export(uri: Uri) = viewModelScope.launch {
        runCatching {
            withContext(Dispatchers.IO) {
                app.contentResolver.openOutputStream(uri, "wt")!!.use { it.write(store.exportJson().toByteArray(Charsets.UTF_8)) }
            }
        }.onSuccess { say("Резервная копия сохранена") }.onFailure { say("Не удалось сохранить файл: ${it.message}") }
    }

    fun import(uri: Uri) = viewModelScope.launch {
        runCatching {
            val text = withContext(Dispatchers.IO) { app.contentResolver.openInputStream(uri)!!.use { String(it.readBytes(), Charsets.UTF_8) } }
            store.importJson(text)
        }.onSuccess { say("Импортировано записей: $it") }.onFailure { say(it.message ?: "Не удалось импортировать файл") }
    }
}
