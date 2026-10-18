using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Services;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Головна модель представлення вікна застосунку.
/// Відповідає за навігацію між вкладками (Огляд, Процеси, Налаштування) та стан робочої області (Dashboard),
/// а також за статус-бар платформи та рівня доступу до сенсорів (Issue #9).
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>
    /// Модель представлення вкладки загального огляду системи.
    /// </summary>
    public OverviewViewModel Overview { get; }

    /// <summary>
    /// Модель представлення вкладки моніторингу процесів.
    /// </summary>
    public ProcessesViewModel Processes { get; }

    /// <summary>
    /// Модель представлення вкладки налаштувань.
    /// </summary>
    public SettingsViewModel Settings { get; }

    /// <summary>
    /// Модель представлення вкладки журналу подій та сповіщень.
    /// </summary>
    public EventsLogViewModel EventsLog { get; }

    /// <summary>
    /// Сервіс інформації про платформу та рівень доступу до сенсорів (для статус-бара).
    /// </summary>
    public IPlatformInfoService PlatformInfo { get; }

    /// <summary>
    /// Активний провайдер телеметрії, розв'язаний DI-контейнером за поточною ОС.
    /// </summary>
    public ITelemetryProvider? TelemetryProvider { get; }

    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    [ObservableProperty]
    private string _currentSectionTitle = "Огляд системи";

    [ObservableProperty]
    private string _currentSectionSubtitle =
        "Зведені показники навантаження CPU, RAM, дискової підсистеми та мережі";

    [ObservableProperty]
    private bool _isOverviewSelected = true;

    [ObservableProperty]
    private bool _isProcessesSelected;

    [ObservableProperty]
    private bool _isSettingsSelected;

    [ObservableProperty]
    private bool _isEventsLogSelected;

    // --- Статус-бар платформи та рівня доступу до сенсорів (Issue #9) ---

    [ObservableProperty]
    private string _platformDisplayName = string.Empty;

    [ObservableProperty]
    private string _platformDetailsText = string.Empty;

    [ObservableProperty]
    private string _providerDisplayName = string.Empty;

    [ObservableProperty]
    private SensorAccessLevel _sensorAccessLevel = SensorAccessLevel.Restricted;

    [ObservableProperty]
    private string _sensorAccessText = string.Empty;

    [ObservableProperty]
    private bool _isSafeMode;

    [ObservableProperty]
    private string _safeModeText = string.Empty;

    [ObservableProperty]
    private string _statusBarText = string.Empty;

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="MainViewModel"/> зі стандартними дочірніми ViewModels.
    /// </summary>
    public MainViewModel()
        : this(new OverviewViewModel(), new ProcessesViewModel(), new SettingsViewModel(), new EventsLogViewModel())
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="MainViewModel"/> із заданими моделями представлення вкладок.
    /// </summary>
    public MainViewModel(
        OverviewViewModel overview,
        ProcessesViewModel processes,
        SettingsViewModel settings,
        EventsLogViewModel? eventsLog = null,
        IPlatformInfoService? platformInfo = null,
        ITelemetryProvider? telemetryProvider = null)
    {
        Overview = overview ?? throw new ArgumentNullException(nameof(overview));
        Processes = processes ?? throw new ArgumentNullException(nameof(processes));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        EventsLog = eventsLog ?? new EventsLogViewModel();
        PlatformInfo = platformInfo ?? new PlatformInfoService();
        TelemetryProvider = telemetryProvider;

        Overview.PollingIntervalMs = Settings.PollingIntervalMs;
        Overview.ApplyThemeVariant(Settings.IsDarkThemeEnabled);
        Settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.PollingIntervalMs))
            {
                Overview.PollingIntervalMs = Settings.PollingIntervalMs;
            }
            else if (args.PropertyName == nameof(SettingsViewModel.IsDarkThemeEnabled) ||
                     args.PropertyName == nameof(SettingsViewModel.SelectedTheme))
            {
                Overview.ApplyThemeVariant(Settings.IsDarkThemeEnabled);
            }
        };

        // Оновлення статус-бара при зміні обмежень сенсорів у вкладці огляду.
        Overview.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(OverviewViewModel.HasRestrictedMetrics) ||
                args.PropertyName == nameof(OverviewViewModel.IsCpuTemperatureRestricted) ||
                args.PropertyName == nameof(OverviewViewModel.IsCpuFanSpeedRestricted))
            {
                RefreshPlatformStatus();
            }
        };

        _currentViewModel = Overview;
        RefreshPlatformStatus();
    }

    /// <summary>
    /// Оновлює тексти статус-бара: поточна платформа, провайдер телеметрії,
    /// рівень доступу до сенсорів та режим Safe Mode.
    /// </summary>
    public void RefreshPlatformStatus()
    {
        PlatformDisplayName = PlatformInfo.PlatformDisplayName;
        PlatformDetailsText = $"{PlatformInfo.OsDescription} • {PlatformInfo.Architecture}";
        ProviderDisplayName = TelemetryProvider?.ProviderName
            ?? Overview.ProviderName;
        IsSafeMode = PlatformInfo.IsSafeMode;
        SafeModeText = PlatformInfo.SafeModeDisplayName;

        // Рівень доступу виводимо з фактичних обмежень вкладки огляду,
        // щоб статус-бар відповідав бейджам «Заблоковано політикою безпеки ОС».
        SensorAccessLevel level;
        bool tempRestricted = Overview.IsCpuTemperatureRestricted;
        bool fanRestricted = Overview.IsCpuFanSpeedRestricted;
        if (!Overview.IsFirstSnapshotLoaded && string.IsNullOrWhiteSpace(Overview.OsDescription))
        {
            // Знімок ще не зібрано — сенсорів фактично немає (очікування першого опитування).
            level = SensorAccessLevel.Restricted;
        }
        else if (!tempRestricted && !fanRestricted)
        {
            level = SensorAccessLevel.Full;
        }
        else if (tempRestricted && fanRestricted)
        {
            level = SensorAccessLevel.Restricted;
        }
        else
        {
            level = SensorAccessLevel.Limited;
        }

        SensorAccessLevel = level;
        SensorAccessText = PlatformInfo.GetSensorAccessDisplayName(level);
        StatusBarText = $"{PlatformInfo.GetStatusBarText()} | {SensorAccessText} | Джерело: {ProviderDisplayName}";
    }

    partial void OnCurrentViewModelChanged(ViewModelBase value)
    {
        IsOverviewSelected = ReferenceEquals(value, Overview);
        IsProcessesSelected = ReferenceEquals(value, Processes);
        IsSettingsSelected = ReferenceEquals(value, Settings);
        IsEventsLogSelected = ReferenceEquals(value, EventsLog);

        if (IsOverviewSelected)
        {
            CurrentSectionTitle = "Огляд системи";
            CurrentSectionSubtitle = "Зведені показники навантаження CPU, RAM, дискової підсистеми та мережі";
        }
        else if (IsProcessesSelected)
        {
            CurrentSectionTitle = "Активні процеси";
            CurrentSectionSubtitle = "Моніторинг споживання оперативної пам'яті та керування процесами ОС";
        }
        else if (IsSettingsSelected)
        {
            CurrentSectionTitle = "Налаштування";
            CurrentSectionSubtitle = "Конфігурація інтервалу опитування сенсорів та кольорової теми інтерфейсу";
        }
        else if (IsEventsLogSelected)
        {
            CurrentSectionTitle = "Журнал інцидентів";
            CurrentSectionSubtitle = "Історія подій перевищення порогів навантаження та температури апаратного забезпечення";
        }
    }

    /// <summary>
    /// Перемикає активну вкладку робочої області на «Огляд системи».
    /// </summary>
    [RelayCommand]
    public void NavigateToOverview()
    {
        CurrentViewModel = Overview;
    }

    /// <summary>
    /// Перемикає активну вкладку робочої області на «Активні процеси».
    /// </summary>
    [RelayCommand]
    public void NavigateToProcesses()
    {
        CurrentViewModel = Processes;
    }

    /// <summary>
    /// Перемикає активну вкладку робочої області на «Налаштування».
    /// </summary>
    [RelayCommand]
    public void NavigateToSettings()
    {
        CurrentViewModel = Settings;
    }

    /// <summary>
    /// Перемикає активну вкладку робочої області на «Журнал інцидентів».
    /// </summary>
    [RelayCommand]
    public void NavigateToEventsLog()
    {
        CurrentViewModel = EventsLog;
    }
    [ObservableProperty]
    private string _greeting = "Ласкаво просимо до FullMonitoring!";
}
