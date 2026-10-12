using System;
using System.Runtime.InteropServices;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FullMonitoring.Services;

/// <summary>
/// Реєстрація залежностей застосунку в контейнері <c>Microsoft.Extensions.DependencyInjection</c> (Issue #9).
/// Забезпечує автоматичну резолюцію <see cref="ITelemetryProvider"/> за поточною ОС
/// без зміни коду (єдиний бінарний білд для Windows, Linux та macOS).
/// </summary>
public static class TelemetryServiceRegistration
{
    /// <summary>
    /// Визначає тип провайдера телеметрії для заданої платформи.
    /// Чиста функція без побічних ефектів — придатна для модульного тестування.
    /// </summary>
    /// <param name="platform">Логічна платформа виконання.</param>
    /// <returns>Тип реалізації <see cref="ITelemetryProvider"/>.</returns>
    public static Type ResolveTelemetryProviderType(PlatformKind platform) => platform switch
    {
        PlatformKind.Windows => typeof(WindowsTelemetryProvider),
        PlatformKind.Linux => typeof(LinuxTelemetryProvider),
        PlatformKind.MacOs => typeof(MacOsTelemetryProvider),
        _ => typeof(BclTelemetryProvider)
    };

    /// <summary>
    /// Визначає тип провайдера телеметрії за поточною ОС через <c>RuntimeInformation.IsOSPlatform</c>.
    /// </summary>
    /// <returns>Тип реалізації <see cref="ITelemetryProvider"/> для поточної ОС.</returns>
    public static Type ResolveTelemetryProviderTypeForCurrentOs()
    {
        // Порядок перевірок фіксований відповідно до вимог Issue #9:
        // Windows → Linux → macOS → резервний BCL-провайдер.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return typeof(WindowsTelemetryProvider);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return typeof(LinuxTelemetryProvider);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return typeof(MacOsTelemetryProvider);
        }

        return typeof(BclTelemetryProvider);
    }

    /// <summary>
    /// Створює екземпляр <see cref="ITelemetryProvider"/> для поточної ОС із безпечним fallback.
    /// Якщо платформо-специфічний провайдер не вдалося створити (відсутні драйвери, брак прав),
    /// повертається базовий <see cref="BclTelemetryProvider"/> — застосунок продовжує роботу у Safe Mode.
    /// </summary>
    /// <returns>Готовий до використання провайдер телеметрії.</returns>
    public static ITelemetryProvider CreateTelemetryProviderForCurrentOs()
    {
        Type providerType = ResolveTelemetryProviderTypeForCurrentOs();

        try
        {
            if (Activator.CreateInstance(providerType) is ITelemetryProvider provider && provider.IsSupported)
            {
                return provider;
            }
        }
        catch (Exception)
        {
            // Ігноруємо — нижче повертаємо безпечний BCL-fallback.
        }

        // Чесний fallback: базова BCL-телеметрія доступна на будь-якій ОС.
        return new BclTelemetryProvider();
    }

    /// <summary>
    /// Реєструє платформо-залежні та спільні сервіси застосунку в DI-контейнері.
    /// </summary>
    /// <param name="services">Колекція сервісів для наповнення.</param>
    /// <returns>Та сама колекція для ланцюгових викликів.</returns>
    public static IServiceCollection AddFullMonitoringPlatformServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Інформація про платформу та рівень доступу сенсорів (для статус-бара).
        services.AddSingleton<IPlatformInfoService, PlatformInfoService>();

        // Платформова резолюція ITelemetryProvider через RuntimeInformation.IsOSPlatform.
        services.AddSingleton<ITelemetryProvider>(_ => CreateTelemetryProviderForCurrentOs());

        // Модуль процесів (кросплатформний, без прив'язки до ОС).
        services.AddSingleton<IProcessManagerService, ProcessManagerService>();

        // Порогові сповіщення з прив'язкою до активного провайдера телеметрії.
        services.AddSingleton<IAlertService>(provider => new AlertService(
            ThresholdSettings.Default,
            provider.GetRequiredService<ITelemetryProvider>(),
            TimeSpan.FromSeconds(1)));

        // Сервіс експорту зрізів телеметрії, процесів та журналу подій у формати JSON/CSV (Issue #12).
        services.AddSingleton<IExportService, ExportService>();

        // Сервіс взаємодії з нативним діалогом вибору файлу Avalonia StorageProvider (Issue #12).
        services.AddSingleton<IFileSaveDialogService, AvaloniaStorageFileSaveDialogService>();

        // ViewModels вкладок.
        services.AddTransient<OverviewViewModel>(provider => new OverviewViewModel(
            provider.GetRequiredService<ITelemetryProvider>()));
        services.AddTransient<ProcessesViewModel>(provider => new ProcessesViewModel(
            provider.GetRequiredService<IProcessManagerService>()));
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<EventsLogViewModel>(provider => new EventsLogViewModel(
            provider.GetRequiredService<IAlertService>()));

        // Головна ViewModel із статус-баром платформи.
        services.AddSingleton<MainViewModel>(provider => new MainViewModel(
            provider.GetRequiredService<OverviewViewModel>(),
            provider.GetRequiredService<ProcessesViewModel>(),
            provider.GetRequiredService<SettingsViewModel>(),
            provider.GetRequiredService<EventsLogViewModel>(),
            provider.GetRequiredService<IPlatformInfoService>(),
            provider.GetRequiredService<ITelemetryProvider>()));

        return services;
    }
}
