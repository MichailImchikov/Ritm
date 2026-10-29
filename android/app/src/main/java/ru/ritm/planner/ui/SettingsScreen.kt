package ru.ritm.planner.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TimePicker
import androidx.compose.material3.rememberTimePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import ru.ritm.planner.BuildConfig
import ru.ritm.planner.data.Preferences

class SettingsActions(
    val connectDrive: () -> Unit,
    val export: () -> Unit,
    val import: () -> Unit,
    val exactAlarms: (() -> Unit)?,
    val batterySettings: () -> Unit,
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(prefs: Preferences, busy: Boolean, vm: AppViewModel, actions: SettingsActions) {
    var timeDialog by remember { mutableStateOf(false) }
    BigTitleScaffold(title = "Настройки", subtitle = null) { padding ->
        LazyColumn(
            Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = padding.calculateTopPadding(), bottom = 32.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            item {
                Group("Оформление") {
                    SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
                        listOf("system" to "Система", "light" to "Светлая", "dark" to "Тёмная").forEachIndexed { i, (k, n) ->
                            SegmentedButton(prefs.theme == k, { vm.setTheme(k) }, SegmentedButtonDefaults.itemShape(i, 3)) { Text(n, maxLines = 1) }
                        }
                    }
                    Spacer(Modifier.height(16.dp))
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Palettes.forEach { p ->
                            Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.clip(MaterialTheme.shapes.small).clickable { vm.setPalette(p.id) }.padding(4.dp)) {
                                val selected = prefs.palette == p.id
                                Box(
                                    Modifier.size(44.dp).clip(CircleShape).background(Color(p.light[0]))
                                        .border(if (selected) 3.dp else 0.dp, MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f), CircleShape),
                                    contentAlignment = Alignment.Center,
                                ) { if (selected) Icon(Icons.Default.Check, null, tint = Color.White) }
                                Spacer(Modifier.height(4.dp))
                                Text(p.name, style = MaterialTheme.typography.labelMedium)
                            }
                        }
                    }
                    Spacer(Modifier.height(8.dp))
                    SwitchRow("Плавные анимации интерфейса", null, !prefs.reduceMotion) { vm.setReduceMotion(!it) }
                }
            }
            item {
                Group("Google Диск") {
                    Text(
                        "Синхронизация с Ритмом на компьютере через скрытую папку приложения на вашем Google Диске.",
                        style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(Modifier.height(10.dp))
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        if (busy) { CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp); Spacer(Modifier.width(10.dp)) }
                        Text(
                            prefs.syncStatus ?: if (prefs.driveConnected) "Подключено" else "Не подключено · задачи хранятся только на телефоне",
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                    Spacer(Modifier.height(12.dp))
                    if (prefs.driveConnected) Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Button(vm::syncNow, enabled = !busy) { Text("Синхронизировать") }
                        OutlinedButton(vm::driveDisconnect) { Text("Отключить") }
                    } else Button(actions.connectDrive) { Text("Войти через Google") }
                }
            }
            item {
                Group("Напоминания") {
                    SwitchRow("План на день", "Каждое утро — задачи на сегодня и просроченные", prefs.digestEnabled) { vm.setDigest(it) }
                    if (prefs.digestEnabled) TextButton({ timeDialog = true }) {
                        Text("Время: %02d:%02d".format(prefs.digestMinutes / 60, prefs.digestMinutes % 60))
                    }
                    HorizontalDivider(Modifier.padding(vertical = 8.dp))
                    if (actions.exactAlarms != null) {
                        Hint("Чтобы сигнал таймера фокуса звучал точно вовремя, разрешите «Будильники и напоминания».")
                        TextButton(actions.exactAlarms) { Text("Разрешить точные будильники") }
                    }
                    Hint("Samsung может усыплять приложения. Если уведомления опаздывают: Батарея → «Без ограничений».")
                    TextButton(actions.batterySettings) { Text("Открыть настройки приложения") }
                }
            }
            item {
                Group("Данные") {
                    Hint("Резервная копия совместима с Ритмом для Windows. Импорт добавляет записи как новые.")
                    Spacer(Modifier.height(8.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        OutlinedButton(actions.export) { Text("Экспорт JSON") }
                        OutlinedButton(actions.import) { Text("Импорт JSON") }
                    }
                }
            }
            item {
                Text("Ритм для Android ${BuildConfig.VERSION_NAME}", Modifier.fillMaxWidth().padding(top = 8.dp),
                    textAlign = TextAlign.Center, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
    if (timeDialog) {
        val state = rememberTimePickerState(prefs.digestMinutes / 60, prefs.digestMinutes % 60, is24Hour = true)
        AlertDialog(
            onDismissRequest = { timeDialog = false },
            confirmButton = { TextButton({ vm.setDigest(true, state.hour * 60 + state.minute); timeDialog = false }) { Text("Готово") } },
            dismissButton = { TextButton({ timeDialog = false }) { Text("Отмена") } },
            text = { TimePicker(state) },
        )
    }
}

@Composable
private fun Group(title: String, content: @Composable ColumnScope.() -> Unit) {
    Surface(shape = MaterialTheme.shapes.large, color = MaterialTheme.colorScheme.surfaceContainerLow, tonalElevation = 1.dp) {
        Column(Modifier.fillMaxWidth().padding(18.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.primary)
            Spacer(Modifier.height(12.dp))
            content()
        }
    }
}

@Composable
private fun Hint(text: String) =
    Text(text, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)

@Composable
private fun SwitchRow(title: String, subtitle: String?, checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(Modifier.fillMaxWidth().clip(MaterialTheme.shapes.small).clickable { onChange(!checked) }.padding(vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge)
            if (subtitle != null) Hint(subtitle)
        }
        Switch(checked, onChange)
    }
}
