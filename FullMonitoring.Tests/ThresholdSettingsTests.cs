using System;
using FullMonitoring.Models;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для моделі конфігурації ThresholdSettings (Issue #8).
/// </summary>
public class ThresholdSettingsTests
{
    [Fact]
    public void DefaultSettings_HaveCorrectStandardThresholds()
    {
        var settings = new ThresholdSettings();

        Assert.Equal(90.0, settings.CpuUsageThresholdPercentage);
        Assert.Equal(85.0, settings.CpuTemperatureThresholdCelsius);
        Assert.Equal(90.0, settings.RamUsageThresholdPercentage);
        Assert.Equal(3.0, settings.DurationThresholdSeconds);
    }

    [Fact]
    public void StaticDefault_MatchesParameterlessConstructor()
    {
        var defaultInstance = ThresholdSettings.Default;
        var newInstance = new ThresholdSettings();

        Assert.Equal(newInstance, defaultInstance);
    }

    [Fact]
    public void Constructor_WithCustomValues_InitializesCorrectly()
    {
        var settings = new ThresholdSettings(75.5, 78.0, 80.0, 5.0);

        Assert.Equal(75.5, settings.CpuUsageThresholdPercentage);
        Assert.Equal(78.0, settings.CpuTemperatureThresholdCelsius);
        Assert.Equal(80.0, settings.RamUsageThresholdPercentage);
        Assert.Equal(5.0, settings.DurationThresholdSeconds);
    }

    [Theory]
    [InlineData(-10.0, 0.0)]
    [InlineData(115.0, 100.0)]
    [InlineData(50.0, 50.0)]
    public void Constructor_ClampsCpuUsagePercentage_ToValidRange(double input, double expected)
    {
        var settings = new ThresholdSettings(input, 85.0, 90.0);
        Assert.Equal(expected, settings.CpuUsageThresholdPercentage);
    }

    [Theory]
    [InlineData(-50.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(95.0, 95.0)]
    public void Constructor_ClampsTemperature_NonNegative(double input, double expected)
    {
        var settings = new ThresholdSettings(90.0, input, 90.0);
        Assert.Equal(expected, settings.CpuTemperatureThresholdCelsius);
    }

    [Theory]
    [InlineData(-5.0, 0.0)]
    [InlineData(105.0, 100.0)]
    [InlineData(88.0, 88.0)]
    public void Constructor_ClampsRamUsagePercentage_ToValidRange(double input, double expected)
    {
        var settings = new ThresholdSettings(90.0, 85.0, input);
        Assert.Equal(expected, settings.RamUsageThresholdPercentage);
    }

    [Theory]
    [InlineData(-10.0, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(10.0, 10.0)]
    public void Constructor_ClampsDurationThreshold_NonNegative(double input, double expected)
    {
        var settings = new ThresholdSettings(90.0, 85.0, 90.0, input);
        Assert.Equal(expected, settings.DurationThresholdSeconds);
    }

    [Fact]
    public void RecordWith_CreatesNewInstanceWithModifiedProperties()
    {
        var original = new ThresholdSettings();
        var modified = original with { CpuUsageThresholdPercentage = 80.0, DurationThresholdSeconds = 2.0 };

        Assert.Equal(80.0, modified.CpuUsageThresholdPercentage);
        Assert.Equal(2.0, modified.DurationThresholdSeconds);
        Assert.Equal(90.0, original.CpuUsageThresholdPercentage);
        Assert.Equal(3.0, original.DurationThresholdSeconds);
    }
}
