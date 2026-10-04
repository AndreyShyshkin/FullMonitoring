using Avalonia;
using System;

namespace FullMonitoring;

sealed class Program
{
    // Код ініціалізації. Не використовуйте Avalonia, сторонні API або будь-який код,
    // що залежить від SynchronizationContext, до виклику AppMain: компоненти ще не ініціалізовані.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Конфігурація Avalonia, не видаляти; також використовується візуальним дизайнером.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}