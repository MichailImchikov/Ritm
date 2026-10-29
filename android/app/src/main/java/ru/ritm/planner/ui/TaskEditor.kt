package ru.ritm.planner.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.DeleteOutline
import androidx.compose.material.icons.filled.Event
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.unit.dp
import ru.ritm.planner.data.Dates
import ru.ritm.planner.data.NO_PROJECT
import ru.ritm.planner.data.Step
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.data.newId
import java.time.LocalDate

/** Форма задачи: название, описание, проект, дата, приоритет, статус, подзадачи. */
@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun TaskEditor(
    original: TaskItem,
    projects: List<String>,
    onDismiss: () -> Unit,
    onSave: (TaskItem) -> Boolean,
    onDelete: (TaskItem) -> Unit,
) {
    val sheet = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    var title by remember { mutableStateOf(original.title) }
    var description by remember { mutableStateOf(original.description ?: "") }
    var project by remember { mutableStateOf(original.category) }
    var date by remember { mutableStateOf(original.localDate) }
    var priority by remember { mutableStateOf(original.priority) }
    var status by remember { mutableStateOf(original.status ?: "todo") }
    val steps = remember { mutableStateListOf<Step>().apply { addAll(original.steps) } }
    var newStep by remember { mutableStateOf("") }
    var picking by remember { mutableStateOf(false) }
    var projectMenu by remember { mutableStateOf(false) }
    var newProject by remember { mutableStateOf<String?>(null) }
    val isNew = original.version == 0

    fun build() = original.copy(
        title = title, description = description, category = project.ifBlank { NO_PROJECT },
        date = date?.toString(), priority = priority, status = status, completed = status == "done", subtasks = steps.toList(),
    )

    ModalBottomSheet(onDismissRequest = onDismiss, sheetState = sheet) {
        Column(Modifier.fillMaxSize().imePadding()) {
            Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp), verticalAlignment = Alignment.CenterVertically) {
                IconButton(onDismiss) { Icon(Icons.Default.Close, "Закрыть") }
                Text(if (isNew) "Новая задача" else "Задача", style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                if (!isNew) IconButton({ onDelete(original); onDismiss() }) { Icon(Icons.Default.DeleteOutline, "Удалить", tint = Ritm.Urgent) }
                Button({ if (onSave(build())) onDismiss() }, enabled = title.isNotBlank(), modifier = Modifier.padding(end = 8.dp)) { Text("Сохранить") }
            }
            Column(Modifier.weight(1f).verticalScroll(rememberScrollState()).padding(horizontal = 20.dp, vertical = 8.dp)) {
                OutlinedTextField(
                    title, { title = it.take(200) }, Modifier.fillMaxWidth(), label = { Text("Название") },
                    textStyle = MaterialTheme.typography.titleMedium, shape = MaterialTheme.shapes.medium,
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences),
                )
                Spacer(Modifier.height(12.dp))
                OutlinedTextField(
                    description, { description = it.take(10000) }, Modifier.fillMaxWidth().height(120.dp), label = { Text("Описание") },
                    shape = MaterialTheme.shapes.medium, keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences),
                )
                Spacer(Modifier.height(16.dp))
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    androidx.compose.foundation.layout.Box {
                        AssistChip(
                            onClick = { projectMenu = true },
                            label = { Text(if (project == NO_PROJECT) "Без проекта" else project) },
                            leadingIcon = { Icon(Icons.Default.Folder, null, tint = if (project == NO_PROJECT) MaterialTheme.colorScheme.onSurfaceVariant else Ritm.project(project)) },
                        )
                        DropdownMenu(projectMenu, { projectMenu = false }) {
                            DropdownMenuItem({ Text("Без проекта") }, { project = NO_PROJECT; projectMenu = false })
                            projects.forEach { p -> DropdownMenuItem({ Text(p) }, { project = p; projectMenu = false }) }
                            DropdownMenuItem({ Text("+ Новый проект…") }, { projectMenu = false; newProject = "" })
                        }
                    }
                    AssistChip(
                        onClick = { picking = true },
                        label = { Text(date?.let { Dates.friendly(it.toString()) } ?: "Без даты") },
                        leadingIcon = { Icon(Icons.Default.Event, null) },
                    )
                    if (date != null) TextButton({ date = null }) { Text("Убрать дату") }
                }
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    val today = LocalDate.now()
                    listOf("Сегодня" to today, "Завтра" to today.plusDays(1), "Через неделю" to today.plusDays(7)).forEach { (label, d) ->
                        FilterChip(date == d, { date = d }, { Text(label) })
                    }
                }
                Spacer(Modifier.height(16.dp))
                Text("Приоритет", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(6.dp))
                SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
                    (1..4).forEach { p ->
                        SegmentedButton(priority == p, { priority = p }, SegmentedButtonDefaults.itemShape(p - 1, 4),
                            colors = SegmentedButtonDefaults.colors(activeContainerColor = Ritm.priority(p).copy(alpha = 0.18f))) {
                            Text(Dates.priority(p), maxLines = 1)
                        }
                    }
                }
                Spacer(Modifier.height(16.dp))
                Text("Статус", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(6.dp))
                SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
                    StatusNames.entries.forEachIndexed { i, (key, name) ->
                        SegmentedButton(status == key, { status = key }, SegmentedButtonDefaults.itemShape(i, 3)) { Text(name, maxLines = 1) }
                    }
                }
                Spacer(Modifier.height(16.dp))
                Text("Подзадачи ${if (steps.isEmpty()) "" else "${steps.count { it.done }}/${steps.size}"}",
                    style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
                steps.forEachIndexed { i, s ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        CheckCircle(s.done, MaterialTheme.colorScheme.primary, { steps[i] = s.copy(done = !s.done) }, 22.dp)
                        Text(s.title, Modifier.weight(1f), style = MaterialTheme.typography.bodyLarge)
                        IconButton({ steps.removeAt(i) }) { Icon(Icons.Default.Close, "Удалить подзадачу", tint = MaterialTheme.colorScheme.onSurfaceVariant) }
                    }
                }
                val addStep = {
                    if (newStep.isNotBlank() && steps.size < 50) { steps += Step(newId(), newStep.trim().take(200), false); newStep = "" }
                }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OutlinedTextField(
                        newStep, { newStep = it }, Modifier.weight(1f), placeholder = { Text("Новая подзадача") }, singleLine = true,
                        shape = MaterialTheme.shapes.medium,
                        keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences, imeAction = ImeAction.Done),
                        keyboardActions = KeyboardActions(onDone = { addStep() }),
                    )
                    Spacer(Modifier.width(8.dp))
                    IconButton(addStep) { Icon(Icons.Default.Add, "Добавить подзадачу") }
                }
                Spacer(Modifier.height(40.dp))
            }
        }
    }

    if (picking) DatePick(date, { picking = false }) { date = it }
    newProject?.let { value ->
        AlertDialog(
            onDismissRequest = { newProject = null },
            title = { Text("Новый проект") },
            text = { OutlinedTextField(value, { newProject = it.take(50) }, singleLine = true, label = { Text("Название") }) },
            confirmButton = { TextButton({ if (value.isNotBlank()) project = value.trim(); newProject = null }) { Text("Готово") } },
            dismissButton = { TextButton({ newProject = null }) { Text("Отмена") } },
        )
    }
}
