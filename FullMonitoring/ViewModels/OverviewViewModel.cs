using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Модель представлення для вкладки загального огляду системи (Dashboard).
/// Формує зведені картки показників CPU, RAM, накопичувачів (Disk) та мережі (Network).
/// </summary>
public partial class OverviewViewModel : ViewModelBase
{
    private readonly ITelemetryProvider _telemetryProvider;

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

    // --- Показники CPU ---

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

    // --- Показники RAM ---

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
        : this(TelemetryProviderFactory.Create())
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="OverviewViewModel"/> із заданим провайдером телеметрії.
    /// </summary>
    /// <param name="telemetryProvider">Провайдер системної телеметрії.</param>
    public OverviewViewModel(ITelemetryProvider telemetryProvider)
    {
        _telemetryProvider = telemetryProvider ?? throw new ArgumentNullException(nameof(telemetryProvider));
        ProviderName = _telemetryProvider.ProviderName;
        RefreshMetrics();
    }

    /// <summary>
    /// Оновлює всі показники зведених карток (CPU, RAM, Disk, Network) зі знімка телеметрії.
    /// </summary>
    [RelayCommand]
    public void RefreshMetrics()
    {
        var snapshot = _telemetryProvider.GetSnapshot();
        ApplySnapshot(snapshot);
    }

    /// <summary>
    /// Застосовує переданий знімок телеметрії до властивостей представлення.
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
        CpuUsagePercentage = Math.Round(snapshot.Cpu.TotalUsagePercentage, 1);
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

        // RAM
        RamUsagePercentage = Math.Round(snapshot.Ram.UsedPercentage, 1);
        RamUsageFormatted = string.Create(CultureInfo.InvariantCulture, $"{RamUsagePercentage:F1} %");
        RamUsedFormatted = FormatBytesToGigabytes(snapshot.Ram.UsedBytes);
        RamFreeFormatted = FormatBytesToGigabytes(snapshot.Ram.FreeBytes);
        RamTotalFormatted = FormatBytesToGigabytes(snapshot.Ram.TotalBytes);

        RamState = SystemStatePalette.EvaluateByPercentage(RamUsagePercentage);
        RamStateLabel = SystemStatePalette.GetDisplayName(RamState);
        RamStateBrush = SystemStatePalette.GetBrush(RamState);
        RamStateBackgroundBrush = SystemStatePalette.GetBackgroundBrush(RamState);

        // Disk
        DiskUsagePercentage = Math.Round(snapshot.Disk.OverallUsedPercentage, 1);
        DiskUsageFormatted = string.Create(CultureInfo.InvariantCulture, $"{DiskUsagePercentage:F1} %");
        DiskUsedFormatted = FormatBytesToGigabytes(snapshot.Disk.UsedBytes);
        DiskFreeFormatted = FormatBytesToGigabytes(snapshot.Disk.FreeBytes);
        DiskTotalFormatted = FormatBytesToGigabytes(snapshot.Disk.TotalBytes);

        Drives.Clear();
        foreach (var drive in snapshot.Disk.Drives.Where(d => d.IsReady))
        {
            Drives.Add(drive);
        }
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

        NetworkInterfaces.Clear();
        foreach (var nic in snapshot.Network.Interfaces)
        {
            NetworkInterfaces.Add(nic);
        }
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
