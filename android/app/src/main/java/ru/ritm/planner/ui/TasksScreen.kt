package ru.ritm.planner.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.ViewKanban
import androidx.compose.material.icons.automirrored.filled.ViewList
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.PrimaryTabRow
import androidx.compose.material3.Tab
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import ru.ritm.planner.data.NO_PROJECT
import ru.ritm.planner.data.TaskItem
import java.time.LocalDate

private const val ALL = ""

@OptIn(androidx.compose.material3.ExperimentalMaterial3Api::class)
@Composable
fun TasksScreen(
    records: List<TaskItem>,
    projects: List<String>,
    vm: AppViewModel,
    actions: TaskActions,
    onQuickAdd: (status: String, project: String?) -> Unit,
) {
    var project by rememberSaveable { mutableStateOf(ALL) }
    var query by rememberSaveable { mutableStateOf("") }
    var searching by rememberSaveable { mutableStateOf(false) }
    val context = if (project == ALL) "all" else "project:$project"
    var layout by remember(context) { mutableStateOf(vm.layout(context)) }
    var projectDialog by remember { mutableStateOf<Pair<String?, String>?>(null) } // (старое имя или null для нового, текст)
    var projectMenu by remember { mutableStateOf<String?>(null) }

    val q = query.trim().lowercase()
    val tasks = records.filter { !it.deleted && !it.isProject }
        .filter { project == ALL || it.category == project }
        .filter { q.isEmpty() || (it.title + " " + (it.description ?: "") + " " + it.category).lowercase().contains(q) }
    val pager = rememberPagerState { 3 }
    val scope = rememberCoroutineScope()
    val statuses = StatusNames.keys.toList()

    BigTitleScaffold(
        title = when (project) { ALL -> "Все задачи"; NO_PROJECT -> "Без проекта"; else -> project },
        subtitle = "Всего: ${tasks.size} · Открыто: ${tasks.count { !it.completed }}",
        actions = {
            IconButton({ searching = !searching; if (!searching) query = "" }) { Icon(Icons.Default.Search, "Поиск") }
            if (project != ALL && project != NO_PROJECT) IconButton({ projectMenu = project }) { Icon(Icons.Default.Edit, "Проект") }
            IconButton({
                layout = if (layout == "board") "list" else "board"; vm.setLayout(context, layout)
            }) { Icon(if (layout == "board") Icons.AutoMirrored.Filled.ViewList else Icons.Default.ViewKanban, if (layout == "board") "Список" else "Доска") }
        },
        fab = {
            AddFab {
                val status = if (layout == "board") statuses[pager.currentPage] else "todo"
                onQuickAdd(status, project.takeIf { it != ALL })
            }
        },
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(top = padding.calculateTopPadding())) {
            if (searching) OutlinedTextField(
                query, { query = it }, Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp),
                placeholder = { Text("Поиск задач…") }, singleLine = true, shape = MaterialTheme.shapes.extraLarge,
                leadingIcon = { Icon(Icons.Default.Search, null) },
                trailingIcon = { if (query.isNotEmpty()) IconButton({ query = "" }) { Icon(Icons.Default.Clear, "Очистить") } },
            )
            ProjectChips(projects, project, records, { project = it }, { projectDialog = null to "" })
            if (layout == "board") {
                androidx.compose.material3.PrimaryScrollableTabRow(pager.currentPage, containerColor = MaterialTheme.colorScheme.background, edgePadding = 16.dp) {
                    statuses.forEachIndexed { i, s ->
                        Tab(pager.currentPage == i, { scope.launch { pager.animateScrollToPage(i) } },
                            text = {
                                androidx.compose.foundation.layout.Column(horizontalAlignment = androidx.compose.ui.Alignment.CenterHorizontally) {
                                    Text(StatusNames[s]!!, maxLines = 1, softWrap = false)
                                    Text("${tasks.count { it.status == s }}", style = MaterialTheme.typography.labelSmall)
                                }
                            })
                    }
                }
                HorizontalPager(pager, Modifier.fillMaxSize(), beyondViewportPageCount = 1) { page ->
                    val column = tasks.filter { it.status == statuses[page] }.sortedWith(boardOrder)
                    LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(16.dp, 12.dp, 16.dp, 96.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        if (column.isEmpty()) item {
                            EmptyState(
                                if (page == 2) "Пока ничего не готово" else "Колонка пуста",
                                if (page == 0) "Нажмите «Задача», чтобы добавить." else "Долгое нажатие на карточке → переместить сюда.",
                            )
                        }
                        items(column, key = { it.id }) { TaskCard(it, actions, Modifier.animateItem(), showProject = project == ALL) }
                    }
                }
            } else {
                val groups = groupForList(tasks)
                LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(16.dp, 4.dp, 16.dp, 96.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    if (tasks.isEmpty()) item { EmptyState(if (q.isEmpty()) "Задач нет" else "Ничего не найдено", "Нажмите «Задача», чтобы добавить.") }
                    groups.forEach { (title, list) ->
                        item(key = "h-$title") { SectionTitle(title, list.size, if (title == "Просрочено") Ritm.Urgent else MaterialTheme.colorScheme.onSurfaceVariant) }
                        items(list, key = { it.id }) { TaskCard(it, actions, Modifier.animateItem(), showProject = project == ALL) }
                    }
                }
            }
        }
    }

    projectMenu?.let { name ->
        AlertDialog(
            onDismissRequest = { projectMenu = null },
            title = { Text(name) },
            text = { Text("Задачи удалённого проекта останутся — «Без проекта».") },
            confirmButton = { TextButton({ projectDialog = name to name; projectMenu = null }) { Text("Переименовать") } },
            dismissButton = {
                TextButton({ vm.deleteProject(name); if (project == name) project = ALL; projectMenu = null }) { Text("Удалить", color = Ritm.Urgent) }
            },
        )
    }
    projectDialog?.let { (old, text) ->
        AlertDialog(
            onDismissRequest = { projectDialog = null },
            title = { Text(if (old == null) "Новый проект" else "Переименовать проект") },
            text = { OutlinedTextField(text, { projectDialog = old to it.take(50) }, singleLine = true, label = { Text("Название") }) },
            confirmButton = {
                TextButton({
                    if (old == null) vm.createProject(text) else { vm.renameProject(old, text); if (project == old) project = text.trim() }
                    projectDialog = null
                }) { Text("Готово") }
            },
            dismissButton = { TextButton({ projectDialog = null }) { Text("Отмена") } },
        )
    }
}

