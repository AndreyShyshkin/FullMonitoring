using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Головна модель представлення вікна застосунку.
/// Відповідає за навігацію між вкладками (Огляд, Процеси, Налаштування) та стан робочої області (Dashboard).
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
        EventsLogViewModel? eventsLog = null)
    {
        Overview = overview ?? throw new ArgumentNullException(nameof(overview));
        Processes = processes ?? throw new ArgumentNullException(nameof(processes));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        EventsLog = eventsLog ?? new EventsLogViewModel();

        _currentViewModel = Overview;
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