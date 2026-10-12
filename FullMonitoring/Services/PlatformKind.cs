namespace FullMonitoring.Services;

/// <summary>
/// Логічна платформа виконання застосунку (Issue #9).
/// Визначається через <c>RuntimeInformation.IsOSPlatform</c> без зміни коду під кожну ОС.
/// </summary>
public enum PlatformKind
{
    /// <summary>
    /// Платформа не розпізнана (резервний варіант для єдиного бінарного білда).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Microsoft Windows (провайдер <c>WindowsTelemetryProvider</c>).
    /// </summary>
    Windows = 1,

    /// <summary>
    /// Ядро Linux (провайдер <c>LinuxTelemetryProvider</c>).
    /// </summary>
    Linux = 2,

    /// <summary>
    /// Apple macOS (провайдер <c>MacOsTelemetryProvider</c>).
    /// </summary>
    MacOs = 3
}
