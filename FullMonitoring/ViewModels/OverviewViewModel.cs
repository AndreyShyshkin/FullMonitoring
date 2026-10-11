using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Модель представлення для вкладки загального огляду системи (Dashboard).
/// Формує зведені картки показників CPU, RAM, накопичувачів (Disk) та мережі (Network),
/// а також забезпечує візуалізацію динаміки навантаження в реальному часі через LiveCharts2 та <see cref="DispatcherTimer"/>.
/// </summary>
public partial class OverviewViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Максимальна кількість точок історії завантаження CPU (останні 60 секунд при опитуванні раз на секунду).
    /// </summary>
    public const int CpuHistoryCapacity = 60;

    /// <summary>
    /// Інтервал опитування за замовчуванням у мілісекундах (1 секунда).
    /// </summary>
    public const int DefaultPollingIntervalMs = 1000;

    /// <summary>
    /// Стандартний інтервал опитування таймера (1 секунда).
    /// </summary>
    public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromMilliseconds(DefaultPollingIntervalMs);

    // Попередньо створені пензлі SkiaSharp для уникнення алокацій у купі під час тривалого опитування
    private static readonly SolidColorPaint NormalStrokePaint =
        new(SKColor.Parse(SystemStatePalette.NormalHex)) { StrokeThickness = 2.5f };

    private static readonly SolidColorPaint WarningStrokePaint =
        new(SKColor.Parse(SystemStatePalette.WarningHex)) { StrokeThickness = 2.5f };

    private static readonly SolidColorPaint CriticalStrokePaint =
        new(SKColor.Parse(SystemStatePalette.CriticalHex)) { StrokeThickness = 2.5f };

    private static readonly SolidColorPaint NormalFillPaint =
        new(SKColor.Parse(SystemStatePalette.NormalBackgroundHex));

    private static readonly SolidColorPaint WarningFillPaint =
        new(SKColor.Parse(SystemStatePalette.WarningBackgroundHex));

    private static readonly SolidColorPaint CriticalFillPaint =
        new(SKColor.Parse(SystemStatePalette.CriticalBackgroundHex));

    private static readonly SolidColorPaint NormalSlicePaint =
        new(SKColor.Parse(SystemStatePalette.NormalHex));

    private static readonly SolidColorPaint WarningSlicePaint =
        new(SKColor.Parse(SystemStatePalette.WarningHex));

    private static readonly SolidColorPaint CriticalSlicePaint =
        new(SKColor.Parse(SystemStatePalette.CriticalHex));

    private static readonly SolidColorPaint FreeRamSlicePaint =
        new(new SKColor(128, 128, 128, 55));

    private static readonly SolidColorPaint AxisLabelsPaint =
        new(new SKColor(150, 150, 150, 210));

    private static readonly SolidColorPaint AxisSeparatorsPaint =
        new(new SKColor(128, 128, 128, 35)) { StrokeThickness = 1f };

    private readonly ITelemetryProvider _telemetryProvider;
    private readonly DispatcherTimer _pollingTimer;
    private readonly LineSeries<double> _cpuLineSeries;
    private readonly PieSeries<ObservableValue> _ramUsedPieSeries;
    private readonly PieSeries<ObservableValue> _ramFreePieSeries;
    private bool _disposed;

    [ObservableProperty]
    private string _providerName = string.Empty;

    [ObservableProperty]
    private string _osDescription = string.Empty;

    [ObservableProperty]
    private string _architecture = string.Empty;

    [ObservableProperty]
    private string _machineName = string.Empty;

    [ObservableProperty]
    private string _uptimeFormatted = string.Empty;

    [ObservableProperty]
    private string _lastUpdatedFormatted = string.Empty;

    // --- Таймер опитування (DispatcherTimer) ---

    [ObservableProperty]
    private int _pollingIntervalMs = DefaultPollingIntervalMs;

    [ObservableProperty]
    private bool _isPollingActive;

    // --- Показники та графік CPU (останні 60 секунд) ---

    [ObservableProperty]
    private double _cpuUsagePercentage;

    [ObservableProperty]
    private string _cpuUsageFormatted = "0.0 %";

    [ObservableProperty]
    private int _cpuCoreCount;

    [ObservableProperty]
    private string _cpuTemperatureFormatted = "Н/Д";

    [ObservableProperty]
    private string _cpuFanSpeedFormatted = "Н/Д";

    [ObservableProperty]
    private SystemHealthState _cpuState = SystemHealthState.Normal;

    [ObservableProperty]
    private string _cpuStateLabel = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    private IBrush _cpuStateBrush = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    private IBrush _cpuStateBackgroundBrush = SystemStatePalette.NormalBackgroundBrush;

    /// <summary>
    /// Кільцевий буфер фіксованого розміру (60 точок) для зберігання історії утилізації CPU за останні 60 секунд.
    /// </summary>
    public BoundedObservableBuffer<double> CpuUsageHistory { get; } = new(CpuHistoryCapacity);

    /// <summary>
    /// Серії лінійного графіка динаміки утилізації CPU для прив'язки до <c>CartesianChart</c>.
    /// </summary>
    public ISeries[] CpuSeries { get; }

    /// <summary>
    /// Конфігурація осі часу (X) для графіка утилізації CPU (вікно 60 секунд).
    /// </summary>
    public Axis[] CpuXAxes { get; }

    /// <summary>
    /// Конфігурація осі навантаження (Y, 0–100%) для графіка утилізації CPU.
    /// </summary>
    public Axis[] CpuYAxes { get; }

    // --- Показники та діаграма розподілу RAM ---

    [ObservableProperty]
    private double _ramUsagePercentage;

    [ObservableProperty]
    private string _ramUsageFormatted = "0.0 %";

    [ObservableProperty]
    private string _ramUsedFormatted = "0.00 ГБ";

    [ObservableProperty]
    private string _ramFreeFormatted = "0.00 ГБ";

    [ObservableProperty]
    private string _ramTotalFormatted = "0.00 ГБ";

    [ObservableProperty]
    private SystemHealthState _ramState = SystemHealthState.Normal;

    [ObservableProperty]
    private string _ramStateLabel = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    private IBrush _ramStateBrush = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    private IBrush _ramStateBackgroundBrush = SystemStatePalette.NormalBackgroundBrush;

    /// <summary>
    /// Спостережуване значення зайнятої пам'яті RAM (%) для кругової діаграми без повторних алокацій.
    /// </summary>
    public ObservableValue RamUsedChartValue { get; } = new(0);

    /// <summary>
    /// Спостережуване значення вільної пам'яті RAM (%) для кругової діаграми без повторних алокацій.
    /// </summary>
    public ObservableValue RamFreeChartValue { get; } = new(100);

    /// <summary>
    /// Серії кругової діаграми (Donut Chart) розподілу пам'яті RAM (Використано / Вільно).
    /// </summary>
    public ISeries[] RamSeries { get; }

    // --- Показники Disk ---

    [ObservableProperty]
    private double _diskUsagePercentage;

    [ObservableProperty]
    private string _diskUsageFormatted = "0.0 %";

    [ObservableProperty]
    private string _diskUsedFormatted = "0.00 ГБ";

    [ObservableProperty]
    private string _diskFreeFormatted = "0.00 ГБ";

    [ObservableProperty]
    private string _diskTotalFormatted = "0.00 ГБ";

    [ObservableProperty]
    private int _activeDrivesCount;

    [ObservableProperty]
    private SystemHealthState _diskState = SystemHealthState.Normal;

    [ObservableProperty]
    private string _diskStateLabel = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    private IBrush _diskStateBrush = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    private IBrush _diskStateBackgroundBrush = SystemStatePalette.NormalBackgroundBrush;

    /// <summary>
    /// Перелік активних накопичувачів для відображення в картці Disk.
    /// </summary>
    public ObservableCollection<DriveItemMetrics> Drives { get; } = new();

    // --- Показники Network ---

    [ObservableProperty]
    private string _networkRxSpeedFormatted = "0.0 КБ/с";

    [ObservableProperty]
    private string _networkTxSpeedFormatted = "0.0 КБ/с";

    [ObservableProperty]
    private string _networkTotalRxFormatted = "0.00 ГБ";

    [ObservableProperty]
    private string _networkTotalTxFormatted = "0.00 ГБ";

    [ObservableProperty]
    private int _activeInterfacesCount;

    [ObservableProperty]
    private SystemHealthState _networkState = SystemHealthState.Normal;

    [ObservableProperty]
    private string _networkStateLabel = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    private IBrush _networkStateBrush = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    private IBrush _networkStateBackgroundBrush = SystemStatePalette.NormalBackgroundBrush;

    /// <summary>
    /// Перелік мережевих інтерфейсів для відображення в картці Network.
    /// </summary>
    public ObservableCollection<NetworkInterfaceMetrics> NetworkInterfaces { get; } = new();

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="OverviewViewModel"/> з використанням симулятора за замовчуванням.
    /// </summary>
    public OverviewViewModel()
        : this(new MockTelemetryProvider(), autoStartPolling: true)
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="OverviewViewModel"/> із заданим провайдером телеметрії.
    /// </summary>
    /// <param name="telemetryProvider">Провайдер системної телеметрії.</param>
    /// <param name="autoStartPolling">Чи запускати <see cref="DispatcherTimer"/> автоматично після ініціалізації.</param>
    public OverviewViewModel(ITelemetryProvider telemetryProvider, bool autoStartPolling = true)
    {
        _telemetryProvider = telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));
        ProviderName = _telemetryProvider.ProviderName;

        _cpuLineSeries = new LineSeries<double>
        {
            Name = "Утилізація CPU (%)",
            Values = CpuUsageHistory,
            GeometrySize = 0,
            LineSmoothness = 0.35,
            Stroke = NormalStrokePaint,
            Fill = NormalFillPaint
        };

        CpuSeries = new ISeries[] { _cpuLineSeries };

        CpuXAxes = new[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = CpuHistoryCapacity - 1,
                TextSize = 10,
                LabelsPaint = AxisLabelsPaint,
                SeparatorsPaint = AxisSeparatorsPaint,
                ShowSeparatorLines = false,
                Labeler = value =>
                {
                    int secondsAgo = Math.Max(0, (CpuHistoryCapacity - 1) - (int)Math.Round(value));
                    return secondsAgo == 0 ? "0 с" : $"-{secondsAgo} с";
                }
            }
        };

        CpuYAxes = new[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                MinStep = 25,
                TextSize = 10,
                LabelsPaint = AxisLabelsPaint,
                SeparatorsPaint = AxisSeparatorsPaint,
                Labeler = value => string.Create(CultureInfo.InvariantCulture, $"{value:F0}%")
            }
        };

        _ramUsedPieSeries = new PieSeries<ObservableValue>
        {
            Name = "Використано",
            Values = new[] { RamUsedChartValue },
            InnerRadius = 38,
            Fill = NormalSlicePaint,
            HoverPushout = 2
        };

        _ramFreePieSeries = new PieSeries<ObservableValue>
        {
            Name = "Вільно",
            Values = new[] { RamFreeChartValue },
            InnerRadius = 38,
            Fill = FreeRamSlicePaint,
            HoverPushout = 0
        };

        RamSeries = new ISeries[] { _ramUsedPieSeries, _ramFreePieSeries };

        _pollingTimer = new DispatcherTimer(
            DefaultPollingInterval,
            DispatcherPriority.Background,
            Dispatcher.UIThread,
            OnPollingTimerTick);
        _pollingTimer.Stop();

        RefreshMetrics();

        if (autoStartPolling)
        {
            StartPolling();
        }
    }

    partial void OnPollingIntervalMsChanged(int value)
    {
        var clamped = Math.Clamp(value, SettingsViewModel.MinPollingIntervalMs, SettingsViewModel.MaxPollingIntervalMs);
        if (clamped != value)
        {
            PollingIntervalMs = clamped;
            return;
        }

        _pollingTimer.Interval = TimeSpan.FromMilliseconds(clamped);
    }

    /// <summary>
    /// Запускає періодичне опитування метрик через <see cref="DispatcherTimer"/> в UI-потоці.
    /// </summary>
    [RelayCommand]
    public void StartPolling()
    {
        if (_disposed)
        {
            return;
        }

        _pollingTimer.Interval = TimeSpan.FromMilliseconds(PollingIntervalMs);
        _pollingTimer.Start();
        IsPollingActive = _pollingTimer.IsEnabled;
    }

    /// <summary>
    /// Зупиняє періодичне опитування метрик через <see cref="DispatcherTimer"/>.
    /// </summary>
    [RelayCommand]
    public void StopPolling()
    {
        _pollingTimer.Stop();
        IsPollingActive = _pollingTimer.IsEnabled;
    }

    /// <summary>
    /// Перемикає стан активності таймера опитування метрик.
    /// </summary>
    [RelayCommand]
    public void TogglePolling()
    {
        if (IsPollingActive)
        {
            StopPolling();
        }
        else
        {
            StartPolling();
        }
    }

    /// <summary>
    /// Оновлює всі показники зведених карток (CPU, RAM, Disk, Network) та графіків зі знімка телеметрії.
    /// Якщо викликано з фонового потоку, автоматично маршалізує оновлення колекцій у UI-потік.
    /// </summary>
    [RelayCommand]
    public void RefreshMetrics()
    {
        if (_disposed)
        {
            return;
        }

        var snapshot = _telemetryProvider.GetSnapshot();
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplySnapshot(snapshot);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot), DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Застосовує переданий знімок телеметрії до властивостей представлення та буферів графіків.
    /// </summary>
    /// <param name="snapshot">Знімок системної телеметрії.</param>
    public void ApplySnapshot(SystemTelemetrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        OsDescription = string.IsNullOrWhiteSpace(snapshot.OsDescription) ? "Невідома ОС" : snapshot.OsDescription;
        Architecture = string.IsNullOrWhiteSpace(snapshot.Architecture) ? "Н/Д" : snapshot.Architecture;
        MachineName = string.IsNullOrWhiteSpace(snapshot.MachineName) ? "Локальний вузол" : snapshot.MachineName;
        UptimeFormatted = FormatUptime(snapshot.Uptime);
        LastUpdatedFormatted = $"Оновлено: {snapshot.Timestamp.ToLocalTime():HH:mm:ss}";

        // CPU
        CpuUsagePercentage = Math.Clamp(Math.Round(snapshot.Cpu.TotalUsagePercentage, 1), 0.0, 100.0);
        CpuUsageFormatted = string.Create(CultureInfo.InvariantCulture, $"{CpuUsagePercentage:F1} %");
        CpuCoreCount = snapshot.Cpu.CoreUsagesPercentage.Count > 0
            ? snapshot.Cpu.CoreUsagesPercentage.Count
            : Environment.ProcessorCount;
        CpuTemperatureFormatted = snapshot.Cpu.TemperatureCelsius.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{snapshot.Cpu.TemperatureCelsius.Value:F1} °C")
            : "Н/Д";
        CpuFanSpeedFormatted = snapshot.Cpu.FanSpeedRpm.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"{snapshot.Cpu.FanSpeedRpm.Value:F0} об/хв")
            : "Н/Д";

        var cpuLoadState = SystemStatePalette.EvaluateByPercentage(CpuUsagePercentage);
        var cpuTempState = SystemStatePalette.EvaluateByTemperature(snapshot.Cpu.TemperatureCelsius);
        CpuState = (SystemHealthState)Math.Max((int)cpuLoadState, (int)cpuTempState);
        CpuStateLabel = SystemStatePalette.GetDisplayName(CpuState);
        CpuStateBrush = SystemStatePalette.GetBrush(CpuState);
        CpuStateBackgroundBrush = SystemStatePalette.GetBackgroundBrush(CpuState);

        // Додавання нової точки в кільцевий буфер (не більше 60 точок) та оновлення кольору серії CPU
        CpuUsageHistory.Push(CpuUsagePercentage);
        UpdateCpuSeriesPalette(CpuState);

        // RAM
        RamUsagePercentage = Math.Clamp(Math.Round(snapshot.Ram.UsedPercentage, 1), 0.0, 100.0);
        RamUsageFormatted = string.Create(CultureInfo.InvariantCulture, $"{RamUsagePercentage:F1} %");
        RamUsedFormatted = FormatBytesToGigabytes(snapshot.Ram.UsedBytes);
        RamFreeFormatted = FormatBytesToGigabytes(snapshot.Ram.FreeBytes);
        RamTotalFormatted = FormatBytesToGigabytes(snapshot.Ram.TotalBytes);

        RamState = SystemStatePalette.EvaluateByPercentage(RamUsagePercentage);
        RamStateLabel = SystemStatePalette.GetDisplayName(RamState);
        RamStateBrush = SystemStatePalette.GetBrush(RamState);
        RamStateBackgroundBrush = SystemStatePalette.GetBackgroundBrush(RamState);

        // Оновлення кругової діаграми розподілу RAM in-place (без створення нових об'єктів)
        RamUsedChartValue.Value = RamUsagePercentage;
        RamFreeChartValue.Value = Math.Round(Math.Max(0.0, 100.0 - RamUsagePercentage), 1);
        UpdateRamSeriesPalette(RamState);

        // Disk
        DiskUsagePercentage = Math.Clamp(Math.Round(snapshot.Disk.OverallUsedPercentage, 1), 0.0, 100.0);
        DiskUsageFormatted = string.Create(CultureInfo.InvariantCulture, $"{DiskUsagePercentage:F1} %");
        DiskUsedFormatted = FormatBytesToGigabytes(snapshot.Disk.UsedBytes);
        DiskFreeFormatted = FormatBytesToGigabytes(snapshot.Disk.FreeBytes);
        DiskTotalFormatted = FormatBytesToGigabytes(snapshot.Disk.TotalBytes);

        SyncObservableCollection(Drives, snapshot.Disk.Drives.Where(d => d.IsReady));
        ActiveDrivesCount = Drives.Count;

        DiskState = SystemStatePalette.EvaluateByPercentage(DiskUsagePercentage);
        DiskStateLabel = SystemStatePalette.GetDisplayName(DiskState);
        DiskStateBrush = SystemStatePalette.GetBrush(DiskState);
        DiskStateBackgroundBrush = SystemStatePalette.GetBackgroundBrush(DiskState);

        // Network
        NetworkRxSpeedFormatted = FormatSpeedPerSecond(snapshot.Network.RxSpeedBytesPerSecond);
        NetworkTxSpeedFormatted = FormatSpeedPerSecond(snapshot.Network.TxSpeedBytesPerSecond);
        NetworkTotalRxFormatted = FormatBytesToGigabytes(snapshot.Network.TotalBytesReceived);
        NetworkTotalTxFormatted = FormatBytesToGigabytes(snapshot.Network.TotalBytesSent);

        SyncObservableCollection(NetworkInterfaces, snapshot.Network.Interfaces);
        ActiveInterfacesCount = NetworkInterfaces.Count;

        // Оцінка навантаження мережі відносно 100 МБ/с
        var combinedThroughputPercent = Math.Clamp(
            (snapshot.Network.RxSpeedBytesPerSecond + snapshot.Network.TxSpeedBytesPerSecond) / (100.0 * 1024 * 1024) * 100.0,
            0.0,
            100.0);
        NetworkState = SystemStatePalette.EvaluateByPercentage(combinedThroughputPercent);
        NetworkStateLabel = SystemStatePalette.GetDisplayName(NetworkState);
        NetworkStateBrush = SystemStatePalette.GetBrush(NetworkState);
        NetworkStateBackgroundBrush = SystemStatePalette.GetBackgroundBrush(NetworkState);
    }

    /// <summary>
    /// Звільняє ресурси таймера опитування.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollingTimer.Stop();
        _pollingTimer.Tick -= OnPollingTimerTick;
        IsPollingActive = false;
    }

    private void OnPollingTimerTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            RefreshMetrics();
        }
        else
        {
            Dispatcher.UIThread.Post(RefreshMetrics, DispatcherPriority.Background);
        }
    }

    private void UpdateCpuSeriesPalette(SystemHealthState state)
    {
        var targetStroke = state switch
        {
            SystemHealthState.Critical => CriticalStrokePaint,
            SystemHealthState.Warning => WarningStrokePaint,
            _ => NormalStrokePaint
        };

        var targetFill = state switch
        {
            SystemHealthState.Critical => CriticalFillPaint,
            SystemHealthState.Warning => WarningFillPaint,
            _ => NormalFillPaint
        };

        if (!ReferenceEquals(_cpuLineSeries.Stroke, targetStroke))
        {
            _cpuLineSeries.Stroke = targetStroke;
        }

        if (!ReferenceEquals(_cpuLineSeries.Fill, targetFill))
        {
            _cpuLineSeries.Fill = targetFill;
        }
    }

    private void UpdateRamSeriesPalette(SystemHealthState state)
    {
        var targetSlice = state switch
        {
            SystemHealthState.Critical => CriticalSlicePaint,
            SystemHealthState.Warning => WarningSlicePaint,
            _ => NormalSlicePaint
        };

        if (!ReferenceEquals(_ramUsedPieSeries.Fill, targetSlice))
        {
            _ramUsedPieSeries.Fill = targetSlice;
        }
    }

    private static void SyncObservableCollection<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
    {
        var items = source as System.Collections.Generic.IList<T> ?? source.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            if (i < target.Count)
            {
                target[i] = items[i];
            }
            else
            {
                target.Add(items[i]);
            }
        }

        while (target.Count > items.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static string FormatBytesToGigabytes(long bytes)
    {
        double gb = bytes / (1024.0 * 1024.0 * 1024.0);
        return string.Create(CultureInfo.InvariantCulture, $"{gb:F2} ГБ");
    }

    private static string FormatSpeedPerSecond(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024.0 * 1024.0)
        {
            double mbps = bytesPerSecond / (1024.0 * 1024.0);
            return string.Create(CultureInfo.InvariantCulture, $"{mbps:F2} МБ/с");
        }

        double kbps = bytesPerSecond / 1024.0;
        return string.Create(CultureInfo.InvariantCulture, $"{kbps:F1} КБ/с");
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays} дн {uptime.Hours:D2} год {uptime.Minutes:D2} хв";
        }

        return $"{uptime.Hours:D2} год {uptime.Minutes:D2} хв {uptime.Seconds:D2} с";
    }
}
