using System;
using System.Globalization;
using FullMonitoring.Models;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для моделі AlertIncident (Issue #8).
/// </summary>
public class AlertIncidentTests
{
    [Fact]
    public void Constructor_SetsAllPropertiesCorrectly()
    {
        var timestamp = new DateTimeOffset(2026, 10, 10, 14, 30, 0, TimeSpan.Zero);
        var incident = new AlertIncident(
            AlertMetricType.CpuUsage,
            actualValue: 95.5,
            thresholdValue: 90.0,
            overloadDuration: TimeSpan.FromSeconds(3.5),
            severity: AlertSeverity.Critical,
            timestamp: timestamp);

        Assert.NotEqual(Guid.Empty, incident.Id);
        Assert.Equal(AlertMetricType.CpuUsage, incident.MetricType);
        Assert.Equal(95.5, incident.ActualValue);
        Assert.Equal(90.0, incident.ThresholdValue);
        Assert.Equal(TimeSpan.FromSeconds(3.5), incident.OverloadDuration);
        Assert.Equal(AlertSeverity.Critical, incident.Severity);
        Assert.Equal(timestamp, incident.Timestamp);
        Assert.Contains("95.5%", incident.Message);
        Assert.Contains("90.0%", incident.Message);
    }

    [Fact]
    public void TemperatureIncident_HasCorrectFormattingAndUnits()
    {
        var incident = new AlertIncident(
            AlertMetricType.CpuTemperature,
            actualValue: 88.2,
            thresholdValue: 85.0,
            overloadDuration: TimeSpan.FromSeconds(4.0));

        Assert.Equal("Температура CPU", incident.MetricDisplayName);
        Assert.Equal("88.2 °C", incident.FormattedActualValue);
        Assert.Equal("85.0 °C", incident.FormattedThresholdValue);
        Assert.Equal("4.0 с", incident.FormattedDuration);
        Assert.Contains("°C", incident.Message);
    }

    [Fact]
    public void RamIncident_HasCorrectFormattingAndUnits()
    {
        var incident = new AlertIncident(
            AlertMetricType.RamUsage,
            actualValue: 92.0,
            thresholdValue: 90.0,
            overloadDuration: TimeSpan.FromSeconds(5.2));

        Assert.Equal("Використання RAM", incident.MetricDisplayName);
        Assert.Equal("92.0 %", incident.FormattedActualValue);
        Assert.Equal("90.0 %", incident.FormattedThresholdValue);
        Assert.Equal("5.2 с", incident.FormattedDuration);
    }

    [Fact]
    public void CustomMessage_IsPreservedWhenProvided()
    {
        var custom = "Спеціальне повідомлення про збій";
        var incident = new AlertIncident(
            AlertMetricType.CpuUsage,
            91.0,
            90.0,
            TimeSpan.FromSeconds(3),
            message: custom);

        Assert.Equal(custom, incident.Message);
    }

    [Fact]
    public void FormattedTimestamp_MatchesPattern()
    {
        var fixedTime = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        var incident = new AlertIncident(
            AlertMetricType.CpuUsage,
            95.0,
            90.0,
            TimeSpan.FromSeconds(3),
            timestamp: fixedTime);

        Assert.False(string.IsNullOrWhiteSpace(incident.FormattedTimestamp));
        Assert.Contains("2026", incident.FormattedTimestamp);
    }

    [Fact]
    public void TwoIncidents_HaveUniqueGuids()
    {
        var inc1 = new AlertIncident(AlertMetricType.CpuUsage, 91.0, 90.0, TimeSpan.FromSeconds(3));
        var inc2 = new AlertIncident(AlertMetricType.CpuUsage, 91.0, 90.0, TimeSpan.FromSeconds(3));

        Assert.NotEqual(inc1.Id, inc2.Id);
    }
}
