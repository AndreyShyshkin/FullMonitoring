using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FullMonitoring.Interfaces;
using FullMonitoring.Services;
using FullMonitoring.ViewModels;
using FullMonitoring.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FullMonitoring;

/// <summary>
/// Точка входу Avalonia-застосунку з конфігурацією DI-контейнера (Issue #9).
/// Резолюція <see cref="ITelemetryProvider"/> виконується автоматично за поточною ОС
/// через <c>RuntimeInformation.IsOSPlatform</c>: Windows, Linux або macOS — єдиний бінарний білд.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ITelemetryProvider? _telemetryProvider;
    private IAlertService? _alertService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 1. Конфігурація DI-контейнера з платформою резолюцією (Issue #9).
            var services = new ServiceCollection();
            services.AddFullMonitoringPlatformServices();
            _serviceProvider = services.BuildServiceProvider();

            // 2. Резолюція залежностей із контейнера.
            _telemetryProvider = _serviceProvider.GetRequiredService<ITelemetryProvider>();
            _alertService = _serviceProvider.GetRequiredService<IAlertService>();
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };

            // 3. Перший збір метрик — асинхронно, вже після створення вікна.
            // RefreshMetricsAsync виконує важкі виклики провайдера у пулі потоків,
            // тому навіть повільні сенсори/диски не блокують появу вікна; метод
            // перехоплює всі винятки провайдера у InfoBar, тож виклик без очікування безпечний.
            _ = mainViewModel.Overview.RefreshMetricsAsync();

            // 4. Підписка на вивільнення ресурсів при закритті (Issue #10).
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Послідовна зупинка та вивільнення фонових процесів.
        _alertService?.Stop();
        (_alertService as IDisposable)?.Dispose();
        _telemetryProvider?.Dispose();

        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }

        _serviceProvider = null;
        _telemetryProvider = null;
        _alertService = null;
    }
}
