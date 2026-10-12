using FullMonitoring.Models;

namespace FullMonitoring.Services;

/// <summary>
/// Контракт сервісу інформації про платформу та рівень доступу до сенсорів (Issue #9).
/// Надає дані для статус-бара: поточна платформа, провайдер телеметрії та режим Safe Mode.
/// </summary>
public interface IPlatformInfoService
{
    /// <summary>
    /// Логічна платформа виконання, визначена через <c>RuntimeInformation.IsOSPlatform</c>.
    /// </summary>
    PlatformKind CurrentPlatform { get; }

    /// <summary>
    /// Локалізована назва платформи українською (наприклад, «Windows», «Linux», «macOS»).
    /// </summary>
    string PlatformDisplayName { get; }

    /// <summary>
    /// Детальний опис ОС із <c>RuntimeInformation.OSDescription</c>.
    /// </summary>
    string OsDescription { get; }

    /// <summary>
    /// Архітектура процесу (наприклад, X64, Arm64).
    /// </summary>
    string Architecture { get; }

    /// <summary>
    /// Вказує, чи запущено застосунок з правами адміністратора (лише Windows; на інших ОС — false).
    /// </summary>
    bool IsAdministrator { get; }

    /// <summary>
    /// Вказує, чи працює застосунок у режимі обмеженої функціональності (Safe Mode):
    /// без прав адміністратора або з обмеженим доступом до сенсорів.
    /// </summary>
    bool IsSafeMode { get; }

    /// <summary>
    /// Локалізована назва режиму роботи («Повний доступ» або «Безпечний режим (Safe Mode)»).
    /// </summary>
    string SafeModeDisplayName { get; }

    /// <summary>
    /// Оцінює рівень доступу до сенсорів за знімком CPU (наявність температури та обертів кулера).
    /// </summary>
    /// <param name="cpu">Метрики CPU або null, якщо знімок ще не зібрано.</param>
    /// <returns>Рівень доступу до сенсорів.</returns>
    SensorAccessLevel EvaluateSensorAccess(CpuMetrics? cpu);

    /// <summary>
    /// Повертає локалізовану назву рівня доступу до сенсорів українською.
    /// </summary>
    /// <param name="level">Рівень доступу.</param>
    /// <returns>Текст для статус-бара.</returns>
    string GetSensorAccessDisplayName(SensorAccessLevel level);

    /// <summary>
    /// Формує короткий рядок статус-бара: платформа, архітектура та режим роботи.
    /// </summary>
    /// <returns>Текст статус-бара.</returns>
    string GetStatusBarText();
}
