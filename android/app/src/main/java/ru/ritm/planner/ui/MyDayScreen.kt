package ru.ritm.planner.ui

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Pause
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.FilledTonalIconButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LargeTopAppBar
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.rememberTopAppBarState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.input.nestedscroll.nestedScroll
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import ru.ritm.planner.data.Dates
import ru.ritm.planner.data.FocusState
import ru.ritm.planner.data.FocusTimer
import ru.ritm.planner.data.RU
import ru.ritm.planner.data.Store
import ru.ritm.planner.data.TaskItem
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter

/** Крупный заголовок в стиле One UI: сворачивается при прокрутке. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BigTitleScaffold(
    title: String,
    subtitle: String?,
    actions: @Composable RowScope.() -> Unit = {},
    fab: @Composable () -> Unit = {},
    content: @Composable (PaddingValues) -> Unit,
) {
    val behavior = TopAppBarDefaults.exitUntilCollapsedScrollBehavior(rememberTopAppBarState())
    Scaffold(
        modifier = Modifier.nestedScroll(behavior.nestedScrollConnection),
        contentWindowInsets = WindowInsets(0, 0, 0, 0),
        topBar = {
            LargeTopAppBar(
                title = {
                    Column {
                        Text(title, maxLines = 1)
                        val expanded = behavior.state.collapsedFraction < 0.5f
                        if (subtitle != null && expanded) Text(subtitle, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                },
                actions = actions,
                scrollBehavior = behavior,
                colors = TopAppBarDefaults.largeTopAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    scrolledContainerColor = MaterialTheme.colorScheme.background,
                ),
            )
        },
        floatingActionButton = fab,
        containerColor = MaterialTheme.colorScheme.background,
        content = content,
    )
}

@Composable
fun AddFab(text: String = "Задача", onClick: () -> Unit) {
    ExtendedFloatingActionButton(text = { Text(text) }, icon = { Icon(Icons.Default.Add, null) }, onClick = onClick,
        containerColor = MaterialTheme.colorScheme.primary, contentColor = MaterialTheme.colorScheme.onPrimary)
}

@Composable
fun MyDayScreen(
    records: List<TaskItem>,
    focus: FocusState,
    focusTaskId: String?,
    vm: AppViewModel,
    actions: TaskActions,
    onQuickAdd: () -> Unit,
) {
    val today = LocalDate.now()
    val todayKey = today.toString()
    val tasks = records.filter { !it.deleted && !it.isProject }
    val overdue = tasks.filter { !it.completed && it.date != null && it.date < todayKey }.sortedWith(compareBy({ it.date }, { it.priority }))
    val dueToday = tasks.filter { !it.completed && it.date == todayKey }.sortedWith(compareBy({ it.status != "doing" }, { it.priority }, { it.createdAt }))
    val doneToday = tasks.filter { it.completed && (it.date == todayKey || completedOn(it) == today) }
    val hour = LocalTime.now().hour
    BigTitleScaffold(
        title = Dates.greeting(hour),
        subtitle = today.format(DateTimeFormatter.ofPattern("EEEE, d MMMM", RU)).replaceFirstChar { it.uppercase(RU) },
        fab = { AddFab(onClick = onQuickAdd) },
    ) { padding ->
        val colors = MaterialTheme.colorScheme
        LazyColumn(
            Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = padding.calculateTopPadding(), bottom = 96.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            item { DayProgress(doneToday.size, dueToday.size + overdue.size + doneToday.size, tasks) }
            item { FocusCard(focus, focusTaskId?.let { id -> tasks.firstOrNull { it.id == id } }, vm) }
            section("Просрочено", overdue, Ritm.Urgent, actions)
            section("Сегодня", dueToday, colors.primary, actions)
            if (overdue.isEmpty() && dueToday.isEmpty()) item {
                EmptyState("На сегодня всё", if (doneToday.isEmpty()) "Нажмите «Задача» внизу." else "Отличная работа! Можно отдохнуть или взять задачу на завтра.")
            }
            if (doneToday.isNotEmpty()) section("Выполнено", doneToday, colors.onSurfaceVariant, actions)
        }
    }
}

private fun LazyListScope.section(title: String, list: List<TaskItem>, color: androidx.compose.ui.graphics.Color, actions: TaskActions) {
    if (list.isEmpty()) return
    item(key = "h-$title") { SectionTitle(title, list.size, color) }
    items(list, key = { "$title-${it.id}" }) { TaskCard(it, actions, Modifier.animateItem()) }
}

fun completedOn(t: TaskItem): LocalDate? =
    if (!t.completed) null else Store.parse(t.updatedUtc)?.atZone(ZoneId.systemDefault())?.toLocalDate()

@Composable
private fun DayProgress(done: Int, total: Int, tasks: List<TaskItem>) {
    val colors = MaterialTheme.colorScheme
    val today = LocalDate.now()
    val week = (6 downTo 0).map { today.minusDays(it.toLong()) }
    val counts = week.map { d -> tasks.count { completedOn(it) == d } }
    var streak = 0
    var day = if (counts.last() > 0) today else today.minusDays(1)
    val byDay = tasks.mapNotNull { completedOn(it) }.toSet()
    while (day in byDay) { streak++; day = day.minusDays(1) }
    Surface(shape = MaterialTheme.shapes.large, color = colors.surfaceContainerLow, tonalElevation = 1.dp) {
        Row(Modifier.fillMaxWidth().padding(18.dp), verticalAlignment = Alignment.CenterVertically) {
            val progress = if (total == 0) 0f else done / total.toFloat()
            ProgressRing(progress, 84.dp, 9.dp, colors.primary, colors.primaryContainer) {
                Text("${(progress * 100).toInt()}%", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.width(18.dp))
            Column(Modifier.weight(1f)) {
                Text(if (total == 0) "План пуст" else "Готово $done из $total", style = MaterialTheme.typography.titleMedium)
                Text(
                    if (streak > 0) "Серия $streak ${Dates.plural(streak, "день", "дня", "дней")}" else "Выполните задачу, чтобы начать серию",
                    style = MaterialTheme.typography.bodySmall, color = colors.onSurfaceVariant,
                )
                Spacer(Modifier.height(10.dp))
                val max = (counts.maxOrNull() ?: 0).coerceAtLeast(1)
                Row(Modifier.height(44.dp), horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.Bottom) {
                    week.forEachIndexed { i, d ->
                        Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.weight(1f).fillMaxHeight(), verticalArrangement = Arrangement.Bottom) {
                            val h by animateFloatAsState(counts[i] / max.toFloat(), tween(if (LocalReduceMotion.current) 0 else 700), label = "bar")
                            Box(Modifier.fillMaxWidth().height((4 + 26 * h).dp).clip(RoundedCornerShape(4.dp))
                                .background(if (d == today) colors.primary else colors.primary.copy(alpha = 0.35f)))
                            Text(d.dayOfWeek.getDisplayName(java.time.format.TextStyle.SHORT, RU), fontSize = 10.sp, color = colors.onSurfaceVariant)
                        }
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FocusCard(state: FocusState, task: TaskItem?, vm: AppViewModel) {
    val colors = MaterialTheme.colorScheme
    var now by remember { mutableStateOf(Instant.now()) }
    LaunchedEffect(state.running, state.deadlineUtc) {
        while (state.running) {
            now = Instant.now()
            if (FocusTimer.remaining(state, now) <= 0) { vm.focusTick(); break }
            delay(250)
        }
    }
    val left = FocusTimer.remaining(state, now)
    val total = FocusTimer.duration(state)
    val tint = if (state.mode == "break") Ritm.Success else colors.primary
    Surface(shape = MaterialTheme.shapes.large, color = colors.surfaceContainerLow, tonalElevation = 1.dp) {
        Column(Modifier.fillMaxWidth().padding(18.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Фокус", style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
                Text("Сессий: ${state.sessions}", style = MaterialTheme.typography.labelMedium, color = colors.onSurfaceVariant)
            }
            Spacer(Modifier.height(12.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                ProgressRing(1f - left / total.toFloat(), 120.dp, 10.dp, tint, tint.copy(alpha = 0.15f)) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text("%02d:%02d".format(left / 60, left % 60), fontSize = 28.sp, fontWeight = FontWeight.Bold)
                        Text(if (state.mode == "break") "перерыв" else "работа", style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
                    }
                }
                Spacer(Modifier.width(18.dp))
                Column(Modifier.weight(1f)) {
                    SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
                        SegmentedButton(state.mode == "focus", { vm.focusMode("focus") }, SegmentedButtonDefaults.itemShape(0, 2)) { Text("25 мин") }
                        SegmentedButton(state.mode == "break", { vm.focusMode("break") }, SegmentedButtonDefaults.itemShape(1, 2)) { Text("5 мин") }
                    }
                    Spacer(Modifier.height(12.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        FilledIconButton({ if (state.running) vm.focusPause() else vm.focusStart() }, Modifier.width(64.dp).height(48.dp)) {
                            Icon(if (state.running) Icons.Default.Pause else Icons.Default.PlayArrow, if (state.running) "Пауза" else "Старт")
                        }
                        FilledTonalIconButton({ vm.focusReset() }, Modifier.height(48.dp)) { Icon(Icons.Default.Refresh, "Сбросить") }
                    }
                }
            }
            if (task != null) {
                Spacer(Modifier.height(10.dp))
                Row(Modifier.fillMaxWidth().clip(MaterialTheme.shapes.small).background(colors.primaryContainer).padding(start = 12.dp),
                    verticalAlignment = Alignment.CenterVertically) {
                    Text(task.title, Modifier.weight(1f), maxLines = 1, style = MaterialTheme.typography.bodyMedium, color = colors.onPrimaryContainer)
                    IconButton({ vm.focusTask(null) }) { Icon(Icons.Default.Close, "Отвязать задачу") }
                }
            } else {
                Spacer(Modifier.height(6.dp))
                Text("Долгое нажатие на задаче → «Фокус на задаче»", style = MaterialTheme.typography.bodySmall, color = colors.onSurfaceVariant)
            }
        }
    }
}
