using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Services;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Модель представлення для вкладки моніторингу та керування активними процесами ОС.
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
    public partial ProcessItem? SelectedProcess { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalProcessesCount { get; set; }

    [ObservableProperty]
    public partial string TotalRamUsageFormatted { get; set; } = "0.00 МБ";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="ProcessesViewModel"/> зі стандартним сервісом процесів.
    /// </summary>
    public ProcessesViewModel()
        : this(new ProcessManagerService())
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="ProcessesViewModel"/> із заданим сервісом процесів.
    /// </summary>
    /// <param name="processManagerService">Сервіс отримання та завершення процесів.</param>
    public ProcessesViewModel(IProcessManagerService processManagerService)
    {
        _processManagerService = processManagerService ?? throw new ArgumentNullException(nameof(processManagerService));
        RefreshProcesses();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Оновлює таблицю активних процесів (топ-50 за споживанням оперативної пам'яті).
    /// </summary>
    [RelayCommand]
    public void RefreshProcesses()
    {
        _allProcesses = _processManagerService.GetRunningProcesses(50);
        ApplyFilter();
        StatusMessage = $"Завантажено процесів: {Processes.Count} (оновлено о {DateTimeOffset.Now:HH:mm:ss})";
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
        bool killed = _processManagerService.KillProcess(target.Pid);

        if (killed)
        {
            RefreshProcesses();
            StatusMessage = $"Процес «{target.Name}» (PID: {target.Pid}) успішно завершено.";
        }
        else
        {
            StatusMessage = $"Не вдалося завершити процес «{target.Name}» (PID: {target.Pid}): недостатньо прав доступу.";
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
