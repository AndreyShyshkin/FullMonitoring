using System;
using System.Runtime.InteropServices;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.Services;
using FullMonitoring.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести механізму платформої резолюції (DI) та macOS fallback-провайдера (Issue #9).
/// </summary>
public class PlatformDiAndMacOsTests
{
    // --- MacOsSysctlReader: безпечний fallback без винятків ---

    [Fact]
    public void MacOsSysctlReader_SafeFallback_DoesNotThrowOnAnyOs()
    {
        // Виклик не повинен кидати виняток на жодній ОС (Windows / Linux / macOS).
        var ex = Record.Exception(() =>
        {
            bool u64 = MacOsSysctlReader.TryGetUInt64("hw.memsize", out ulong _);
            bool i32 = MacOsSysctlReader.TryGetInt32("hw.logicalcpu", out int _);
            bool str = MacOsSysctlReader.TryGetString("machdep.cpu.brand_string", out string _);
            bool mem = MacOsSysctlReader.TryGetTotalMemoryBytes(out long _);
            bool cpu = MacOsSysctlReader.TryGetLogicalCpuCount(out int _);
        });

        Assert.Null(ex);
    }

    [Fact]
    public void MacOsSysctlReader_InvalidKey_ReturnsFalseWithoutThrowing()
    {
        Assert.False(MacOsSysctlReader.TryGetUInt64(string.Empty, out _));
        Assert.False(MacOsSysctlReader.TryGetUInt64("неіснуючий.ключ.sysctl", out _));
        Assert.False(MacOsSysctlReader.TryGetInt32(string.Empty, out _));
        Assert.False(MacOsSysctlReader.TryGetString(string.Empty, out _));
        Assert.False(MacOsSysctlReader.TryGetString("  ", out _));
    }

    [Fact]
    public void MacOsSysctlReader_IsAvailable_MatchesCurrentOs()
    {
        bool expected = OperatingSystem.IsMacOS();
        Assert.Equal(expected, MacOsSysctlReader.IsAvailable);
    }

    // --- MacOsTelemetryProvider: чесний fallback із BCL-метриками ---

    [Fact]
    public void MacOsTelemetryProvider_HasExpectedProviderName()
    {
        using var provider = new MacOsTelemetryProvider();
        Assert.Equal("MacOS Telemetry Provider (sysctl / BCL Fallback)", provider.ProviderName);
    }

    [Fact]
    public void MacOsTelemetryProvider_IsSupported_MatchesCurrentOs()
    {
        using var provider = new MacOsTelemetryProvider();
        Assert.Equal(OperatingSystem.IsMacOS(), provider.IsSupported);
    }

    [Fact]
    public void MacOsTelemetryProvider_GetCpuMetrics_ReturnsValidRangeWithRestrictedSensors()
    {
        using var provider = new MacOsTelemetryProvider();

        var cpu = provider.GetCpuMetrics();

        Assert.NotNull(cpu);
        Assert.InRange(cpu.TotalUsagePercentage, 0.0, 100.0);
        // На macOS температура та кулер обмежені політикою ОС (SMC) — чесний null.
        Assert.Null(cpu.TemperatureCelsius);
        Assert.Null(cpu.FanSpeedRpm);
        Assert.True(cpu.CoreUsagesPercentage.Count >= 1);
        foreach (double core in cpu.CoreUsagesPercentage)
        {
            Assert.InRange(core, 0.0, 100.0);
        }
    }

    [Fact]
    public void MacOsTelemetryProvider_GetRamMetrics_ReturnsPositiveTotal()
    {
        using var provider = new MacOsTelemetryProvider();

        var ram = provider.GetRamMetrics();

        Assert.NotNull(ram);
        Assert.True(ram.TotalBytes > 0, "Загальний обсяг RAM має бути більшим за нуль");
        Assert.True(ram.UsedBytes >= 0);
        Assert.True(ram.FreeBytes >= 0);
        Assert.InRange(ram.UsedPercentage, 0.0, 100.0);
    }

    [Fact]
    public void MacOsTelemetryProvider_GetSnapshot_DoesNotThrowAndContainsOsInfo()
    {
        using var provider = new MacOsTelemetryProvider();

        var snapshot = provider.GetSnapshot();

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Cpu);
        Assert.NotNull(snapshot.Ram);
        Assert.NotNull(snapshot.Disk);
        Assert.NotNull(snapshot.Network);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.OsDescription));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Architecture));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.MachineName));
    }

    [Fact]
    public void MacOsTelemetryProvider_LogicalCpuCount_IsAtLeastOne()
    {
        using var provider = new MacOsTelemetryProvider();
        Assert.True(provider.LogicalCpuCount >= 1);
    }

    [Fact]
    public void MacOsTelemetryProvider_CpuBrand_DoesNotThrow()
    {
        using var provider = new MacOsTelemetryProvider();
        string brand = provider.CpuBrand;
        Assert.NotNull(brand);
    }

    // --- Платформова резолюція: чиста функція мапінгу ---

    [Theory]
    [InlineData(PlatformKind.Windows, typeof(WindowsTelemetryProvider))]
    [InlineData(PlatformKind.Linux, typeof(LinuxTelemetryProvider))]
    [InlineData(PlatformKind.MacOs, typeof(MacOsTelemetryProvider))]
    [InlineData(PlatformKind.Unknown, typeof(BclTelemetryProvider))]
    public void ResolveTelemetryProviderType_MapsPlatformToExpectedProvider(
        PlatformKind platform, Type expectedType)
    {
        Type actual = TelemetryServiceRegistration.ResolveTelemetryProviderType(platform);
        Assert.Equal(expectedType, actual);
    }

    [Fact]
    public void ResolveTelemetryProviderTypeForCurrentOs_MatchesRuntimeInformation()
    {
        Type actual = TelemetryServiceRegistration.ResolveTelemetryProviderTypeForCurrentOs();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(typeof(WindowsTelemetryProvider), actual);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Equal(typeof(LinuxTelemetryProvider), actual);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Equal(typeof(MacOsTelemetryProvider), actual);
        }
        else
        {
            Assert.Equal(typeof(BclTelemetryProvider), actual);
        }
    }

    [Fact]
    public void CreateTelemetryProviderForCurrentOs_ReturnsWorkingProviderWithoutThrowing()
    {
        using ITelemetryProvider provider = TelemetryServiceRegistration.CreateTelemetryProviderForCurrentOs();

        Assert.NotNull(provider);
        Assert.False(string.IsNullOrWhiteSpace(provider.ProviderName));

        // Провайдер для поточної ОС повинен віддавати валідний знімок без затримок.
        var snapshot = provider.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.InRange(snapshot.Cpu.TotalUsagePercentage, 0.0, 100.0);
    }

    // --- PlatformInfoService: платформа та Safe Mode ---

    [Fact]
    public void PlatformInfoService_DetectPlatform_MatchesRuntimeInformation()
    {
        PlatformKind detected = PlatformInfoService.DetectPlatform();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(PlatformKind.Windows, detected);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Equal(PlatformKind.Linux, detected);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Equal(PlatformKind.MacOs, detected);
        }
        else
        {
            Assert.Equal(PlatformKind.Unknown, detected);
        }
    }

    [Theory]
    [InlineData(PlatformKind.Windows, "Windows")]
    [InlineData(PlatformKind.Linux, "Linux")]
    [InlineData(PlatformKind.MacOs, "macOS")]
    [InlineData(PlatformKind.Unknown, "Невідома платформа")]
    public void PlatformInfoService_GetPlatformDisplayName_ReturnsUkrainianName(
        PlatformKind platform, string expected)
    {
        Assert.Equal(expected, PlatformInfoService.GetPlatformDisplayName(platform));
    }

    [Fact]
    public void PlatformInfoService_ExposesNonEmptyPlatformDetails()
    {
        var service = new PlatformInfoService();

        Assert.False(string.IsNullOrWhiteSpace(service.PlatformDisplayName));
        Assert.False(string.IsNullOrWhiteSpace(service.OsDescription));
        Assert.False(string.IsNullOrWhiteSpace(service.Architecture));
        Assert.False(string.IsNullOrWhiteSpace(service.SafeModeDisplayName));
        Assert.False(string.IsNullOrWhiteSpace(service.GetStatusBarText()));
        Assert.Contains("Платформа:", service.GetStatusBarText());
    }

    [Fact]
    public void PlatformInfoService_IsAdministrator_IsFalseOnNonWindows()
    {
        var service = new PlatformInfoService();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.False(service.IsAdministrator);
        }
    }

    [Theory]
    [InlineData(SensorAccessLevel.Full, "Сенсори: повний доступ")]
    [InlineData(SensorAccessLevel.Limited, "Сенсори: частковий доступ")]
    [InlineData(SensorAccessLevel.Restricted, "Сенсори: обмежено політикою ОС")]
    public void PlatformInfoService_GetSensorAccessDisplayName_ReturnsUkrainianText(
        SensorAccessLevel level, string expected)
    {
        var service = new PlatformInfoService();
        Assert.Equal(expected, service.GetSensorAccessDisplayName(level));
    }

    [Fact]
    public void PlatformInfoService_EvaluateSensorAccess_ClassifiesCorrectly()
    {
        var service = new PlatformInfoService();

        // Відсутній знімок — обмежений доступ.
        Assert.Equal(SensorAccessLevel.Restricted, service.EvaluateSensorAccess(null));

        // Обидва сенсори наявні — повний доступ.
        var full = new CpuMetrics(25.0, new double[] { 20, 30 }, 55.0, 1500);
        Assert.Equal(SensorAccessLevel.Full, service.EvaluateSensorAccess(full));

        // Лише температура — частковий доступ.
        var tempOnly = new CpuMetrics(25.0, new double[] { 20, 30 }, 55.0, null);
        Assert.Equal(SensorAccessLevel.Limited, service.EvaluateSensorAccess(tempOnly));

        // Лише кулер — частковий доступ.
        var fanOnly = new CpuMetrics(25.0, new double[] { 20, 30 }, null, 1500);
        Assert.Equal(SensorAccessLevel.Limited, service.EvaluateSensorAccess(fanOnly));

        // Жодного сенсора — обмежений доступ (Safe Mode).
        var none = new CpuMetrics(25.0, new double[] { 20, 30 }, null, null);
        Assert.Equal(SensorAccessLevel.Restricted, service.EvaluateSensorAccess(none));
    }

    // --- DI-контейнер: резолюція залежностей ---

    [Fact]
    public void AddFullMonitoringPlatformServices_ResolvesTelemetryProviderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddFullMonitoringPlatformServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<ITelemetryProvider>();
        var second = provider.GetRequiredService<ITelemetryProvider>();

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first.ProviderName));
    }

    [Fact]
    public void AddFullMonitoringPlatformServices_ResolvesAllViewModelsAndMainViewModel()
    {
        var services = new ServiceCollection();
        services.AddFullMonitoringPlatformServices();
        using var provider = services.BuildServiceProvider();

        var platformInfo = provider.GetRequiredService<IPlatformInfoService>();
        var processService = provider.GetRequiredService<IProcessManagerService>();
        var alertService = provider.GetRequiredService<IAlertService>();
        var mainViewModel = provider.GetRequiredService<MainViewModel>();

        Assert.NotNull(platformInfo);
        Assert.NotNull(processService);
        Assert.NotNull(alertService);
        Assert.NotNull(mainViewModel);
        Assert.NotNull(mainViewModel.Overview);
        Assert.NotNull(mainViewModel.Processes);
        Assert.NotNull(mainViewModel.Settings);
        Assert.NotNull(mainViewModel.EventsLog);
        Assert.Same(platformInfo, mainViewModel.PlatformInfo);
    }

    // --- MainViewModel: статус-бар платформи ---

    [Fact]
    public void MainViewModel_ExposesPlatformStatusBarOnConstruction()
    {
        using var telemetry = new MockTelemetryProvider(seed: 42);
        var overview = new OverviewViewModel(telemetry, autoStartPolling: false);
        var main = new MainViewModel(
            overview,
            new ProcessesViewModel(new StubProcessService()),
            new SettingsViewModel(),
            new EventsLogViewModel(),
            new PlatformInfoService(),
            telemetry);

        Assert.False(string.IsNullOrWhiteSpace(main.PlatformDisplayName));
        Assert.False(string.IsNullOrWhiteSpace(main.PlatformDetailsText));
        Assert.False(string.IsNullOrWhiteSpace(main.ProviderDisplayName));
        Assert.False(string.IsNullOrWhiteSpace(main.SensorAccessText));
        Assert.False(string.IsNullOrWhiteSpace(main.SafeModeText));
        Assert.False(string.IsNullOrWhiteSpace(main.StatusBarText));
        Assert.Contains("Платформа:", main.StatusBarText);
    }

    [Fact]
    public void MainViewModel_RefreshPlatformStatus_ReflectsFullSensorAccess()
    {
        using var telemetry = new MockTelemetryProvider(seed: 5);
        var overview = new OverviewViewModel(telemetry, autoStartPolling: false);
        var main = new MainViewModel(
            overview,
            new ProcessesViewModel(new StubProcessService()),
            new SettingsViewModel(),
            new EventsLogViewModel(),
            new PlatformInfoService(),
            telemetry);

        // Знімок із повним доступом до сенсорів.
        overview.ApplySnapshot(new SystemTelemetrySnapshot
        {
            OsDescription = "Тестова ОС",
            Architecture = "X64",
            MachineName = "ТЕСТ-ВУЗОЛ",
            Cpu = new CpuMetrics(30.0, new double[] { 25, 35 }, 55.0, 1500),
            Ram = new RamMetrics(8L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024),
        });
        main.RefreshPlatformStatus();

        Assert.Equal(SensorAccessLevel.Full, main.SensorAccessLevel);
        Assert.Equal("Сенсори: повний доступ", main.SensorAccessText);
    }

    [Fact]
    public void MainViewModel_RefreshPlatformStatus_ReflectsRestrictedSensors()
    {
        using var telemetry = new MockTelemetryProvider(seed: 6);
        var overview = new OverviewViewModel(telemetry, autoStartPolling: false);
        var main = new MainViewModel(
            overview,
            new ProcessesViewModel(new StubProcessService()),
            new SettingsViewModel(),
            new EventsLogViewModel(),
            new PlatformInfoService(),
            telemetry);

        // Знімок без сенсорів (обмежено політикою безпеки ОС).
        overview.ApplySnapshot(new SystemTelemetrySnapshot
        {
            OsDescription = "Тестова ОС",
            Architecture = "Arm64",
            MachineName = "ТЕСТ-ВУЗОЛ",
            Cpu = new CpuMetrics(30.0, new double[] { 25, 35 }, null, null),
            Ram = new RamMetrics(8L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024),
        });
        main.RefreshPlatformStatus();

        Assert.Equal(SensorAccessLevel.Restricted, main.SensorAccessLevel);
        Assert.Equal("Сенсори: обмежено політикою ОС", main.SensorAccessText);
    }

    private sealed class StubProcessService : IProcessManagerService
    {
        public IReadOnlyList<ProcessItem> GetRunningProcesses(int limit = 50) =>
            Array.Empty<ProcessItem>();

        public System.Threading.Tasks.Task<IReadOnlyList<ProcessItem>> GetRunningProcessesAsync(
            int limit = 50,
            System.Threading.CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult<IReadOnlyList<ProcessItem>>(Array.Empty<ProcessItem>());

        public bool KillProcess(int pid) => false;

        public System.Threading.Tasks.Task<bool> KillProcessAsync(
            int pid,
            System.Threading.CancellationToken cancellationToken = default) =>
            System.Threading.Tasks.Task.FromResult(false);
    }
}
