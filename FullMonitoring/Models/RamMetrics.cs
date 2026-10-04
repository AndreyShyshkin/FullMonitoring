using System;

namespace FullMonitoring.Models;

/// <summary>
/// Telemetry metrics for Random Access Memory (RAM).
/// </summary>
public sealed record RamMetrics
{
    /// <summary>
    /// Total physical RAM available to the system in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Currently allocated / in-use RAM in bytes.
    /// </summary>
    public long UsedBytes { get; init; }

    /// <summary>
    /// Free / available RAM in bytes.
    /// </summary>
    public long FreeBytes { get; init; }

    /// <summary>
    /// Percentage of RAM currently in use (0.0 to 100.0).
    /// </summary>
    public double UsedPercentage { get; init; }

    public RamMetrics() { }

    public RamMetrics(long totalBytes, long usedBytes, long freeBytes, double? usedPercentage = null)
    {
        TotalBytes = Math.Max(0, totalBytes);
        UsedBytes = Math.Max(0, usedBytes);
        FreeBytes = Math.Max(0, freeBytes);
        UsedPercentage = usedPercentage ?? (TotalBytes > 0
            ? Math.Clamp((double)UsedBytes / TotalBytes * 100.0, 0.0, 100.0)
            : 0.0);
    }
}
