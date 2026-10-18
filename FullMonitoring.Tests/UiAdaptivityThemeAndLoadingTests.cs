using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Styling;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.ViewModels;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для фінальної адаптивності, станів первинного завантаження (Skeleton/Spinner),
/// візуальних підказок обмеження прав ОС («Заблоковано політикою безпеки ОС»), динамічного перемикання теми (Dark/Light),
/// перевірки контрастності WCAG 2.1 та ненав'язливої обробки помилок провайдерів через InfoBar (Issue #11).
/// </summary>
public class UiAdaptivityThemeAndLoadingTests
{
    [Fact]
    public void OverviewViewModel_DeferredInitialCollection_KeepsIsInitialLoadingTrueUntilFirstSnapshot()
    {
        using var provider = new MockTelemetryProvider(seed: 21);
        using var vm = new OverviewViewModel(provider, autoStartPolling: false, deferInitialCollection: true);

        // Поки триває перший збір метрик при старті програми — активний індикатор завантаження (Skeleton / Spinner)
        Assert.True(vm.IsInitialLoading);
        Assert.False(vm.IsFirstSnapshotLoaded);

        // Після завершення першого збору метрик індикатор завантаження вимикається
        vm.RefreshMetrics();
        Assert.False(vm.IsInitialLoading);
        Assert.True(vm.IsFirstSnapshotLoaded);
    }

    [Fact]
    public async Task OverviewViewModel_RefreshMetricsAsync_CompletesInitialLoadingState()
    {
        using var provider = new MockTelemetryProvider(seed: 33);
        using var vm = new OverviewViewModel(provider, autoStartPolling: false, deferInitialCollection: true);

        Assert.True(vm.IsInitialLoading);

        await vm.RefreshMetricsAsync();

        Assert.False(vm.IsInitialLoading);
        Assert.True(vm.IsFirstSnapshotLoaded);
    }

    [Fact]
    public void OverviewViewModel_WhenHardwareSensorsAreNull_SetsOsSecurityRestrictionTooltips()
    {
        using var bclProvider = new BclTelemetryProvider();
        using var vm = new OverviewViewModel(bclProvider, autoStartPolling: false);

        // У базовому BCL-провайдері прямий доступ до термодатчиків та кулера відсутній (null)
        Assert.True(vm.IsCpuTemperatureRestricted);
        Assert.True(vm.IsCpuFanSpeedRestricted);
        Assert.True(vm.HasRestrictedMetrics);
        Assert.Equal("Заблоковано політикою безпеки ОС", vm.OsSecurityRestrictionText);
        Assert.Equal("Заблоковано політикою безпеки ОС", vm.CpuTemperatureRestrictionTooltip);
        Assert.Equal("Заблоковано політикою безпеки ОС", vm.CpuFanSpeedRestrictionTooltip);

        // Якщо надходить знімок із доступними датчиками — прапорці обмеження скидаються
        var fullSnapshot = new SystemTelemetrySnapshot
        {
            Cpu = new CpuMetrics(45.0, new[] { 45.0 }, temperatureCelsius: 58.0, fanSpeedRpm: 1350)
        };
        vm.ApplySnapshot(fullSnapshot);

        Assert.False(vm.IsCpuTemperatureRestricted);
        Assert.False(vm.IsCpuFanSpeedRestricted);
        Assert.False(vm.HasRestrictedMetrics);
    }

