using System;

namespace FullMonitoring.Models;

/// <summary>
/// Фіксована колірна палітра та правила визначення системних станів (нормальний / попередження / критичний).
/// </summary>
public static class SystemStatePalette
{
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
    /// Повертає HEX-код напівпрозорого фону для заданого стану системи.
    /// </summary>
    public static string GetBackgroundHex(SystemHealthState state) => state switch
    {
        SystemHealthState.Warning => WarningBackgroundHex,
        SystemHealthState.Critical => CriticalBackgroundHex,
        _ => NormalBackgroundHex
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
}
