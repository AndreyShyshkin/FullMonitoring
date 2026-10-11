using System;
using Avalonia.Media;

namespace FullMonitoring.Models;

/// <summary>
/// Фіксована колірна палітра, правила визначення системних станів (нормальний / попередження / критичний)
/// та утиліти перевірки контрастності відповідно до стандартів доступності WCAG 2.1 (Issue #3, Issue #11).
/// </summary>
public static class SystemStatePalette
{
    /// <summary>
    /// Стандартний текст візуальної підказки для метрик, доступ до яких обмежено правами ОС (Issue #11).
    /// </summary>
    public const string OsSecurityPolicyRestrictedText = "Заблоковано політикою безпеки ОС";

    /// <summary>
    /// Мінімальний коефіцієнт контрастності для звичайного тексту згідно з WCAG 2.1 AA (1.4.3).
    /// </summary>
    public const double MinimumWcagAaTextContrastRatio = 4.5;

    /// <summary>
    /// Мінімальний коефіцієнт контрастності для великого тексту, графіків та UI-компонентів згідно з WCAG 2.1 AA (1.4.11).
    /// </summary>
    public const double MinimumWcagAaGraphicalContrastRatio = 3.0;

    /// <summary>
    /// Основний колір для нормального стану системи (смарагдово-зелений).
    /// </summary>
    public const string NormalHex = "#10B981";

    /// <summary>
    /// Фоновий напівпрозорий колір для нормального стану системи.
    /// </summary>
    public const string NormalBackgroundHex = "#1A10B981";

    /// <summary>
    /// Основний колір для стану попередження (бурштиново-жовтий).
    /// </summary>
    public const string WarningHex = "#F59E0B";

    /// <summary>
    /// Фоновий напівпрозорий колір для стану попередження.
    /// </summary>
    public const string WarningBackgroundHex = "#1AF59E0B";

    /// <summary>
    /// Основний колір для критичного стану (червоний).
    /// </summary>
    public const string CriticalHex = "#EF4444";

    /// <summary>
    /// Фоновий напівпрозорий колір для критичного стану.
    /// </summary>
    public const string CriticalBackgroundHex = "#1AEF4444";

    // --- Кольори з підвищеною контрастністю для світлої теми (WCAG 2.1 AA >= 4.5:1 на білому фоні) ---

    /// <summary>
    /// Висококонтрастний колір тексту нормального стану для світлої теми.
    /// </summary>
    public const string NormalLightTextHex = "#047857";

    /// <summary>
    /// Висококонтрастний колір тексту стану попередження для світлої теми.
    /// </summary>
    public const string WarningLightTextHex = "#B45309";

    /// <summary>
    /// Висококонтрастний колір тексту критичного стану для світлої теми.
    /// </summary>
    public const string CriticalLightTextHex = "#B91C1C";

    // --- Базові кольори поверхонь та тексту для Темної та Світлої тем (Issue #11) ---

    /// <summary>
    /// Колір основного фону вікна у темній темі.
    /// </summary>
    public const string DarkSurfaceHex = "#0F172A";

    /// <summary>
    /// Колір фону карток у темній темі.
    /// </summary>
    public const string DarkCardHex = "#1E293B";

    /// <summary>
    /// Колір основного тексту у темній темі.
    /// </summary>
    public const string DarkPrimaryTextHex = "#F8FAFC";

    /// <summary>
    /// Колір вторинного тексту та підписів осей графіків у темній темі.
    /// </summary>
    public const string DarkSecondaryTextHex = "#CBD5E1";

    /// <summary>
    /// Колір основного фону вікна у світлій темі.
    /// </summary>
    public const string LightSurfaceHex = "#F8FAFC";

    /// <summary>
    /// Колір фону карток у світлій темі.
    /// </summary>
    public const string LightCardHex = "#FFFFFF";

    /// <summary>
    /// Колір основного тексту у світлій темі.
    /// </summary>
    public const string LightPrimaryTextHex = "#0F172A";

    /// <summary>
    /// Колір вторинного тексту та підписів осей графіків у світлій темі.
    /// </summary>
    public const string LightSecondaryTextHex = "#334155";

    /// <summary>
    /// Пензель основного кольору для нормального стану.
    /// </summary>
    public static IBrush NormalBrush { get; } = Brush.Parse(NormalHex);

