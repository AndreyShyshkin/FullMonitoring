using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FullMonitoring.Models;

namespace FullMonitoring.ViewModels;

/// <summary>
/// Модель представлення для вкладки налаштувань застосунку (інтервал опитування та вибір теми оформлення).
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    /// <summary>
    /// Мінімально допустимий інтервал опитування сенсорів у мілісекундах.
    /// </summary>
    public const int MinPollingIntervalMs = 250;

    /// <summary>
    /// Максимально допустимий інтервал опитування сенсорів у мілісекундах.
    /// </summary>
    public const int MaxPollingIntervalMs = 10000;

    /// <summary>
    /// Інтервал опитування за замовчуванням у мілісекундах (1 секунда).
    /// </summary>
    public const int DefaultPollingIntervalMs = 1000;

    /// <summary>
    /// Доступні варіанти інтервалу опитування (мс).
    /// </summary>
    public IReadOnlyList<int> AvailablePollingIntervalsMs { get; } = new[] { 500, 1000, 2000, 5000 };

    /// <summary>
    /// Доступні режими кольорової теми інтерфейсу.
    /// </summary>
    public IReadOnlyList<string> AvailableThemes { get; } = new[] { "Системна", "Світла", "Темна" };

    [ObservableProperty]
    private int _pollingIntervalMs = DefaultPollingIntervalMs;

    [ObservableProperty]
    private string _pollingIntervalFormatted = "1000 мс (1.0 с)";

    [ObservableProperty]
    private string _selectedTheme = "Системна";

    [ObservableProperty]
    private bool _isDarkThemeEnabled;

    [ObservableProperty]
    private string _statusMessage = "Налаштування за замовчуванням активні.";

    // --- Порогові значення сповіщень про критичне навантаження (Issue #8) ---

    [ObservableProperty]
    private double _cpuThresholdPercentage = ThresholdSettings.DefaultCpuUsageThresholdPercentage;

    [ObservableProperty]
    private double _cpuTemperatureThresholdCelsius = ThresholdSettings.DefaultCpuTemperatureThresholdCelsius;

    [ObservableProperty]
    private double _ramThresholdPercentage = ThresholdSettings.DefaultRamUsageThresholdPercentage;

    [ObservableProperty]
    private double _durationThresholdSeconds = ThresholdSettings.DefaultDurationThresholdSeconds;

    /// <summary>
    /// Отримує актуальний знімок конфігурації порогів сповіщень.
    /// </summary>
    public ThresholdSettings CurrentThresholdSettings =>
        new(CpuThresholdPercentage, CpuTemperatureThresholdCelsius, RamThresholdPercentage, DurationThresholdSeconds);

    partial void OnCpuThresholdPercentageChanged(double value)
    {
        var clamped = Math.Clamp(value, 1.0, 100.0);
        if (Math.Abs(clamped - value) > 0.001)
        {
            CpuThresholdPercentage = clamped;
            return;
        }
        StatusMessage = $"Поріг завантаження CPU встановлено: {clamped:F0}%.";
    }

    partial void OnCpuTemperatureThresholdCelsiusChanged(double value)
    {
        var clamped = Math.Clamp(value, 30.0, 120.0);
        if (Math.Abs(clamped - value) > 0.001)
        {
            CpuTemperatureThresholdCelsius = clamped;
            return;
        }
        StatusMessage = $"Поріг температури CPU встановлено: {clamped:F0}°C.";
    }

    partial void OnRamThresholdPercentageChanged(double value)
    {
        var clamped = Math.Clamp(value, 1.0, 100.0);
        if (Math.Abs(clamped - value) > 0.001)
        {
            RamThresholdPercentage = clamped;
            return;
        }
        StatusMessage = $"Поріг використання RAM встановлено: {clamped:F0}%.";
    }

    partial void OnDurationThresholdSecondsChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.5, 60.0);
        if (Math.Abs(clamped - value) > 0.001)
        {
            DurationThresholdSeconds = clamped;
            return;
        }
        StatusMessage = $"Мінімальну тривалість перевантаження встановлено: {clamped:F1} с.";
    }

    partial void OnPollingIntervalMsChanged(int value)
    {
        var clamped = Math.Clamp(value, MinPollingIntervalMs, MaxPollingIntervalMs);
        if (clamped != value)
        {
            PollingIntervalMs = clamped;
            return;
        }

        double seconds = clamped / 1000.0;
        PollingIntervalFormatted = string.Create(CultureInfo.InvariantCulture, $"{clamped} мс ({seconds:F1} с)");
        StatusMessage = $"Інтервал опитування встановлено: {PollingIntervalFormatted}.";
    }

    partial void OnSelectedThemeChanged(string value)
    {
        IsDarkThemeEnabled = string.Equals(value, "Темна", StringComparison.OrdinalIgnoreCase);
        StatusMessage = $"Обрано тему оформлення: {value}.";
    }

    partial void OnIsDarkThemeEnabledChanged(bool value)
    {
        var targetTheme = value ? "Темна" : "Світла";
        if (!string.Equals(SelectedTheme, targetTheme, StringComparison.OrdinalIgnoreCase) &&
            !(value == false && string.Equals(SelectedTheme, "Системна", StringComparison.OrdinalIgnoreCase)))
        {
            SelectedTheme = targetTheme;
        }
    }

    /// <summary>
    /// Встановлює світлу тему оформлення.
    /// </summary>
    [RelayCommand]
    public void SetLightTheme()
    {
        SelectedTheme = "Світла";
    }

    /// <summary>
    /// Встановлює темну тему оформлення.
    /// </summary>
    [RelayCommand]
    public void SetDarkTheme()
    {
        SelectedTheme = "Темна";
    }

    /// <summary>
    /// Встановлює системну тему оформлення.
    /// </summary>
    [RelayCommand]
    public void SetSystemTheme()
    {
        SelectedTheme = "Системна";
    }

    /// <summary>
    /// Скидає параметри опитування та теми до значень за замовчуванням.
    /// </summary>
    [RelayCommand]
    public void ResetDefaults()
    {
        PollingIntervalMs = DefaultPollingIntervalMs;
        SelectedTheme = "Системна";
        CpuThresholdPercentage = ThresholdSettings.DefaultCpuUsageThresholdPercentage;
        CpuTemperatureThresholdCelsius = ThresholdSettings.DefaultCpuTemperatureThresholdCelsius;
        RamThresholdPercentage = ThresholdSettings.DefaultRamUsageThresholdPercentage;
        DurationThresholdSeconds = ThresholdSettings.DefaultDurationThresholdSeconds;
        StatusMessage = "Усі налаштування повернуто до значень замовчуванням.";
    }
}
