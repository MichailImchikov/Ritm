package ru.ritm.planner.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.LinkOff
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.PrimaryScrollableTabRow
import androidx.compose.material3.Surface
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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import ru.ritm.planner.data.MeetingSchedule
import ru.ritm.planner.data.Preferences
import ru.ritm.planner.data.ROUTE_DRIVE
import ru.ritm.planner.data.ROUTE_SERVER
import ru.ritm.planner.data.TaskItem
import ru.ritm.planner.data.TeamLink
import ru.ritm.planner.data.TeamNames

private const val ALL = ""
private const val MINE = "\u0000mine"

/** Вкладка «Общие»: список проектов с компьютера, внутри — доска, исполнители и встречи. */
@Composable
fun SharedScreen(
    records: List<TaskItem>,
    prefs: Preferences,
    busy: Boolean,
    selected: String?,
    onSelect: (String?) -> Unit,
    vm: AppViewModel,
    actions: TaskActions,
    onQuickAdd: (project: String, status: String) -> Unit,
) {
    val link = prefs.teams.firstOrNull { it.name == selected }
    if (link != null) ProjectView(link, records, vm, actions, { onSelect(null) }) { onQuickAdd(link.name, it) }
    else ProjectList(records, prefs, busy, vm, onSelect)
}

@Composable
private fun ProjectList(records: List<TaskItem>, prefs: Preferences, busy: Boolean, vm: AppViewModel, onSelect: (String) -> Unit) {
    var server by remember { mutableStateOf(false) }
    var connect by remember { mutableStateOf(false) }
    BigTitleScaffold(
        title = "Общие проекты",
        subtitle = if (prefs.teams.isEmpty()) null else "Подключено: ${prefs.teams.size}",
        actions = {
            if (busy) CircularProgressIndicator(Modifier.size(20.dp).padding(end = 2.dp), strokeWidth = 2.dp)
            else IconButton(vm::syncNow) { Icon(Icons.Default.Sync, "Синхронизировать") }
        },
    ) { padding ->
        LazyColumn(
            Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = padding.calculateTopPadding(), bottom = 32.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            if (prefs.teams.isEmpty()) item {
                EmptyState(
                    "Проектов пока нет",
                    if (prefs.driveConnected) "Они появятся сами, когда Ритм на компьютере обновится и выложит список на Google Диск."
                    else "Войдите в Google в настройках — список придёт с компьютера. Или подключите папку на сервере по адресу.",
                )
            }
            items(prefs.teams, key = { it.name }) { team ->
                val tasks = records.filter { it.isTask && it.category == team.name && !it.completed }
                val mine = tasks.count { TeamNames.same(it.assignee, team.user) }
                Surface(
                    Modifier.fillMaxWidth().clip(MaterialTheme.shapes.large).clickable { onSelect(team.name) },
                    shape = MaterialTheme.shapes.large, color = MaterialTheme.colorScheme.surfaceContainerLow, tonalElevation = 1.dp,
                ) {
                    Column(Modifier.padding(16.dp)) {
                        Text(team.name, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
                        Spacer(Modifier.height(6.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                            RouteChip(team)
                            Chip("Открыто: ${tasks.size}", MaterialTheme.colorScheme.onSurfaceVariant)
                            if (mine > 0) Chip("Мои: $mine", MaterialTheme.colorScheme.primary)
                        }
                        team.status?.let {
                            Spacer(Modifier.height(6.dp))
                            Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            }
            item {
                Spacer(Modifier.height(4.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    OutlinedButton({ server = true }) { Text("Доступ к серверу") }
                    OutlinedButton({ connect = true }) { Text("Подключить папку") }
                }
                Text(
                    "В Wi-Fi рядом с сервером телефон работает с папкой напрямую. Если сервер недоступен, обмен идёт через Google Диск, а на сервер изменения переносит включённый компьютер.",
                    style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 10.dp),
                )
            }
        }
    }
    if (server) ServerDialog(prefs, { server = false }) { login, domain, password -> vm.saveServer(login, domain, password); server = false }
    if (connect) ConnectDialog({ connect = false }) { path, user -> vm.connectTeam(path, user); connect = false }
}

@Composable
private fun RouteChip(team: TeamLink) {
    val colors = MaterialTheme.colorScheme
    when (team.route) {
        ROUTE_SERVER -> Chip("Сервер", Ritm.Success)
        ROUTE_DRIVE -> Chip("Google Диск", colors.primary)
        else -> Chip("Нет связи", if (team.status != null) Ritm.Urgent else colors.onSurfaceVariant)
    }
}

@OptIn(androidx.compose.material3.ExperimentalMaterial3Api::class)
@Composable
private fun ProjectView(
    link: TeamLink,
    records: List<TaskItem>,
    vm: AppViewModel,
    actions: TaskActions,
    onBack: () -> Unit,
    onQuickAdd: (status: String) -> Unit,
) {
    BackHandler(onBack = onBack)
    var person by rememberSaveable(link.name) { mutableStateOf(ALL) }
    var ask by remember { mutableStateOf(false) }
    val members = vm.members(link.name)
    val tasks = records.filter { it.isTask && it.category == link.name }.filter {
        when (person) { ALL -> true; MINE -> TeamNames.same(it.assignee, link.user); else -> TeamNames.same(it.assignee, person) }
    }.sortedWith(compareBy({ it.date == null }, { it.date }, { it.priority }, { it.createdAt }))
    val meetings = records.filter { it.isMeeting && !it.deleted && it.category == link.name }.sortedBy { it.meetingTime }
    val statuses = StatusNames.keys.toList()
    val pager = rememberPagerState { 4 }
    val scope = rememberCoroutineScope()
    BigTitleScaffold(
        title = link.name,
        subtitle = "Вы: ${link.user} · участников: ${members.size}",
        actions = {
            IconButton(onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "К списку проектов") }
            if (link.manual) IconButton({ ask = true }) { Icon(Icons.Default.LinkOff, "Отключить проект") }
        },
        fab = { if (pager.currentPage < 3) AddFab { onQuickAdd(statuses[pager.currentPage]) } },
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(top = padding.calculateTopPadding())) {
            LazyRow(contentPadding = PaddingValues(horizontal = 16.dp, vertical = 6.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                item { FilterChip(person == ALL, { person = ALL }, { Text("Все") }) }
                item { FilterChip(person == MINE, { person = MINE }, { Text("Мои") }) }
                items(members.filter { !TeamNames.same(it, link.user) }) { m -> FilterChip(person == m, { person = m }, { Text(m) }) }
            }
            PrimaryScrollableTabRow(pager.currentPage, containerColor = MaterialTheme.colorScheme.background, edgePadding = 16.dp) {
                (statuses.map { s -> StatusNames[s]!! to tasks.count { it.status == s } } + ("Встречи" to meetings.size)).forEachIndexed { i, (name, count) ->
                    Tab(pager.currentPage == i, { scope.launch { pager.animateScrollToPage(i) } }, text = {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(name, maxLines = 1, softWrap = false)
                            Text("$count", style = MaterialTheme.typography.labelSmall)
                        }
                    })
                }
            }
            HorizontalPager(pager, Modifier.fillMaxSize(), beyondViewportPageCount = 1) { page ->
                LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(16.dp, 12.dp, 16.dp, 96.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    if (page == 3) {
                        if (meetings.isEmpty()) item { EmptyState("Встреч нет", "Встречи создаются на компьютере.") }
                        items(meetings, key = { it.id }) { MeetingRow(it) }
                    } else {
                        val column = tasks.filter { it.status == statuses[page] }
                        if (column.isEmpty()) item { EmptyState("Колонка пуста", "Долгое нажатие на карточке → переместить сюда.") }
                        items(column, key = { it.id }) { TaskCard(it, actions, Modifier.animateItem(), showProject = false) }
                    }
                }
            }
        }
    }
    if (ask) AlertDialog(
        onDismissRequest = { ask = false },
        title = { Text("Отключить проект?") },
        text = { Text("Проект исчезнет с телефона. Задачи на сервере останутся.") },
        confirmButton = { TextButton({ ask = false; vm.disconnectTeam(link.name); onBack() }) { Text("Отключить", color = Ritm.Urgent) } },
        dismissButton = { TextButton({ ask = false }) { Text("Отмена") } },
    )
}

@Composable
private fun MeetingRow(meeting: TaskItem) {
    val upcoming = MeetingSchedule.occursOn(meeting, java.time.LocalDate.now())
    Surface(Modifier.fillMaxWidth(), shape = MaterialTheme.shapes.medium, color = MaterialTheme.colorScheme.surfaceContainerLow, tonalElevation = 1.dp) {
        Column(Modifier.padding(14.dp)) {
            Text(meeting.title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Medium)
            Spacer(Modifier.height(4.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(MeetingSchedule.label(meeting), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                if (upcoming) { Spacer(Modifier.width(8.dp)); Chip("Сегодня", MaterialTheme.colorScheme.primary) }
            }
        }
    }
}

@Composable
private fun ServerDialog(prefs: Preferences, onDismiss: () -> Unit, onSave: (String, String, String?) -> Unit) {
    var login by remember { mutableStateOf(prefs.serverLogin) }
    var domain by remember { mutableStateOf(prefs.serverDomain) }
    var password by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Доступ к серверу") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Те же логин и пароль, что для входа в папку на компьютере. Если папка открыта всем, логин оставьте пустым.", style = MaterialTheme.typography.bodySmall)
                OutlinedTextField(login, { login = it }, singleLine = true, label = { Text("Логин") })
                OutlinedTextField(domain, { domain = it }, singleLine = true, label = { Text("Домен (если есть)") })
                OutlinedTextField(
                    password, { password = it }, singleLine = true, label = { Text("Пароль") },
                    placeholder = { Text("Оставьте пустым, чтобы не менять") },
                    visualTransformation = PasswordVisualTransformation(), keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                )
            }
        },
        confirmButton = { Button({ onSave(login, domain, password.ifEmpty { null }) }) { Text("Сохранить") } },
        dismissButton = { TextButton(onDismiss) { Text("Отмена") } },
    )
}

@Composable
private fun ConnectDialog(onDismiss: () -> Unit, onConnect: (String, String) -> Unit) {
    var path by remember { mutableStateOf("") }
    var user by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Подключить папку") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Папка проекта, уже подключённая на компьютере. Сначала заполните «Доступ к серверу».", style = MaterialTheme.typography.bodySmall)
                OutlinedTextField(path, { path = it }, label = { Text("Адрес") }, placeholder = { Text("\\\\сервер\\папка\\проект") })
                OutlinedTextField(user, { user = it.take(50) }, singleLine = true, label = { Text("Ваше имя в проекте") })
            }
        },
        confirmButton = { Button({ onConnect(path, user) }, enabled = path.isNotBlank() && user.isNotBlank()) { Text("Подключить") } },
        dismissButton = { TextButton(onDismiss) { Text("Отмена") } },
    )
}
