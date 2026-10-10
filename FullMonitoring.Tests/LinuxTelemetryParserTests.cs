using System;
using FullMonitoring.Providers;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Тести низькорівневого span-парсера procfs/sysfs.
/// </summary>
public class LinuxTelemetryParserTests
{
    [Fact]
    public void TryParseCpuStat_ParsesAggregateAndCores()
    {
        const string content =
            "cpu  100 20 30 800 50 5 5 0 0 0\n" +
            "cpu0 50 10 15 400 25 2 3 0 0 0\n" +
            "cpu1 50 10 15 400 25 3 2 0 0 0\n" +
            "intr 123456\n" +
            "ctxt 654321\n";

        bool parsed = LinuxTelemetryParser.TryParseCpuStat(content, out var snapshot);

        Assert.True(parsed);
        // idle = idle(800) + iowait(50); total = сума перших 8 полів (100+20+30+800+50+5+5+0)
        Assert.Equal(850, snapshot.Aggregate.Idle);
        Assert.Equal(1010, snapshot.Aggregate.Total);

        Assert.Equal(2, snapshot.Cores.Length);
        Assert.Equal(425, snapshot.Cores[0].Idle);
        Assert.Equal(505, snapshot.Cores[0].Total);
        Assert.Equal(425, snapshot.Cores[1].Idle);
        Assert.Equal(505, snapshot.Cores[1].Total);
    }

    [Fact]
    public void TryParseCpuStat_IgnoresGuestFieldsInTotal()
    {
        // guest (поле 9) та guest_nice (поле 10) вже входять у user/nice, тому не подвоюють total.
        const string content = "cpu  100 0 0 100 0 0 0 0 50 50\n";

        bool parsed = LinuxTelemetryParser.TryParseCpuStat(content, out var snapshot);

        Assert.True(parsed);
        Assert.Equal(100, snapshot.Aggregate.Idle);
        Assert.Equal(200, snapshot.Aggregate.Total);
        Assert.Empty(snapshot.Cores);
    }

    [Fact]
    public void TryParseCpuStat_ReturnsFalse_ForEmptyContent()
    {
        bool parsed = LinuxTelemetryParser.TryParseCpuStat(string.Empty, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseCpuStat_DoesNotCrash_ForMalformedContent()
    {
        const string content = "некоректні дані без жодного числа\ncpuX абв\n";

        bool parsed = LinuxTelemetryParser.TryParseCpuStat(content, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParseMemInfo_ParsesTotalAndAvailable()
    {
        const string content =
            "MemTotal:       16384248 kB\n" +
            "MemFree:         1000000 kB\n" +
            "MemAvailable:    8012300 kB\n" +
            "Buffers:          200000 kB\n" +
            "Cached:          5000000 kB\n";

        bool parsed = LinuxTelemetryParser.TryParseMemInfo(content, out long total, out long available);

        Assert.True(parsed);
        Assert.Equal(16384248L * 1024, total);
        Assert.Equal(8012300L * 1024, available);
    }

    [Fact]
    public void TryParseMemInfo_FallsBack_WhenAvailableMissing()
    {
        const string content =
            "MemTotal:       8000000 kB\n" +
            "MemFree:        1000000 kB\n" +
            "Buffers:          200000 kB\n" +
            "Cached:         3000000 kB\n";

        bool parsed = LinuxTelemetryParser.TryParseMemInfo(content, out long total, out long available);

        Assert.True(parsed);
        Assert.Equal(8000000L * 1024, total);
        Assert.Equal(4200000L * 1024, available);
    }

    [Fact]
    public void TryParseMemInfo_ReturnsFalse_WhenTotalMissing()
    {
        const string content = "MemFree: 1000 kB\n";

        bool parsed = LinuxTelemetryParser.TryParseMemInfo(content, out _, out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("45000\n", 45.0)]
    [InlineData("38000\n", 38.0)]
    [InlineData("0\n", 0.0)]
    [InlineData("21500", 21.5)]
    public void TryParseTemperatureCelsius_ConvertsMillidegrees(string content, double expected)
    {
        bool parsed = LinuxTelemetryParser.TryParseTemperatureCelsius(content, out double celsius);

        Assert.True(parsed);
        Assert.Equal(expected, celsius, 3);
    }

    [Fact]
    public void TryParseTemperatureCelsius_ReturnsFalse_ForGarbage()
    {
        bool parsed = LinuxTelemetryParser.TryParseTemperatureCelsius("n/a\n", out double celsius);

        Assert.False(parsed);
        Assert.Equal(0.0, celsius);
    }

    [Fact]
    public void TryParseFanRpm_ParsesValue()
    {
        bool parsed = LinuxTelemetryParser.TryParseFanRpm("2100\n", out double rpm);

        Assert.True(parsed);
        Assert.Equal(2100.0, rpm);
    }
}
