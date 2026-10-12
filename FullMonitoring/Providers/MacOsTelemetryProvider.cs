using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using FullMonitoring.Models;

namespace FullMonitoring.Providers;

/// <summary>
/// Провайдер телеметрії для macOS на базі <c>sysctl</c> та Mach API із чесним fallback на BCL-метрики.
/// Призначення (Issue #9):
/// <list type="bullet">
/// <item>Читання системних параметрів через <c>sysctlbyname</c> (<c>hw.memsize</c>, <c>hw.logicalcpu</c>, <c>machdep.cpu.brand_string</c>).</item>
/// <item>Системне завантаження CPU та статистика пам'яті — через Mach API
/// (<c>host_statistics</c>, <c>host_processor_info</c>, <c>host_statistics64</c>),
/// тобто з тих самих джерел, що й Activity Monitor.</item>
/// <item>Диски та мережа успадковуються з кросплатформного <see cref="BclTelemetryProvider"/>.</item>
/// <item>Температура CPU та оберти кулера повертають <c>null</c> (обмежено політикою безпеки macOS / SMC без привілеїв).</item>
/// <item>Відсутність прав або ключів sysctl не призводить до падіння — застосунок працює у безпечному режимі (Safe Mode)
/// і переходить на базові BCL-метрики.</item>
/// </list>
/// </summary>
[SupportedOSPlatform("osx")]
public sealed class MacOsTelemetryProvider : BclTelemetryProvider
{
    private readonly object _cpuLock = new();
    private int? _cachedLogicalCpuCount;
    private long? _cachedTotalMemoryBytes;
    private string? _cachedCpuBrand;

    // Попередні знімки тактів Mach — з ними рахуються делти між опитуваннями.
    private MacOsMachReader.CpuTicks? _lastSystemTicks;
    private MacOsMachReader.CpuTicks[]? _lastCoreTicks;
    private double[]? _lastCoreUsage;
    private double _lastTotalUsage;

    /// <summary>
    /// Ініціалізує новий екземпляр провайдера macOS.
    /// Конструктор не виконує платформо-залежних викликів, тому безпечний на будь-якій ОС.
    /// </summary>
    public MacOsTelemetryProvider()
    {
    }

