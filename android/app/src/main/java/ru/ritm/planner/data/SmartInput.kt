package ru.ritm.planner.data

import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.format.TextStyle
import java.time.temporal.ChronoUnit
import java.util.Locale

val RU: Locale = Locale.forLanguageTag("ru-RU")

data class SmartTask(val title: String, val date: String? = null, val project: String? = null, val priority: Int = 3) {
    val hasExtras get() = date != null || project != null || priority != 3
}

// Быстрый ввод: «Позвонить маме завтра !1 #Личное» → название, дата, приоритет и проект (как SmartInput.cs).
object SmartInput {
    private val weekdays = listOf(
        listOf("пн", "понедельник"), listOf("вт", "вторник"), listOf("ср", "среда", "среду"),
        listOf("чт", "четверг"), listOf("пт", "пятница", "пятницу"), listOf("сб", "суббота", "субботу"), listOf("вс", "воскресенье"),
    )

    fun parse(text: String, today: LocalDate, projects: List<String>): SmartTask {
        var date: String? = null
        var project: String? = null
        var priority = 3
        val words = mutableListOf<String>()
        for (raw in text.split(' ', '\t').filter { it.isNotEmpty() }) {
            val w = raw.lowercase(RU).trimEnd(',', '.')
            if (w.length == 2 && w[0] == '!' && w[1] in '1'..'4') { priority = w[1] - '0'; continue }
            if (raw.length > 1 && raw[0] == '#' && project == null) {
                val tag = raw.substring(1)
                project = projects.firstOrNull { it.equals(tag, true) || it.replace(' ', '_').equals(tag, true) } ?: tag.replace('_', ' ')
                continue
            }
            if (date == null) {
                val day = day(w, today)
                if (day != null) {
                    date = day.toString()
                    if (words.isNotEmpty() && words.last().lowercase(RU) in setOf("в", "во")) words.removeAt(words.lastIndex)
                    continue
                }
            }
            words += raw
        }
        val title = words.joinToString(" ").trim()
        if (title.isEmpty()) return SmartTask(text.trim())
        if (project != null && (project.length > 50 || project == NO_PROJECT)) project = null
        return SmartTask(title, date, project, priority)
    }

    private fun day(w: String, today: LocalDate): LocalDate? {
        when (w) {
            "сегодня" -> return today
            "завтра" -> return today.plusDays(1)
            "послезавтра" -> return today.plusDays(2)
        }
        val index = weekdays.indexOfFirst { w in it }
        if (index >= 0) {
            val current = today.dayOfWeek.value - 1
            val ahead = (index - current + 7) % 7
            return today.plusDays(if (ahead == 0) 7L else ahead.toLong())
        }
        val parts = w.split('.')
        if (parts.size in 2..3 && parts.all { p -> p.isNotEmpty() && p.all { it.isDigit() } }) {
            val d = parts[0].toInt(); val m = parts[1].toInt()
            if (parts.size == 3) {
                if (parts[2].length != 4) return null
                return runCatching { LocalDate.of(parts[2].toInt(), m, d) }.getOrNull()
            }
            val candidate = runCatching { LocalDate.of(today.year, m, d) }.getOrNull() ?: return null
            return if (candidate < today) candidate.plusYears(1) else candidate
        }
        return null
    }

    fun describe(t: SmartTask, today: LocalDate): String {
        val parts = buildList {
            t.date?.let { add(Dates.friendly(it, today)) }
            if (t.priority != 3) add(Dates.priority(t.priority))
            t.project?.let { add("#$it") }
        }
        return if (parts.isEmpty()) "" else "→ " + parts.joinToString(" · ")
    }
}

object Dates {
    private val dayMonth = DateTimeFormatter.ofPattern("d MMMM", RU)
    private val dayMonthYear = DateTimeFormatter.ofPattern("d MMMM yyyy", RU)

    fun priority(p: Int) = listOf("", "Срочный", "Высокий", "Обычный", "Низкий")[p.coerceIn(0, 4)]

    fun display(date: String, today: LocalDate = LocalDate.now()): String {
        val d = LocalDate.parse(date)
        return d.format(if (d.year == today.year) dayMonth else dayMonthYear)
    }

    fun friendly(date: String?, today: LocalDate = LocalDate.now()): String {
        if (date.isNullOrEmpty()) return "Без даты"
        val d = runCatching { LocalDate.parse(date) }.getOrNull() ?: return date
        val diff = ChronoUnit.DAYS.between(today, d)
        return when {
            diff == 0L -> "Сегодня"
            diff == 1L -> "Завтра"
            diff == -1L -> "Вчера"
            diff in 2..6 -> d.dayOfWeek.getDisplayName(TextStyle.FULL, RU)
            else -> display(date, today)
        }
    }

    fun plural(n: Int, one: String, few: String, many: String): String {
        val m10 = n % 10; val m100 = n % 100
        return when {
            m10 == 1 && m100 != 11 -> one
            m10 in 2..4 && m100 !in 12..14 -> few
            else -> many
        }
    }

    fun greeting(hour: Int) = when {
        hour < 5 -> "Доброй ночи"
        hour < 12 -> "Доброе утро"
        hour < 18 -> "Добрый день"
        hour < 23 -> "Добрый вечер"
        else -> "Доброй ночи"
    }
}