    [Fact]
    public void SettingsViewModel_DynamicThemeSwitching_UpdatesThemeVariantAndSyncsWithChartAxes()
    {
        using var provider = new MockTelemetryProvider(seed: 12);
        using var overviewVm = new OverviewViewModel(provider, autoStartPolling: false);
        var processesVm = new ProcessesViewModel(new StubProcessService());
        var settingsVm = new SettingsViewModel();
        _ = new MainViewModel(overviewVm, processesVm, settingsVm);

        var initialLightLabelsPaint = overviewVm.CpuXAxes[0].LabelsPaint;

        // Перемикання на Темну тему
        settingsVm.SetDarkThemeCommand.Execute(null);
        Assert.Equal("Темна", settingsVm.SelectedTheme);
        Assert.True(settingsVm.IsDarkThemeEnabled);
        Assert.Equal(ThemeVariant.Dark, settingsVm.CurrentThemeVariant);

        var darkLabelsPaint = overviewVm.CpuXAxes[0].LabelsPaint;
        Assert.NotSame(initialLightLabelsPaint, darkLabelsPaint);

        // Перемикання на Світлу тему
        settingsVm.SetLightThemeCommand.Execute(null);
        Assert.Equal("Світла", settingsVm.SelectedTheme);
        Assert.False(settingsVm.IsDarkThemeEnabled);
        Assert.Equal(ThemeVariant.Light, settingsVm.CurrentThemeVariant);
        Assert.Same(initialLightLabelsPaint, overviewVm.CpuXAxes[0].LabelsPaint);

        // Швидке перемикання ToggleThemeCommand
        settingsVm.ToggleThemeCommand.Execute(null);
        Assert.Equal("Темна", settingsVm.SelectedTheme);
        Assert.Equal(ThemeVariant.Dark, settingsVm.CurrentThemeVariant);

        // Повернення до Системної теми
        settingsVm.SetSystemThemeCommand.Execute(null);
        Assert.Equal("Системна", settingsVm.SelectedTheme);
        Assert.Equal(ThemeVariant.Default, settingsVm.CurrentThemeVariant);
    }

    [Fact]
    public void SystemStatePalette_ContrastRatios_MeetWcagAccessibilityStandardsInDarkAndLightThemes()
    {
        // Перевірка контрастності основного та вторинного тексту у Темній темі (>= 4.5:1)
        Assert.True(SystemStatePalette.MeetsWcagTextContrast(
            SystemStatePalette.DarkPrimaryTextHex,
            SystemStatePalette.DarkSurfaceHex));
        Assert.True(SystemStatePalette.MeetsWcagTextContrast(
            SystemStatePalette.DarkSecondaryTextHex,
            SystemStatePalette.DarkCardHex));

        // Перевірка контрастності основного та вторинного тексту у Світлій темі (>= 4.5:1)
        Assert.True(SystemStatePalette.MeetsWcagTextContrast(
            SystemStatePalette.LightPrimaryTextHex,
            SystemStatePalette.LightSurfaceHex));
        Assert.True(SystemStatePalette.MeetsWcagTextContrast(
            SystemStatePalette.LightSecondaryTextHex,
            SystemStatePalette.LightCardHex));

        // Перевірка контрастності кольорів графіків та індикаторів станів у Темній темі (>= 3.0:1 та >= 4.5:1)
        Assert.True(SystemStatePalette.MeetsWcagGraphicalContrast(
            SystemStatePalette.NormalHex,
            SystemStatePalette.DarkCardHex));
        Assert.True(SystemStatePalette.MeetsWcagGraphicalContrast(
            SystemStatePalette.WarningHex,
            SystemStatePalette.DarkCardHex));
        Assert.True(SystemStatePalette.MeetsWcagGraphicalContrast(
            SystemStatePalette.CriticalHex,
            SystemStatePalette.DarkCardHex));

        // Перевірка адаптованих висококонтрастних кольорів станів у Світлій темі (>= 4.5:1)
        foreach (var state in new[] { SystemHealthState.Normal, SystemHealthState.Warning, SystemHealthState.Critical })
        {
            string lightHex = SystemStatePalette.GetAccessibleTextColorHex(state, isDarkTheme: false);
            string darkHex = SystemStatePalette.GetAccessibleTextColorHex(state, isDarkTheme: true);

            Assert.True(SystemStatePalette.MeetsWcagTextContrast(lightHex, SystemStatePalette.LightCardHex));
            Assert.True(SystemStatePalette.MeetsWcagTextContrast(darkHex, SystemStatePalette.DarkSurfaceHex));
        }
    }

