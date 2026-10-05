using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
        StatusMessage = "Усі налаштування повернуто до значень замовчуванням.";
    }
}
