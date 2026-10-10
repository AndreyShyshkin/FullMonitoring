using System;
using System.Globalization;

namespace FullMonitoring.Models;

/// <summary>
/// Модель зареєстрованого інциденту критичного навантаження для журналу подій (Events Log) (Issue #8).
/// </summary>
public sealed record AlertIncident
{
    /// <summary>
    /// Унікальний ідентифікатор інциденту.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Часова мітка UTC моменту фіксації події перевищення ліміту.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Тип метрики телеметрії, для якої зареєстровано критичне перевищення.
    /// </summary>
    public AlertMetricType MetricType { get; init; }

    /// <summary>
    /// Рівень серйозності інциденту.
    /// </summary>
    public AlertSeverity Severity { get; init; } = AlertSeverity.Critical;

    /// <summary>
    /// Встановлене граничне значення (поріг).
    /// </summary>
    public double ThresholdValue { get; init; }

    /// <summary>
    /// Фактичне зафіксоване значення метрики в момент інциденту.
    /// </summary>
    public double ActualValue { get; init; }

    /// <summary>
    /// Тривалість безперервного перебування метрики вище порогового значення на момент генерації сповіщення.
    /// </summary>
    public TimeSpan OverloadDuration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Деталізоване текстове повідомлення про інцидент.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Форматована локальна часова мітка для відображення в UI.
    /// </summary>
    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("HH:mm:ss dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Короткий текстовий опис типу метрики.
    /// </summary>
    public string MetricDisplayName => MetricType switch
    {
        AlertMetricType.CpuUsage => "Завантаження CPU",
        AlertMetricType.CpuTemperature => "Температура CPU",
        AlertMetricType.RamUsage => "Використання RAM",
        _ => MetricType.ToString()
    };

    /// <summary>
    /// Форматоване фактичне значення з відповідною одиницею виміру.
    /// </summary>
    public string FormattedActualValue => MetricType switch
    {
        AlertMetricType.CpuTemperature => string.Create(CultureInfo.InvariantCulture, $"{ActualValue:F1} °C"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{ActualValue:F1} %")
    };

    /// <summary>
    /// Форматоване порогове значення з одиницею виміру.
    /// </summary>
    public string FormattedThresholdValue => MetricType switch
    {
        AlertMetricType.CpuTemperature => string.Create(CultureInfo.InvariantCulture, $"{ThresholdValue:F1} °C"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{ThresholdValue:F1} %")
    };

    /// <summary>
    /// Форматована тривалість перевантаження.
    /// </summary>
    public string FormattedDuration => string.Create(CultureInfo.InvariantCulture, $"{OverloadDuration.TotalSeconds:F1} с");

    public AlertIncident() { }

    public AlertIncident(
        AlertMetricType metricType,
        double actualValue,
        double thresholdValue,
        TimeSpan overloadDuration,
        AlertSeverity severity = AlertSeverity.Critical,
        DateTimeOffset? timestamp = null,
        string? message = null)
    {
        MetricType = metricType;
        ActualValue = actualValue;
        ThresholdValue = thresholdValue;
        OverloadDuration = overloadDuration;
        Severity = severity;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;

        Message = message ?? GenerateDefaultMessage(metricType, actualValue, thresholdValue, overloadDuration);
    }

    private static string GenerateDefaultMessage(
        AlertMetricType metricType,
        double actualValue,
        double thresholdValue,
        TimeSpan duration)
    {
        string metricName = metricType switch
        {
            AlertMetricType.CpuUsage => "Завантаження CPU",
            AlertMetricType.CpuTemperature => "Температура CPU",
            AlertMetricType.RamUsage => "Використання RAM",
            _ => "Показник"
        };

        string unit = metricType == AlertMetricType.CpuTemperature ? "°C" : "%";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Критичне значення {metricName}: {actualValue:F1}{unit} (поріг: {thresholdValue:F1}{unit}), триває {duration.TotalSeconds:F1} с");
    }
}
