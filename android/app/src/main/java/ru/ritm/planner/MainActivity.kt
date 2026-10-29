package ru.ritm.planner

import android.Manifest
import android.app.AlarmManager
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.IntentSenderRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.Checklist
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.WbSunny
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarDuration
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.SnackbarResult
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.lifecycleScope
import com.google.android.gms.auth.api.identity.Identity
import com.google.android.gms.common.api.ApiException
import com.google.android.gms.common.api.CommonStatusCodes
import kotlinx.coroutines.launch
import ru.ritm.planner.data.NO_PROJECT
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.sync.GoogleAuth
import ru.ritm.planner.ui.AppViewModel
import ru.ritm.planner.ui.CalendarScreen
import ru.ritm.planner.ui.DatePick
import ru.ritm.planner.ui.LocalReduceMotion
import ru.ritm.planner.ui.MyDayScreen
import ru.ritm.planner.ui.QuickAddSheet
import ru.ritm.planner.ui.RitmTheme
import ru.ritm.planner.ui.SettingsActions
import ru.ritm.planner.ui.SettingsScreen
import ru.ritm.planner.ui.TaskActions
import ru.ritm.planner.ui.TaskEditor
import ru.ritm.planner.ui.TasksScreen
import ru.ritm.planner.ui.isDark
import java.time.LocalDate

private data class QuickAdd(val title: String, val status: String = "todo", val project: String? = null, val date: String? = null)

class MainActivity : ComponentActivity() {
    private val vm: AppViewModel by viewModels()
    private var tab by mutableStateOf("today")
    private var quick by mutableStateOf<QuickAdd?>(null)
    private var editing by mutableStateOf<TaskItem?>(null)

