using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FullMonitoring.Providers;

/// <summary>
/// Зчитувальник системної статистики macOS через Mach API (<c>host_statistics</c>, <c>host_statistics64</c>,
/// <c>host_processor_info</c>) — ті самі джерела, з яких бере дані Activity Monitor.
/// </summary>
/// <remarks>
/// <para>
/// Чому не BCL: <c>GC.GetGCMemoryInfo()</c> повертає навантаження купи GC, а
/// <c>Process.TotalProcessorTime</c> — час лише поточного процесу, тому базові метрики
/// системи не відображають. Mach API дає системні лічильники тактов CPU та сторінок пам'яті.
/// </para>
/// <para>
/// Усі методи є чесними fallback-обгортками: на не-macOS платформах або за збою нативного виклику
/// повертають <c>false</c> без винятків, дозволяючи провайдеру перейти на базові метрики.
/// </para>
/// </remarks>
[SupportedOSPlatform("osx")]
internal static class MacOsMachReader
{
    // Значення flavors — з mach/host_info.h та mach/processor_info.h.
    private const int HostCpuLoadInfo = 3;
    private const int HostVmInfo64 = 4;
    private const int ProcessorCpuLoadInfo = 2;

    private const int CpuStateMax = 4;

    /// <summary>
    /// Розмір буфера для <c>host_statistics64</c> у 32-бітних словах.
    /// XNU лише додає поля в кінець <c>vm_statistics64</c>, тому буфер свідомо більший за поточну
    /// структуру (104 слова / 416 байт) — виклик лишається безпечним для майбутніх ревізій ядра,
    /// а ядро саме повертає фактичну кількість слів у <c>count</c>.
    /// </summary>
    private const int VmInfoCapacityWords = 256;

    private const int VmInfoCapacityBytes = VmInfoCapacityWords * sizeof(int);

    // Офсети полів struct vm_statistics64 (mach/vm_statistics.h), перевірені компілятором
    // (sizeof = 416, HOST_VM_INFO64_COUNT = 104). Ревізії ядра лише дописують поля в кінець,
    // тому початкові офсети стабільні.
    private const int OffsetFreeCount = 0;
    private const int OffsetActiveCount = 4;
    private const int OffsetInactiveCount = 8;
    private const int OffsetWireCount = 12;
    private const int OffsetPurgeableCount = 88;
    private const int OffsetSpeculativeCount = 92;
    private const int OffsetCompressorPageCount = 128;
    private const int OffsetInternalCount = 140;

    private static readonly Lazy<uint> HostPort = new Lazy<uint>(mach_host_self);
    private static readonly Lazy<uint> TaskPort = new Lazy<uint>(ReadTaskPort);

    // RTLD_DEFAULT для dlsym — публічна змінна mach_task_self_ оголошена в mach/mach_init.h.
    private static readonly IntPtr RtldDefault = new IntPtr(-2);

    /// <summary>
    /// Вказує, чи доступний Mach API в поточному середовищі (лише macOS).
    /// </summary>
    public static bool IsAvailable => OperatingSystem.IsMacOS();

    /// <summary>
    /// Знімок тактів CPU по всіх ядрах (<c>HOST_CPU_LOAD_INFO</c>): user / system / idle / nice.
    /// Лічильники 32-бітні та переповнюються — різниця рахується в арифметиці беззнакового
    /// віднімання, тому коректна й після переповнення.
    /// </summary>
    public readonly record struct CpuTicks(uint User, uint System, uint Idle, uint Nice)
    {
        /// <summary>Сума всіх тактів.</summary>
        public long Total => (long)User + System + Idle + Nice;

        /// <summary>Сума тактів, коли ядро працювало (без простою).</summary>
        public long Busy => (long)User + System + Nice;

        /// <summary>Різниця між двома знімками з урахуванням переповнення 32-бітних лічильників.</summary>
        public static CpuTicks Delta(CpuTicks previous, CpuTicks current) => new(
            unchecked(current.User - previous.User),
            unchecked(current.System - previous.System),
            unchecked(current.Idle - previous.Idle),
            unchecked(current.Nice - previous.Nice));
    }

