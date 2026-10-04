using System;
using System.Collections.Generic;

namespace FullMonitoring.Models;

/// <summary>
/// Telemetry metrics for an individual storage volume or mount point.
/// </summary>
public sealed record DriveItemMetrics
{
    /// <summary>
    /// Drive identifier or root path (e.g., "C:\", "/dev/sda1", "/").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Volume label or friendly name.
    /// </summary>
    public string VolumeLabel { get; init; } = string.Empty;

    /// <summary>
    /// Root mount point directory path.
    /// </summary>
    public string RootDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Drive type (e.g., Fixed, Removable, Network).
    /// </summary>
    public string DriveType { get; init; } = string.Empty;

    /// <summary>
    /// File system format (e.g., NTFS, ext4, APFS).
    /// </summary>
    public string DriveFormat { get; init; } = string.Empty;

    /// <summary>
    /// Total storage capacity of the drive in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Available free space for the current user in bytes.
    /// </summary>
    public long AvailableFreeBytes { get; init; }

    /// <summary>
    /// Total free space on drive in bytes.
    /// </summary>
    public long TotalFreeBytes { get; init; }

    /// <summary>
    /// Used storage capacity in bytes.
    /// </summary>
    public long UsedBytes => TotalBytes >= AvailableFreeBytes ? TotalBytes - AvailableFreeBytes : 0;

    /// <summary>
    /// Percentage of storage currently in use (0.0 to 100.0).
    /// </summary>
    public double UsedPercentage => TotalBytes > 0
        ? Math.Clamp((double)UsedBytes / TotalBytes * 100.0, 0.0, 100.0)
        : 0.0;

    /// <summary>
    /// Indicates whether the drive is mounted and ready for I/O operations.
    /// </summary>
    public bool IsReady { get; init; } = true;
}

/// <summary>
/// Telemetry metrics aggregating system-wide storage devices.
/// </summary>
public sealed record DiskMetrics
{
    /// <summary>
    /// Collection of individual mounted drives.
    /// </summary>
    public IReadOnlyList<DriveItemMetrics> Drives { get; init; } = Array.Empty<DriveItemMetrics>();

    /// <summary>
    /// Total storage capacity across all active drives in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Total used storage across all active drives in bytes.
    /// </summary>
    public long UsedBytes { get; init; }

    /// <summary>
    /// Total free storage across all active drives in bytes.
    /// </summary>
    public long FreeBytes { get; init; }

    /// <summary>
    /// Overall storage utilization percentage across all active drives (0.0 to 100.0).
    /// </summary>
    public double OverallUsedPercentage => TotalBytes > 0
        ? Math.Clamp((double)UsedBytes / TotalBytes * 100.0, 0.0, 100.0)
        : 0.0;

    public DiskMetrics() { }

    public DiskMetrics(IReadOnlyList<DriveItemMetrics> drives)
    {
        Drives = drives ?? Array.Empty<DriveItemMetrics>();
        long total = 0;
        long used = 0;
        long free = 0;

        foreach (var d in Drives)
        {
            if (d.IsReady)
            {
                total += d.TotalBytes;
                used += d.UsedBytes;
                free += d.AvailableFreeBytes;
            }
        }

        TotalBytes = total;
        UsedBytes = used;
        FreeBytes = free;
    }
}
