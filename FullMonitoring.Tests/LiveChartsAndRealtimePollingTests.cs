using System;
using System.Linq;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.ViewModels;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для інтеграції LiveCharts2, фіксованого буфера <see cref="BoundedObservableBuffer{T}"/>
/// та таймера опитування <c>DispatcherTimer</c> у реальному часі (Issue #7).
/// </summary>
public class LiveChartsAndRealtimePollingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public void BoundedObservableBuffer_Constructor_ThrowsOnInvalidCapacity(int invalidCapacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedObservableBuffer<double>(invalidCapacity));
    }

    [Fact]
    public void BoundedObservableBuffer_FillWithDefault_PopulatesExactMaxCapacity()
    {
        var buffer = new BoundedObservableBuffer<double>(maxCapacity: 60);
        buffer.FillWithDefault(0.0);

        Assert.Equal(60, buffer.MaxCapacity);
        Assert.Equal(60, buffer.Count);
        Assert.All(buffer, value => Assert.Equal(0.0, value));
    }

    [Fact]
    public void BoundedObservableBuffer_PushAndAdd_EvictsOldestElementInFifoOrder()
    {
        var buffer = new BoundedObservableBuffer<int>(maxCapacity: 5);

        for (int i = 1; i <= 8; i++)
        {
            buffer.Push(i);
        }

        Assert.Equal(5, buffer.Count);
        Assert.Equal(new[] { 4, 5, 6, 7, 8 }, buffer.ToArray());
    }

    [Fact]
    public void BoundedObservableBuffer_TenMinutesContinuousSimulation_NeverExceedsSixtyPoints()
    {
        var buffer = new BoundedObservableBuffer<double>(maxCapacity: 60);
        buffer.FillWithDefault(0.0);

        // Імітація 10 хвилин безперервного опитування з частотою 1 Гц (600 тіків)
        for (int second = 1; second <= 600; second++)
        {
            buffer.Push(second % 100);
            Assert.Equal(60, buffer.Count);
        }

        Assert.Equal(600 % 100, buffer[^1]);
    }

    [Fact]
    public void OverviewViewModel_InitializesLiveChartsSeriesAndAxesCorrectly()
    {
        using var provider = new MockTelemetryProvider(seed: 42);
        using var vm = new OverviewViewModel(provider);

        // Перевірка буфера історії CPU за 60 секунд
        Assert.Equal(OverviewViewModel.CpuHistoryCapacity, vm.CpuUsageHistory.MaxCapacity);
        Assert.Equal(OverviewViewModel.CpuHistoryCapacity, vm.CpuUsageHistory.Count);

        // Перевірка лінійної серії CPU
        Assert.Single(vm.CpuSeries);
        var cpuLineSeries = Assert.IsType<LineSeries<double>>(vm.CpuSeries[0]);
        Assert.Same(vm.CpuUsageHistory, cpuLineSeries.Values);

        // Перевірка осей графіка CPU (0..59 секунд та 0..100%)
        Assert.Single(vm.CpuXAxes);
        Assert.Equal(0, vm.CpuXAxes[0].MinLimit);
        Assert.Equal(OverviewViewModel.CpuHistoryCapacity - 1, vm.CpuXAxes[0].MaxLimit);

        Assert.Single(vm.CpuYAxes);
        Assert.Equal(0, vm.CpuYAxes[0].MinLimit);
        Assert.Equal(100, vm.CpuYAxes[0].MaxLimit);

        // Перевірка кругової діаграми RAM (зайнято / вільно)
        Assert.Equal(2, vm.RamSeries.Length);
        Assert.IsType<PieSeries<ObservableValue>>(vm.RamSeries[0]);
        Assert.IsType<PieSeries<ObservableValue>>(vm.RamSeries[1]);
    }

    [Fact]
    public void OverviewViewModel_ApplySnapshot_UpdatesChartValuesInPlaceWithoutReallocatingSeries()
    {
        using var provider = new MockTelemetryProvider(seed: 11);
        using var vm = new OverviewViewModel(provider);

        var initialCpuSeriesRef = vm.CpuSeries;
        var initialRamSeriesRef = vm.RamSeries;
        var initialUsedChartValueRef = vm.RamUsedChartValue;
        var initialFreeChartValueRef = vm.RamFreeChartValue;

        var snapshot = CreateSnapshot(cpuUsage: 82.5, ramUsedPercent: 64.0);
        vm.ApplySnapshot(snapshot);

        // Посилання на серії та ObservableValue залишаються тими самими (нульова алокація об'єктів графіків)
        Assert.Same(initialCpuSeriesRef, vm.CpuSeries);
        Assert.Same(initialRamSeriesRef, vm.RamSeries);
        Assert.Same(initialUsedChartValueRef, vm.RamUsedChartValue);
        Assert.Same(initialFreeChartValueRef, vm.RamFreeChartValue);

        // Остання точка буфера CPU та значення сегментів RAM оновлені
        Assert.Equal(OverviewViewModel.CpuHistoryCapacity, vm.CpuUsageHistory.Count);
        Assert.Equal(82.5, vm.CpuUsageHistory[^1]);
        Assert.Equal(64.0, vm.RamUsedChartValue.Value);
        Assert.Equal(36.0, vm.RamFreeChartValue.Value);
    }

    [Fact]
    public void OverviewViewModel_TenMinutesContinuousPolling_KeepsMemoryCollectionsStable()
    {
        using var provider = new MockTelemetryProvider(seed: 99);
        using var vm = new OverviewViewModel(provider);

        // Імітація 10 хвилин безперервної роботи (600 оновлень підряд з частотою 1 секунда)
        for (int tick = 0; tick < 600; tick++)
        {
            double cpu = (tick * 7) % 100;
            double ram = 40.0 + (tick % 55);
            vm.ApplySnapshot(CreateSnapshot(cpu, ram));
        }

        Assert.Equal(OverviewViewModel.CpuHistoryCapacity, vm.CpuUsageHistory.Count);
        Assert.Single(vm.Drives);
        Assert.Single(vm.NetworkInterfaces);
    }

    [Fact]
    public void OverviewViewModel_DispatcherTimerLifecycle_SupportsStartStopToggleAndClamping()
    {
        using var provider = new MockTelemetryProvider(seed: 5);
        using var vm = new OverviewViewModel(provider);

        // За замовчуванням таймер активний з частотою 1 секунда (1000 мс)
        Assert.True(vm.IsPollingActive);
        Assert.Equal(OverviewViewModel.DefaultPollingIntervalMs, vm.PollingIntervalMs);

        // Призупинення опитування
        vm.StopPollingCommand.Execute(null);
        Assert.False(vm.IsPollingActive);

        // Повторний запуск опитування
        vm.StartPollingCommand.Execute(null);
        Assert.True(vm.IsPollingActive);

        // Перемикання через TogglePollingCommand
        vm.TogglePollingCommand.Execute(null);
        Assert.False(vm.IsPollingActive);
        vm.TogglePollingCommand.Execute(null);
        Assert.True(vm.IsPollingActive);

        // Перевірка обмеження діапазону інтервалу опитування
        vm.PollingIntervalMs = 50;
        Assert.Equal(SettingsViewModel.MinPollingIntervalMs, vm.PollingIntervalMs);

        vm.PollingIntervalMs = 30000;
        Assert.Equal(SettingsViewModel.MaxPollingIntervalMs, vm.PollingIntervalMs);

        // Звільнення ресурсів зупиняє таймер
        vm.Dispose();
        Assert.False(vm.IsPollingActive);
        vm.StartPolling();
        Assert.False(vm.IsPollingActive);
    }

    [Fact]
    public void MainViewModel_SynchronizesSettingsPollingInterval_WithOverviewTimer()
    {
        using var provider = new MockTelemetryProvider(seed: 15);
        using var overviewVm = new OverviewViewModel(provider);
        var processesVm = new ProcessesViewModel(new NoOpProcessManagerService());
        var settingsVm = new SettingsViewModel();
        var mainVm = new MainViewModel(overviewVm, processesVm, settingsVm);

        Assert.Equal(1000, mainVm.Overview.PollingIntervalMs);

        mainVm.Settings.PollingIntervalMs = 2000;
        Assert.Equal(2000, mainVm.Overview.PollingIntervalMs);
    }

    private static SystemTelemetrySnapshot CreateSnapshot(double cpuUsage, double ramUsedPercent)
    {
        long totalRam = 16L * 1024 * 1024 * 1024;
        long usedRam = (long)(totalRam * (ramUsedPercent / 100.0));
        long freeRam = totalRam - usedRam;

        return new SystemTelemetrySnapshot
        {
            OsDescription = "Windows 11",
            Architecture = "X64",
            MachineName = "WORKSTATION",
            Uptime = TimeSpan.FromHours(12),
            Cpu = new CpuMetrics(cpuUsage, new[] { cpuUsage, cpuUsage }, temperatureCelsius: 55.0, fanSpeedRpm: 1500),
            Ram = new RamMetrics(totalRam, usedRam, freeRam, ramUsedPercent),
            Disk = new DiskMetrics(new[]
            {
                new DriveItemMetrics
                {
                    Name = "C:\\",
                    DriveFormat = "NTFS",
                    TotalBytes = 500L * 1024 * 1024 * 1024,
                    AvailableFreeBytes = 250L * 1024 * 1024 * 1024,
                    TotalFreeBytes = 250L * 1024 * 1024 * 1024,
                    IsReady = true
                }
            }),
            Network = new NetworkMetrics(new[]
            {
                new NetworkInterfaceMetrics
                {
                    Id = "eth0",
                    Name = "Ethernet",
                    InterfaceType = "Ethernet",
                    OperationalStatus = "Up",
                    BytesReceived = 1024 * 1024,
                    BytesSent = 512 * 1024,
                    RxSpeedBytesPerSecond = 2048,
                    TxSpeedBytesPerSecond = 1024
                }
            })
        };
    }

    private sealed class NoOpProcessManagerService : IProcessManagerService
    {
        public System.Collections.Generic.IReadOnlyList<ProcessItem> GetRunningProcesses(int limit = 50) =>
            Array.Empty<ProcessItem>();

        public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<ProcessItem>> GetRunningProcessesAsync(
            int limit = 50,
            System.Threading.CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<ProcessItem>>(Array.Empty<ProcessItem>());

        public bool KillProcess(int pid) => false;

        public System.Threading.Tasks.Task<bool> KillProcessAsync(
            int pid,
            System.Threading.CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult(false);
    }
}
