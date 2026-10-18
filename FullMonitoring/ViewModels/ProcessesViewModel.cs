using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Services;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Модель представлення для вкладки моніторингу та керування активними процесами ОС.
/// Підтримує індикатор первинного завантаження, підказку блокування політикою безпеки ОС
/// та ненав'язливе виведення помилок провайдера (InfoBar) без падіння вікна (Issue #3, Issue #11).
/// </summary>
public partial class ProcessesViewModel : ViewModelBase
{
    private readonly IProcessManagerService _processManagerService;
    private IReadOnlyList<ProcessItem> _allProcesses = Array.Empty<ProcessItem>();

    /// <summary>
    /// Колекція процесів, відфільтрована відповідно до пошукового запиту для відображення у таблиці.
    /// </summary>
    public ObservableCollection<ProcessItem> Processes { get; } = new();

    [ObservableProperty]
    private ProcessItem? _selectedProcess;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private int _totalProcessesCount;

    [ObservableProperty]
    private string _totalRamUsageFormatted = "0.00 МБ";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isInitialLoading = true;

    [ObservableProperty]
    private bool _hasProviderError;

    private readonly IExportService _exportService;
    private readonly IFileSaveDialogService _fileSaveDialogService;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string _providerErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _isLastActionRestrictedByOs;

    /// <summary>
    /// Стандартний текст візуальної підказки для дій, доступ до яких обмежено правами ОС.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "Exposed as instance property for Avalonia XAML DataBinding")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Exposed as instance property for Avalonia XAML DataBinding")]
    public string OsSecurityRestrictionText => SystemStatePalette.OsSecurityPolicyRestrictedText;

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="ProcessesViewModel"/> зі стандартним сервісом процесів.
    /// </summary>
    public ProcessesViewModel()
        : this(new ProcessManagerService(), deferInitialCollection: false)
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="ProcessesViewModel"/> із заданим сервісом процесів.
    /// </summary>
    public ProcessesViewModel(IProcessManagerService processManagerService, bool deferInitialCollection = false)
        : this(processManagerService, new ExportService(), new AvaloniaStorageFileSaveDialogService(), deferInitialCollection)
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="ProcessesViewModel"/> із заданим сервісом процесів та сервісами експорту.
    /// </summary>
    /// <param name="processManagerService">Сервіс отримання та завершення процесів.</param>
    /// <param name="exportService">Сервіс експорту даних у JSON/CSV.</param>
    /// <param name="fileSaveDialogService">Сервіс виклику системного діалогу збереження файлу.</param>
    /// <param name="deferInitialCollection">Чи відкласти первинне завантаження списку процесів.</param>
    public ProcessesViewModel(
        IProcessManagerService processManagerService,
        IExportService exportService,
        IFileSaveDialogService fileSaveDialogService,
        bool deferInitialCollection = false)
    {
        _processManagerService = processManagerService ?? throw new ArgumentNullException(nameof(processManagerService));
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
        _fileSaveDialogService = fileSaveDialogService ?? throw new ArgumentNullException(nameof(fileSaveDialogService));

        if (!deferInitialCollection)
        {
            RefreshProcesses();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Асинхронно експортує список процесів у формат CSV (RFC 4180) через системний діалог збереження.
    /// </summary>
    [RelayCommand]
    public async Task ExportToCsvAsync()
    {
        await ExportProcessesInternalAsync(ExportFormatOption.Csv);
    }

    /// <summary>
    /// Асинхронно експортує список процесів у формат JSON через системний діалог збереження.
    /// </summary>
    [RelayCommand]
    public async Task ExportToJsonAsync()
    {
        await ExportProcessesInternalAsync(ExportFormatOption.Json);
    }

    /// <summary>
    /// Експортує список процесів за безпосереднім шляхом до файлу.
    /// </summary>
    public async Task ExportToFileAsync(string filePath, ExportFormatOption format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        IsExporting = true;
        try
        {
            var dataToExport = Processes.Count > 0 ? (IEnumerable<ProcessItem>)Processes : _allProcesses;
            if (format == ExportFormatOption.Csv)
            {
                await _exportService.ExportToCsvAsync(filePath, dataToExport).ConfigureAwait(false);
            }
            else
            {
                await _exportService.ExportProcessesToJsonAsync(filePath, dataToExport).ConfigureAwait(false);
            }
            StatusMessage = $"Процеси успішно збережено: {System.IO.Path.GetFileName(filePath)}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async Task ExportProcessesInternalAsync(ExportFormatOption format)
    {
        try
        {
            string ext = format == ExportFormatOption.Csv ? "csv" : "json";
            string title = format == ExportFormatOption.Csv ? "Експорт процесів у CSV" : "Експорт процесів у JSON";
            string fileName = $"processes_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";

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
            HasProviderError = true;
            ProviderErrorMessage = $"Помилка експорту списку процесів: {ex.Message}";
        }
    }

    /// <summary>
    /// Оновлює таблицю активних процесів (топ-50 за споживанням оперативної пам'яті).
    /// У разі винятку провайдера формує ненав'язливе повідомлення InfoBar.
    /// </summary>
    [RelayCommand]
    public void RefreshProcesses()
    {
        try
        {
            _allProcesses = _processManagerService.GetRunningProcesses(50);
            ApplyFilter();
            IsInitialLoading = false;
            StatusMessage = $"Завантажено процесів: {Processes.Count} (оновлено о {DateTimeOffset.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            IsInitialLoading = false;
            HasProviderError = true;
            ProviderErrorMessage = $"Помилка отримання списку процесів: {ex.Message}";
            StatusMessage = "Не вдалося оновити список процесів.";
        }
    }

    /// <summary>
    /// Закриває ненав'язливе повідомлення про помилку провайдера процесів (InfoBar).
    /// </summary>
    [RelayCommand]
    public void DismissProviderError()
    {
        HasProviderError = false;
        ProviderErrorMessage = string.Empty;
    }

    /// <summary>
    /// Завершує обраний у таблиці процес за його PID.
    /// </summary>
    [RelayCommand]
    public void KillSelectedProcess()
    {
        if (SelectedProcess is null)
        {
            StatusMessage = "Оберіть процес у таблиці для його завершення.";
            return;
        }

        var target = SelectedProcess;
        try
        {
            bool killed = _processManagerService.KillProcess(target.Pid);

            if (killed)
            {
                IsLastActionRestrictedByOs = false;
                RefreshProcesses();
                StatusMessage = $"Процес «{target.Name}» (PID: {target.Pid}) успішно завершено.";
            }
            else
            {
                IsLastActionRestrictedByOs = true;
                StatusMessage = $"{SystemStatePalette.OsSecurityPolicyRestrictedText}: не вдалося завершити процес «{target.Name}» (PID: {target.Pid}).";
            }
        }
        catch (Exception ex)
        {
            HasProviderError = true;
            ProviderErrorMessage = $"Збій при спробі завершення процесу «{target.Name}» (PID: {target.Pid}): {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        IEnumerable<ProcessItem> filtered = _allProcesses;

        if (!string.IsNullOrEmpty(query))
        {
            filtered = _allProcesses.Where(p =>
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal));
        }

        var list = filtered.ToList();
        Processes.Clear();
        double sumMb = 0;

        foreach (var item in list)
        {
            Processes.Add(item);
            sumMb += item.RamUsageMb;
        }

        TotalProcessesCount = Processes.Count;
        TotalRamUsageFormatted = string.Create(CultureInfo.InvariantCulture, $"{sumMb:F2} МБ");
    }
}
