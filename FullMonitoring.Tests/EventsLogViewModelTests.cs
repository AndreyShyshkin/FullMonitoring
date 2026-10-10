using System;
using FullMonitoring.Models;
using FullMonitoring.Services;
using FullMonitoring.ViewModels;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для моделі представлення журналу подій EventsLogViewModel (Issue #8).
/// </summary>
public class EventsLogViewModelTests
{
    private static DateTimeOffset BaseTime => new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithEmptyService_InitializesDefaults()
    {
        using var service = new AlertService();
        var vm = new EventsLogViewModel(service);

        Assert.Equal("Всі", vm.SelectedFilter);
        Assert.Equal(0, vm.TotalIncidentsCount);
        Assert.Equal(0, vm.CriticalIncidentsCount);
        Assert.False(vm.HasIncidents);
        Assert.Empty(vm.Incidents);
        Assert.Contains("порожній", vm.StatusMessage);
    }

    [Fact]
    public void RefreshLog_LoadsExistingIncidentsFromService()
    {
        using var service = new AlertService(new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0));
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime.AddSeconds(1));

        var vm = new EventsLogViewModel(service);

        Assert.Equal(1, vm.TotalIncidentsCount);
        Assert.Equal(1, vm.CriticalIncidentsCount);
        Assert.True(vm.HasIncidents);
        Assert.Single(vm.Incidents);
        Assert.Equal(AlertMetricType.CpuUsage, vm.Incidents[0].MetricType);
    }

    [Fact]
    public void ClearLogCommand_ClearsIncidentsAndResetsCounters()
    {
        using var service = new AlertService(new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0));
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime.AddSeconds(1));

        var vm = new EventsLogViewModel(service);
        Assert.True(vm.HasIncidents);

        vm.ClearLogCommand.Execute(null);

        Assert.Empty(vm.Incidents);
        Assert.Empty(service.EventLog);
        Assert.Equal(0, vm.TotalIncidentsCount);
        Assert.False(vm.HasIncidents);
        Assert.Contains("очищено", vm.StatusMessage);
    }

    [Fact]
    public void Filtering_ByMetric_FiltersCollectionCorrectly()
    {
        using var service = new AlertService(new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0));

        // CPU інцидент:
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime.AddSeconds(1));

        // Температура інцидент:
        service.ProcessMetrics(50.0, 90.0, 50.0, BaseTime.AddSeconds(5));
        service.ProcessMetrics(50.0, 90.0, 50.0, BaseTime.AddSeconds(6));

        // RAM інцидент:
        service.ProcessMetrics(50.0, 70.0, 95.0, BaseTime.AddSeconds(10));
        service.ProcessMetrics(50.0, 70.0, 95.0, BaseTime.AddSeconds(11));

        var vm = new EventsLogViewModel(service);
        Assert.Equal(3, vm.TotalIncidentsCount);

        // Фільтр: CPU
        vm.SetFilterCommand.Execute("CPU");
        Assert.Single(vm.Incidents);
        Assert.Equal(AlertMetricType.CpuUsage, vm.Incidents[0].MetricType);

        // Фільтр: Температура
        vm.SetFilterCommand.Execute("Температура");
        Assert.Single(vm.Incidents);
        Assert.Equal(AlertMetricType.CpuTemperature, vm.Incidents[0].MetricType);

        // Фільтр: RAM
        vm.SetFilterCommand.Execute("RAM");
        Assert.Single(vm.Incidents);
        Assert.Equal(AlertMetricType.RamUsage, vm.Incidents[0].MetricType);

        // Фільтр: Всі
        vm.SetFilterCommand.Execute("Всі");
        Assert.Equal(3, vm.Incidents.Count);
    }

    [Fact]
    public void DynamicAlertTriggered_AddsIncidentToViewModel()
    {
        using var service = new AlertService(new ThresholdSettings(90.0, 85.0, 90.0, durationThresholdSeconds: 1.0));
        var vm = new EventsLogViewModel(service);

        Assert.Equal(0, vm.TotalIncidentsCount);

        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime);
        service.ProcessMetrics(95.0, 70.0, 50.0, BaseTime.AddSeconds(1));

        Assert.Equal(1, vm.TotalIncidentsCount);
        Assert.Single(vm.Incidents);
        Assert.Contains("95.0%", vm.StatusMessage);
    }
}
