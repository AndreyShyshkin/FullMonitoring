using System;
using System.IO;
using FullMonitoring.Providers;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Тести читача апаратних датчиків hwmon на тимчасовій файловій структурі.
/// </summary>
public class LinuxHardwareReaderTests : IDisposable
{
    private readonly string _root;

    public LinuxHardwareReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fm-hwmon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception)
        {
            // Тимчасовий каталог може бути вже видалений — не впливає на результат тестів.
        }
    }

    [Fact]
    public void ReadTemperatureSensors_FindsSensorsRecursively()
    {
        Write("hwmon0", "name", "coretemp");
        Write("hwmon0", "temp1_input", "45000\n");
        Write("hwmon0", "temp2_input", "42000\n");
        Write("hwmon1", "name", "nvme");
        Write("hwmon1", "temp1_input", "38000\n");

        var reader = new LinuxHardwareReader(_root);
        var sensors = reader.ReadTemperatureSensors();

        Assert.Equal(3, sensors.Count);
        Assert.Contains(sensors, s => s.ChipName == "coretemp" && Math.Abs(s.Celsius - 45.0) < 0.001);
        Assert.Contains(sensors, s => s.ChipName == "nvme" && Math.Abs(s.Celsius - 38.0) < 0.001);
    }

    [Fact]
    public void ReadCpuTemperature_PrefersKnownCpuChip()
    {
        Write("hwmon0", "name", "nvme");
        Write("hwmon0", "temp1_input", "38000\n");
        Write("hwmon1", "name", "k10temp");
        Write("hwmon1", "temp1_input", "55000\n");

        var reader = new LinuxHardwareReader(_root);

        Assert.Equal(55.0, reader.ReadCpuTemperature());
    }

    [Fact]
    public void ReadFanSpeed_ReturnsFirstPositiveValue()
    {
        Write("hwmon0", "name", "it87");
        Write("hwmon0", "fan1_input", "0\n");
        Write("hwmon0", "fan2_input", "1800\n");

        var reader = new LinuxHardwareReader(_root);

        Assert.Equal(1800.0, reader.ReadFanSpeed());
    }

    [Fact]
    public void MissingHwmonRoot_DoesNotThrow()
    {
        var reader = new LinuxHardwareReader(Path.Combine(_root, "absent"));

        Assert.False(reader.IsAvailable);
        Assert.Empty(reader.ReadTemperatureSensors());
        Assert.Null(reader.ReadCpuTemperature());
        Assert.Null(reader.ReadFanSpeed());
    }

    private void Write(string chipDirectory, string fileName, string content)
    {
        string directory = Path.Combine(_root, chipDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }
}
