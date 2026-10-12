using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using FullMonitoring.Models;

namespace FullMonitoring.Providers;

/// <summary>
/// Провайдер телеметрії для macOS на базі безпечних викликів <c>sysctl</c> із чесним fallback на BCL-метрики.
/// Призначення (Issue #9):
/// <list type="bullet">
/// <item>Читання системних параметрів через <c>sysctlbyname</c> (<c>hw.memsize</c>, <c>hw.logicalcpu</c>, <c>machdep.cpu.brand_string</c>).</item>
/// <item>Диски та мережа успадковуються з кросплатформного <see cref="BclTelemetryProvider"/>.</item>
/// <item>Температура CPU та оберти кулера повертають <c>null</c> (обмежено політикою безпеки macOS / SMC без привілеїв).</item>
/// <item>Відсутність прав або ключів sysctl не призводить до падіння — застосунок працює у безпечному режимі (Safe Mode).</item>
/// </list>
/// </summary>
[SupportedOSPlatform("osx")]
public sealed class MacOsTelemetryProvider : BclTelemetryProvider
{
    private readonly object _cpuLock = new();
    private int? _cachedLogicalCpuCount;
    private long? _cachedTotalMemoryBytes;
    private string? _cachedCpuBrand;

    /// <summary>
    /// Ініціалізує новий екземпляр провайдера macOS.
    /// Конструктор не виконує платформо-залежних викликів, тому безпечний на будь-якій ОС.
    /// </summary>
    public MacOsTelemetryProvider()
    {
    }

    /// <inheritdoc />
    public override string ProviderName => "MacOS Telemetry Provider (sysctl / BCL Fallback)";

    /// <inheritdoc />
    public override bool IsSupported => OperatingSystem.IsMacOS();

    /// <summary>
    /// Назва моделі процесора з <c>machdep.cpu.brand_string</c> або порожній рядок, якщо недоступна.
    /// </summary>
    public string CpuBrand
    {
        get
        {
            if (_cachedCpuBrand is not null)
            {
                return _cachedCpuBrand;
            }

            try
            {
                if (MacOsSysctlReader.TryGetString(MacOsSysctlReader.KeyCpuBrand, out string brand))
                {
                    _cachedCpuBrand = brand;
                    return brand;
                }
            }
            catch (Exception)
            {
                // Ігноруємо — повертаємо порожній рядок як чесний fallback.
            }

            _cachedCpuBrand = string.Empty;
            return string.Empty;
        }
    }

    /// <summary>
    /// Кількість логічних ядер CPU: значення sysctl <c>hw.logicalcpu</c> або <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int LogicalCpuCount
    {
        get
        {
            if (_cachedLogicalCpuCount.HasValue)
            {
                return _cachedLogicalCpuCount.Value;
            }

            try
            {
                if (MacOsSysctlReader.TryGetLogicalCpuCount(out int count) && count > 0)
                {
                    _cachedLogicalCpuCount = count;
                    return count;
                }
            }
            catch (Exception)
            {
                // Ігноруємо — використовуємо значення середовища виконання.
            }

            _cachedLogicalCpuCount = Math.Max(1, Environment.ProcessorCount);
            return _cachedLogicalCpuCount.Value;
        }
    }

    /// <inheritdoc />
    public override CpuMetrics GetCpuMetrics()
    {
        // Базовий збір утилізації (безпечний на будь-якій ОС) + уточнена кількість ядер із sysctl.
        CpuMetrics baseMetrics;
        try
        {
            baseMetrics = base.GetCpuMetrics();
        }
        catch (Exception)
        {
            baseMetrics = new CpuMetrics(0.0, Array.Empty<double>(), null, null);
        }

        int coreCount = LogicalCpuCount;
        IReadOnlyList<double> coreUsages = baseMetrics.CoreUsagesPercentage;

        // Вирівнюємо розбивку по ядрах під фактичну кількість логічних ядер macOS.
        if (coreUsages.Count != coreCount)
        {
            var aligned = new List<double>(coreCount);
            for (int i = 0; i < coreCount; i++)
            {
                if (i < coreUsages.Count)
                {
                    aligned.Add(coreUsages[i]);
                }
                else
                {
                    aligned.Add(Math.Round(baseMetrics.TotalUsagePercentage, 2));
                }
            }

            coreUsages = aligned;
        }

        lock (_cpuLock)
        {
            // На macOS температура та оберти кулера потребують доступу до SMC / привілеїв,
            // тому чесно повертаємо null — UI покаже бейдж «Заблоковано політикою безпеки ОС».
            return new CpuMetrics(
                totalUsagePercentage: baseMetrics.TotalUsagePercentage,
                coreUsagesPercentage: coreUsages,
                temperatureCelsius: null,
                fanSpeedRpm: null);
        }
    }

    /// <inheritdoc />
    public override RamMetrics GetRamMetrics()
    {
        RamMetrics baseMetrics;
        try
        {
            baseMetrics = base.GetRamMetrics();
        }
        catch (Exception)
        {
            return new RamMetrics(0, 0, 0, 0.0);
        }

        // Уточнюємо загальний обсяг через sysctl hw.memsize (точний на Apple Silicon / Intel).
        try
        {
            if (_cachedTotalMemoryBytes is null)
            {
                _cachedTotalMemoryBytes = MacOsSysctlReader.TryGetTotalMemoryBytes(out long total)
                    ? total
                    : baseMetrics.TotalBytes;
            }

            long totalBytes = _cachedTotalMemoryBytes ?? baseMetrics.TotalBytes;
            if (totalBytes <= 0)
            {
                return baseMetrics;
            }

            // Перераховуємо зайнятий обсяг пропорційно до відсотка базового виміру,
            // щоб загальний обсяг відповідав даним sysctl.
            double usedPercentage = Math.Clamp(baseMetrics.UsedPercentage, 0.0, 100.0);
            long usedBytes = (long)Math.Round(totalBytes * usedPercentage / 100.0);
            long freeBytes = Math.Max(0, totalBytes - usedBytes);

            return new RamMetrics(totalBytes, usedBytes, freeBytes, Math.Round(usedPercentage, 2));
        }
        catch (Exception)
        {
            // Будь-який збій sysctl — повертаємо базовий BCL-знімок без падіння.
            return baseMetrics;
        }
    }
}
