using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace FullMonitoring.Providers;

/// <summary>
/// Безпечний читач системних параметрів macOS через нативний виклик <c>sysctlbyname</c> (libc).
/// Усі методи є чесними fallback-обгортками: на не-macOS платформах або за відсутності прав
/// повертають <c>false</c> / <c>null</c> без винятків, дозволяючи провайдеру перейти на BCL-метрики.
/// </summary>
[SupportedOSPlatform("osx")]
internal static class MacOsSysctlReader
{
    // Ключі sysctl, що використовуються провайдером macOS.
    public const string KeyTotalMemory = "hw.memsize";
    public const string KeyLogicalCpu = "hw.logicalcpu";
    public const string KeyPhysicalCpu = "hw.physicalcpu";
    public const string KeyCpuBrand = "machdep.cpu.brand_string";
    public const string KeyOsRelease = "kern.osrelease";
    public const string KeyOsType = "kern.ostype";

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int sysctlbyname(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        IntPtr oldp,
        ref UIntPtr oldlenp,
        IntPtr newp,
        UIntPtr newlen);

    /// <summary>
    /// Вказує, чи доступний нативний <c>sysctl</c> у поточному середовищі (лише macOS).
    /// </summary>
    public static bool IsAvailable => OperatingSystem.IsMacOS();

    /// <summary>
    /// Намагається прочитати 64-бітне ціле значення sysctl (наприклад, <c>hw.memsize</c>).
    /// </summary>
    /// <param name="name">Ім'я ключа sysctl.</param>
    /// <param name="value">Прочитане значення.</param>
    /// <returns>True, якщо читання успішне; інакше false.</returns>
    public static bool TryGetUInt64(string name, out ulong value)
    {
        value = 0;
        if (!IsAvailable || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        try
        {
            UIntPtr length = (UIntPtr)sizeof(ulong);
            IntPtr buffer = Marshal.AllocHGlobal(sizeof(ulong));
            try
            {
                int result = sysctlbyname(name, buffer, ref length, IntPtr.Zero, UIntPtr.Zero);
                if (result != 0 || length.ToUInt64() < sizeof(ulong))
                {
                    return false;
                }

                value = (ulong)Marshal.ReadInt64(buffer);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            // Відсутність ключа, брак прав або відсутність libc — не є критичною помилкою.
            return false;
        }
    }

    /// <summary>
    /// Намагається прочитати 32-бітне ціле значення sysctl (наприклад, <c>hw.logicalcpu</c>).
    /// </summary>
    /// <param name="name">Ім'я ключа sysctl.</param>
    /// <param name="value">Прочитане значення.</param>
    /// <returns>True, якщо читання успішне; інакше false.</returns>
    public static bool TryGetInt32(string name, out int value)
    {
        value = 0;
        if (!IsAvailable || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        try
        {
            UIntPtr length = (UIntPtr)sizeof(int);
            IntPtr buffer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                int result = sysctlbyname(name, buffer, ref length, IntPtr.Zero, UIntPtr.Zero);
                if (result != 0)
                {
                    return false;
                }

                value = Marshal.ReadInt32(buffer);
                return value > 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Намагається прочитати рядкове значення sysctl (наприклад, <c>machdep.cpu.brand_string</c>).
    /// </summary>
    /// <param name="name">Ім'я ключа sysctl.</param>
    /// <param name="value">Прочитаний рядок.</param>
    /// <returns>True, якщо читання успішне; інакше false.</returns>
    public static bool TryGetString(string name, out string value)
    {
        value = string.Empty;
        if (!IsAvailable || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        try
        {
            // Перший виклик: дізнатися потрібний розмір буфера.
            UIntPtr length = UIntPtr.Zero;
            int sizeResult = sysctlbyname(name, IntPtr.Zero, ref length, IntPtr.Zero, UIntPtr.Zero);
            if (sizeResult != 0 || length.ToUInt64() == 0 || length.ToUInt64() > 8192)
            {
                return false;
            }

            int bufferSize = checked((int)length.ToUInt64());
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                int result = sysctlbyname(name, buffer, ref length, IntPtr.Zero, UIntPtr.Zero);
                if (result != 0)
                {
                    return false;
                }

                int actualSize = checked((int)length.ToUInt64());
                byte[] bytes = new byte[actualSize];
                Marshal.Copy(buffer, bytes, 0, actualSize);
                value = Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim();
                return !string.IsNullOrWhiteSpace(value);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            value = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Намагається отримати загальний обсяг фізичної пам'яті через <c>hw.memsize</c>.
    /// </summary>
    public static bool TryGetTotalMemoryBytes(out long totalBytes)
    {
        totalBytes = 0;
        if (TryGetUInt64(KeyTotalMemory, out ulong raw) && raw > 0 && raw <= long.MaxValue)
        {
            totalBytes = (long)raw;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Намагається отримати кількість логічних ядер CPU через <c>hw.logicalcpu</c>.
    /// </summary>
    public static bool TryGetLogicalCpuCount(out int coreCount)
    {
        coreCount = 0;
        if (TryGetInt32(KeyLogicalCpu, out int raw) && raw > 0 && raw <= 1024)
        {
            coreCount = raw;
            return true;
        }

        // Резервний варіант: кількість процесорів із середовища виконання.
        coreCount = Math.Max(1, Environment.ProcessorCount);
        return false;
    }
}
