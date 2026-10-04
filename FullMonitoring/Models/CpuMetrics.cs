using System;
using System.Collections.Generic;

namespace FullMonitoring.Models;

/// <summary>
/// Метрики телеметрії центрального процесора (CPU).
/// </summary>
public sealed record CpuMetrics
{
    /// <summary>
    /// Загальний відсоток навантаження CPU по всіх ядрах (0.0 до 100.0).
    /// </summary>
    public double TotalUsagePercentage { get; init; }

    /// <summary>
    /// Відсоток утилізації для кожного окремого логічного ядра CPU.
    /// </summary>
    public IReadOnlyList<double> CoreUsagesPercentage { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Температура CPU в градусах Цельсія або null, якщо датчики недоступні.
    /// </summary>
    public double? TemperatureCelsius { get; init; }

    /// <summary>
    /// Швидкість обертання кулера CPU в обертах за хвилину (RPM) або null, якщо показник недоступний.
    /// </summary>
    public double? FanSpeedRpm { get; init; }

    public CpuMetrics() { }

    public CpuMetrics(
        double totalUsagePercentage,
        IReadOnlyList<double>? coreUsagesPercentage = null,
        double? temperatureCelsius = null,
        double? fanSpeedRpm = null)
    {
        TotalUsagePercentage = Math.Clamp(totalUsagePercentage, 0.0, 100.0);
        CoreUsagesPercentage = coreUsagesPercentage ?? Array.Empty<double>();
        TemperatureCelsius = temperatureCelsius;
        FanSpeedRpm = fanSpeedRpm;
    }
}
