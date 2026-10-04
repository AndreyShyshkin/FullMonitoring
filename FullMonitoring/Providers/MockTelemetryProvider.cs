using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;

namespace FullMonitoring.Providers;

/// <summary>
/// Mock telemetry provider that generates realistic pseudo-random oscillating metrics.
/// Suitable for parallel UI development, widget styling, and automated testing.
/// </summary>
public sealed class MockTelemetryProvider : ITelemetryProvider
{
    private readonly object _syncRoot = new();
    private readonly Random _random;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    private const int CoreCount = 8;
    private const long TotalRamBytes = 32L * 1024 * 1024 * 1024; // 32 GB

    private double _cpuBaseLoad = 38.0;
    private long _ramUsedBytes = 12L * 1024 * 1024 * 1024; // ~12 GB

    private long _ethRxBytes = 14L * 1024 * 1024 * 1024;
    private long _ethTxBytes = 3L * 1024 * 1024 * 1024;
    private long _wifiRxBytes = 5L * 1024 * 1024 * 1024;
    private long _wifiTxBytes = 1L * 1024 * 1024 * 1024;

    private DateTimeOffset _lastUpdate = DateTimeOffset.UtcNow;

    /// <summary>
    /// Initializes a new instance of <see cref="MockTelemetryProvider"/> with an optional random seed.
    /// </summary>
    /// <param name="seed">Optional seed for deterministic reproducibility in unit tests.</param>
    public MockTelemetryProvider(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <inheritdoc />
    public string ProviderName => "Mock Telemetry Provider (Simulated)";

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public SystemTelemetrySnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            AdvanceSimulation();

            return new SystemTelemetrySnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Cpu = GenerateCpuMetricsInternal(),
                Ram = GenerateRamMetricsInternal(),
                Disk = GenerateDiskMetricsInternal(),
                Network = GenerateNetworkMetricsInternal(),
                OsDescription = "Mock OS 1.0 (Simulation Engine)",
                Architecture = "Arm64",
                MachineName = "MOCK-DEV-RIG",
                Uptime = DateTimeOffset.UtcNow - _startTime
            };
        }
    }

    /// <inheritdoc />
    public Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetSnapshot, cancellationToken);
    }

    /// <inheritdoc />
    public CpuMetrics GetCpuMetrics()
    {
        lock (_syncRoot)
        {
            AdvanceSimulation();
            return GenerateCpuMetricsInternal();
        }
    }

    /// <inheritdoc />
    public Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetCpuMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public RamMetrics GetRamMetrics()
    {
        lock (_syncRoot)
        {
            AdvanceSimulation();
            return GenerateRamMetricsInternal();
        }
    }

    /// <inheritdoc />
    public Task<RamMetrics> GetRamMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetRamMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public DiskMetrics GetDiskMetrics()
    {
        lock (_syncRoot)
        {
            AdvanceSimulation();
            return GenerateDiskMetricsInternal();
        }
    }

    /// <inheritdoc />
    public Task<DiskMetrics> GetDiskMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetDiskMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public NetworkMetrics GetNetworkMetrics()
    {
        lock (_syncRoot)
        {
            AdvanceSimulation();
            return GenerateNetworkMetricsInternal();
        }
    }

    /// <inheritdoc />
    public Task<NetworkMetrics> GetNetworkMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(GetNetworkMetrics, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private void AdvanceSimulation()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsedSeconds = Math.Max(0.1, (now - _lastUpdate).TotalSeconds);

        // CPU random walk with mean-reversion towards 40%
        double cpuStep = (_random.NextDouble() - 0.48) * 8.0;
        double cpuMeanPull = (40.0 - _cpuBaseLoad) * 0.08;
        _cpuBaseLoad = Math.Clamp(_cpuBaseLoad + cpuStep + cpuMeanPull, 5.0, 95.0);

        // RAM random fluctuation (+/- 80 MB)
        long ramDelta = (long)((_random.NextDouble() - 0.49) * 80 * 1024 * 1024);
        _ramUsedBytes = Math.Clamp(_ramUsedBytes + ramDelta, 4L * 1024 * 1024 * 1024, 28L * 1024 * 1024 * 1024);

        // Advance cumulative network traffic counters
        double ethRxRate = 2_000_000 + _random.NextDouble() * 8_000_000;
        double ethTxRate = 400_000 + _random.NextDouble() * 2_000_000;
        _ethRxBytes += (long)(ethRxRate * elapsedSeconds);
        _ethTxBytes += (long)(ethTxRate * elapsedSeconds);

        double wifiRxRate = 500_000 + _random.NextDouble() * 1_500_000;
        double wifiTxRate = 100_000 + _random.NextDouble() * 400_000;
        _wifiRxBytes += (long)(wifiRxRate * elapsedSeconds);
        _wifiTxBytes += (long)(wifiTxRate * elapsedSeconds);

        _lastUpdate = now;
    }

    private CpuMetrics GenerateCpuMetricsInternal()
    {
        var coreLoads = new List<double>(CoreCount);
        for (int i = 0; i < CoreCount; i++)
        {
            double coreOffset = (_random.NextDouble() - 0.5) * 16.0;
            double coreLoad = Math.Clamp(_cpuBaseLoad + coreOffset, 0.0, 100.0);
            coreLoads.Add(Math.Round(coreLoad, 1));
        }

        // Realistic temperature correlates with CPU load (42°C idle up to 82°C max load)
        double temp = 42.0 + (_cpuBaseLoad * 0.38) + (_random.NextDouble() - 0.5) * 3.0;
        // Realistic fan speed correlates with temperature (1000 RPM up to 2600 RPM)
        double fanSpeed = 1000.0 + (_cpuBaseLoad * 15.0) + (_random.NextDouble() - 0.5) * 80.0;

        return new CpuMetrics(
            totalUsagePercentage: Math.Round(_cpuBaseLoad, 1),
            coreUsagesPercentage: coreLoads,
            temperatureCelsius: Math.Round(temp, 1),
            fanSpeedRpm: Math.Round(fanSpeed, 0)
        );
    }

    private RamMetrics GenerateRamMetricsInternal()
    {
        long freeBytes = Math.Max(0, TotalRamBytes - _ramUsedBytes);
        double usedPercentage = (double)_ramUsedBytes / TotalRamBytes * 100.0;

        return new RamMetrics(
            totalBytes: TotalRamBytes,
            usedBytes: _ramUsedBytes,
            freeBytes: freeBytes,
            usedPercentage: Math.Round(usedPercentage, 1)
        );
    }

    private DiskMetrics GenerateDiskMetricsInternal()
    {
        const long systemDriveTotal = 512L * 1024 * 1024 * 1024;   // 512 GB
        const long systemDriveFree = 186L * 1024 * 1024 * 1024;    // 186 GB
        const long dataDriveTotal = 2048L * 1024 * 1024 * 1024;    // 2 TB
        const long dataDriveFree = 920L * 1024 * 1024 * 1024;      // 920 GB

        var drives = new List<DriveItemMetrics>
        {
            new()
            {
                Name = "/",
                VolumeLabel = "System",
                RootDirectory = "/",
                DriveType = "Fixed",
                DriveFormat = "APFS",
                TotalBytes = systemDriveTotal,
                AvailableFreeBytes = systemDriveFree,
                TotalFreeBytes = systemDriveFree,
                IsReady = true
            },
            new()
            {
                Name = "/Volumes/Data",
                VolumeLabel = "DataStorage",
                RootDirectory = "/Volumes/Data",
                DriveType = "Fixed",
                DriveFormat = "APFS",
                TotalBytes = dataDriveTotal,
                AvailableFreeBytes = dataDriveFree,
                TotalFreeBytes = dataDriveFree,
                IsReady = true
            }
        };

        return new DiskMetrics(drives);
    }

    private NetworkMetrics GenerateNetworkMetricsInternal()
    {
        double ethRxRate = 2_000_000 + _random.NextDouble() * 8_000_000;
        double ethTxRate = 400_000 + _random.NextDouble() * 2_000_000;
        double wifiRxRate = 500_000 + _random.NextDouble() * 1_500_000;
        double wifiTxRate = 100_000 + _random.NextDouble() * 400_000;

        var interfaces = new List<NetworkInterfaceMetrics>
        {
            new()
            {
                Id = "mock-eth0",
                Name = "Ethernet",
                Description = "Virtual Gigabit Network Adapter",
                InterfaceType = "Ethernet",
                OperationalStatus = "Up",
                SpeedBitsPerSecond = 1_000_000_000,
                BytesReceived = _ethRxBytes,
                BytesSent = _ethTxBytes,
                RxSpeedBytesPerSecond = Math.Round(ethRxRate, 2),
                TxSpeedBytesPerSecond = Math.Round(ethTxRate, 2)
            },
            new()
            {
                Id = "mock-wlan0",
                Name = "Wi-Fi",
                Description = "Virtual 802.11ax Wireless Controller",
                InterfaceType = "Wireless80211",
                OperationalStatus = "Up",
                SpeedBitsPerSecond = 866_000_000,
                BytesReceived = _wifiRxBytes,
                BytesSent = _wifiTxBytes,
                RxSpeedBytesPerSecond = Math.Round(wifiRxRate, 2),
                TxSpeedBytesPerSecond = Math.Round(wifiTxRate, 2)
            }
        };

        return new NetworkMetrics(interfaces);
    }
}