    /// <summary>
    /// Пензель фонового кольору для нормального стану.
    /// </summary>
    public static IBrush NormalBackgroundBrush { get; } = Brush.Parse(NormalBackgroundHex);

    /// <summary>
    /// Пензель основного кольору для стану попередження.
    /// </summary>
    public static IBrush WarningBrush { get; } = Brush.Parse(WarningHex);

    /// <summary>
    /// Пензель фонового кольору для стану попередження.
    /// </summary>
    public static IBrush WarningBackgroundBrush { get; } = Brush.Parse(WarningBackgroundHex);

    /// <summary>
    /// Пензель основного кольору для критичного стану.
    /// </summary>
    public static IBrush CriticalBrush { get; } = Brush.Parse(CriticalHex);

    /// <summary>
    /// Пензель фонового кольору для критичного стану.
    /// </summary>
    public static IBrush CriticalBackgroundBrush { get; } = Brush.Parse(CriticalBackgroundHex);

    /// <summary>
    /// Висококонтрастний пензель нормального стану для світлої теми.
    /// </summary>
    public static IBrush NormalLightTextBrush { get; } = Brush.Parse(NormalLightTextHex);

    /// <summary>
    /// Висококонтрастний пензель стану попередження для світлої теми.
    /// </summary>
    public static IBrush WarningLightTextBrush { get; } = Brush.Parse(WarningLightTextHex);

    /// <summary>
    /// Висококонтрастний пензель критичного стану для світлої теми.
    /// </summary>
    public static IBrush CriticalLightTextBrush { get; } = Brush.Parse(CriticalLightTextHex);

    /// <summary>
    /// Поріг попередження за замовчуванням для утилізації ресурсів (%).
    /// </summary>
    public const double DefaultWarningPercentage = 70.0;

    /// <summary>
    /// Критичний поріг за замовчуванням для утилізації ресурсів (%).
    /// </summary>
    public const double DefaultCriticalPercentage = 90.0;

    /// <summary>
    /// Поріг попередження за замовчуванням для температури (°C).
    /// </summary>
    public const double DefaultWarningTemperatureCelsius = 70.0;

    /// <summary>
    /// Критичний поріг за замовчуванням для температури (°C).
    /// </summary>
    public const double DefaultCriticalTemperatureCelsius = 85.0;

    /// <summary>
    /// Визначає системний стан за відсотком навантаження (0–100%).
    /// </summary>
    public static SystemHealthState EvaluateByPercentage(
        double percentage,
        double warningThreshold = DefaultWarningPercentage,
        double criticalThreshold = DefaultCriticalPercentage)
    {
        var clamped = Math.Clamp(percentage, 0.0, 100.0);

        if (clamped >= criticalThreshold)
        {
            return SystemHealthState.Critical;
        }

        if (clamped >= warningThreshold)
        {
            return SystemHealthState.Warning;
        }

        return SystemHealthState.Normal;
    }

    /// <summary>
    /// Визначає системний стан за показником температури (°C).
    /// </summary>
    public static SystemHealthState EvaluateByTemperature(
        double? temperatureCelsius,
        double warningThreshold = DefaultWarningTemperatureCelsius,
        double criticalThreshold = DefaultCriticalTemperatureCelsius)
    {
        if (!temperatureCelsius.HasValue)
        {
            return SystemHealthState.Normal;
        }

        if (temperatureCelsius.Value >= criticalThreshold)
        {
            return SystemHealthState.Critical;
        }

        if (temperatureCelsius.Value >= warningThreshold)
        {
            return SystemHealthState.Warning;
        }

        return SystemHealthState.Normal;
    }

    /// <summary>
    /// Повертає HEX-код основного кольору для заданого стану системи.
    /// </summary>
    public static string GetColorHex(SystemHealthState state) => state switch
    {
        SystemHealthState.Warning => WarningHex,
        SystemHealthState.Critical => CriticalHex,
        _ => NormalHex
    };

    /// <summary>
    /// Повертає HEX-код текстового кольору для заданого стану з урахуванням активної теми (WCAG 2.1 AA >= 4.5:1).
    /// </summary>
    public static string GetAccessibleTextColorHex(SystemHealthState state, bool isDarkTheme)
    {
        if (isDarkTheme)
        {
            return GetColorHex(state);
        }

        return state switch
        {
            SystemHealthState.Warning => WarningLightTextHex,
            SystemHealthState.Critical => CriticalLightTextHex,
            _ => NormalLightTextHex
        };
    }

