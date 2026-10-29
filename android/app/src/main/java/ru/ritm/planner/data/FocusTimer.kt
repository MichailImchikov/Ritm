package ru.ritm.planner.data

import java.time.Instant
import kotlin.math.ceil
import kotlin.math.max

/** Таймер фокуса 25/5 — та же модель состояния, что FocusTimer.cs: дедлайн в UTC переживает закрытие приложения. */
object FocusTimer {
    const val FOCUS = 1500
    const val BREAK = 300

    fun duration(s: FocusState) = if (s.mode == "break") BREAK else FOCUS

    fun normalize(s: FocusState): FocusState {
        var st = if (s.mode != "focus" && s.mode != "break") s.copy(mode = "focus") else s
        if (st.remainingSeconds <= 0 || st.remainingSeconds > duration(st)) st = st.copy(remainingSeconds = duration(st), running = false)
        if (st.running && Store.parse(st.deadlineUtc) == null) st = st.copy(running = false)
        return st
    }

    fun remaining(s: FocusState, now: Instant = Instant.now()): Int {
        if (!s.running) return max(0, s.remainingSeconds)
        val due = Store.parse(s.deadlineUtc) ?: return s.remainingSeconds
        return max(0, ceil((due.toEpochMilli() - now.toEpochMilli()) / 1000.0).toInt())
    }

    fun deadline(s: FocusState): Instant? = if (s.running) Store.parse(s.deadlineUtc) else null

    fun start(s: FocusState, now: Instant = Instant.now()): FocusState {
        if (s.running) return s
        val left = if (remaining(s, now) <= 0) duration(s) else s.remainingSeconds
        return s.copy(remainingSeconds = left, running = true, deadlineUtc = now.plusSeconds(left.toLong()).toString())
    }

    fun pause(s: FocusState, now: Instant = Instant.now()) = s.copy(remainingSeconds = remaining(s, now), running = false, deadlineUtc = null)

    fun reset(s: FocusState) = s.copy(running = false, deadlineUtc = null, remainingSeconds = duration(s))

    fun setMode(s: FocusState, mode: String) = reset(s.copy(mode = mode))

    /** Возвращает новое состояние, если отрезок закончился, иначе null. */
    fun tick(s: FocusState, now: Instant = Instant.now()): FocusState? {
        if (!s.running || remaining(s, now) > 0) return null
        val sessions = if (s.mode == "focus") s.sessions + 1 else s.sessions
        return reset(s.copy(mode = if (s.mode == "focus") "break" else "focus", sessions = sessions))
    }
}
