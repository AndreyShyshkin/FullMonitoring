using System;
using System.Collections.Generic;

namespace FullMonitoring.Models;

/// <summary>
/// Telemetry metrics for Central Processing Unit (CPU).
/// </summary>
public sealed record CpuMetrics
{
    /// <summary>
    /// Total CPU load percentage across all cores (0.0 to 100.0).
    /// </summary>
    public double TotalUsagePercentage { get; init; }

    /// <summary>
    /// Individual CPU utilization percentage for each logical core.
    /// </summary>
    public IReadOnlyList<double> CoreUsagesPercentage { get; init; } = Array.Empty<double>();

    /// <summary>
    /// CPU package / average temperature in degrees Celsius, or null if sensors are unavailable.
    /// </summary>
    public double? TemperatureCelsius { get; init; }

    /// <summary>
    /// CPU cooler fan speed in Revolutions Per Minute (RPM), or null if unavailable.
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