    /// <summary>
    /// Повертає пензель текстового кольору для заданого стану з урахуванням активної теми.
    /// </summary>
    public static IBrush GetAccessibleTextBrush(SystemHealthState state, bool isDarkTheme)
    {
        if (isDarkTheme)
        {
            return GetBrush(state);
        }

        return state switch
        {
            SystemHealthState.Warning => WarningLightTextBrush,
            SystemHealthState.Critical => CriticalLightTextBrush,
            _ => NormalLightTextBrush
        };
    }

    /// <summary>
    /// Повертає HEX-код напівпрозорого фону для заданого стану системи.
    /// </summary>
    public static string GetBackgroundHex(SystemHealthState state) => state switch
    {
        SystemHealthState.Warning => WarningBackgroundHex,
        SystemHealthState.Critical => CriticalBackgroundHex,
        _ => NormalBackgroundHex
    };

    /// <summary>
    /// Повертає пензель основного кольору для заданого стану системи.
    /// </summary>
    public static IBrush GetBrush(SystemHealthState state) => state switch
    {
        SystemHealthState.Warning => WarningBrush,
        SystemHealthState.Critical => CriticalBrush,
        _ => NormalBrush
    };

    /// <summary>
    /// Повертає пензель напівпрозорого фону для заданого стану системи.
    /// </summary>
    public static IBrush GetBackgroundBrush(SystemHealthState state) => state switch
    {
        SystemHealthState.Warning => WarningBackgroundBrush,
        SystemHealthState.Critical => CriticalBackgroundBrush,
        _ => NormalBackgroundBrush
    };

    /// <summary>
    /// Повертає локалізовану назву стану системи українською мовою.
    /// </summary>
    public static string GetDisplayName(SystemHealthState state) => state switch
    {
        SystemHealthState.Warning => "Попередження",
        SystemHealthState.Critical => "Критичний",
        _ => "Нормальний"
    };

    /// <summary>
    /// Обчислює відносну яскравість кольору (Relative Luminance) за формулою стандарту WCAG 2.1.
    /// </summary>
    public static double CalculateRelativeLuminance(Color color)
    {
        double r = LinearizeChannel(color.R / 255.0);
        double g = LinearizeChannel(color.G / 255.0);
        double b = LinearizeChannel(color.B / 255.0);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Обчислює коефіцієнт контрастності між двома кольорами за стандартом WCAG 2.1 (від 1.0 до 21.0).
    /// </summary>
    public static double CalculateContrastRatio(Color foreground, Color background)
    {
        double lum1 = CalculateRelativeLuminance(foreground);
        double lum2 = CalculateRelativeLuminance(background);

        double brightest = Math.Max(lum1, lum2);
        double darkest = Math.Min(lum1, lum2);

        return Math.Round((brightest + 0.05) / (darkest + 0.05), 2);
    }

    /// <summary>
    /// Обчислює коефіцієнт контрастності між двома HEX-кольорами за стандартом WCAG 2.1.
    /// </summary>
    public static double CalculateContrastRatio(string foregroundHex, string backgroundHex)
    {
        var fg = Color.Parse(foregroundHex);
        var bg = Color.Parse(backgroundHex);
        return CalculateContrastRatio(fg, bg);
    }

    /// <summary>
    /// Перевіряє, чи відповідає пара кольорів вимогам контрастності звичайного тексту WCAG 2.1 AA (&gt;= 4.5:1).
    /// </summary>
    public static bool MeetsWcagTextContrast(string foregroundHex, string backgroundHex) =>
        CalculateContrastRatio(foregroundHex, backgroundHex) >= MinimumWcagAaTextContrastRatio;

    /// <summary>
    /// Перевіряє, чи відповідає пара кольорів вимогам контрастності графіків та UI-елементів WCAG 2.1 AA (&gt;= 3.0:1).
    /// </summary>
    public static bool MeetsWcagGraphicalContrast(string foregroundHex, string backgroundHex) =>
        CalculateContrastRatio(foregroundHex, backgroundHex) >= MinimumWcagAaGraphicalContrastRatio;

    private static double LinearizeChannel(double sRgbChannel) =>
        sRgbChannel <= 0.03928
            ? sRgbChannel / 12.92
            : Math.Pow((sRgbChannel + 0.055) / 1.055, 2.4);
}
