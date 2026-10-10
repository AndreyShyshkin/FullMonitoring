using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FullMonitoring.Models;
using FullMonitoring.Services;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для фонового сервісу AlertService (Issue #8).
/// Перевіряють відстеження тривалості перевантаження (> N с), дебаунсинг (відсутність дублювання щосекунди)
/// та збереження історії інцидентів.
/// </summary>
public class AlertServiceTests
{
    private static DateTimeOffset BaseTime => new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShortSpike_UnderDurationThreshold_DoesNotTriggerAlert()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 3.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);

        // Стрибок триває 2 секунди (менше 3 с):
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime);
        service.ProcessMetrics(96.0, 70.0, 50.0, BaseTime.AddSeconds(1));
        service.ProcessMetrics(94.0, 70.0, 50.0, BaseTime.AddSeconds(2));

        // Показник повертається в норму:
        service.ProcessMetrics(50.0, 70.0, 50.0, BaseTime.AddSeconds(3));

        Assert.Empty(triggeredAlerts);
        Assert.Empty(service.EventLog);
        Assert.False(service.HasActiveOverload(AlertMetricType.CpuUsage));
    }

    [Fact]
    public void Overload_EqualOrExceedingDurationThreshold_TriggersAlert()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 3.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);

        // Секунди 0, 1, 2:
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime.AddSeconds(1));
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime.AddSeconds(2));
        Assert.Empty(triggeredAlerts);

        // Секунда 3: тривалість досягла 3.0 с:
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime.AddSeconds(3));

        Assert.Single(triggeredAlerts);
        Assert.Single(service.EventLog);

        var incident = triggeredAlerts[0];
        Assert.Equal(AlertMetricType.CpuUsage, incident.MetricType);
        Assert.Equal(95.0, incident.ActualValue);
        Assert.Equal(90.0, incident.ThresholdValue);
        Assert.True(incident.OverloadDuration.TotalSeconds >= 3.0);
        Assert.True(service.HasActiveOverload(AlertMetricType.CpuUsage));
    }

    [Fact]
    public void Debouncing_ContinuousOverload_DoesNotDuplicateAlertsEverySecond()
    {
        // DoD: Сповіщення не дублюються щосекунди, доки показник перебуває вище критичної межі (debouncing).
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 2.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);

        // Безперервне перевантаження протягом 10 секунд:
        for (int sec = 0; sec <= 10; sec++)
        {
            service.ProcessMetrics(96.0, 60.0, 60.0, BaseTime.AddSeconds(sec));
        }

        // Повинно згенеруватися РІВНО ОДНЕ сповіщення, а не 9 чи 10!
        Assert.Single(triggeredAlerts);
        Assert.Single(service.EventLog);
        Assert.True(service.HasActiveOverload(AlertMetricType.CpuUsage));
    }

    [Fact]
    public void Recovery_ResetsDebounce_AllowingSubsequentSpikesToTriggerNewAlert()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 2.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        var resolvedAlerts = new List<AlertIncident>();

        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);
        service.AlertResolved += (_, incident) => resolvedAlerts.Add(incident);

        // Перший інцидент (секунди 0, 1, 2):
        service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime.AddSeconds(1));
        service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime.AddSeconds(2));

        Assert.Single(triggeredAlerts);
        Assert.Empty(resolvedAlerts);

        // Нормалізація на секунді 5:
        service.ProcessMetrics(40.0, 60.0, 50.0, BaseTime.AddSeconds(5));

        Assert.Single(resolvedAlerts);
        Assert.False(service.HasActiveOverload(AlertMetricType.CpuUsage));

        // Другий інцидент (секунди 10, 11, 12):
        service.ProcessMetrics(98.0, 60.0, 50.0, BaseTime.AddSeconds(10));
        service.ProcessMetrics(98.0, 60.0, 50.0, BaseTime.AddSeconds(11));
        service.ProcessMetrics(98.0, 60.0, 50.0, BaseTime.AddSeconds(12));

        // Повинно згенеруватися 2 окремих інциденти:
        Assert.Equal(2, triggeredAlerts.Count);
        Assert.Equal(2, service.EventLog.Count);
    }

    [Fact]
    public void TemperatureOverload_TriggersAlertCorrectly()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 2.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);

        service.ProcessMetrics(50.0, 88.0, 50.0, BaseTime);
        service.ProcessMetrics(50.0, 89.0, 50.0, BaseTime.AddSeconds(1));
        service.ProcessMetrics(50.0, 90.0, 50.0, BaseTime.AddSeconds(2));

        Assert.Single(triggeredAlerts);
        Assert.Equal(AlertMetricType.CpuTemperature, triggeredAlerts[0].MetricType);
        Assert.Equal(90.0, triggeredAlerts[0].ActualValue);
        Assert.Equal(85.0, triggeredAlerts[0].ThresholdValue);
    }

    [Fact]
    public void NullTemperature_DoesNotTriggerAlertAndResetsState()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 2.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);

        // Спочатку стрибок температури
        service.ProcessMetrics(50.0, 90.0, 50.0, BaseTime);
        // Потім сенсор повертає null
        service.ProcessMetrics(50.0, null, 50.0, BaseTime.AddSeconds(1));
        service.ProcessMetrics(50.0, null, 50.0, BaseTime.AddSeconds(2));

        Assert.Empty(triggeredAlerts);
        Assert.False(service.HasActiveOverload(AlertMetricType.CpuTemperature));
    }

    [Fact]
    public void RamOverload_TriggersAlertCorrectly()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 2.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);

        service.ProcessMetrics(50.0, 60.0, 95.0, BaseTime);
        service.ProcessMetrics(50.0, 60.0, 96.0, BaseTime.AddSeconds(1));
        service.ProcessMetrics(50.0, 60.0, 97.0, BaseTime.AddSeconds(2));

        Assert.Single(triggeredAlerts);
        Assert.Equal(AlertMetricType.RamUsage, triggeredAlerts[0].MetricType);
        Assert.Equal(97.0, triggeredAlerts[0].ActualValue);
        Assert.Equal(90.0, triggeredAlerts[0].ThresholdValue);
    }

    [Fact]
    public void MultipleSimultaneousOverloads_AreTrackedIndependently()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0);
        using var service = new AlertService(settings);

        var triggeredAlerts = new List<AlertIncident>();
        service.AlertTriggered += (_, incident) => triggeredAlerts.Add(incident);

        // Одночасно CPU та RAM перевищують поріг
        service.ProcessMetrics(95.0, 60.0, 95.0, BaseTime);
        service.ProcessMetrics(96.0, 60.0, 96.0, BaseTime.AddSeconds(1));

        Assert.Equal(2, triggeredAlerts.Count);
        Assert.Equal(2, service.ActiveOverloadCount);
        Assert.True(service.HasActiveOverload(AlertMetricType.CpuUsage));
        Assert.True(service.HasActiveOverload(AlertMetricType.RamUsage));
        Assert.False(service.HasActiveOverload(AlertMetricType.CpuTemperature));
    }

    [Fact]
    public void ProcessSnapshot_AnalyzesSnapshotProperties()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0);
        using var service = new AlertService(settings);

        var snapshot1 = new SystemTelemetrySnapshot
        {
            Timestamp = BaseTime,
            Cpu = new CpuMetrics(95.0, temperatureCelsius: 60.0),
            Ram = new RamMetrics(16000, 15000, 1000, usedPercentage: 94.0)
        };

        var snapshot2 = new SystemTelemetrySnapshot
        {
            Timestamp = BaseTime.AddSeconds(1),
            Cpu = new CpuMetrics(96.0, temperatureCelsius: 60.0),
            Ram = new RamMetrics(16000, 15000, 1000, usedPercentage: 94.0)
        };

        service.ProcessSnapshot(snapshot1);
        service.ProcessSnapshot(snapshot2);

        Assert.Equal(2, service.EventLog.Count);
    }

    [Fact]
    public void ClearEventLog_EmptiesHistory()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0);
        using var service = new AlertService(settings);

        service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime.AddSeconds(1));
        Assert.NotEmpty(service.EventLog);

        service.ClearEventLog();
        Assert.Empty(service.EventLog);
    }

    [Fact]
    public void MaxLogCapacity_TrimsOldestEntries()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 0.1);
        using var service = new AlertService(settings) { MaxLogCapacity = 10 };

        for (int i = 0; i < 20; i++)
        {
            service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime.AddSeconds(i * 10));
            service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime.AddSeconds(i * 10 + 1));
            // Нормалізуємо для генерації наступного
            service.ProcessMetrics(50.0, 60.0, 50.0, BaseTime.AddSeconds(i * 10 + 2));
        }

        Assert.Equal(10, service.EventLog.Count);
    }

    [Fact]
    public void GetIncidentsByMetric_FiltersCorrectly()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0);
        using var service = new AlertService(settings);

        service.ProcessMetrics(95.0, 90.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 90.0, 50.0, BaseTime.AddSeconds(1));

        var cpuIncidents = service.GetIncidentsByMetric(AlertMetricType.CpuUsage);
        var tempIncidents = service.GetIncidentsByMetric(AlertMetricType.CpuTemperature);
        var ramIncidents = service.GetIncidentsByMetric(AlertMetricType.RamUsage);

        Assert.Single(cpuIncidents);
        Assert.Single(tempIncidents);
        Assert.Empty(ramIncidents);
    }

    [Fact]
    public void GetRecentIncidents_ReturnsReverseChronologicalList()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0);
        using var service = new AlertService(settings);

        service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 60.0, 50.0, BaseTime.AddSeconds(1));

        service.ProcessMetrics(50.0, 60.0, 50.0, BaseTime.AddSeconds(2));

        service.ProcessMetrics(98.0, 60.0, 50.0, BaseTime.AddSeconds(5));
        service.ProcessMetrics(98.0, 60.0, 50.0, BaseTime.AddSeconds(6));

        var recent = service.GetRecentIncidents(1);
        Assert.Single(recent);
        Assert.Equal(98.0, recent[0].ActualValue);
    }

    [Fact]
    public void UpdateSettings_ChangesThresholdsDynamically()
    {
        using var service = new AlertService(new ThresholdSettings(90.0, 85.0, 90.0));
        var newSettings = new ThresholdSettings(70.0, 75.0, 80.0, durationThresholdSeconds: 1.0);

        service.UpdateSettings(newSettings);

        Assert.Equal(70.0, service.Settings.CpuUsageThresholdPercentage);
        Assert.Equal(75.0, service.Settings.CpuTemperatureThresholdCelsius);
        Assert.Equal(80.0, service.Settings.RamUsageThresholdPercentage);
        Assert.Equal(1.0, service.Settings.DurationThresholdSeconds);
    }

    [Fact]
    public void ConcurrentProcessMetrics_IsThreadSafe()
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 0.1);
        using var service = new AlertService(settings);

        Parallel.For(0, 100, i =>
        {
            service.ProcessMetrics(95.0, 88.0, 95.0, BaseTime.AddSeconds(i));
        });

        Assert.NotNull(service.EventLog);
    }
}
