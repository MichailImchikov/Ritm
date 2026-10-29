package ru.ritm.planner.widget

import android.content.Context
import android.content.Intent
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.glance.ColorFilter
import androidx.glance.GlanceId
import androidx.glance.GlanceModifier
import androidx.glance.GlanceTheme
import androidx.glance.Image
import androidx.glance.ImageProvider
import androidx.glance.action.ActionParameters
import androidx.glance.action.actionParametersOf
import androidx.glance.action.clickable
import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.GlanceAppWidgetReceiver
import androidx.glance.appwidget.action.ActionCallback
import androidx.glance.appwidget.action.actionRunCallback
import androidx.glance.appwidget.action.actionStartActivity
import androidx.glance.appwidget.cornerRadius
import androidx.glance.appwidget.lazy.LazyColumn
import androidx.glance.appwidget.lazy.items
import androidx.glance.appwidget.provideContent
import androidx.glance.appwidget.updateAll
import androidx.glance.background
import androidx.glance.layout.Alignment
import androidx.glance.layout.Box
import androidx.glance.layout.Column
import androidx.glance.layout.Row
import androidx.glance.layout.Spacer
import androidx.glance.layout.fillMaxSize
import androidx.glance.layout.fillMaxWidth
import androidx.glance.layout.height
import androidx.glance.layout.padding
import androidx.glance.layout.size
import androidx.glance.layout.width
import androidx.glance.text.FontWeight
import androidx.glance.text.Text
import androidx.glance.text.TextStyle
import androidx.glance.unit.ColorProvider
import ru.ritm.planner.MainActivity
import ru.ritm.planner.R
import ru.ritm.planner.RitmApp
import ru.ritm.planner.data.Dates
import ru.ritm.planner.data.TaskItem
import java.time.LocalDate

/** Виджет «Мой день»: задачи на сегодня и просроченные; кружок отмечает задачу выполненной. */
class TodayWidget : GlanceAppWidget() {
    override suspend fun provideGlance(context: Context, id: GlanceId) {
        val store = (context.applicationContext as RitmApp).store
        provideContent {
            val records by store.records.collectAsState()
            GlanceTheme { Content(context, records) }
        }
    }

    @Composable
    private fun Content(context: Context, records: List<TaskItem>) {
        val today = LocalDate.now().toString()
        val open = records.filter { !it.deleted && !it.isProject && !it.completed && it.date != null && it.date <= today }
            .sortedWith(compareBy<TaskItem>({ it.date == today }, { it.priority }, { it.createdAt }))
        val doneToday = records.count { !it.deleted && !it.isProject && it.completed && it.date == today }
        Column(
            GlanceModifier.fillMaxSize().cornerRadius(24.dp).background(GlanceTheme.colors.widgetBackground).padding(14.dp),
        ) {
            Row(GlanceModifier.fillMaxWidth().clickable(actionStartActivity(open(context, null))), verticalAlignment = Alignment.CenterVertically) {
                Column(GlanceModifier.defaultWeight()) {
                    Text("Мой день", style = TextStyle(color = GlanceTheme.colors.onSurface, fontSize = 17.sp, fontWeight = FontWeight.Bold))
                    Text(
                        if (open.isEmpty()) "Всё сделано" else "Осталось ${open.size} · готово $doneToday",
                        style = TextStyle(color = GlanceTheme.colors.onSurfaceVariant, fontSize = 12.sp),
                    )
                }
                Box(
                    GlanceModifier.size(40.dp).cornerRadius(20.dp).background(GlanceTheme.colors.primary)
                        .clickable(actionStartActivity(open(context, "quick"))),
                    contentAlignment = Alignment.Center,
                ) {
                    Image(ImageProvider(R.drawable.ic_add), "Новая задача", GlanceModifier.size(22.dp), colorFilter = ColorFilter.tint(GlanceTheme.colors.onPrimary))
                }
            }
            Spacer(GlanceModifier.height(10.dp))
            if (open.isEmpty()) {
                Box(GlanceModifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Text("На сегодня задач нет ✓", style = TextStyle(color = GlanceTheme.colors.onSurfaceVariant, fontSize = 13.sp))
                }
            } else LazyColumn {
                items(open, itemId = { it.id.hashCode().toLong() }) { task ->
                    Row(
                        GlanceModifier.fillMaxWidth().padding(vertical = 5.dp).cornerRadius(14.dp)
                            .background(GlanceTheme.colors.secondaryContainer).padding(horizontal = 10.dp, vertical = 9.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Image(
                            ImageProvider(R.drawable.ic_circle), "Выполнено",
                            GlanceModifier.size(22.dp).clickable(actionRunCallback<CompleteTask>(actionParametersOf(TaskKey to task.id))),
                            colorFilter = ColorFilter.tint(GlanceTheme.colors.primary),
                        )
                        Spacer(GlanceModifier.width(10.dp))
                        Text(
                            task.title, maxLines = 1,
                            modifier = GlanceModifier.defaultWeight().clickable(actionStartActivity(open(context, "task:" + task.id))),
                            style = TextStyle(color = GlanceTheme.colors.onSecondaryContainer, fontSize = 14.sp),
                        )
                        if (task.date != today) Text(
                            Dates.friendly(task.date), maxLines = 1,
                            style = TextStyle(color = ColorProvider(Color(0xFFE5484D)), fontSize = 11.sp),
                        )
                    }
                }
            }
        }
    }

    companion object {
        val TaskKey = ActionParameters.Key<String>("task")
        suspend fun refresh(context: Context) = TodayWidget().updateAll(context)

        fun open(context: Context, action: String?): Intent =
            Intent(context, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP).apply {
                putExtra(MainActivity.EXTRA_TAB, "today")
                if (action != null) putExtra(MainActivity.EXTRA_ACTION, action)
                // Разные data, чтобы PendingIntent'ы разных строк не склеивались.
                data = android.net.Uri.parse("ritm://widget/" + (action ?: "open"))
            }
    }
}

class CompleteTask : ActionCallback {
    override suspend fun onAction(context: Context, glanceId: GlanceId, parameters: ActionParameters) {
        val store = (context.applicationContext as RitmApp).store
        val id = parameters[TodayWidget.TaskKey] ?: return
        val task = store.find(id) ?: return
        runCatching { store.save(task.copy(completed = true, status = "done")) }
    }
}

class TodayWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = TodayWidget()
}
