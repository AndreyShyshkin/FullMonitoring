using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.Services;
using FullMonitoring.ViewModels;
using FullMonitoring.Views;

namespace FullMonitoring;

public partial class App : Application
{
    private ITelemetryProvider? _telemetryProvider;
    private IAlertService? _alertService;
    private IProcessManagerService? _processManagerService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 1. Ініціалізація провайдера телеметрії (Issue #6)
            _telemetryProvider = OperatingSystem.IsWindows()
                ? new WindowsTelemetryProvider()
                : OperatingSystem.IsLinux()
                    ? new LinuxTelemetryProvider()
                    : new BclTelemetryProvider();

            // 2. Ініціалізація допоміжних сервісів
            _alertService = new AlertService(ThresholdSettings.Default, _telemetryProvider, TimeSpan.FromSeconds(1));
            _processManagerService = new ProcessManagerService();

            // 3. Створення дочірніх ViewModels
            var overviewViewModel = new OverviewViewModel(_telemetryProvider);
            var processesViewModel = new ProcessesViewModel(_processManagerService);
            var settingsViewModel = new SettingsViewModel();
            var eventsLogViewModel = new EventsLogViewModel(_alertService);

            // 4. Створення MainViewModel із передачею всіх чотирьох підмодулів
            var mainViewModel = new MainViewModel(
                overviewViewModel,
                processesViewModel,
                settingsViewModel,
                eventsLogViewModel);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };

            // 5. Підписка на вивільнення ресурсів при закритті (Issue #10)
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Послідовна зупинка та вивільнення фонових процесів
        _alertService?.Stop();
        _alertService?.Dispose();
        _telemetryProvider?.Dispose();
    }
}