package ru.ritm.planner.ui

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import ru.ritm.planner.data.Dates
import ru.ritm.planner.data.NO_PROJECT
import ru.ritm.planner.data.SmartInput
import ru.ritm.planner.data.TaskItem
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneOffset

val StatusNames = linkedMapOf("todo" to "К выполнению", "doing" to "В работе", "done" to "Готово")

@Composable
fun CheckCircle(checked: Boolean, color: Color, onClick: () -> Unit, size: Dp = 26.dp) {
    val haptic = LocalHapticFeedback.current
    val still = LocalReduceMotion.current
    val fill by animateColorAsState(if (checked) color else Color.Transparent, if (still) tween(0) else tween(220), label = "check")
    val scale by animateFloatAsState(if (checked) 1f else 0.94f, if (still) tween(0) else spring(dampingRatio = Spring.DampingRatioMediumBouncy), label = "pop")
    Box(
        Modifier.size(size + 14.dp).clip(CircleShape).clickable {
            haptic.performHapticFeedback(HapticFeedbackType.LongPress); onClick()
        },
        contentAlignment = Alignment.Center,
    ) {
        Box(
            Modifier.size(size).scale(scale).clip(CircleShape).background(fill).border(2.dp, color, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            if (checked) Icon(Icons.Default.Check, "Выполнено", tint = Color.White, modifier = Modifier.size(size * 0.62f))
        }
    }
}

@Composable
fun Chip(text: String, color: Color, background: Color = color.copy(alpha = 0.13f)) {
    Text(
        text, color = color, fontSize = 12.sp, fontWeight = FontWeight.Medium, maxLines = 1,
        modifier = Modifier.clip(RoundedCornerShape(8.dp)).background(background).padding(horizontal = 8.dp, vertical = 3.dp),
    )
}

/** Карточка задачи: тап — открыть, долгий тап или ⋯ — меню действий, кружок — выполнено. */
@OptIn(ExperimentalFoundationApi::class)
@Composable
fun TaskCard(
    task: TaskItem,
    actions: TaskActions,
    modifier: Modifier = Modifier,
    showProject: Boolean = true,
) {
    var menu by remember { mutableStateOf(false) }
    val haptic = LocalHapticFeedback.current
    val today = LocalDate.now()
    val colors = MaterialTheme.colorScheme
    Surface(
        modifier = modifier.fillMaxWidth().clip(MaterialTheme.shapes.medium).combinedClickable(
            onClick = { actions.open(task) },
            onLongClick = { haptic.performHapticFeedback(HapticFeedbackType.LongPress); menu = true },
        ),
        shape = MaterialTheme.shapes.medium,
        color = colors.surfaceContainerLow,
        tonalElevation = 1.dp,
        shadowElevation = 1.dp,
    ) {
        Row(Modifier.padding(start = 4.dp, end = 2.dp, top = 6.dp, bottom = 8.dp), verticalAlignment = Alignment.Top) {
            CheckCircle(task.completed, Ritm.priority(task.priority), { actions.toggle(task) })
            Column(Modifier.weight(1f).padding(top = 9.dp)) {
                if (showProject && task.category != NO_PROJECT) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(Modifier.size(7.dp).clip(CircleShape).background(Ritm.project(task.category)))
                        Spacer(Modifier.width(6.dp))
                        Text(task.category, style = MaterialTheme.typography.labelMedium, color = colors.onSurfaceVariant, maxLines = 1)
                    }
                    Spacer(Modifier.height(3.dp))
                }
                Text(
                    task.title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Medium,
                    color = if (task.completed) colors.onSurfaceVariant else colors.onSurface,
                    textDecoration = if (task.completed) TextDecoration.LineThrough else null,
                    maxLines = 3, overflow = TextOverflow.Ellipsis,
                )
                if (!task.description.isNullOrBlank()) {
                    Text(task.description, style = MaterialTheme.typography.bodySmall, color = colors.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis)
                }
                val steps = task.steps
                if (task.date != null || task.priority <= 2 || steps.isNotEmpty() || task.status == "doing") {
                    Spacer(Modifier.height(6.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                        task.localDate?.let { d ->
                            val overdue = !task.completed && d < today
                            val color = when { overdue -> Ritm.Urgent; d == today -> colors.primary; else -> colors.onSurfaceVariant }
                            Chip(Dates.friendly(task.date, today), color)
                        }
                        if (task.priority <= 2) Chip(Dates.priority(task.priority), Ritm.priority(task.priority))
                        if (task.status == "doing" && !task.completed) Chip("В работе", colors.primary)
                        if (steps.isNotEmpty()) {
                            val done = steps.count { it.done }
                            Chip("$done/${steps.size}", colors.onSurfaceVariant)
                            Box(Modifier.width(36.dp).height(4.dp).clip(CircleShape).background(colors.outlineVariant)) {
                                Box(Modifier.fillMaxWidth(done / steps.size.toFloat()).height(4.dp).background(Ritm.Success))
                            }
                        }
                    }
                }
            }
            Box {
                IconButton(onClick = { menu = true }) { Icon(Icons.Default.MoreVert, "Действия", tint = colors.onSurfaceVariant) }
                TaskMenu(task, menu, { menu = false }, actions)
            }
        }
    }
}

class TaskActions(
    val open: (TaskItem) -> Unit,
    val toggle: (TaskItem) -> Unit,
    val status: (TaskItem, String) -> Unit,
    val date: (TaskItem, LocalDate?) -> Unit,
    val pickDate: (TaskItem) -> Unit,
    val duplicate: (TaskItem) -> Unit,
    val delete: (TaskItem) -> Unit,
    val focus: (TaskItem) -> Unit,
)

