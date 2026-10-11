using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace FullMonitoring.Providers;

/// <summary>
/// Датчик температури, знайдений у віртуальній файловій системі sysfs.
/// </summary>
/// <param name="ChipName">Назва мікросхеми з файлу <c>name</c> (наприклад, <c>coretemp</c>, <c>k10temp</c>).</param>
/// <param name="SensorName">Назва конкретного датчика (наприклад, <c>temp1_input</c>).</param>
/// <param name="Celsius">Температура в градусах Цельсія.</param>
internal sealed record LinuxTemperatureSensor(string ChipName, string SensorName, double Celsius);

/// <summary>
/// Читач апаратних датчиків Linux через каталог <c>/sys/class/hwmon/</c>.
/// Забезпечує рекурсивний пошук сенсорів температури та вентиляторів без сторонніх бінарних залежностей.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxHardwareReader
{
    private const string DefaultHwmonRoot = "/sys/class/hwmon";

    /// <summary>
    /// Відомі назви мікросхем, що відповідають за температуру CPU.
    /// </summary>
    private static readonly string[] CpuChipNames =
    {
        "coretemp",   // Intel
        "k10temp",    // AMD
        "zenpower",   // AMD (альтернативний драйвер)
        "cpu_thermal", // ARM / SoC
        "cpu-thermal",
        "soc_thermal",
        "soc-thermal",
        "acpitz"      // ACPI thermal zone (часто єдина на віртуальних машинах)
    };

    private readonly string _hwmonRoot;

    /// <summary>
    /// Ініціалізує новий екземпляр читача.
    /// </summary>
    /// <param name="hwmonRoot">
    /// Кореневий каталог hwmon. За замовчуванням <c>/sys/class/hwmon</c>;
    /// явне значення використовується для ізольованого тестування.
    /// </param>
    public LinuxHardwareReader(string? hwmonRoot = null)
    {
        _hwmonRoot = string.IsNullOrWhiteSpace(hwmonRoot) ? DefaultHwmonRoot : hwmonRoot;
    }

    /// <summary>
    /// Вказує, чи доступний каталог hwmon у поточному середовищі (наприклад, відсутній у контейнерах).
    /// </summary>
    public bool IsAvailable => Directory.Exists(_hwmonRoot);

    /// <summary>
    /// Рекурсивно знаходить усі датчики температури та зчитує їхні поточні показники.
    /// </summary>
    /// <returns>Список знайдених сенсорів; порожній список, якщо каталог відсутній.</returns>
    public IReadOnlyList<LinuxTemperatureSensor> ReadTemperatureSensors()
    {
        var sensors = new List<LinuxTemperatureSensor>();
        if (!Directory.Exists(_hwmonRoot))
        {
            return sensors;
        }

        foreach (var inputFile in EnumerateSensorFiles("temp*_input"))
        {
            try
            {
                if (!LinuxTelemetryParser.TryParseTemperatureCelsius(File.ReadAllText(inputFile), out double celsius))
                {
                    continue;
                }

                string directory = Path.GetDirectoryName(inputFile) ?? _hwmonRoot;
                sensors.Add(new LinuxTemperatureSensor(
                    ChipName: ReadChipName(directory),
                    SensorName: Path.GetFileNameWithoutExtension(inputFile),
                    Celsius: celsius));
            }
            catch (Exception)
            {
                // Окремі датчики можуть зникнути під час читання або вимагати підвищених привілеїв.
            }
        }

        return sensors;
    }

    /// <summary>
    /// Визначає температуру CPU серед знайдених сенсорів, віддаючи перевагу відомим мікросхемам.
    /// </summary>
    /// <returns>Температура в градусах Цельсія або <c>null</c>, якщо відповідний сенсор відсутній.</returns>
    public double? ReadCpuTemperature()
    {
        double? hottest = null;

        foreach (var sensor in ReadTemperatureSensors())
        {
            if (!IsCpuChip(sensor.ChipName))
            {
                continue;
            }

            if (hottest is null || sensor.Celsius > hottest)
            {
                hottest = sensor.Celsius;
            }
        }

        return hottest;
    }

    /// <summary>
    /// Зчитує швидкість обертання першого доступного вентилятора.
    /// </summary>
    /// <returns>Оберти за хвилину (RPM) або <c>null</c>, якщо датчиків вентиляторів немає.</returns>
    public double? ReadFanSpeed()
    {
        if (!Directory.Exists(_hwmonRoot))
        {
            return null;
        }

        foreach (var inputFile in EnumerateSensorFiles("fan*_input"))
        {
            try
            {
                if (LinuxTelemetryParser.TryParseFanRpm(File.ReadAllText(inputFile), out double rpm) && rpm > 0)
                {
                    return rpm;
                }
            }
            catch (Exception)
            {
                // Датчик вентилятора може бути недоступним або зникнути під час читання.
            }
        }

        return null;
    }

    private string[] EnumerateSensorFiles(string pattern)
    {
        try
        {
            return Directory.GetFiles(_hwmonRoot, pattern, SearchOption.AllDirectories);
        }
        catch (Exception)
        {
            // Каталог може бути відсутнім або недоступним (віртуальні машини, контейнери, sandbox).
            return Array.Empty<string>();
        }
    }

    private string ReadChipName(string directory)
    {
        try
        {
            string namePath = Path.Combine(directory, "name");
            return File.Exists(namePath) ? File.ReadAllText(namePath).Trim() : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool IsCpuChip(string chipName) =>
        !string.IsNullOrEmpty(chipName) &&
        CpuChipNames.Any(name => chipName.Contains(name, StringComparison.OrdinalIgnoreCase));
}
