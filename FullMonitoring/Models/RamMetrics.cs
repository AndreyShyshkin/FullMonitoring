using System;

namespace FullMonitoring.Models;

/// <summary>
/// Метрики телеметрії оперативної пам'яті (RAM).
/// </summary>
public sealed record RamMetrics
{
    /// <summary>
    /// Загальний обсяг фізичної оперативної пам'яті, доступний системі, у байтах.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Поточний виділений / зайнятий обсяг оперативної пам'яті у байтах.
    /// </summary>
    public long UsedBytes { get; init; }

    /// <summary>
    /// Вільний / доступний обсяг оперативної пам'яті у байтах.
    /// </summary>
    public long FreeBytes { get; init; }

    /// <summary>
    /// Відсоток використання оперативної пам'яті (0.0 до 100.0).
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
