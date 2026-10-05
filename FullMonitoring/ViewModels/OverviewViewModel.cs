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
    public partial string ProviderName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OsDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Architecture { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MachineName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UptimeFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastUpdatedFormatted { get; set; } = string.Empty;

    // --- Показники CPU ---

    [ObservableProperty]
    public partial double CpuUsagePercentage { get; set; }

    [ObservableProperty]
    public partial string CpuUsageFormatted { get; set; } = "0.0 %";

    [ObservableProperty]
    public partial int CpuCoreCount { get; set; }

    [ObservableProperty]
    public partial string CpuTemperatureFormatted { get; set; } = "Н/Д";

    [ObservableProperty]
    public partial string CpuFanSpeedFormatted { get; set; } = "Н/Д";

    [ObservableProperty]
    public partial SystemHealthState CpuState { get; set; } = SystemHealthState.Normal;

    [ObservableProperty]
    public partial string CpuStateLabel { get; set; } = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    public partial IBrush CpuStateBrush { get; set; } = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    public partial IBrush CpuStateBackgroundBrush { get; set; } = SystemStatePalette.NormalBackgroundBrush;

    // --- Показники RAM ---

    [ObservableProperty]
    public partial double RamUsagePercentage { get; set; }

    [ObservableProperty]
    public partial string RamUsageFormatted { get; set; } = "0.0 %";

    [ObservableProperty]
    public partial string RamUsedFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial string RamFreeFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial string RamTotalFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial SystemHealthState RamState { get; set; } = SystemHealthState.Normal;

    [ObservableProperty]
    public partial string RamStateLabel { get; set; } = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    public partial IBrush RamStateBrush { get; set; } = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    public partial IBrush RamStateBackgroundBrush { get; set; } = SystemStatePalette.NormalBackgroundBrush;

    // --- Показники Disk ---

    [ObservableProperty]
    public partial double DiskUsagePercentage { get; set; }

    [ObservableProperty]
    public partial string DiskUsageFormatted { get; set; } = "0.0 %";

    [ObservableProperty]
    public partial string DiskUsedFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial string DiskFreeFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial string DiskTotalFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial int ActiveDrivesCount { get; set; }

    [ObservableProperty]
    public partial SystemHealthState DiskState { get; set; } = SystemHealthState.Normal;

    [ObservableProperty]
    public partial string DiskStateLabel { get; set; } = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    public partial IBrush DiskStateBrush { get; set; } = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    public partial IBrush DiskStateBackgroundBrush { get; set; } = SystemStatePalette.NormalBackgroundBrush;

    /// <summary>
    /// Перелік активних накопичувачів для відображення в картці Disk.
    /// </summary>
    public ObservableCollection<DriveItemMetrics> Drives { get; } = new();

    // --- Показники Network ---

    [ObservableProperty]
    public partial string NetworkRxSpeedFormatted { get; set; } = "0.0 КБ/с";

    [ObservableProperty]
    public partial string NetworkTxSpeedFormatted { get; set; } = "0.0 КБ/с";

    [ObservableProperty]
    public partial string NetworkTotalRxFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial string NetworkTotalTxFormatted { get; set; } = "0.00 ГБ";

    [ObservableProperty]
    public partial int ActiveInterfacesCount { get; set; }

    [ObservableProperty]
    public partial SystemHealthState NetworkState { get; set; } = SystemHealthState.Normal;

    [ObservableProperty]
    public partial string NetworkStateLabel { get; set; } = SystemStatePalette.GetDisplayName(SystemHealthState.Normal);

    [ObservableProperty]
    public partial IBrush NetworkStateBrush { get; set; } = SystemStatePalette.NormalBrush;

    [ObservableProperty]
    public partial IBrush NetworkStateBackgroundBrush { get; set; } = SystemStatePalette.NormalBackgroundBrush;

    /// <summary>
    /// Перелік мережевих інтерфейсів для відображення в картці Network.
    /// </summary>
    public ObservableCollection<NetworkInterfaceMetrics> NetworkInterfaces { get; } = new();

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="OverviewViewModel"/> з використанням симулятора за замовчуванням.
    /// </summary>
    public OverviewViewModel()
        : this(new MockTelemetryProvider())
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
