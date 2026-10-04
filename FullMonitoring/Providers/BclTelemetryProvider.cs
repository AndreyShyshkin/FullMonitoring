using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;

namespace FullMonitoring.Providers;

/// <summary>
/// Cross-platform telemetry provider using standard .NET Base Class Library (BCL) APIs.
/// Provides baseline telemetry for RAM (GC/Process), Disks (DriveInfo), and Network (NetworkInterface).
/// </summary>
public class BclTelemetryProvider : ITelemetryProvider
{
    private readonly object _cpuLock = new();
    private readonly object _networkLock = new();

    private DateTimeOffset _lastCpuCheckTime = DateTimeOffset.MinValue;
    private TimeSpan _lastCpuTotalProcessorTime = TimeSpan.Zero;
    private double _lastCalculatedCpuUsage;

    private DateTimeOffset _lastNetworkCheckTime = DateTimeOffset.MinValue;
    private readonly Dictionary<string, (long bytesReceived, long bytesSent)> _previousNetworkStats = new();

    /// <inheritdoc />
    public virtual string ProviderName => "BCL Base Telemetry Provider";

    /// <inheritdoc />
    public virtual bool IsSupported => true;

    /// <inheritdoc />
    public virtual SystemTelemetrySnapshot GetSnapshot()
    {
        return new SystemTelemetrySnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Cpu = GetCpuMetrics(),
            Ram = GetRamMetrics(),
            Disk = GetDiskMetrics(),
            Network = GetNetworkMetrics(),
            OsDescription = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            MachineName = Environment.MachineName,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
        };
    }

    /// <inheritdoc />
    public virtual Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetSnapshot, cancellationToken);
    }

    /// <inheritdoc />
    public virtual CpuMetrics GetCpuMetrics()
    {
        var coreCount = Math.Max(1, Environment.ProcessorCount);
        double usage = 0.0;
        var now = DateTimeOffset.UtcNow;

        lock (_cpuLock)
        {
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                var currentProcessorTime = currentProcess.TotalProcessorTime;

                if (_lastCpuCheckTime != DateTimeOffset.MinValue)
                {
                    var wallElapsedMs = (now - _lastCpuCheckTime).TotalMilliseconds;
                    var cpuElapsedMs = (currentProcessorTime - _lastCpuTotalProcessorTime).TotalMilliseconds;

                    if (wallElapsedMs > 0)
                    {
                        usage = Math.Clamp((cpuElapsedMs / (wallElapsedMs * coreCount)) * 100.0, 0.0, 100.0);
                        _lastCalculatedCpuUsage = usage;
                    }
                    else
                    {
                        usage = _lastCalculatedCpuUsage;
                    }
                }

                _lastCpuTotalProcessorTime = currentProcessorTime;
                _lastCpuCheckTime = now;
            }
            catch (Exception)
            {
                usage = _lastCalculatedCpuUsage;
            }
        }

        var coreLoads = new List<double>(coreCount);
        for (int i = 0; i < coreCount; i++)
        {
            coreLoads.Add(Math.Round(usage, 2));
        }

        return new CpuMetrics(
            totalUsagePercentage: Math.Round(usage, 2),
            coreUsagesPercentage: coreLoads,
            temperatureCelsius: null,
            fanSpeedRpm: null
        );
    }

    /// <inheritdoc />
    public virtual Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetCpuMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public virtual RamMetrics GetRamMetrics()
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        long totalBytes = memoryInfo.TotalAvailableMemoryBytes;
        long usedBytes = memoryInfo.MemoryLoadBytes;

        if (totalBytes <= 0)
        {
            // Fallback for environments where GC info is not populated
            try
            {
                using var proc = Process.GetCurrentProcess();
                usedBytes = proc.WorkingSet64;
                totalBytes = Math.Max(usedBytes, 1024L * 1024 * 1024);
            }
            catch (Exception)
            {
                totalBytes = 1024L * 1024 * 1024;
                usedBytes = 0;
            }
        }
        else if (usedBytes <= 0 || usedBytes > totalBytes)
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                usedBytes = Math.Min(totalBytes, proc.WorkingSet64);
            }
            catch (Exception)
            {
                usedBytes = 0;
            }
        }

        long freeBytes = totalBytes >= usedBytes ? totalBytes - usedBytes : 0;
        double usedPercentage = totalBytes > 0
            ? Math.Clamp((double)usedBytes / totalBytes * 100.0, 0.0, 100.0)
            : 0.0;

        return new RamMetrics(
            totalBytes: totalBytes,
            usedBytes: usedBytes,
            freeBytes: freeBytes,
            usedPercentage: Math.Round(usedPercentage, 2)
        );
    }

    /// <inheritdoc />
    public virtual Task<RamMetrics> GetRamMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetRamMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public virtual DiskMetrics GetDiskMetrics()
    {
        var drivesList = new List<DriveItemMetrics>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        drivesList.Add(new DriveItemMetrics
                        {
                            Name = drive.Name,
                            VolumeLabel = string.Empty,
                            RootDirectory = drive.RootDirectory.FullName,
                            DriveType = drive.DriveType.ToString(),
                            DriveFormat = string.Empty,
                            TotalBytes = 0,
                            AvailableFreeBytes = 0,
                            TotalFreeBytes = 0,
                            IsReady = false
                        });
                        continue;
                    }

                    drivesList.Add(new DriveItemMetrics
                    {
                        Name = drive.Name,
                        VolumeLabel = drive.VolumeLabel,
                        RootDirectory = drive.RootDirectory.FullName,
                        DriveType = drive.DriveType.ToString(),
                        DriveFormat = drive.DriveFormat,
                        TotalBytes = drive.TotalSize,
                        AvailableFreeBytes = drive.AvailableFreeSpace,
                        TotalFreeBytes = drive.TotalFreeSpace,
                        IsReady = true
                    });
                }
                catch (Exception)
                {
                    // Drive might have been unmounted during query or require elevated privileges
                }
            }
        }
        catch (Exception)
        {
            // Handle restricted sandbox environment
        }

        return new DiskMetrics(drivesList);
    }

    /// <inheritdoc />
    public virtual Task<DiskMetrics> GetDiskMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetDiskMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public virtual NetworkMetrics GetNetworkMetrics()
    {
        var interfacesList = new List<NetworkInterfaceMetrics>();

        lock (_networkLock)
        {
            var now = DateTimeOffset.UtcNow;
            double elapsedSeconds = _lastNetworkCheckTime == DateTimeOffset.MinValue
                ? 0
                : (now - _lastNetworkCheckTime).TotalSeconds;

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    try
                    {
                        var stats = ni.GetIPStatistics();
                        long rxBytes = stats.BytesReceived;
                        long txBytes = stats.BytesSent;

                        double rxSpeed = 0.0;
                        double txSpeed = 0.0;

                        if (elapsedSeconds > 0 && _previousNetworkStats.TryGetValue(ni.Id, out var prev))
                        {
                            long rxDiff = rxBytes - prev.bytesReceived;
                            long txDiff = txBytes - prev.bytesSent;

                            if (rxDiff >= 0)
                            {
                                rxSpeed = rxDiff / elapsedSeconds;
                            }

                            if (txDiff >= 0)
                            {
                                txSpeed = txDiff / elapsedSeconds;
                            }
                        }

                        _previousNetworkStats[ni.Id] = (rxBytes, txBytes);

                        interfacesList.Add(new NetworkInterfaceMetrics
                        {
                            Id = ni.Id,
                            Name = ni.Name,
                            Description = ni.Description,
                            InterfaceType = ni.NetworkInterfaceType.ToString(),
                            OperationalStatus = ni.OperationalStatus.ToString(),
                            SpeedBitsPerSecond = ni.Speed,
                            BytesReceived = rxBytes,
                            BytesSent = txBytes,
                            RxSpeedBytesPerSecond = Math.Round(rxSpeed, 2),
                            TxSpeedBytesPerSecond = Math.Round(txSpeed, 2)
                        });
                    }
                    catch (Exception)
                    {
                        // Statistics may fail for specific virtual interfaces or loopback
                    }
                }
            }
            catch (Exception)
            {
                // Restricted environments
            }

            _lastNetworkCheckTime = now;
        }

        return new NetworkMetrics(interfacesList);
    }

    /// <inheritdoc />
    public virtual Task<NetworkMetrics> GetNetworkMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetNetworkMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
