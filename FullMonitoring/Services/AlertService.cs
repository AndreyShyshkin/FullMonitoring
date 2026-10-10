using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;

namespace FullMonitoring.Services;

/// <summary>
/// Фоновий сервіс аналізу метрик, порогових сповіщень та журналу інцидентів (Issue #8).
/// Реалізує відстеження тривалості перевантаження (> N секунд), захист від дублювання (debouncing)
/// та збереження історії інцидентів.
/// </summary>
public class AlertService : IAlertService
{
    private readonly object _syncLock = new();
    private readonly List<AlertIncident> _eventLog = new();
    private readonly ITelemetryProvider? _telemetryProvider;
    private readonly TimeSpan _pollingInterval;

    private ThresholdSettings _settings;
    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    private bool _isDisposed;

    // Стан відстеження для кожної метрики:
    private readonly MetricTracker _cpuTracker = new(AlertMetricType.CpuUsage);
    private readonly MetricTracker _tempTracker = new(AlertMetricType.CpuTemperature);
    private readonly MetricTracker _ramTracker = new(AlertMetricType.RamUsage);

    /// <summary>
    /// Поточні конфігураційні параметри порогів та тривалості перевантаження.
    /// </summary>
    public ThresholdSettings Settings
    {
        get
        {
            lock (_syncLock)
            {
                return _settings;
            }
        }
    }

    /// <summary>
    /// Журнал подій усіх зафіксованих інцидентів (потокобезпечна копія списку).
    /// </summary>
    public IReadOnlyList<AlertIncident> EventLog
    {
        get
        {
            lock (_syncLock)
            {
                return _eventLog.ToArray();
            }
        }
    }

    /// <summary>
    /// Подія, що виникає при реєстрації нового інциденту критичного навантаження.
    /// </summary>
    public event EventHandler<AlertIncident>? AlertTriggered;

    /// <summary>
    /// Подія, що виникає при нормалізації показника (поверненні в норму після перевантаження).
    /// </summary>
    public event EventHandler<AlertIncident>? AlertResolved;

