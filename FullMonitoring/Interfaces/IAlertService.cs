using System;
using System.Collections.Generic;
using FullMonitoring.Models;

namespace FullMonitoring.Interfaces;

/// <summary>
/// Контракт сервісу порогових сповіщень про критичне навантаження (Issue #8).
/// Відповідає за відстеження тривалості перевантаження, дебаунсинг сповіщень та ведення журналу інцидентів.
/// </summary>
public interface IAlertService : IDisposable
{
    /// <summary>
    /// Поточні параметри порогових лімітів та тривалості перевантаження.
    /// </summary>
    ThresholdSettings Settings { get; }

    /// <summary>
    /// Оновлює конфігурацію порогових лімітів.
    /// </summary>
    /// <param name="settings">Нові налаштування порогів.</param>
    void UpdateSettings(ThresholdSettings settings);

    /// <summary>
    /// Журнал подій (Events Log) усіх зафіксованих інцидентів із мітками часу.
    /// </summary>
    IReadOnlyList<AlertIncident> EventLog { get; }

    /// <summary>
    /// Подія, що виникає при фіксації нового інциденту критичного навантаження.
    /// </summary>
    event EventHandler<AlertIncident>? AlertTriggered;

    /// <summary>
    /// Подія, що виникає при нормалізації показника (поверненні нижче порогу після перевантаження).
    /// </summary>
    event EventHandler<AlertIncident>? AlertResolved;

    /// <summary>
    /// Аналізує знімок системної телеметрії на перевищення встановлених порогів.
    /// </summary>
    /// <param name="snapshot">Знімок метрик.</param>
    void ProcessSnapshot(SystemTelemetrySnapshot snapshot);

    /// <summary>
    /// Аналізує поточні числові показники навантаження CPU, температури та RAM.
    /// </summary>
    /// <param name="cpuUsagePercentage">Навантаження CPU (0..100%).</param>
    /// <param name="cpuTemperatureCelsius">Температура CPU (°C) або null.</param>
    /// <param name="ramUsagePercentage">Заповнення RAM (0..100%).</param>
    /// <param name="timestamp">Опціональна часова мітка вимірювання (якщо null — використовується поточний час).</param>
    void ProcessMetrics(
        double cpuUsagePercentage,
        double? cpuTemperatureCelsius,
        double ramUsagePercentage,
        DateTimeOffset? timestamp = null);

    /// <summary>
    /// Очищує журнал подій.
    /// </summary>
    void ClearEventLog();

    /// <summary>
    /// Отримує відфільтрований список інцидентів за типом метрики.
    /// </summary>
    /// <param name="metricType">Тип метрики.</param>
    IReadOnlyList<AlertIncident> GetIncidentsByMetric(AlertMetricType metricType);

    /// <summary>
    /// Отримує останні N інцидентів із журналу подій у зворотному хронологічному порядку.
    /// </summary>
    /// <param name="maxCount">Максимальна кількість записів.</param>
    IReadOnlyList<AlertIncident> GetRecentIncidents(int maxCount);

    /// <summary>
    /// Перевіряє, чи перебуває вказана метрика у стані активного перевантаження в поточний момент.
    /// </summary>
    /// <param name="metricType">Тип метрики.</param>
    bool HasActiveOverload(AlertMetricType metricType);

    /// <summary>
    /// Кількість метрик, які перебувають у стані активного перевантаження в поточний момент.
    /// </summary>
    int ActiveOverloadCount { get; }

    /// <summary>
    /// Максимальна кількість записів у журналі подій (для запобігання переповненню пам'яті).
    /// </summary>
    int MaxLogCapacity { get; set; }

    /// <summary>
    /// Запускає фоновий моніторинг телеметрії (якщо налаштовано провайдер телеметрії).
    /// </summary>
    void Start();

    /// <summary>
    /// Зупиняє фоновий моніторинг.
    /// </summary>
    void Stop();

    /// <summary>
    /// Вказує, чи активний фоновий моніторинг у поточний момент.
    /// </summary>
    bool IsRunning { get; }
}
