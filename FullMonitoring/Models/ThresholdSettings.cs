using System;

namespace FullMonitoring.Models;

/// <summary>
/// Модель конфігурації порогових значень навантаження та температури для генерації сповіщень (Issue #8).
/// </summary>
public sealed record ThresholdSettings
{
    /// <summary>
    /// Порогове навантаження CPU за замовчуванням (90%).
    /// </summary>
    public const double DefaultCpuUsageThresholdPercentage = 90.0;

    /// <summary>
    /// Порогова температура CPU за замовчуванням (85°C).
    /// </summary>
    public const double DefaultCpuTemperatureThresholdCelsius = 85.0;

    /// <summary>
    /// Порогове заповнення оперативної пам'яті за замовчуванням (90%).
    /// </summary>
    public const double DefaultRamUsageThresholdPercentage = 90.0;

    /// <summary>
    /// Тривалість перевантаження за замовчуванням у секундах (3 секунди) для усунення хибних спрацьовувань.
    /// </summary>
    public const double DefaultDurationThresholdSeconds = 3.0;

    /// <summary>
    /// Граничне завантаження CPU у відсотках (0.0..100.0).
    /// </summary>
    public double CpuUsageThresholdPercentage { get; init; } = DefaultCpuUsageThresholdPercentage;

    /// <summary>
    /// Гранична температура CPU у градусах Цельсія.
    /// </summary>
    public double CpuTemperatureThresholdCelsius { get; init; } = DefaultCpuTemperatureThresholdCelsius;

    /// <summary>
    /// Граничне заповнення RAM у відсотках (0.0..100.0).
    /// </summary>
    public double RamUsageThresholdPercentage { get; init; } = DefaultRamUsageThresholdPercentage;

    /// <summary>
    /// Мінімальна тривалість перебування показника вище порогового значення (в секундах),
    /// після якої фіксується інцидент та генерується сповіщення.
    /// </summary>
    public double DurationThresholdSeconds { get; init; } = DefaultDurationThresholdSeconds;

    /// <summary>
    /// Екземпляр конфігурації за замовчуванням.
    /// </summary>
    public static ThresholdSettings Default { get; } = new();

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="ThresholdSettings"/> зі стандартними параметрами.
    /// </summary>
    public ThresholdSettings() { }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="ThresholdSettings"/> із заданими значеннями та валідацією меж.
    /// </summary>
    /// <param name="cpuUsageThresholdPercentage">Граничне завантаження CPU (0..100%).</param>
    /// <param name="cpuTemperatureThresholdCelsius">Гранична температура CPU (°C).</param>
    /// <param name="ramUsageThresholdPercentage">Граничне заповнення RAM (0..100%).</param>
    /// <param name="durationThresholdSeconds">Мінімальна тривалість перевищення в секундах (>= 0).</param>
    public ThresholdSettings(
        double cpuUsageThresholdPercentage,
        double cpuTemperatureThresholdCelsius,
        double ramUsageThresholdPercentage,
        double durationThresholdSeconds = DefaultDurationThresholdSeconds)
    {
        CpuUsageThresholdPercentage = Math.Clamp(cpuUsageThresholdPercentage, 0.0, 100.0);
        CpuTemperatureThresholdCelsius = Math.Max(0.0, cpuTemperatureThresholdCelsius);
        RamUsageThresholdPercentage = Math.Clamp(ramUsageThresholdPercentage, 0.0, 100.0);
        DurationThresholdSeconds = Math.Max(0.0, durationThresholdSeconds);
    }
}
