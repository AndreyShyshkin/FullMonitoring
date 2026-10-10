using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;

namespace FullMonitoring.Providers;

/// <summary>
/// Провайдер телеметрії для Linux, що реалізує збір метрик через парсинг системних файлів /proc та /sys.
/// </summary>
public class LinuxTelemetryProvider : BclTelemetryProvider
{
    private readonly object _cpuLock = new();
    private long _lastTotalCpuTime = 0;
    private long _lastIdleCpuTime = 0;

    /// <inheritdoc />
    public override string ProviderName => "Linux System Provider";

    /// <inheritdoc />
    public override bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <inheritdoc />
    public override CpuMetrics GetCpuMetrics()
    {
        lock (_cpuLock)
        {
            try
            {
                string statContent = File.ReadAllText("/proc/stat");
                ReadOnlySpan<char> span = statContent.AsSpan();

                // Шукаємо перший рядок, який починається з "cpu "
                int lineEnd = span.IndexOf('\n');
                if (lineEnd == -1) return base.GetCpuMetrics();

                ReadOnlySpan<char> cpuLine = span.Slice(0, lineEnd);

                // Парсимо значення CPU: user, nice, system, idle, iowait, irq, softirq, steal
                var values = ParseCpuLine(cpuLine);

                long total = 0;
                long idle = 0;

                for (int i = 0; i < values.Length; i++)
                {
                    total += values[i];
                    if (i == 3 || i == 4) // idle та iowait
                    {
                        idle += values[i];
                    }
                }

                double usagePercentage = 0.0;
                if (_lastTotalCpuTime != 0)
                {
                    long totalDelta = total - _lastTotalCpuTime;
                    long idleDelta = idle - _lastIdleCpuTime;

                    if (totalDelta > 0)
                    {
                        usagePercentage = 100.0 * (1.0 - (double)idleDelta / totalDelta);
                    }
                }

                _lastTotalCpuTime = total;
                _lastIdleCpuTime = idle;

                // Збір температури
                double? temperature = GetSystemTemperature();

                return new CpuMetrics(
                    totalUsagePercentage: Math.Round(Math.Clamp(usagePercentage, 0.0, 100.0), 2),
                    temperatureCelsius: temperature
                );
            }
            catch (Exception)
            {
                return base.GetCpuMetrics();
            }
        }
    }

    /// <inheritdoc />
    public override RamMetrics GetRamMetrics()
    {
        try
        {
            string memInfoContent = File.ReadAllText("/proc/meminfo");
            ReadOnlySpan<char> span = memInfoContent.AsSpan();

            long totalBytes = ParseMemInfoValue(span, "MemTotal:");
            long availableBytes = ParseMemInfoValue(span, "MemAvailable:");

            if (totalBytes <= 0) return base.GetRamMetrics();

            long usedBytes = totalBytes - availableBytes;

            return new RamMetrics(
                totalBytes: totalBytes,
                usedBytes: usedBytes,
                freeBytes: availableBytes
            );
        }
        catch (Exception)
        {
            return base.GetRamMetrics();
        }
    }

    private static long[] ParseCpuLine(ReadOnlySpan<char> line)
    {
        // Пропускаємо "cpu "
        var slice = line.Slice(4).TrimStart();
        var result = new long[8];
        int count = 0;
        int start = 0;

        for (int i = 0; i < slice.Length && count < 8; i++)
        {
            if (char.IsWhiteSpace(slice[i]))
            {
                if (i > start)
                {
                    result[count++] = long.Parse(slice.Slice(start, i - start));
                }
                start = i + 1;
            }
        }

        if (start < slice.Length && count < 8)
        {
            result[count] = long.Parse(slice.Slice(start));
        }

        return result;
    }

    private static long ParseMemInfoValue(ReadOnlySpan<char> span, string key)
    {
        int keyIndex = span.IndexOf(key.AsSpan());
        if (keyIndex == -1) return 0;

        var line = span.Slice(keyIndex);
        int lineEnd = line.IndexOf('\n');
        if (lineEnd == -1) line = line; else line = line.Slice(0, lineEnd);

        // Знаходимо числове значення
        int valueStart = -1;
        int valueEnd = -1;

        for (int i = 0; i < line.Length; i++)
        {
            if (char.IsDigit(line[i]))
            {
                if (valueStart == -1) valueStart = i;
                valueEnd = i + 1;
            }
            else if (valueStart != -1)
            {
                break;
            }
        }

        if (valueStart == -1) return 0;

        if (long.TryParse(line.Slice(valueStart, valueEnd - valueStart), out long value))
        {
            // Значення в /proc/meminfo зазвичай у kB
            return value * 1024;
        }

        return 0;
    }

    private double? GetSystemTemperature()
    {
        try
        {
            string hwmonPath = "/sys/class/hwmon";
            if (!Directory.Exists(hwmonPath)) return null;

            string[] folders = Directory.GetDirectories(hwmonPath);
            foreach (var folder in folders)
            {
                // Шукаємо temp*_input
                string[] files = Directory.GetFiles(folder, "temp*_input");
                foreach (var file in files)
                {
                    string content = File.ReadAllText(file).Trim();
                    if (int.TryParse(content, out int milliCelsius))
                    {
                        return milliCelsius / 1000.0;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ігноруємо помилки доступу або відсутність датчиків
        }

        return null;
    }
}