    /// <summary>
    /// Вказує, чи активний фоновий моніторинг.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_syncLock)
            {
                return _backgroundTask != null && !_backgroundTask.IsCompleted;
            }
        }
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="AlertService"/> із параметрами за замовчуванням.
    /// </summary>
    public AlertService()
        : this(ThresholdSettings.Default)
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="AlertService"/> із заданими налаштуваннями.
    /// </summary>
    /// <param name="settings">Конфігурація порогів.</param>
    public AlertService(ThresholdSettings settings)
        : this(settings, null, TimeSpan.FromSeconds(1))
    {
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="AlertService"/> із підтримкою автоматичного фонового опитування провайдера.
    /// </summary>
    /// <param name="settings">Конфігурація порогів.</param>
    /// <param name="telemetryProvider">Провайдер телеметрії (опціонально).</param>
    /// <param name="pollingInterval">Інтервал фонового опитування.</param>
    public AlertService(
        ThresholdSettings settings,
        ITelemetryProvider? telemetryProvider,
        TimeSpan pollingInterval)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _telemetryProvider = telemetryProvider;
        _pollingInterval = pollingInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : pollingInterval;
    }

    /// <summary>
    /// Оновлює конфігурацію порогових лімітів.
    /// </summary>
    public void UpdateSettings(ThresholdSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_syncLock)
        {
            _settings = settings;
        }
    }

    private int _maxLogCapacity = 500;

    /// <summary>
    /// Максимальна кількість записів у журналі подій (за замовчуванням 500).
    /// </summary>
    public int MaxLogCapacity
    {
        get
        {
            lock (_syncLock)
            {
                return _maxLogCapacity;
            }
        }
        set
        {
            lock (_syncLock)
            {
                _maxLogCapacity = Math.Max(10, value);
                TrimLogIfNeeded();
            }
        }
    }

    /// <summary>
    /// Кількість метрик, які перебувають у стані активного перевантаження в поточний момент.
    /// </summary>
    public int ActiveOverloadCount
    {
        get
        {
            lock (_syncLock)
            {
                int count = 0;
                if (_cpuTracker.AlertFired) count++;
                if (_tempTracker.AlertFired) count++;
                if (_ramTracker.AlertFired) count++;
                return count;
            }
        }
    }

    /// <summary>
    /// Перевіряє, чи перебуває вказана метрика у стані активного перевантаження.
    /// </summary>
    public bool HasActiveOverload(AlertMetricType metricType)
    {
        lock (_syncLock)
        {
            return metricType switch
            {
                AlertMetricType.CpuUsage => _cpuTracker.AlertFired,
                AlertMetricType.CpuTemperature => _tempTracker.AlertFired,
                AlertMetricType.RamUsage => _ramTracker.AlertFired,
                _ => false
            };
        }
    }

    /// <summary>
    /// Отримує список інцидентів, відфільтрований за типом метрики.
    /// </summary>
    public IReadOnlyList<AlertIncident> GetIncidentsByMetric(AlertMetricType metricType)
    {
        lock (_syncLock)
        {
            var result = new List<AlertIncident>();
            foreach (var incident in _eventLog)
            {
                if (incident.MetricType == metricType)
                {
                    result.Add(incident);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Отримує останні N інцидентів у зворотному хронологічному порядку.
    /// </summary>
    public IReadOnlyList<AlertIncident> GetRecentIncidents(int maxCount)
    {
        if (maxCount <= 0)
            return Array.Empty<AlertIncident>();

        lock (_syncLock)
        {
            int count = Math.Min(maxCount, _eventLog.Count);
            var result = new List<AlertIncident>(count);
            for (int i = _eventLog.Count - 1; i >= _eventLog.Count - count; i--)
            {
                result.Add(_eventLog[i]);
            }
            return result;
        }
    }

    private void TrimLogIfNeeded()
    {
        while (_eventLog.Count > _maxLogCapacity)
        {
            _eventLog.RemoveAt(0);
        }
    }

    /// <summary>
    /// Очищує журнал подій.
    /// </summary>
    public void ClearEventLog()
    {
        lock (_syncLock)
        {
            _eventLog.Clear();
        }
    }

    /// <summary>
    /// Аналізує знімок системної телеметрії на перевищення встановлених порогів.
    /// </summary>
    public void ProcessSnapshot(SystemTelemetrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ProcessMetrics(
            snapshot.Cpu.TotalUsagePercentage,
            snapshot.Cpu.TemperatureCelsius,
            snapshot.Ram.UsedPercentage,
            snapshot.Timestamp);
    }

    /// <summary>
    /// Аналізує поточні числові показники навантаження CPU, температури та RAM.
    /// </summary>
    public void ProcessMetrics(
        double cpuUsagePercentage,
        double? cpuTemperatureCelsius,
        double ramUsagePercentage,
        DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? DateTimeOffset.UtcNow;
        ThresholdSettings currentSettings;

        lock (_syncLock)
        {
            currentSettings = _settings;
        }

        // Аналіз трьох ключових показників із debouncing та відстеженням тривалості:
        EvaluateMetric(
            _cpuTracker,
            cpuUsagePercentage,
            currentSettings.CpuUsageThresholdPercentage,
            currentSettings.DurationThresholdSeconds,
            now);

        EvaluateMetric(
            _tempTracker,
            cpuTemperatureCelsius,
            currentSettings.CpuTemperatureThresholdCelsius,
            currentSettings.DurationThresholdSeconds,
            now);

        EvaluateMetric(
            _ramTracker,
            ramUsagePercentage,
            currentSettings.RamUsageThresholdPercentage,
            currentSettings.DurationThresholdSeconds,
            now);
    }

    private void EvaluateMetric(
        MetricTracker tracker,
        double? actualValue,
        double threshold,
        double durationThresholdSeconds,
        DateTimeOffset now)
    {
        if (!actualValue.HasValue)
        {
            // Якщо сенсор не повернув значення (наприклад, температура недоступна), стан скидається.
            ResetTracker(tracker, now);
            return;
        }

        double val = actualValue.Value;
        bool isExceeding = val > threshold;

        AlertIncident? incidentToFire = null;
        AlertIncident? resolutionToFire = null;

        lock (_syncLock)
        {
            if (isExceeding)
            {
                // Показник перевищує поріг
                if (!tracker.OverloadStartTime.HasValue)
                {
                    // Початок нового періоду перевантаження
                    tracker.OverloadStartTime = now;
                }

                var elapsed = now - tracker.OverloadStartTime.Value;
                if (elapsed.TotalSeconds < 0)
                {
                    // Захист від немонотонного часу
                    tracker.OverloadStartTime = now;
                    elapsed = TimeSpan.Zero;
                }

                // Перевіряємо умову тривалості (>= N секунд) та умову debouncing (!AlertFired):
                // Сповіщення генерується ЛИШЕ якщо перевантаження триває довше порогу,
                // і НЕ дублюється щосекунди, поки показник вище ліміту.
                if (elapsed.TotalSeconds >= durationThresholdSeconds && !tracker.AlertFired)
                {
                    tracker.AlertFired = true;

                    var incident = new AlertIncident(
                        tracker.MetricType,
                        val,
                        threshold,
                        elapsed,
                        AlertSeverity.Critical,
                        now);

                    _eventLog.Add(incident);
                    TrimLogIfNeeded();
                    incidentToFire = incident;
                }
            }
            else
            {
                // Показник повернувся в межі норми (<= threshold)
                if (tracker.AlertFired)
                {
                    // Було зафіксовано попереднє перевантаження, створюємо подію нормалізації
                    var totalDuration = tracker.OverloadStartTime.HasValue
                        ? now - tracker.OverloadStartTime.Value
                        : TimeSpan.Zero;

                    resolutionToFire = new AlertIncident(
                        tracker.MetricType,
                        val,
                        threshold,
                        totalDuration,
                        AlertSeverity.Warning,
                        now,
                        $"{tracker.MetricType} нормалізовано: {val:F1} (поріг: {threshold:F1})");
                }

                // Скидаємо стан відстеження
                tracker.OverloadStartTime = null;
                tracker.AlertFired = false;
            }
        }

        // Виклик подій поза межами lock для уникнення deadlocks
        if (incidentToFire != null)
        {
            AlertTriggered?.Invoke(this, incidentToFire);
        }

        if (resolutionToFire != null)
        {
            AlertResolved?.Invoke(this, resolutionToFire);
        }
    }

    private void ResetTracker(MetricTracker tracker, DateTimeOffset now)
    {
        lock (_syncLock)
        {
            tracker.OverloadStartTime = null;
            tracker.AlertFired = false;
        }
    }

    /// <summary>
    /// Запускає фоновий потік моніторингу телеметрії (якщо надано ITelemetryProvider).
    /// </summary>
    public void Start()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AlertService));

            if (IsRunning || _telemetryProvider == null)
                return;

            _cts = new CancellationTokenSource();
            _backgroundTask = Task.Run(() => PollingLoopAsync(_cts.Token));
        }
    }

    /// <summary>
    /// Зупиняє фоновий потік моніторингу.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource? ctsToCancel;
        Task? taskToWait;

        lock (_syncLock)
        {
            ctsToCancel = _cts;
            taskToWait = _backgroundTask;
            _cts = null;
            _backgroundTask = null;
        }

        if (ctsToCancel != null)
        {
            try
            {
                ctsToCancel.Cancel();
                taskToWait?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException) { }
            catch (OperationCanceledException) { }
            finally
            {
                ctsToCancel.Dispose();
            }
        }
    }

    private async Task PollingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_telemetryProvider != null)
                {
                    var snapshot = await _telemetryProvider.GetSnapshotAsync(token).ConfigureAwait(false);
                    ProcessSnapshot(snapshot);
                }

                await Task.Delay(_pollingInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Запобігаємо падінню фонового циклу при тимчасових збоях датчиків
                try
                {
                    await Task.Delay(_pollingInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            Stop();
        }

        _isDisposed = true;
    }

    /// <summary>
    /// Внутрішній стан відстеження перевантаження для конкретної метрики.
    /// </summary>
    private sealed class MetricTracker
    {
        public AlertMetricType MetricType { get; }
        public DateTimeOffset? OverloadStartTime { get; set; }
        public bool AlertFired { get; set; }

        public MetricTracker(AlertMetricType metricType)
        {
            MetricType = metricType;
        }
    }
}
