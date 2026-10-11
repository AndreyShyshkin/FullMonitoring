using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using FullMonitoring.Models;

namespace FullMonitoring.Providers;

/// <summary>
/// Провайдер телеметрії для ядра Linux на базі прямого парсингу procfs та sysfs.
/// Реалізує нативний збір метрик без сторонніх бінарних залежностей:
/// <list type="bullet">
/// <item><c>/proc/stat</c> — загальна та погодинна утилізація CPU через дельту між idle та total.</item>
/// <item><c>/proc/meminfo</c> — загальний і доступний обсяг оперативної пам'яті.</item>
/// <item><c>/sys/class/hwmon/</c> — температури та швидкості обертання вентиляторів.</item>
/// </list>
/// Дискові та мережеві метрики успадковуються з крос-платформного <see cref="BclTelemetryProvider"/>.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxTelemetryProvider : BclTelemetryProvider
{
    private const string DefaultProcRoot = "/proc";
    private const string DefaultSysRoot = "/sys";

    private readonly string _procRoot;
    private readonly LinuxHardwareReader _hardwareReader;
    private readonly object _cpuLock = new();

    private LinuxTelemetryParser.CpuTimeSample _lastCpuAggregate;
    private LinuxTelemetryParser.CpuTimeSample[] _lastCpuCores = Array.Empty<LinuxTelemetryParser.CpuTimeSample>();
    private IReadOnlyList<double> _lastCoreUsages = Array.Empty<double>();
    private bool _hasCpuBaseline;
    private double _lastCpuUsage;

    /// <summary>
    /// Ініціалізує провайдер із типовими шляхами <c>/proc</c> та <c>/sys</c>.
    /// </summary>
    public LinuxTelemetryProvider()
        : this(DefaultProcRoot, DefaultSysRoot)
    {
    }

    /// <summary>
    /// Ініціалізує провайдер із явно заданими кореневими каталогами (використовується для тестування).
    /// </summary>
    /// <param name="procRoot">Кореневий каталог procfs (типово <c>/proc</c>).</param>
    /// <param name="sysRoot">Кореневий каталог sysfs (типово <c>/sys</c>).</param>
    public LinuxTelemetryProvider(string procRoot, string sysRoot)
    {
        _procRoot = string.IsNullOrWhiteSpace(procRoot) ? DefaultProcRoot : procRoot;
        _hardwareReader = new LinuxHardwareReader(
            string.IsNullOrWhiteSpace(sysRoot) ? null : Path.Combine(sysRoot, "class", "hwmon"));

        CaptureCpuBaseline();
    }

    /// <inheritdoc />
    public override string ProviderName => "Linux Telemetry Provider (procfs/sysfs)";

    /// <inheritdoc />
    public override bool IsSupported => OperatingSystem.IsLinux();

    /// <inheritdoc />
    public override CpuMetrics GetCpuMetrics()
    {
        double? temperature = _hardwareReader.ReadCpuTemperature();
        double? fanSpeed = _hardwareReader.ReadFanSpeed();

        lock (_cpuLock)
        {
            var statPath = Path.Combine(_procRoot, "stat");
            if (TryReadFile(statPath, out string content) &&
                LinuxTelemetryParser.TryParseCpuStat(content, out var snapshot))
            {
                double aggregateUsage = _hasCpuBaseline
                    ? CalculateUsage(_lastCpuAggregate, snapshot.Aggregate, _lastCpuUsage)
                    : 0.0;

                var coreUsages = new double[snapshot.Cores.Length];
                for (int i = 0; i < snapshot.Cores.Length; i++)
                {
                    double fallback = i < _lastCoreUsages.Count ? _lastCoreUsages[i] : aggregateUsage;
                    coreUsages[i] = _hasCpuBaseline && i < _lastCpuCores.Length
                        ? CalculateUsage(_lastCpuCores[i], snapshot.Cores[i], fallback)
                        : fallback;
                }

                _lastCpuAggregate = snapshot.Aggregate;
                _lastCpuCores = snapshot.Cores;
                _lastCoreUsages = coreUsages;
                _lastCpuUsage = aggregateUsage;
                _hasCpuBaseline = true;

                return new CpuMetrics(
                    totalUsagePercentage: Math.Round(aggregateUsage, 2),
                    coreUsagesPercentage: coreUsages,
                    temperatureCelsius: temperature,
                    fanSpeedRpm: fanSpeed);
            }

            // procfs недоступний (контейнер, sandbox) — повертаємо останній відомий знімок.
            return new CpuMetrics(
                totalUsagePercentage: Math.Round(_lastCpuUsage, 2),
                coreUsagesPercentage: _lastCoreUsages,
                temperatureCelsius: temperature,
                fanSpeedRpm: fanSpeed);
        }
    }

    /// <inheritdoc />
    public override RamMetrics GetRamMetrics()
    {
        var memInfoPath = Path.Combine(_procRoot, "meminfo");
        if (TryReadFile(memInfoPath, out string content) &&
            LinuxTelemetryParser.TryParseMemInfo(content, out long totalBytes, out long availableBytes))
        {
            availableBytes = Math.Clamp(availableBytes, 0, totalBytes);
            long usedBytes = totalBytes - availableBytes;
            return new RamMetrics(totalBytes, usedBytes, availableBytes);
        }

        // Резервний варіант, якщо procfs недоступний (наприклад, обмежене середовище).
        return base.GetRamMetrics();
    }

    /// <summary>
    /// Фіксує початковий зріз часу CPU, щоб перший виклик <see cref="GetCpuMetrics"/> вже мав дельту для розрахунку.
    /// </summary>
    private void CaptureCpuBaseline()
    {
        var statPath = Path.Combine(_procRoot, "stat");
        if (TryReadFile(statPath, out string content) &&
            LinuxTelemetryParser.TryParseCpuStat(content, out var snapshot))
        {
            _lastCpuAggregate = snapshot.Aggregate;
            _lastCpuCores = snapshot.Cores;
            _hasCpuBaseline = true;
        }
    }

    /// <summary>
    /// Обчислює відсоток утилізації CPU як частку не-idle часу в загальній дельті тіків.
    /// </summary>
    private static double CalculateUsage(
        LinuxTelemetryParser.CpuTimeSample previous,
        LinuxTelemetryParser.CpuTimeSample current,
        double fallback)
    {
        long deltaTotal = current.Total - previous.Total;
        long deltaIdle = current.Idle - previous.Idle;

        if (deltaTotal <= 0 || deltaIdle < 0)
        {
            return fallback;
        }

        double usage = (1.0 - (double)deltaIdle / deltaTotal) * 100.0;
        return Math.Clamp(usage, 0.0, 100.0);
    }

    private static bool TryReadFile(string path, out string content)
    {
        try
        {
            content = File.ReadAllText(path);
            return true;
        }
        catch (Exception)
        {
            // Файл відсутній або недоступний (контейнер, sandbox, недостатньо прав) — не є критичною помилкою.
            content = string.Empty;
            return false;
        }
    }
}
