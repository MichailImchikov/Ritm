package ru.ritm.planner.ui

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectHorizontalDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Today
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import ru.ritm.planner.data.Dates
import ru.ritm.planner.data.RU
import ru.ritm.planner.data.TaskItem
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.YearMonth
import java.time.format.DateTimeFormatter
import java.time.format.TextStyle
import java.time.temporal.TemporalAdjusters

@Composable
fun CalendarScreen(records: List<TaskItem>, actions: TaskActions, onQuickAdd: (LocalDate) -> Unit) {
    val today = LocalDate.now()
    var selectedKey by rememberSaveable { mutableStateOf(today.toString()) }
    var week by rememberSaveable { mutableStateOf(false) }
    val selected = LocalDate.parse(selectedKey)
    val tasks = records.filter { !it.deleted && !it.isProject && it.date != null }
    val byDay = tasks.groupBy { it.date!! }
    val month = YearMonth.from(selected)
    val title = if (week) {
        val start = selected.with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY))
        "${start.dayOfMonth}–${Dates.display(start.plusDays(6).toString(), today)}"
    } else month.atDay(1).format(DateTimeFormatter.ofPattern("LLLL yyyy", RU)).replaceFirstChar { it.uppercase(RU) }

    fun shift(direction: Long) {
        selectedKey = (if (week) selected.plusWeeks(direction) else selected.plusMonths(direction).withDayOfMonth(1)).toString()
    }

    BigTitleScaffold(
        title = "Календарь",
        subtitle = null,
        actions = { IconButton({ selectedKey = today.toString() }) { Icon(Icons.Default.Today, "Сегодня") } },
        fab = { AddFab { onQuickAdd(selected) } },
    ) { padding ->
        LazyColumn(
            Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 12.dp, end = 12.dp, top = padding.calculateTopPadding(), bottom = 96.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            item {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    IconButton({ shift(-1) }) { Icon(Icons.AutoMirrored.Filled.KeyboardArrowLeft, "Назад") }
                    Text(title, Modifier.weight(1f), style = MaterialTheme.typography.titleMedium, textAlign = TextAlign.Center)
                    IconButton({ shift(1) }) { Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, "Вперёд") }
                }
                SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth().padding(horizontal = 4.dp)) {
                    SegmentedButton(!week, { week = false }, SegmentedButtonDefaults.itemShape(0, 2)) { Text("Месяц") }
                    SegmentedButton(week, { week = true }, SegmentedButtonDefaults.itemShape(1, 2)) { Text("Неделя") }
                }
                Spacer(Modifier.height(8.dp))
            }
            item {
                Surface(shape = MaterialTheme.shapes.large, color = MaterialTheme.colorScheme.surfaceContainerLow, tonalElevation = 1.dp) {
                    val still = LocalReduceMotion.current
                    AnimatedContent(
                        targetState = if (week) selected.with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY)) else month.atDay(1),
                        transitionSpec = {
                            val forward = targetState > initialState
                            val d = if (still) 0 else 260
                            (slideInHorizontally(tween(d)) { if (forward) it / 3 else -it / 3 } + fadeIn(tween(d))) togetherWith
                                (slideOutHorizontally(tween(d)) { if (forward) -it / 3 else it / 3 } + fadeOut(tween(d)))
                        },
                        label = "calendar",
                        modifier = Modifier.pointerInput(week, selectedKey) {
                            var drag = 0f
                            detectHorizontalDragGestures(
                                onDragStart = { drag = 0f },
                                onDragEnd = { if (drag > 120) shift(-1) else if (drag < -120) shift(1) },
                            ) { _, amount -> drag += amount }
                        },
                    ) { start ->
                        Column(Modifier.padding(8.dp)) {
                            Row {
                                DayOfWeek.entries.forEach { d ->
                                    Text(d.getDisplayName(TextStyle.SHORT, RU), Modifier.weight(1f), textAlign = TextAlign.Center,
                                        style = MaterialTheme.typography.labelMedium,
                                        color = if (d.value >= 6) Ritm.Urgent.copy(alpha = 0.8f) else MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                            }
                            val first = start.with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY))
                            val rows = if (week) 1 else {
                                val last = YearMonth.from(start).atEndOfMonth()
                                ((java.time.temporal.ChronoUnit.DAYS.between(first, last) / 7) + 1).toInt()
                            }
                            repeat(rows) { r ->
                                Row {
                                    repeat(7) { c ->
                                        val day = first.plusDays((r * 7 + c).toLong())
                                        DayCell(day, day == selected, day == today, week || day.month == start.month,
                                            byDay[day.toString()].orEmpty(), Modifier.weight(1f)) { selectedKey = day.toString() }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            val list = byDay[selectedKey].orEmpty().sortedWith(compareBy({ it.completed }, { it.priority }))
            item(key = "day-title") {
                SectionTitle(Dates.friendly(selectedKey, today).replaceFirstChar { it.uppercase(RU) } +
                    (if (Dates.friendly(selectedKey, today) != Dates.display(selectedKey, today)) " · " + Dates.display(selectedKey, today) else ""),
                    list.size)
            }
            if (list.isEmpty()) item { EmptyState("Свободный день", "Нажмите «Задача», чтобы запланировать дело на эту дату.") }
            items(list, key = { it.id }) { TaskCard(it, actions, Modifier.animateItem()) }
        }
    }
}

@Composable
private fun DayCell(day: LocalDate, selected: Boolean, today: Boolean, inMonth: Boolean, tasks: List<TaskItem>, modifier: Modifier, onClick: () -> Unit) {
    val colors = MaterialTheme.colorScheme
    val open = tasks.count { !it.completed }
    Column(
        modifier.aspectRatio(0.82f).padding(2.dp).clip(MaterialTheme.shapes.small)
            .background(if (selected) colors.primaryContainer else androidx.compose.ui.graphics.Color.Transparent)
            .clickable(onClick = onClick),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Box(
            Modifier.size(32.dp).clip(CircleShape).background(if (today) colors.primary else androidx.compose.ui.graphics.Color.Transparent),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                "${day.dayOfMonth}", fontSize = 15.sp,
                fontWeight = if (today || selected) FontWeight.Bold else FontWeight.Normal,
                color = when { today -> colors.onPrimary; !inMonth -> colors.onSurfaceVariant.copy(alpha = 0.45f); else -> colors.onSurface },
            )
        }
        Row(Modifier.height(8.dp), horizontalArrangement = Arrangement.spacedBy(2.dp), verticalAlignment = Alignment.CenterVertically) {
            tasks.sortedBy { it.priority }.take(3).forEach { t ->
                Box(Modifier.size(5.dp).clip(CircleShape).background(if (t.completed) colors.outline.copy(alpha = 0.5f) else Ritm.priority(t.priority)))
            }
            if (open > 3) Text("+", fontSize = 8.sp, color = colors.onSurfaceVariant)
        }
    }
}
