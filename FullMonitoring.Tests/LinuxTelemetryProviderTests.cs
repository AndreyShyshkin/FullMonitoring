using System;
using System.IO;
using FullMonitoring.Providers;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Тести Linux-провайдера телеметрії на ізольованих каталогах procfs/sysfs.
/// </summary>
public class LinuxTelemetryProviderTests : IDisposable
{
    private readonly string _baseDirectory;
    private readonly string _procRoot;
    private readonly string _sysRoot;

    public LinuxTelemetryProviderTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "fm-linux-" + Guid.NewGuid().ToString("N"));
        _procRoot = Path.Combine(_baseDirectory, "proc");
        _sysRoot = Path.Combine(_baseDirectory, "sys");
        Directory.CreateDirectory(_procRoot);
        Directory.CreateDirectory(_sysRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
        catch (Exception)
        {
            // Тимчасовий каталог може бути вже видалений — не впливає на результат тестів.
        }
    }

    [Fact]
    public void GetCpuMetrics_ComputesUsageFromProcStatDelta()
    {
        WriteProc("stat",
            "cpu  0 0 0 1000 0 0 0 0 0 0\n" +
            "cpu0 0 0 0 500 0 0 0 0 0 0\n" +
            "cpu1 0 0 0 500 0 0 0 0 0 0\n");

        using var provider = new LinuxTelemetryProvider(_procRoot, _sysRoot);

        // Наступний зріз: +80% зайнятості, +20% простою => 80% утилізації.
        WriteProc("stat",
            "cpu  80 0 0 1020 0 0 0 0 0 0\n" +
            "cpu0 40 0 0 510 0 0 0 0 0 0\n" +
            "cpu1 40 0 0 510 0 0 0 0 0 0\n");

        var cpu = provider.GetCpuMetrics();

        Assert.Equal(80.0, cpu.TotalUsagePercentage, 2);
        Assert.Equal(2, cpu.CoreUsagesPercentage.Count);
        Assert.Equal(80.0, cpu.CoreUsagesPercentage[0], 2);
        Assert.Equal(80.0, cpu.CoreUsagesPercentage[1], 2);
    }

    [Fact]
    public void GetRamMetrics_ParsesMemInfo()
    {
        WriteProc("meminfo",
            "MemTotal:       16777216 kB\n" +
            "MemAvailable:    8388608 kB\n");

        using var provider = new LinuxTelemetryProvider(_procRoot, _sysRoot);

        var ram = provider.GetRamMetrics();

        Assert.Equal(16777216L * 1024, ram.TotalBytes);
        Assert.Equal(8388608L * 1024, ram.UsedBytes);
        Assert.Equal(8388608L * 1024, ram.FreeBytes);
        Assert.Equal(50.0, ram.UsedPercentage, 2);
    }

    [Fact]
    public void GetCpuMetrics_ReadsTemperatureAndFanFromHwmon()
    {
        WriteProc("stat", "cpu  0 0 0 100 0 0 0 0 0 0\n");

        string hwmon = Path.Combine(_sysRoot, "class", "hwmon", "hwmon0");
        Directory.CreateDirectory(hwmon);
        File.WriteAllText(Path.Combine(hwmon, "name"), "coretemp");
        File.WriteAllText(Path.Combine(hwmon, "temp1_input"), "47000\n");
        File.WriteAllText(Path.Combine(hwmon, "fan1_input"), "2400\n");

        using var provider = new LinuxTelemetryProvider(_procRoot, _sysRoot);
        var cpu = provider.GetCpuMetrics();

        Assert.Equal(47.0, cpu.TemperatureCelsius);
        Assert.Equal(2400.0, cpu.FanSpeedRpm);
    }

    [Fact]
    public void MissingProcAndSysFiles_DoesNotThrow()
    {
        using var provider = new LinuxTelemetryProvider(_procRoot, _sysRoot);

        var cpu = provider.GetCpuMetrics();
        var ram = provider.GetRamMetrics();
        var snapshot = provider.GetSnapshot();

        Assert.InRange(cpu.TotalUsagePercentage, 0.0, 100.0);
        Assert.NotNull(ram);
        Assert.NotNull(snapshot);
        // Резервний BCL-варіант має повернути додатний загальний обсяг RAM.
        Assert.True(ram.TotalBytes > 0);
    }

    [Fact]
    public void ProviderMetadata_IsConsistent()
    {
        using var provider = new LinuxTelemetryProvider(_procRoot, _sysRoot);

        Assert.Contains("Linux", provider.ProviderName);
        Assert.Equal(OperatingSystem.IsLinux(), provider.IsSupported);
    }

    private void WriteProc(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_procRoot, fileName), content);
    }
}
