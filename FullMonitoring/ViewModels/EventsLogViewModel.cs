using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Services;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Модель представлення для вкладки журналу подій та порогових сповіщень (Issue #8, Issue #12).
/// Відповідає за відображення списку зафіксованих інцидентів, фільтрацію, очищення та експорт журналу у JSON/CSV.
/// </summary>
public partial class EventsLogViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;
    private readonly IExportService _exportService;
    private readonly IFileSaveDialogService _fileSaveDialogService;
    private readonly SynchronizationContext? _syncContext;
    private readonly List<AlertIncident> _allIncidents = new();

    /// <summary>
    /// Колекція інцидентів, відфільтрована відповідно до обраної категорії, для відображення у списку.
    /// </summary>
    public ObservableCollection<AlertIncident> Incidents { get; } = new();

    /// <summary>
    /// Доступні варіанти фільтрації журналу подій.
    /// </summary>
    public IReadOnlyList<string> AvailableFilters { get; } = new[] { "Всі", "CPU", "Температура", "RAM" };

    [ObservableProperty]
    private string _selectedFilter = "Всі";

    [ObservableProperty]
    private int _totalIncidentsCount;

    [ObservableProperty]
    private int _criticalIncidentsCount;

    [ObservableProperty]
    private int _warningIncidentsCount;

    [ObservableProperty]
    private int _activeOverloadsCount;

    [ObservableProperty]
    private string _statusMessage = "Журнал подій готовий до роботи.";

    [ObservableProperty]
    private bool _hasIncidents;

    [ObservableProperty]
    private bool _isExporting;

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="EventsLogViewModel"/> зі стандартним сервісом сповіщень.
    /// </summary>
    public EventsLogViewModel()
        : this(new AlertService(), new ExportService(), new AvaloniaStorageFileSaveDialogService())
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="EventsLogViewModel"/> із заданим сервісом сповіщень.
    /// </summary>
    /// <param name="alertService">Сервіс порогових сповіщень та журналу інцидентів.</param>
    public EventsLogViewModel(IAlertService alertService)
        : this(alertService, new ExportService(), new AvaloniaStorageFileSaveDialogService())
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="EventsLogViewModel"/> із заданим сервісом сповіщень та сервісами експорту.
    /// </summary>
    /// <param name="alertService">Сервіс порогових сповіщень та журналу інцидентів.</param>
    /// <param name="exportService">Сервіс експорту журналу у JSON/CSV.</param>
    /// <param name="fileSaveDialogService">Сервіс системного діалогу вибору файлу.</param>
    public EventsLogViewModel(
        IAlertService alertService,
        IExportService exportService,
        IFileSaveDialogService fileSaveDialogService)
    {
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _fileSaveDialogService = fileSaveDialogService ?? throw new ArgumentNullException(nameof(fileSaveDialogService));
        _syncContext = SynchronizationContext.Current;

        _alertService.AlertTriggered += OnAlertTriggered;
        _alertService.AlertResolved += OnAlertResolved;

        RefreshLog();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        ApplyFilter();
    }

    private void OnAlertTriggered(object? sender, AlertIncident incident)
    {
        void UpdateAction()
        {
            _allIncidents.Insert(0, incident);
            UpdateStatistics();
            ApplyFilter();
            StatusMessage = $"Новий інцидент: {incident.Message} ({incident.FormattedTimestamp})";
        }

        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => UpdateAction(), null);
        }
        else
        {
            UpdateAction();
        }
    }

    private void OnAlertResolved(object? sender, AlertIncident incident)
    {
        void UpdateAction()
        {
            ActiveOverloadsCount = _alertService.ActiveOverloadCount;
            StatusMessage = $"Нормалізовано: {incident.MetricDisplayName} ({incident.FormattedTimestamp})";
        }

        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => UpdateAction(), null);
        }
        else
        {
            UpdateAction();
        }
    }

    /// <summary>
    /// Оновлює список інцидентів з сервісу сповіщень.
    /// </summary>
    [RelayCommand]
    public void RefreshLog()
    {
        _allIncidents.Clear();
        var fromService = _alertService.EventLog;
        for (int i = fromService.Count - 1; i >= 0; i--)
        {
            _allIncidents.Add(fromService[i]);
        }

        UpdateStatistics();
        ApplyFilter();

        StatusMessage = _allIncidents.Count == 0
            ? "Журнал подій порожній. Інцидентів перевантаження не зафіксовано."
            : $"Завантажено інцидентів: {_allIncidents.Count} (оновлено о {DateTimeOffset.Now:HH:mm:ss})";
    }

    /// <summary>
    /// Очищує весь журнал подій.
    /// </summary>
    [RelayCommand]
    public void ClearLog()
    {
        _alertService.ClearEventLog();
        _allIncidents.Clear();
        Incidents.Clear();
        UpdateStatistics();
        HasIncidents = false;
        StatusMessage = "Журнал подій успішно очищено.";
    }

    /// <summary>
    /// Встановлює фільтр за типом метрики.
    /// </summary>
    /// <param name="filter">Назва фільтра ("Всі", "CPU", "Температура", "RAM").</param>
    [RelayCommand]
    public void SetFilter(string filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            SelectedFilter = filter;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<AlertIncident> filtered = _allIncidents;

        if (string.Equals(SelectedFilter, "CPU", StringComparison.OrdinalIgnoreCase))
        {
            filtered = _allIncidents.Where(i => i.MetricType == AlertMetricType.CpuUsage);
        }
        else if (string.Equals(SelectedFilter, "Температура", StringComparison.OrdinalIgnoreCase))
        {
            filtered = _allIncidents.Where(i => i.MetricType == AlertMetricType.CpuTemperature);
        }
        else if (string.Equals(SelectedFilter, "RAM", StringComparison.OrdinalIgnoreCase))
        {
            filtered = _allIncidents.Where(i => i.MetricType == AlertMetricType.RamUsage);
        }

        Incidents.Clear();
        foreach (var item in filtered)
        {
            Incidents.Add(item);
        }

        HasIncidents = Incidents.Count > 0;
    }

    /// <summary>
    /// Асинхронно експортує журнал подій у формат CSV (RFC 4180) через системний діалог збереження.
    /// </summary>
    [RelayCommand]
    public async Task ExportToCsvAsync()
    {
        await ExportEventsInternalAsync(ExportFormatOption.Csv);
    }

    /// <summary>
    /// Асинхронно експортує журнал подій у формат JSON через системний діалог збереження.
    /// </summary>
    [RelayCommand]
    public async Task ExportToJsonAsync()
    {
        await ExportEventsInternalAsync(ExportFormatOption.Json);
    }

    /// <summary>
    /// Експортує журнал подій за безпосереднім шляхом до файлу.
    /// </summary>
    public async Task ExportToFileAsync(string filePath, ExportFormatOption format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        IsExporting = true;
        try
        {
            var dataToExport = Incidents.Count > 0 ? (IEnumerable<AlertIncident>)Incidents : _allIncidents;
            if (format == ExportFormatOption.Csv)
            {
                await _exportService.ExportToCsvAsync(filePath, dataToExport).ConfigureAwait(false);
            }
            else
            {
                await _exportService.ExportIncidentsToJsonAsync(filePath, dataToExport).ConfigureAwait(false);
            }
            StatusMessage = $"Журнал подій успішно збережено: {Path.GetFileName(filePath)}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async Task ExportEventsInternalAsync(ExportFormatOption format)
    {
        try
        {
            string ext = format == ExportFormatOption.Csv ? "csv" : "json";
            string title = format == ExportFormatOption.Csv ? "Експорт журналу подій у CSV" : "Експорт журналу подій у JSON";
            string fileName = $"events_log_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";

            var options = new FileSaveDialogOptions
            {
                Title = title,
                DefaultExtension = ext,
                SuggestedFileName = fileName,
                Format = format
            };

            string? chosenPath = await _fileSaveDialogService.ShowSaveFileDialogAsync(options);
            if (string.IsNullOrWhiteSpace(chosenPath))
            {
                return;
            }

            await ExportToFileAsync(chosenPath, format);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Помилка експорту журналу подій: {ex.Message}";
        }
    }

    private void UpdateStatistics()
    {
        TotalIncidentsCount = _allIncidents.Count;
        CriticalIncidentsCount = _allIncidents.Count(i => i.Severity == AlertSeverity.Critical);
        WarningIncidentsCount = _allIncidents.Count(i => i.Severity == AlertSeverity.Warning);
        ActiveOverloadsCount = _alertService.ActiveOverloadCount;
        HasIncidents = _allIncidents.Count > 0;
    }
}
