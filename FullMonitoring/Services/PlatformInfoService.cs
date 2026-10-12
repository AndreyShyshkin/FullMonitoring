using System;
using System.Runtime.InteropServices;
using FullMonitoring.Models;

namespace FullMonitoring.Services;

/// <summary>
/// Сервіс інформації про платформу та рівень доступу до сенсорів (Issue #9).
/// Визначає ОС через <c>RuntimeInformation.IsOSPlatform</c>, перевіряє права адміністратора
/// та формує дані для статус-бара головного вікна. Усі виклики безпечні: відсутність прав
/// або сенсорів переводить застосунок у режим обмеженої функціональності (Safe Mode) без падіння.
/// </summary>
public sealed class PlatformInfoService : IPlatformInfoService
{
    private readonly Lazy<PlatformKind> _platform;
    private readonly Lazy<bool> _isAdministrator;

    /// <summary>
    /// Ініціалізує новий екземпляр сервісу з автоматичним визначенням платформи.
    /// </summary>
    public PlatformInfoService()
    {
        _platform = new Lazy<PlatformKind>(DetectPlatform);
        _isAdministrator = new Lazy<bool>(DetectAdministrator);
    }

    /// <inheritdoc />
    public PlatformKind CurrentPlatform => _platform.Value;

    /// <inheritdoc />
    public string PlatformDisplayName => GetPlatformDisplayName(CurrentPlatform);

    /// <inheritdoc />
    public string OsDescription
    {
        get
        {
            try
            {
                return RuntimeInformation.OSDescription;
            }
            catch (Exception)
            {
                return "Невідома ОС";
            }
        }
    }

    /// <inheritdoc />
    public string Architecture
    {
        get
        {
            try
            {
                return RuntimeInformation.ProcessArchitecture.ToString();
            }
            catch (Exception)
            {
                return "Н/Д";
            }
        }
    }

    /// <inheritdoc />
    public bool IsAdministrator => _isAdministrator.Value;

    /// <inheritdoc />
    public bool IsSafeMode
    {
        get
        {
            // На Windows безпечний режим вмикається без прав адміністратора (Ring 0 драйвер недоступний).
            if (CurrentPlatform == PlatformKind.Windows && !IsAdministrator)
            {
                return true;
            }

            // На macOS сенсори SMC завжди обмежені політикою ОС — це очікуваний Safe Mode.
            if (CurrentPlatform == PlatformKind.MacOs)
            {
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public string SafeModeDisplayName => IsSafeMode
        ? "Безпечний режим (Safe Mode)"
        : "Повний доступ";

    /// <inheritdoc />
    public SensorAccessLevel EvaluateSensorAccess(CpuMetrics? cpu)
    {
        if (cpu is null)
        {
            return SensorAccessLevel.Restricted;
        }

        bool hasTemperature = cpu.TemperatureCelsius.HasValue;
        bool hasFan = cpu.FanSpeedRpm.HasValue;

        if (hasTemperature && hasFan)
        {
            return SensorAccessLevel.Full;
        }

        if (hasTemperature || hasFan)
        {
            return SensorAccessLevel.Limited;
        }

        return SensorAccessLevel.Restricted;
    }

    /// <inheritdoc />
    public string GetSensorAccessDisplayName(SensorAccessLevel level) => level switch
    {
        SensorAccessLevel.Full => "Сенсори: повний доступ",
        SensorAccessLevel.Limited => "Сенсори: частковий доступ",
        _ => "Сенсори: обмежено політикою ОС"
    };

    /// <inheritdoc />
    public string GetStatusBarText()
    {
        string mode = SafeModeDisplayName;
        return $"Платформа: {PlatformDisplayName} ({Architecture}) | {mode}";
    }

    /// <summary>
    /// Повертає локалізовану назву платформи українською.
    /// </summary>
    public static string GetPlatformDisplayName(PlatformKind platform) => platform switch
    {
        PlatformKind.Windows => "Windows",
        PlatformKind.Linux => "Linux",
        PlatformKind.MacOs => "macOS",
        _ => "Невідома платформа"
    };

    /// <summary>
    /// Визначає логічну платформу через <c>RuntimeInformation.IsOSPlatform</c>.
    /// Порядок перевірок фіксований: Windows → Linux → macOS → Unknown.
    /// </summary>
    public static PlatformKind DetectPlatform()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return PlatformKind.Windows;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return PlatformKind.Linux;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return PlatformKind.MacOs;
            }
        }
        catch (Exception)
        {
            // Ігноруємо — повертаємо Unknown як чесний fallback єдиного білда.
        }

        return PlatformKind.Unknown;
    }

    private static bool DetectAdministrator()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            // Будь-який збій перевірки прав — вважаємо, що прав адміністратора немає (Safe Mode).
            return false;
        }
    }
}
