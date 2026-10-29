package ru.ritm.planner.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Палитры десктопного Ритма (Theme.cs): акцент, мягкий акцент, фон, карточка, панель, линия, второстепенный текст. */
class Palette(val id: String, val name: String, val light: List<Long>, val dark: List<Long>)

val Palettes = listOf(
    Palette("violet", "Ирис", listOf(0xFF6554D9, 0xFFE4DFFC, 0xFFF4F6FB, 0xFFFFFFFF, 0xFFECEFF7, 0xFFDEE2ED, 0xFF657087), listOf(0xFF6E5ACF, 0xFF292541, 0xFF13151D, 0xFF1D202B, 0xFF0E1017, 0xFF303442, 0xFF9CA3B7)),
    Palette("ocean", "Океан", listOf(0xFF2768C8, 0xFFDFEBFE, 0xFFF3F7FC, 0xFFFFFFFF, 0xFFEAF1FA, 0xFFDDE4EF, 0xFF5F6F89), listOf(0xFF2D6ABB, 0xFF1D2F49, 0xFF111821, 0xFF1B2633, 0xFF0C121B, 0xFF2E3C4D, 0xFFA3AFC1)),
    Palette("sage", "Шалфей", listOf(0xFF197961, 0xFFDDF1E9, 0xFFF3F8F5, 0xFFFFFFFF, 0xFFE7F1EC, 0xFFD8E6DE, 0xFF60776A), listOf(0xFF227C65, 0xFF1D352F, 0xFF111B18, 0xFF1B2C25, 0xFF0C1511, 0xFF2C4237, 0xFFA0B6AA)),
    Palette("amber", "Янтарь", listOf(0xFF965F17, 0xFFF9EACD, 0xFFFBF8F1, 0xFFFFFFFF, 0xFFF4EEDD, 0xFFE8DFCA, 0xFF7D6F58), listOf(0xFF96631D, 0xFF362D21, 0xFF1B1712, 0xFF2A241C, 0xFF14110D, 0xFF44392B, 0xFFB9AB95)),
    Palette("rose", "Пудра", listOf(0xFFAE426D, 0xFFF9DFE9, 0xFFFCF5F8, 0xFFFFFFFF, 0xFFF7E9F0, 0xFFEADAE3, 0xFF866579), listOf(0xFFB04572, 0xFF3C2534, 0xFF1D1419, 0xFF2E1F28, 0xFF170E13, 0xFF48313E, 0xFFC2A5B5)),
)

object Ritm {
    val Urgent = Color(0xFFE5484D)
    val High = Color(0xFFE8A33D)
    val Normal = Color(0xFF5978DB)
    val Low = Color(0xFF8B93A7)
    val Success = Color(0xFF2E9E6A)
    fun priority(p: Int) = when (p) { 1 -> Urgent; 2 -> High; 3 -> Normal; else -> Low }
    fun project(name: String): Color {
        var hash = 0
        for (c in name) hash = (hash * 31 + c.code) and 0x7fffffff
        return listOf(Color(0xFF6683A4), Color(0xFFBD8B60), Color(0xFF729781), Color(0xFF9B80AD))[hash % 4]
    }
}

val LocalReduceMotion = compositionLocalOf { false }

private fun scheme(p: Palette, dark: Boolean): ColorScheme {
    val c = (if (dark) p.dark else p.light).map { Color(it) }
    val ink = if (dark) Color(0xFFEEF0F7) else Color(0xFF1F2433)
    return if (dark) darkColorScheme(
        primary = c[0], onPrimary = Color.White, primaryContainer = c[1], onPrimaryContainer = ink,
        secondaryContainer = c[1], onSecondaryContainer = ink,
        background = c[2], onBackground = ink, surface = c[2], onSurface = ink,
        surfaceContainerLowest = c[4], surfaceContainerLow = c[3], surfaceContainer = c[3], surfaceContainerHigh = c[3], surfaceContainerHighest = c[5],
        surfaceVariant = c[4], onSurfaceVariant = c[6], outline = c[6], outlineVariant = c[5], error = Ritm.Urgent,
    ) else lightColorScheme(
        primary = c[0], onPrimary = Color.White, primaryContainer = c[1], onPrimaryContainer = ink,
        secondaryContainer = c[1], onSecondaryContainer = ink,
        background = c[2], onBackground = ink, surface = c[2], onSurface = ink,
        surfaceContainerLowest = c[3], surfaceContainerLow = c[3], surfaceContainer = c[3], surfaceContainerHigh = c[4], surfaceContainerHighest = c[5],
        surfaceVariant = c[4], onSurfaceVariant = c[6], outline = c[6], outlineVariant = c[5], error = Ritm.Urgent,
    )
}

// Крупные округлые формы и заголовки в духе One UI.
private val shapes = Shapes(
    extraSmall = RoundedCornerShape(8.dp), small = RoundedCornerShape(12.dp), medium = RoundedCornerShape(18.dp),
    large = RoundedCornerShape(24.dp), extraLarge = RoundedCornerShape(28.dp),
)

private val typography = Typography().let { t ->
    t.copy(
        displaySmall = t.displaySmall.copy(fontWeight = FontWeight.Bold, fontSize = 34.sp, letterSpacing = (-0.5).sp),
        headlineMedium = t.headlineMedium.copy(fontWeight = FontWeight.Bold),
        titleLarge = t.titleLarge.copy(fontWeight = FontWeight.SemiBold),
        titleMedium = t.titleMedium.copy(fontWeight = FontWeight.SemiBold),
        labelLarge = t.labelLarge.copy(fontWeight = FontWeight.SemiBold),
        bodyLarge = TextStyle(fontSize = 16.sp, lineHeight = 22.sp),
    )
}

@Composable
fun RitmTheme(theme: String, palette: String, reduceMotion: Boolean, content: @Composable () -> Unit) {
    val dark = when (theme) { "dark" -> true; "light" -> false; else -> isSystemInDarkTheme() }
    val p = Palettes.firstOrNull { it.id == palette } ?: Palettes[0]
    CompositionLocalProvider(LocalReduceMotion provides reduceMotion) {
        MaterialTheme(colorScheme = scheme(p, dark), shapes = shapes, typography = typography, content = content)
    }
}

fun isDark(theme: String, system: Boolean) = when (theme) { "dark" -> true; "light" -> false; else -> system }