    /// <summary>
    /// Знімок лічильників сторінок віртуальної пам'яті (<c>HOST_VM_INFO64</c>) в одиницях сторінок.
    /// </summary>
    /// <param name="FreePages">Вільні сторінки.</param>
    /// <param name="ActivePages">Активні сторінки (дані нещодавно використовувалися).</param>
    /// <param name="InactivePages">Неактивні сторінки (кандидати на витіснення / кеш файлів).</param>
    /// <param name="WiredPages">Закріплені (wired) сторінки ядра.</param>
    /// <param name="SpeculativePages">Спекулятивні сторінки (враховуються у вільних).</param>
    /// <param name="PurgeablePages">Сторінки, які система може очистити без запису на диск.</param>
    /// <param name="CompressorPages">Сторінки, які займає компресор пам'яті (stowed compressed).</param>
    /// <param name="InternalPages">Сторінки, зараховані до пам'яті процесів (internal) — без файлового кешу.</param>
    public readonly record struct VmStatistics64(
        long FreePages,
        long ActivePages,
        long InactivePages,
        long WiredPages,
        long SpeculativePages,
        long PurgeablePages,
        long CompressorPages,
        long InternalPages);

    /// <summary>
    /// Зчитує системні такти CPU (<c>HOST_CPU_LOAD_INFO</c>) — аналог того, що показує Activity Monitor
    /// у вкладці «CPU» (System / User / Idle).
    /// </summary>
    public static bool TryGetSystemCpuTicks(out CpuTicks ticks)
    {
        ticks = default;
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            var buffer = default(CpuLoadInfo);
            uint count = CpuStateMax;
            if (host_statistics(HostPort.Value, HostCpuLoadInfo, ref buffer, ref count) != 0 || count < CpuStateMax)
            {
                return false;
            }

            ticks = new CpuTicks(buffer.User, buffer.System, buffer.Idle, buffer.Nice);
            return true;
        }
        catch (Exception)
        {
            // Нативний виклик недоступний — чесно повідомляємо про fallback.
            return false;
        }
    }

    /// <summary>
    /// Зчитує такти CPU окремо для кожного логічного ядра (<c>host_processor_info</c>).
    /// </summary>
    /// <remarks>
    /// Ядро повертає буфер, який треба звільнити через <c>vm_deallocate</c> у межах того ж виклику,
    /// тому метод нічого не накопичує в пам'яті процесу.
    /// </remarks>
    public static bool TryGetPerCoreCpuTicks(out CpuTicks[] cores)
    {
        cores = Array.Empty<CpuTicks>();
        if (!IsAvailable)
        {
            return false;
        }

        uint taskPort = TaskPort.Value;
        if (taskPort == 0)
        {
            // Без порта власної задачі буфер ядра неможливо звільнити — не читаємо, щоб не текти пам'яттю.
            return false;
        }

        IntPtr info = IntPtr.Zero;
        uint infoCount = 0;

        try
        {
            uint cpuCount = 0;
            if (host_processor_info(HostPort.Value, ProcessorCpuLoadInfo, out cpuCount, out info, out infoCount) != 0)
            {
                return false;
            }

            if (info == IntPtr.Zero || cpuCount == 0 || infoCount < cpuCount * CpuStateMax)
            {
                return false;
            }

            var result = new CpuTicks[cpuCount];
            for (uint i = 0; i < cpuCount; i++)
            {
                int offset = checked((int)(i * CpuStateMax * sizeof(int)));
                result[i] = new CpuTicks(
                    unchecked((uint)Marshal.ReadInt32(info, offset)),
                    unchecked((uint)Marshal.ReadInt32(info, offset + sizeof(int))),
                    unchecked((uint)Marshal.ReadInt32(info, offset + 2 * sizeof(int))),
                    unchecked((uint)Marshal.ReadInt32(info, offset + 3 * sizeof(int))));
            }

            cores = result;
            return true;
        }
        catch (Exception)
        {
            cores = Array.Empty<CpuTicks>();
            return false;
        }
        finally
        {
            if (info != IntPtr.Zero)
            {
                try
                {
                    vm_deallocate(taskPort, unchecked((ulong)info.ToInt64()), (ulong)infoCount * sizeof(int));
                }
                catch (Exception)
                {
                    // Звільнення не вдалося — значення вже зчитані, падати не треба.
                }
            }
        }
    }

    /// <summary>
    /// Зчитує лічильники сторінок пам'яті (<c>HOST_VM_INFO64</c>) — джерело даних Activity Monitor.
    /// </summary>
    public static bool TryGetVmStatistics(out VmStatistics64 statistics)
    {
        statistics = default;
        if (!IsAvailable)
        {
            return false;
        }

        IntPtr buffer = Marshal.AllocHGlobal(VmInfoCapacityBytes);
        try
        {
            uint count = VmInfoCapacityWords;
            if (host_statistics64(HostPort.Value, HostVmInfo64, buffer, ref count) != 0 || count == 0)
            {
                return false;
            }

            statistics = new VmStatistics64(
                FreePages: (uint)Marshal.ReadInt32(buffer, OffsetFreeCount),
                ActivePages: (uint)Marshal.ReadInt32(buffer, OffsetActiveCount),
                InactivePages: (uint)Marshal.ReadInt32(buffer, OffsetInactiveCount),
                WiredPages: (uint)Marshal.ReadInt32(buffer, OffsetWireCount),
                SpeculativePages: (uint)Marshal.ReadInt32(buffer, OffsetSpeculativeCount),
                PurgeablePages: (uint)Marshal.ReadInt32(buffer, OffsetPurgeableCount),
                CompressorPages: (uint)Marshal.ReadInt32(buffer, OffsetCompressorPageCount),
                InternalPages: (uint)Marshal.ReadInt32(buffer, OffsetInternalCount));
            return true;
        }
        catch (Exception)
        {
            statistics = default;
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Зчитує розмір сторінки пам'яті через <c>host_page_size</c> (16 КБ на Apple Silicon, 4 КБ на Intel).
    /// </summary>
    public static bool TryGetPageSize(out long pageSize)
    {
        pageSize = 0;
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            return host_page_size(HostPort.Value, out ulong size) == 0 && size > 0 && (pageSize = (long)size) > 0;
        }
        catch (Exception)
        {
            pageSize = 0;
            return false;
        }
    }

    /// <summary>
    /// Обчислює відсоток завантаження CPU між двома знімками тактів.
    /// </summary>
    /// <returns>
    /// Відсоток від 0 до 100 або <c>null</c>, якщо лічильники не змінилися (перший знімок чи нульовий інтервал) —
    /// тоді викликача має залишити попереднє значення.
    /// </returns>
    public static double? ComputeUsagePercentage(in CpuTicks previous, in CpuTicks current)
    {
        CpuTicks delta = CpuTicks.Delta(previous, current);
        long totalDelta = delta.Total;
        if (totalDelta <= 0)
        {
            return null;
        }

        long busyDelta = Math.Max(0, delta.Busy);
        return Math.Clamp(busyDelta * 100.0 / totalDelta, 0.0, 100.0);
    }

    /// <summary>
    /// Обчислює зайнятий обсяг пам'яті так, як це робить Activity Monitor у полі «Memory Used»:
    /// <c>internal + wired + compressor</c> (це і є <c>phys_footprint</c> системи).
    /// Файловий кеш (<c>external</c>) та вільні сторінки не входять — їх macOS може віддати додаткам,
    /// тому вони потрапляють у «вільний» обсяг.
    /// </summary>
    public static long ComputeUsedMemoryBytes(in VmStatistics64 statistics, long pageSize)
    {
        if (pageSize <= 0)
        {
            return 0;
        }

        long pages = statistics.InternalPages + statistics.WiredPages + statistics.CompressorPages;
        return pages < 0 ? 0 : pages * pageSize;
    }

    /// <summary>Структура <c>host_cpu_load_info</c>: такти CPU в кожному режимі (mach/host_info.h).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CpuLoadInfo
    {
        public uint User;
        public uint System;
        public uint Idle;
        public uint Nice;
    }

    private static uint ReadTaskPort()
    {
        try
        {
            IntPtr symbol = dlsym(RtldDefault, "mach_task_self_");
            return symbol == IntPtr.Zero ? 0 : unchecked((uint)Marshal.ReadInt32(symbol));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    [DllImport("libc", EntryPoint = "mach_host_self")]
    private static extern uint mach_host_self();

    [DllImport("libc", EntryPoint = "host_statistics")]
    private static extern int host_statistics(uint host, int flavor, ref CpuLoadInfo info, ref uint count);

    [DllImport("libc", EntryPoint = "host_statistics64")]
    private static extern int host_statistics64(uint host, int flavor, IntPtr info, ref uint count);

    [DllImport("libc", EntryPoint = "host_processor_info")]
    private static extern int host_processor_info(
        uint host,
        int flavor,
        out uint processorCount,
        out IntPtr processorInfo,
        out uint processorInfoCount);

    [DllImport("libc", EntryPoint = "host_page_size")]
    private static extern int host_page_size(uint host, out ulong pageSize);

    [DllImport("libc", EntryPoint = "vm_deallocate")]
    private static extern int vm_deallocate(uint task, ulong address, ulong size);

    [DllImport("libc", EntryPoint = "dlsym")]
    private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string symbolName);
}
