using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.ViewModels;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для навігації головного вікна, палітри системних станів та ViewModels вкладок (Issue #3).
/// </summary>
public class UiNavigationAndViewModelsTests
{
    [Theory]
    [InlineData(0.0, SystemHealthState.Normal)]
    [InlineData(55.5, SystemHealthState.Normal)]
    [InlineData(69.9, SystemHealthState.Normal)]
    [InlineData(70.0, SystemHealthState.Warning)]
    [InlineData(84.9, SystemHealthState.Warning)]
    [InlineData(90.0, SystemHealthState.Critical)]
    [InlineData(100.0, SystemHealthState.Critical)]
    public void SystemStatePalette_EvaluateByPercentage_ReturnsExpectedState(
        double percentage,
        SystemHealthState expected)
    {
        var state = SystemStatePalette.EvaluateByPercentage(percentage);
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData(null, SystemHealthState.Normal)]
    [InlineData(48.0, SystemHealthState.Normal)]
    [InlineData(70.0, SystemHealthState.Warning)]
    [InlineData(82.5, SystemHealthState.Warning)]
    [InlineData(85.0, SystemHealthState.Critical)]
    [InlineData(95.0, SystemHealthState.Critical)]
    public void SystemStatePalette_EvaluateByTemperature_ReturnsExpectedState(
        double? temperature,
        SystemHealthState expected)
    {
        var state = SystemStatePalette.EvaluateByTemperature(temperature);
        Assert.Equal(expected, state);
    }

    [Fact]
    public void SystemStatePalette_ColorCodesAndDisplayNames_AreFixedCorrectly()
    {
        Assert.Equal("#10B981", SystemStatePalette.GetColorHex(SystemHealthState.Normal));
        Assert.Equal("#F59E0B", SystemStatePalette.GetColorHex(SystemHealthState.Warning));
        Assert.Equal("#EF4444", SystemStatePalette.GetColorHex(SystemHealthState.Critical));

        Assert.Equal("Нормальний", SystemStatePalette.GetDisplayName(SystemHealthState.Normal));
        Assert.Equal("Попередження", SystemStatePalette.GetDisplayName(SystemHealthState.Warning));
        Assert.Equal("Критичний", SystemStatePalette.GetDisplayName(SystemHealthState.Critical));

        Assert.NotNull(SystemStatePalette.GetBrush(SystemHealthState.Normal));
        Assert.NotNull(SystemStatePalette.GetBrush(SystemHealthState.Warning));
        Assert.NotNull(SystemStatePalette.GetBrush(SystemHealthState.Critical));
    }

    [Fact]
    public void MainViewModel_SwitchesBetweenTabs_UsingNavigationCommands()
    {
        using var provider = new MockTelemetryProvider(seed: 10);
        var overviewVm = new OverviewViewModel(provider);
        var processesVm = new ProcessesViewModel(new StubProcessManagerService());
        var settingsVm = new SettingsViewModel();
        var mainVm = new MainViewModel(overviewVm, processesVm, settingsVm);

        // Початковий стан — вкладка «Огляд системи»
        Assert.Same(overviewVm, mainVm.CurrentViewModel);
        Assert.True(mainVm.IsOverviewSelected);
        Assert.False(mainVm.IsProcessesSelected);
        Assert.False(mainVm.IsSettingsSelected);
        Assert.Equal("Огляд системи", mainVm.CurrentSectionTitle);

        // Перехід на вкладку «Процеси»
        mainVm.NavigateToProcessesCommand.Execute(null);
        Assert.Same(processesVm, mainVm.CurrentViewModel);
        Assert.False(mainVm.IsOverviewSelected);
        Assert.True(mainVm.IsProcessesSelected);
        Assert.False(mainVm.IsSettingsSelected);
        Assert.Equal("Активні процеси", mainVm.CurrentSectionTitle);

        // Перехід на вкладку «Налаштування»
        mainVm.NavigateToSettingsCommand.Execute(null);
        Assert.Same(settingsVm, mainVm.CurrentViewModel);
        Assert.False(mainVm.IsOverviewSelected);
        Assert.False(mainVm.IsProcessesSelected);
        Assert.True(mainVm.IsSettingsSelected);
        Assert.Equal("Налаштування", mainVm.CurrentSectionTitle);

        // Перехід на вкладку «Журнал інцидентів»
        mainVm.NavigateToEventsLogCommand.Execute(null);
        Assert.Same(mainVm.EventsLog, mainVm.CurrentViewModel);
        Assert.False(mainVm.IsOverviewSelected);
        Assert.False(mainVm.IsProcessesSelected);
        Assert.False(mainVm.IsSettingsSelected);
        Assert.True(mainVm.IsEventsLogSelected);
        Assert.Equal("Журнал інцидентів", mainVm.CurrentSectionTitle);

        // Повернення на вкладку «Огляд системи»
        mainVm.NavigateToOverviewCommand.Execute(null);
        Assert.Same(overviewVm, mainVm.CurrentViewModel);
        Assert.True(mainVm.IsOverviewSelected);
        Assert.False(mainVm.IsEventsLogSelected);
    }

    [Fact]
    public void OverviewViewModel_ApplySnapshot_UpdatesAllSummaryCardsAndHealthStates()
    {
        using var provider = new MockTelemetryProvider(seed: 7);
        var vm = new OverviewViewModel(provider);

        var criticalSnapshot = new SystemTelemetrySnapshot
        {
            OsDescription = "Linux 6.12",
            Architecture = "X64",
            MachineName = "TEST-HOST",
            Uptime = TimeSpan.FromHours(5),
            Cpu = new CpuMetrics(92.4, new double[] { 90, 95, 92, 93 }, temperatureCelsius: 88.0, fanSpeedRpm: 2400),
            Ram = new RamMetrics(
                totalBytes: 16L * 1024 * 1024 * 1024,
                usedBytes: 12L * 1024 * 1024 * 1024,
                freeBytes: 4L * 1024 * 1024 * 1024,
                usedPercentage: 75.0),
            Disk = new DiskMetrics(new[]
            {
                new DriveItemMetrics
                {
                    Name = "/",
                    DriveFormat = "ext4",
                    TotalBytes = 100L * 1024 * 1024 * 1024,
                    AvailableFreeBytes = 60L * 1024 * 1024 * 1024,
                    TotalFreeBytes = 60L * 1024 * 1024 * 1024,
                    IsReady = true
                }
            }),
            Network = new NetworkMetrics(new[]
            {
                new NetworkInterfaceMetrics
                {
                    Id = "eth0",
                    Name = "eth0",
                    InterfaceType = "Ethernet",
                    OperationalStatus = "Up",
                    BytesReceived = 2L * 1024 * 1024 * 1024,
                    BytesSent = 1L * 1024 * 1024 * 1024,
                    RxSpeedBytesPerSecond = 512 * 1024,
                    TxSpeedBytesPerSecond = 256 * 1024
                }
            })
        };

        vm.ApplySnapshot(criticalSnapshot);

        Assert.Equal("Linux 6.12", vm.OsDescription);
        Assert.Equal("X64", vm.Architecture);
        Assert.Equal("TEST-HOST", vm.MachineName);
        Assert.Equal(4, vm.CpuCoreCount);
        Assert.Equal(SystemHealthState.Critical, vm.CpuState);
        Assert.Equal("Критичний", vm.CpuStateLabel);

        Assert.Equal(SystemHealthState.Warning, vm.RamState);
        Assert.Equal("Попередження", vm.RamStateLabel);

        Assert.Equal(SystemHealthState.Normal, vm.DiskState);
        Assert.Single(vm.Drives);
        Assert.Single(vm.NetworkInterfaces);
    }

    [Fact]
    public void ProcessesViewModel_FiltersAndTerminatesProcessesCorrectly()
    {
        var stubService = new StubProcessManagerService();
        var vm = new ProcessesViewModel(stubService);

        Assert.Equal(3, vm.Processes.Count);
        Assert.Equal(3, vm.TotalProcessesCount);

        // Пошук за назвою
        vm.SearchQuery = "rider";
        Assert.Single(vm.Processes);
        Assert.Equal("rider64", vm.Processes[0].Name);

        // Скидання фільтра
        vm.SearchQuery = string.Empty;
        Assert.Equal(3, vm.Processes.Count);

        // Завершення обраного процесу
        vm.SelectedProcess = vm.Processes[0];
        vm.KillSelectedProcessCommand.Execute(null);
        Assert.Equal(2, vm.Processes.Count);
    }

    [Fact]
    public void SettingsViewModel_ClampsPollingIntervalAndSwitchesThemes()
    {
        var vm = new SettingsViewModel();

        Assert.Equal(SettingsViewModel.DefaultPollingIntervalMs, vm.PollingIntervalMs);
        Assert.Equal("Системна", vm.SelectedTheme);

        // Перевірка обмеження мінімального та максимального інтервалу
        vm.PollingIntervalMs = 50;
        Assert.Equal(SettingsViewModel.MinPollingIntervalMs, vm.PollingIntervalMs);

        vm.PollingIntervalMs = 25000;
        Assert.Equal(SettingsViewModel.MaxPollingIntervalMs, vm.PollingIntervalMs);

        // Перемикання тем
        vm.SetDarkThemeCommand.Execute(null);
        Assert.Equal("Темна", vm.SelectedTheme);
        Assert.True(vm.IsDarkThemeEnabled);

        vm.SetLightThemeCommand.Execute(null);
        Assert.Equal("Світла", vm.SelectedTheme);
        Assert.False(vm.IsDarkThemeEnabled);

        // Скидання за замовчуванням
        vm.ResetDefaultsCommand.Execute(null);
        Assert.Equal(SettingsViewModel.DefaultPollingIntervalMs, vm.PollingIntervalMs);
        Assert.Equal("Системна", vm.SelectedTheme);
    }

    [Fact]
    public void Views_AreInstantiable_ViaViewLocatorForEachTabViewModel()
    {
        var locator = new ViewLocator();
        using var provider = new MockTelemetryProvider(seed: 1);

        var overviewVm = new OverviewViewModel(provider);
        var processesVm = new ProcessesViewModel(new StubProcessManagerService());
        var settingsVm = new SettingsViewModel();

        Assert.True(locator.Match(overviewVm));
        Assert.True(locator.Match(processesVm));
        Assert.True(locator.Match(settingsVm));
    }

    private sealed class StubProcessManagerService : IProcessManagerService
    {
        private readonly List<ProcessItem> _items = new()
        {
            new ProcessItem(101, "rider64", 1024L * 1024 * 1024, 64, "Виконується"),
            new ProcessItem(202, "dotnet", 256L * 1024 * 1024, 24, "Виконується"),
            new ProcessItem(303, "chrome", 512L * 1024 * 1024, 40, "Виконується")
        };

        public IReadOnlyList<ProcessItem> GetRunningProcesses(int limit = 50) => _items.ToArray();

        public Task<IReadOnlyList<ProcessItem>> GetRunningProcessesAsync(
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProcessItem>>(_items.ToArray());

        public bool KillProcess(int pid)
        {
            return _items.RemoveAll(p => p.Pid == pid) > 0;
        }

        public Task<bool> KillProcessAsync(int pid, CancellationToken cancellationToken = default) =>
            Task.FromResult(KillProcess(pid));
    }
}