    /// <inheritdoc />
    public override string ProviderName => "MacOS Telemetry Provider (sysctl / Mach)";

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
    /// <remarks>
    /// Основне джерело — системні лічильники тактів Mach (усі ядра системи), а не час поточного процесу:
    /// саме так відсоток збігається з Activity Monitor / <c>top</c>.
    /// Розбивка по ядрах береться з <c>host_processor_info</c>; за першого виміру або збою ядер
    /// тимчасово показується загальне навантаження, доки не накопичиться делта.
    /// </remarks>
    public override CpuMetrics GetCpuMetrics()
    {
        lock (_cpuLock)
        {
            double totalUsage;
            IReadOnlyList<double> coreUsages;

            if (!TryCollectSystemCpuUsage(out totalUsage, out coreUsages))
            {
                // Fallback: базовий BCL-збір (час процесу) — чесно, без падіння.
                try
                {
                    CpuMetrics baseMetrics = base.GetCpuMetrics();
                    totalUsage = _lastTotalUsage = baseMetrics.TotalUsagePercentage;
                    coreUsages = baseMetrics.CoreUsagesPercentage;
                }
                catch (Exception)
                {
                    totalUsage = _lastTotalUsage;
                    coreUsages = Array.Empty<double>();
                }
            }

            int coreCount = LogicalCpuCount;
            var alignedUsages = new List<double>(coreCount);
            for (int i = 0; i < coreCount; i++)
            {
                double value = i < coreUsages.Count ? coreUsages[i] : totalUsage;
                alignedUsages.Add(Math.Round(value, 2));
            }

            // На macOS температура та оберти кулера потребують доступу до SMC / привілеїв,
            // тому чесно повертаємо null — UI покаже бейдж «Заблоковано політикою безпеки ОС».
            return new CpuMetrics(
                totalUsagePercentage: Math.Round(totalUsage, 2),
                coreUsagesPercentage: alignedUsages,
                temperatureCelsius: null,
                fanSpeedRpm: null);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Основне джерело — лічильники сторінок Mach (<c>HOST_VM_INFO64</c>): зайнятий обсяг рахується як
    /// <c>internal + wired + compressor</c> — це <c>phys_footprint</c>, тобто те саме значення,
    /// що Activity Monitor показує як «Memory Used». Файловий кеш і вільні сторінки потрапляють у
    /// «вільний» обсяг, бо їх macOS може віддати додаткам.
    /// </remarks>
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

        try
        {
            // Уточнюємо загальний обсяг через sysctl hw.memsize (точний на Apple Silicon / Intel).
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

            if (MacOsMachReader.TryGetVmStatistics(out MacOsMachReader.VmStatistics64 statistics) &&
                MacOsMachReader.TryGetPageSize(out long pageSize))
            {
                long usedBytes = Math.Clamp(MacOsMachReader.ComputeUsedMemoryBytes(statistics, pageSize), 0, totalBytes);
                long freeBytes = totalBytes - usedBytes;
                double usedPercentage = Math.Round((double)usedBytes / totalBytes * 100.0, 2);

                return new RamMetrics(totalBytes, usedBytes, freeBytes, usedPercentage);
            }

            // Fallback: перераховуємо базовий відсоток пропорційно до обсягу sysctl.
            double usedPercentageFallback = Math.Clamp(baseMetrics.UsedPercentage, 0.0, 100.0);
            long usedBytesFallback = (long)Math.Round(totalBytes * usedPercentageFallback / 100.0);

            return new RamMetrics(
                totalBytes,
                usedBytesFallback,
                Math.Max(0, totalBytes - usedBytesFallback),
                Math.Round(usedPercentageFallback, 2));
        }
        catch (Exception)
        {
            // Будь-який збій sysctl / Mach — повертаємо базовий BCL-знімок без падіння.
            return baseMetrics;
        }
    }

    /// <summary>
    /// Зчитує поточні такти Mach і оновлює оцінку завантаження системи (загальну та по ядрах).
    /// </summary>
    /// <returns><c>true</c>, якщо Mach-джерело відповіло; інакше викликач має використати fallback.</returns>
    private bool TryCollectSystemCpuUsage(out double totalUsage, out IReadOnlyList<double> coreUsages)
    {
        totalUsage = _lastTotalUsage;
        coreUsages = Array.Empty<double>();

        if (!MacOsMachReader.TryGetSystemCpuTicks(out MacOsMachReader.CpuTicks systemTicks))
        {
            return false;
        }

        // Перший знімок лише фіксує базову точку: відсоток з'явиться з наступного опитування.
        if (_lastSystemTicks is { } previousSystem &&
            MacOsMachReader.ComputeUsagePercentage(previousSystem, systemTicks) is { } systemPercentage)
        {
            totalUsage = _lastTotalUsage = systemPercentage;
        }

        _lastSystemTicks = systemTicks;

        if (MacOsMachReader.TryGetPerCoreCpuTicks(out MacOsMachReader.CpuTicks[] coreTicks) && coreTicks.Length > 0)
        {
            MacOsMachReader.CpuTicks[]? previousCores = _lastCoreTicks;
            double[]? previousUsage = _lastCoreUsage;
            var currentUsage = new double[coreTicks.Length];

            for (int i = 0; i < coreTicks.Length; i++)
            {
                double? delta = null;
                if (previousCores is not null && previousCores.Length == coreTicks.Length)
                {
                    delta = MacOsMachReader.ComputeUsagePercentage(previousCores[i], coreTicks[i]);
                }

                if (delta.HasValue)
                {
                    currentUsage[i] = delta.Value;
                }
                else if (previousUsage is not null && previousUsage.Length == coreTicks.Length)
                {
                    // Немає делти (перше вибір ядра) — тримаємо попередню оцінку цього ядра.
                    currentUsage[i] = previousUsage[i];
                }
                else
                {
                    currentUsage[i] = totalUsage;
                }
            }

            _lastCoreTicks = coreTicks;
            _lastCoreUsage = currentUsage;
            coreUsages = currentUsage;
        }
        else
        {
            // Перше вибір або збій читання ядер — скидаємо базову точку, щоб не показувати застарілі значення.
            _lastCoreTicks = null;
            _lastCoreUsage = null;
        }

        return true;
    }
}
