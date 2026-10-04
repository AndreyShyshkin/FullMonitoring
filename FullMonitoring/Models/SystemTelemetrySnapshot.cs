using System;

namespace FullMonitoring.Models;

/// <summary>
/// Immutable snapshot representing overall system telemetry metrics at a specific point in time.
/// </summary>
public sealed record SystemTelemetrySnapshot
{
    /// <summary>
    /// UTC timestamp when the snapshot was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// CPU utilization, core breakdown, and thermal readings.
    /// </summary>
    public CpuMetrics Cpu { get; init; } = new();

    /// <summary>
    /// System physical memory (RAM) allocation metrics.
    /// </summary>
    public RamMetrics Ram { get; init; } = new();

    /// <summary>
    /// Storage volumes, mount points, and disk capacity metrics.
    /// </summary>
    public DiskMetrics Disk { get; init; } = new();

    /// <summary>
    /// Network interfaces, traffic counters, and throughput speeds.
    /// </summary>
    public NetworkMetrics Network { get; init; } = new();

    /// <summary>
    /// Operating system description (e.g., "Microsoft Windows 11", "Linux 6.8", "macOS 15.1").
    /// </summary>
    public string OsDescription { get; init; } = string.Empty;

    /// <summary>
    /// Process architecture / CPU architecture (e.g., X64, Arm64).
    /// </summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>
    /// Host machine name.
    /// </summary>
    public string MachineName { get; init; } = string.Empty;

    /// <summary>
    /// System uptime duration.
    /// </summary>
    public TimeSpan Uptime { get; init; }
}