    [Fact]
    public void OverviewViewModel_WhenProviderThrowsException_DisplaysInfoBarInsteadOfCrashingWindow()
    {
        using var faultyProvider = new FaultyTelemetryProvider("Відсутній доступ до шини сенсорів PCI");
        using var vm = new OverviewViewModel(faultyProvider, autoStartPolling: false);

        // Вікно не падає: помилка перехоплена та виведена у властивості InfoBar
        Assert.False(vm.IsInitialLoading);
        Assert.True(vm.HasProviderError);
        Assert.Equal("Помилка провайдера телеметрії", vm.ProviderErrorTitle);
        Assert.Contains("Відсутній доступ до шини сенсорів PCI", vm.ProviderErrorMessage);

        // Користувач може закрити ненав'язливе повідомлення InfoBar
        vm.DismissProviderErrorCommand.Execute(null);
        Assert.False(vm.HasProviderError);
        Assert.Equal(string.Empty, vm.ProviderErrorMessage);
    }

    [Fact]
    public void ProcessesViewModel_HandlesOsRestrictionAndProviderErrorsWithoutCrashing()
    {
        var restrictedService = new StubProcessService(allowKill: false);
        var vm = new ProcessesViewModel(restrictedService);

        Assert.False(vm.IsInitialLoading);
        Assert.Single(vm.Processes);

        // Спроба завершити захищений системний процес встановлює прапорець обмеження політики безпеки ОС
        vm.SelectedProcess = vm.Processes[0];
        vm.KillSelectedProcessCommand.Execute(null);

        Assert.True(vm.IsLastActionRestrictedByOs);
        Assert.Contains("Заблоковано політикою безпеки ОС", vm.StatusMessage);

        // Перевірка перехоплення винятку сервісу процесів через InfoBar без падіння вікна
        var throwingService = new StubProcessService(throwOnRefresh: true);
        var errorVm = new ProcessesViewModel(throwingService);

        Assert.True(errorVm.HasProviderError);
        Assert.Contains("Відмовлено у доступі до таблиці процесів", errorVm.ProviderErrorMessage);

        errorVm.DismissProviderErrorCommand.Execute(null);
        Assert.False(errorVm.HasProviderError);
    }

    private sealed class FaultyTelemetryProvider : ITelemetryProvider
    {
        private readonly string _errorMessage;

        public FaultyTelemetryProvider(string errorMessage)
        {
            _errorMessage = errorMessage;
        }

        public string ProviderName => "Тестовий аварійний провайдер";
        public bool IsSupported => true;

        public SystemTelemetrySnapshot GetSnapshot() =>
            throw new InvalidOperationException(_errorMessage);

        public Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<SystemTelemetrySnapshot>(new InvalidOperationException(_errorMessage));

        public CpuMetrics GetCpuMetrics() => throw new InvalidOperationException(_errorMessage);
        public Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<CpuMetrics>(new InvalidOperationException(_errorMessage));

        public RamMetrics GetRamMetrics() => throw new InvalidOperationException(_errorMessage);
        public Task<RamMetrics> GetRamMetricsAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<RamMetrics>(new InvalidOperationException(_errorMessage));

        public DiskMetrics GetDiskMetrics() => throw new InvalidOperationException(_errorMessage);
        public Task<DiskMetrics> GetDiskMetricsAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<DiskMetrics>(new InvalidOperationException(_errorMessage));

        public NetworkMetrics GetNetworkMetrics() => throw new InvalidOperationException(_errorMessage);
        public Task<NetworkMetrics> GetNetworkMetricsAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<NetworkMetrics>(new InvalidOperationException(_errorMessage));

        public void Dispose()
        {
        }
    }

    private sealed class StubProcessService : IProcessManagerService
    {
        private readonly bool _allowKill;
        private readonly bool _throwOnRefresh;

        public StubProcessService(bool allowKill = true, bool throwOnRefresh = false)
        {
            _allowKill = allowKill;
            _throwOnRefresh = throwOnRefresh;
        }

        public IReadOnlyList<ProcessItem> GetRunningProcesses(int limit = 50)
        {
            if (_throwOnRefresh)
            {
                throw new UnauthorizedAccessException("Відмовлено у доступі до таблиці процесів");
            }

            return new[]
            {
                new ProcessItem(4, "System", 128L * 1024 * 1024, 120, "Системний")
            };
        }

        public Task<IReadOnlyList<ProcessItem>> GetRunningProcessesAsync(
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetRunningProcesses(limit));

        public bool KillProcess(int pid) => _allowKill;

        public Task<bool> KillProcessAsync(int pid, CancellationToken cancellationToken = default) =>
            Task.FromResult(_allowKill);
    }
}
