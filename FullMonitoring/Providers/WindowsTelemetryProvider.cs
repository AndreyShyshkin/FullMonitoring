using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;

namespace FullMonitoring.Providers;

[SupportedOSPlatform("windows")]
public class WindowsTelemetryProvider : ITelemetryProvider
{
    private readonly WindowsHardwareReader _reader;
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _snapshotLock = new();

    private Task? _pollingTask;
    private bool _disposed;

    // Кешований знімок для виконання DoD "віддає без затримок (O(1))"
    private SystemTelemetrySnapshot _latestSnapshot = new();

    public string ProviderName => "Windows Hardware Telemetry Provider (LibreHardwareMonitor)";

    public bool IsSupported => OperatingSystem.IsWindows();

    public WindowsTelemetryProvider(TimeSpan? refreshInterval = null)
    {
        _reader = new WindowsHardwareReader();

        // 1000 мс забезпечує навантаження процесу на CPU не більше 1-2%
        _timer = new PeriodicTimer(refreshInterval ?? TimeSpan.FromMilliseconds(1000));

        // Початкова ініціалізація першого знімка
        RefreshSnapshot();

        // Запуск фонового періодичного опитування
        StartBackgroundPolling();
    }

    private void StartBackgroundPolling()
    {
        _pollingTask = Task.Run(async () =>
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(_cts.Token))
                {
                    RefreshSnapshot();
                }
            }
            catch (OperationCanceledException)
            {
                // Очікуване завершення при виклику Dispose
            }
        });
    }

    private void RefreshSnapshot()
    {
        try
        {
            // Оновлюємо внутрішні датчики LibreHardwareMonitor
            _reader.Update();

            var cpuMetrics = CollectCpuMetrics();
            var ramMetrics = CollectRamMetrics();
            var diskMetrics = CollectDiskMetrics();
            var networkMetrics = CollectNetworkMetrics();

            var snapshot = new SystemTelemetrySnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Cpu = cpuMetrics,
                Ram = ramMetrics,
                Disk = diskMetrics,
                Network = networkMetrics,
                OsDescription = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                MachineName = Environment.MachineName,
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            };

            lock (_snapshotLock)
            {
                _latestSnapshot = snapshot;
            }
        }
        catch (Exception ex)
        {
            // Захист фонового опитування від падінь
            Console.WriteLine($"Snapshot update warning: {ex.Message}");
        }
    }

    private CpuMetrics CollectCpuMetrics()
    {
        var (totalLoad, coreLoads) = _reader.GetCpuLoads();
        var temp = _reader.GetCpuTemperature();
        var fanSpeed = _reader.GetFanSpeed();

        var coreUsages = coreLoads.Select(c => (double)c).ToArray();

        return new CpuMetrics(
            totalUsagePercentage: totalLoad.HasValue ? (double)totalLoad.Value : 0.0,
            coreUsagesPercentage: coreUsages,
            temperatureCelsius: temp.HasValue ? (double)temp.Value : null,
            fanSpeedRpm: fanSpeed.HasValue ? (double)fanSpeed.Value : null
        );
    }

    private RamMetrics CollectRamMetrics()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            long totalBytes = gcMemoryInfo.TotalAvailableMemoryBytes;

            // Зчитування пам'яті через наявні датчики або системні виклики
            long usedBytes = Environment.WorkingSet;
            long freeBytes = Math.Max(0, totalBytes - usedBytes);

            return new RamMetrics(totalBytes, usedBytes, freeBytes);
        }
        catch
        {
            return new RamMetrics();
        }
    }

    private DiskMetrics CollectDiskMetrics()
    {
        try
        {
            var driveItems = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d =>
                {
                    try
                    {
                        return new DriveItemMetrics
                        {
                            Name = d.Name,
                            VolumeLabel = string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.Name : d.VolumeLabel,
                            RootDirectory = d.RootDirectory.FullName,
                            DriveType = d.DriveType.ToString(),
                            DriveFormat = d.DriveFormat,
                            TotalBytes = d.TotalSize,
                            AvailableFreeBytes = d.AvailableFreeSpace,
                            TotalFreeBytes = d.TotalFreeSpace,
                            IsReady = d.IsReady
                        };
                    }
                    catch
                    {
                        // Відлов винятків для оптичних або знімних дисків під час читання
                        return null;
                    }
                })
                .Where(d => d != null)
                .Cast<DriveItemMetrics>()
                .ToList();

            return new DiskMetrics(driveItems);
        }
        catch
        {
            return new DiskMetrics();
        }
    }

    private NetworkMetrics CollectNetworkMetrics()
    {
        try
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Select(nic =>
                {
                    try
                    {
                        var stats = nic.GetIPStatistics();
                        return new NetworkInterfaceMetrics
                        {
                            Id = nic.Id,
                            Name = nic.Name,
                            Description = nic.Description,
                            InterfaceType = nic.NetworkInterfaceType.ToString(),
                            OperationalStatus = nic.OperationalStatus.ToString(),
                            SpeedBitsPerSecond = nic.Speed,
                            BytesReceived = stats.BytesReceived,
                            BytesSent = stats.BytesSent
                        };
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(nic => nic != null)
                .Cast<NetworkInterfaceMetrics>()
                .ToList();

            return new NetworkMetrics(nics);
        }
        catch
        {
            return new NetworkMetrics();
        }
    }

    // --- Реалізація методів ITelemetryProvider (миттєве повернення за O(1)) ---

    public SystemTelemetrySnapshot GetSnapshot()
    {
        lock (_snapshotLock)
        {
            return _latestSnapshot;
        }
    }

    public Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetSnapshot());
    }

    public CpuMetrics GetCpuMetrics()
    {
        lock (_snapshotLock)
        {
            return _latestSnapshot.Cpu;
        }
    }

    public Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetCpuMetrics());
    }

    public RamMetrics GetRamMetrics()
    {
        lock (_snapshotLock)
        {
            return _latestSnapshot.Ram;
        }
    }

    public Task<RamMetrics> GetRamMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetRamMetrics());
    }

    public DiskMetrics GetDiskMetrics()
    {
        lock (_snapshotLock)
        {
            return _latestSnapshot.Disk;
        }
    }

    public Task<DiskMetrics> GetDiskMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetDiskMetrics());
    }

    public NetworkMetrics GetNetworkMetrics()
    {
        lock (_snapshotLock)
        {
            return _latestSnapshot.Network;
        }
    }

    public Task<NetworkMetrics> GetNetworkMetricsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetNetworkMetrics());
    }

    // --- Звільнення ресурсів ---

    public void Dispose()
    {
        if (_disposed) return;

        _cts.Cancel();
        _timer.Dispose();
        _reader.Dispose();
        _cts.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}