private val boardOrder = compareBy<TaskItem>({ it.date == null }, { it.date }, { it.priority }, { it.createdAt })

private fun groupForList(tasks: List<TaskItem>): List<Pair<String, List<TaskItem>>> {
    val today = LocalDate.now()
    fun group(t: TaskItem): String {
        if (t.completed) return "Выполнено"
        val d = t.localDate ?: return "Без даты"
        return when {
            d < today -> "Просрочено"
            d == today -> "Сегодня"
            d == today.plusDays(1) -> "Завтра"
            d <= today.plusDays(7) -> "Ближайшие 7 дней"
            else -> "Позже"
        }
    }
    val order = listOf("Просрочено", "Сегодня", "Завтра", "Ближайшие 7 дней", "Позже", "Без даты", "Выполнено")
    val grouped = tasks.sortedWith(boardOrder).groupBy(::group)
    return order.mapNotNull { k -> grouped[k]?.let { k to it } }
}

@Composable
private fun ProjectChips(
    projects: List<String>,
    selected: String,
    records: List<TaskItem>,
    onSelect: (String) -> Unit,
    onCreate: () -> Unit,
) {
    fun open(name: String) = records.count { !it.deleted && !it.isProject && !it.completed && (name == ALL || it.category == name) }
    LazyRow(contentPadding = PaddingValues(horizontal = 16.dp, vertical = 6.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        item { FilterChip(selected == ALL, { onSelect(ALL) }, { Text("Все · ${open(ALL)}") }) }
        items(projects) { p ->
            FilterChip(selected == p, { onSelect(p) }, { Text("$p · ${open(p)}") }, leadingIcon = { Dot(Ritm.project(p)) })
        }
        item { FilterChip(selected == NO_PROJECT, { onSelect(NO_PROJECT) }, { Text("Без проекта") }) }
        item { FilterChip(false, onCreate, { Text("Проект") }, leadingIcon = { Icon(Icons.Default.Add, null) }) }
    }
}

@Composable
private fun Dot(color: Color) {
    Box(Modifier.size(8.dp).clip(CircleShape).background(color))
}
