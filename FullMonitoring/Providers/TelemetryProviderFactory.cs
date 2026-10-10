using System;
using System.Runtime.InteropServices;
using FullMonitoring.Interfaces;
using FullMonitoring.Providers;

namespace FullMonitoring.Providers;

/// <summary>
/// Фабрика для вибору оптимального провайдера телеметрії залежно від поточної операційної системи.
/// </summary>
public static class TelemetryProviderFactory
{
    /// <summary>
    /// Повертає найбільш підходящий провайдер телеметрії для поточної ОС.
    /// </summary>
    /// <returns>Екземпляр <see cref="ITelemetryProvider"/>.</returns>
    public static ITelemetryProvider Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsTelemetryProvider();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxTelemetryProvider();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // MacOsProvider ще не реалізовано повністю, використовуємо базовий BCL
            return new BclTelemetryProvider();
        }

        // Fallback для невідомих ОС або середовищ розробки
        return new MockTelemetryProvider();
    }
}