    private val authLauncher = registerForActivityResult(ActivityResultContracts.StartIntentSenderForResult()) { result ->
        if (result.resultCode != RESULT_OK) { vm.driveError("Вход в Google отменён."); return@registerForActivityResult }
        try {
            val auth = Identity.getAuthorizationClient(this).getAuthorizationResultFromIntent(result.data)
            if (auth.accessToken != null) vm.driveConnected() else vm.driveError("Google не выдал доступ к Диску.")
        } catch (e: ApiException) {
            vm.driveError(authError(e))
        }
    }
    private val exportLauncher = registerForActivityResult(ActivityResultContracts.CreateDocument("application/json")) { it?.let(vm::export) }
    private val importLauncher = registerForActivityResult(ActivityResultContracts.OpenDocument()) { it?.let(vm::import) }
    private val notifyPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) {}

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        handle(intent)
        if (Build.VERSION.SDK_INT >= 33 && savedInstanceState == null) notifyPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
        setContent {
            val prefs by vm.prefs.collectAsStateWithLifecycle()
            val records by vm.records.collectAsStateWithLifecycle()
            val busy by vm.syncBusy.collectAsStateWithLifecycle()
            val dark = isDark(prefs.theme, isSystemInDarkTheme())
            LaunchedEffect(dark) {
                val style = if (dark) SystemBarStyle.dark(android.graphics.Color.TRANSPARENT)
                else SystemBarStyle.light(android.graphics.Color.TRANSPARENT, android.graphics.Color.TRANSPARENT)
                enableEdgeToEdge(style, style)
            }
            RitmTheme(prefs.theme, prefs.palette, prefs.reduceMotion) {
                App(records, prefs, busy)
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handle(intent)
    }

    override fun onResume() {
        super.onResume()
        (application as RitmApp).completeFocusIfDue()
        vm.syncNow()
    }

    /** Открытие из виджета, уведомления или ярлыка на иконке. */
    private fun handle(intent: Intent?) {
        intent ?: return
        intent.getStringExtra(EXTRA_TAB)?.let { tab = it }
        val action = intent.getStringExtra(EXTRA_ACTION) ?: intent.data?.takeIf { it.scheme == "ritm" && it.host == "shortcut" }?.lastPathSegment
        when {
            action == "quick" -> { tab = "today"; quick = QuickAdd("Новая задача на сегодня", date = LocalDate.now().toString()) }
            action == "focus" -> { tab = "today"; vm.focusStart() }
            action?.startsWith("task:") == true -> vm.store.find(action.removePrefix("task:"))?.let { editing = it }
        }
    }

    private fun connectDrive() = lifecycleScope.launch {
        try {
            val result = GoogleAuth.authorize(this@MainActivity)
            val pending = result.pendingIntent
            if (result.hasResolution() && pending != null) authLauncher.launch(IntentSenderRequest.Builder(pending.intentSender).build())
            else vm.driveConnected()
        } catch (e: ApiException) {
            vm.driveError(authError(e))
        } catch (e: Exception) {
            vm.driveError("Не удалось войти в Google: ${e.message}")
        }
    }

    private fun authError(e: ApiException) = when (e.statusCode) {
        CommonStatusCodes.DEVELOPER_ERROR -> "Google не узнал приложение. Создайте OAuth-клиент Android (пакет ru.ritm.planner и SHA-1 ключа) в том же проекте Google Cloud, что и для компьютера."
        CommonStatusCodes.NETWORK_ERROR -> "Нет соединения с Google. Проверьте интернет."
        CommonStatusCodes.CANCELED -> "Вход в Google отменён."
        else -> "Google не подтвердил вход (код ${e.statusCode})."
    }

    private fun exactAlarmsAction(): (() -> Unit)? {
        if (Build.VERSION.SDK_INT < 31 || getSystemService(AlarmManager::class.java).canScheduleExactAlarms()) return null
        return { startActivity(Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM, Uri.parse("package:$packageName"))) }
    }

    @Composable
    private fun App(records: List<TaskItem>, prefs: ru.ritm.planner.data.Preferences, busy: Boolean) {
        val snackbar = remember { SnackbarHostState() }
        var picking by remember { mutableStateOf<TaskItem?>(null) }
        LaunchedEffect(Unit) {
            vm.messages.collect { m ->
                val result = snackbar.showSnackbar(m.text, actionLabel = if (m.undo != null) "Отменить" else null, duration = SnackbarDuration.Short)
                if (result == SnackbarResult.ActionPerformed) m.undo?.invoke()
            }
        }
        val actions = remember {
            TaskActions(
                open = { editing = it },
                toggle = vm::toggleComplete,
                status = vm::setStatus,
                date = vm::setDate,
                pickDate = { picking = it },
                duplicate = vm::duplicate,
                delete = vm::delete,
                focus = { vm.focusTask(it.id); tab = "today" },
            )
        }
        val projects = remember(records) { vm.projects() }
        Scaffold(
            contentWindowInsets = WindowInsets(0, 0, 0, 0),
            snackbarHost = { SnackbarHost(snackbar) },
            bottomBar = {
                NavigationBar(containerColor = MaterialTheme.colorScheme.surfaceContainerLow) {
                    listOf(
                        Triple("today", "Мой день", Icons.Default.WbSunny),
                        Triple("tasks", "Задачи", Icons.Default.Checklist),
                        Triple("calendar", "Календарь", Icons.Default.CalendarMonth),
                        Triple("settings", "Настройки", Icons.Default.Settings),
                    ).forEach { (key, label, icon) ->
                        NavigationBarItem(tab == key, { tab = key }, { Icon(icon, null) }, label = { Text(label) })
                    }
                }
            },
        ) { padding ->
            val still = LocalReduceMotion.current
            AnimatedContent(tab, Modifier.fillMaxSize().padding(bottom = padding.calculateBottomPadding()),
                transitionSpec = { fadeIn(tween(if (still) 0 else 180)) togetherWith fadeOut(tween(if (still) 0 else 120)) }, label = "tab") { current ->
                Box(Modifier.fillMaxSize()) {
                    when (current) {
                        "today" -> MyDayScreen(records, prefs.focus, prefs.focusTaskId, vm, actions) {
                            quick = QuickAdd("Задача на сегодня", date = LocalDate.now().toString())
                        }
                        "tasks" -> TasksScreen(records, projects, vm, actions) { status, project ->
                            quick = QuickAdd(if (project == null || project == NO_PROJECT) "Новая задача" else "Задача в «$project»", status, project)
                        }
                        "calendar" -> CalendarScreen(records, actions) { day ->
                            quick = QuickAdd("Задача на ${ru.ritm.planner.data.Dates.friendly(day.toString()).lowercase()}", date = day.toString())
                        }
                        else -> SettingsScreen(prefs, busy, vm, SettingsActions(
                            connectDrive = { connectDrive() },
                            export = { exportLauncher.launch("ritm-backup-${LocalDate.now()}.json") },
                            import = { importLauncher.launch(arrayOf("application/json", "text/plain", "*/*")) },
                            exactAlarms = exactAlarmsAction(),
                            batterySettings = { startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:$packageName"))) },
                        ))
                    }
                }
            }
        }

        quick?.let { q ->
            QuickAddSheet(q.title, projects, { quick = null }) { text -> vm.quickAdd(text, q.status, q.project, q.date) }
        }
        editing?.let { task ->
            val fresh = vm.store.find(task.id) ?: task
            TaskEditor(fresh, projects, { editing = null }, vm::save, vm::delete)
        }
        picking?.let { task -> DatePick(task.localDate, { picking = null }) { vm.setDate(task, it) } }
    }

    companion object {
        const val EXTRA_TAB = "tab"
        const val EXTRA_ACTION = "action"
    }
}