@Composable
fun TaskMenu(task: TaskItem, expanded: Boolean, dismiss: () -> Unit, actions: TaskActions) {
    val today = LocalDate.now()
    DropdownMenu(expanded, dismiss, shape = MaterialTheme.shapes.medium) {
        StatusNames.forEach { (key, name) ->
            if (task.status != key) DropdownMenuItem({ Text("→ $name") }, { dismiss(); actions.status(task, key) })
        }
        HorizontalDivider()
        DropdownMenuItem({ Text("На сегодня") }, { dismiss(); actions.date(task, today) })
        DropdownMenuItem({ Text("На завтра") }, { dismiss(); actions.date(task, today.plusDays(1)) })
        DropdownMenuItem({ Text("Через неделю") }, { dismiss(); actions.date(task, today.plusDays(7)) })
        DropdownMenuItem({ Text("Выбрать дату…") }, { dismiss(); actions.pickDate(task) })
        if (task.date != null) DropdownMenuItem({ Text("Убрать дату") }, { dismiss(); actions.date(task, null) })
        HorizontalDivider()
        if (!task.completed) DropdownMenuItem({ Text("Фокус на задаче") }, { dismiss(); actions.focus(task) })
        DropdownMenuItem({ Text("Дублировать") }, { dismiss(); actions.duplicate(task) })
        DropdownMenuItem({ Text("Удалить", color = Ritm.Urgent) }, { dismiss(); actions.delete(task) })
    }
}

@Composable
fun SectionTitle(text: String, count: Int? = null, color: Color = MaterialTheme.colorScheme.onSurfaceVariant) {
    Row(Modifier.padding(start = 6.dp, top = 18.dp, bottom = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(text, style = MaterialTheme.typography.titleSmall, color = color, fontWeight = FontWeight.SemiBold)
        if (count != null) {
            Spacer(Modifier.width(8.dp))
            Text("$count", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
fun EmptyState(title: String, hint: String, modifier: Modifier = Modifier) {
    Column(modifier.fillMaxWidth().padding(vertical = 40.dp, horizontal = 24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        Text(title, style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onSurface)
        Spacer(Modifier.height(6.dp))
        Text(hint, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun ProgressRing(progress: Float, size: Dp, stroke: Dp, color: Color, track: Color, content: @Composable () -> Unit = {}) {
    val still = LocalReduceMotion.current
    val animated by animateFloatAsState(progress.coerceIn(0f, 1f), if (still) tween(0) else tween(600), label = "ring")
    Box(Modifier.size(size), contentAlignment = Alignment.Center) {
        Canvas(Modifier.size(size)) {
            val w = stroke.toPx()
            val inset = w / 2
            val arc = androidx.compose.ui.geometry.Size(this.size.width - w, this.size.height - w)
            drawArc(track, 0f, 360f, false, Offset(inset, inset), arc, style = Stroke(w))
            drawArc(color, -90f, 360f * animated, false, Offset(inset, inset), arc, style = Stroke(w, cap = StrokeCap.Round))
        }
        content()
    }
}

/** Нижний лист быстрого ввода с подсказкой распознанного: «Позвонить маме завтра !1 #Личное». */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun QuickAddSheet(title: String, projects: List<String>, onDismiss: () -> Unit, onAdd: (String) -> Boolean) {
    val state = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    var text by rememberSaveable { mutableStateOf("") }
    val focus = remember { FocusRequester() }
    val today = LocalDate.now()
    val parsed = remember(text) { SmartInput.parse(text, today, projects) }
    ModalBottomSheet(onDismissRequest = onDismiss, sheetState = state) {
        Column(Modifier.padding(horizontal = 20.dp).padding(bottom = 20.dp).imePadding()) {
            Text(title, style = MaterialTheme.typography.titleLarge)
            Spacer(Modifier.height(12.dp))
            val submit = { if (onAdd(text)) text = "" }
            Row(verticalAlignment = Alignment.CenterVertically) {
                OutlinedTextField(
                    text, { text = it }, Modifier.weight(1f).focusRequester(focus),
                    placeholder = { Text("Позвонить маме завтра !1 #Личное") },
                    singleLine = true,
                    shape = MaterialTheme.shapes.medium,
                    keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.Sentences, imeAction = ImeAction.Done),
                    keyboardActions = KeyboardActions(onDone = { submit() }),
                )
                Spacer(Modifier.width(10.dp))
                FilledIconButton(onClick = submit, enabled = text.isNotBlank(), modifier = Modifier.size(52.dp)) {
                    Icon(Icons.AutoMirrored.Filled.Send, "Добавить")
                }
            }
            val hint = if (parsed.hasExtras) SmartInput.describe(parsed, today) else "Дата: сегодня, завтра, пт, 15.11 · приоритет !1…!4 · проект #название"
            Text(hint, style = MaterialTheme.typography.bodySmall, color = if (parsed.hasExtras) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp, start = 4.dp))
        }
    }
    LaunchedEffect(Unit) { focus.requestFocus() }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DatePick(initial: LocalDate?, onDismiss: () -> Unit, onPick: (LocalDate?) -> Unit) {
    val state = rememberDatePickerState(
        initialSelectedDateMillis = (initial ?: LocalDate.now()).atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli(),
    )
    DatePickerDialog(
        onDismissRequest = onDismiss,
        confirmButton = {
            TextButton({
                onPick(state.selectedDateMillis?.let { Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate() }); onDismiss()
            }) { Text("Готово") }
        },
        dismissButton = { TextButton(onDismiss) { Text("Отмена") } },
    ) { DatePicker(state) }
}
