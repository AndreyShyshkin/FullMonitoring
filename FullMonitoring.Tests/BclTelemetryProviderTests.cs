using System;
using System.Threading.Tasks;
using FullMonitoring.Providers;
using Xunit;

namespace FullMonitoring.Tests;

public class BclTelemetryProviderTests
{
    [Fact]
    public void BclTelemetryProvider_ReturnsValidSnapshot()
    {
        using var provider = new BclTelemetryProvider();

        var snapshot = provider.GetSnapshot();

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Cpu);
        Assert.NotNull(snapshot.Ram);
        Assert.NotNull(snapshot.Disk);
        Assert.NotNull(snapshot.Network);

        Assert.NotEmpty(snapshot.OsDescription);
        Assert.NotEmpty(snapshot.Architecture);
        Assert.NotEmpty(snapshot.MachineName);
        Assert.True(snapshot.Uptime > TimeSpan.Zero);
    }

    [Fact]
    public void BclTelemetryProvider_CollectsRamMetrics()
    {
        using var provider = new BclTelemetryProvider();

        var ram = provider.GetRamMetrics();

        Assert.NotNull(ram);
        Assert.True(ram.TotalBytes > 0, "Загальний обсяг RAM у байтах має бути більшим за нуль");
        Assert.True(ram.UsedBytes >= 0, "Зайнятий обсяг RAM у байтах має бути невід'ємним");
        Assert.InRange(ram.UsedPercentage, 0.0, 100.0);
    }

    [Fact]
    public void BclTelemetryProvider_CollectsDiskMetrics()
    {
        using var provider = new BclTelemetryProvider();

        var disk = provider.GetDiskMetrics();

        Assert.NotNull(disk);
        Assert.NotNull(disk.Drives);
        // На будь-якій стандартній ОС має бути присутній щонайменше один накопичувач
        Assert.NotEmpty(disk.Drives);

        foreach (var drive in disk.Drives)
        {
            Assert.False(string.IsNullOrWhiteSpace(drive.Name));
            if (drive.IsReady)
            {
                Assert.True(drive.TotalBytes >= 0);
                Assert.True(drive.AvailableFreeBytes >= 0);
                if (drive.TotalBytes > 0)
                {
                    Assert.True(drive.UsedBytes <= drive.TotalBytes);
                    Assert.InRange(drive.UsedPercentage, 0.0, 100.0);
                }
            }
        }

        // Щонайменше один фізичний/основний накопичувач має бути готовим із місткістю > 0
        Assert.Contains(disk.Drives, d => d.IsReady && d.TotalBytes > 0);
        Assert.True(disk.TotalBytes > 0);
    }

    [Fact]
    public void BclTelemetryProvider_CollectsNetworkMetrics()
    {
        using var provider = new BclTelemetryProvider();

        var net1 = provider.GetNetworkMetrics();
        Assert.NotNull(net1);
        Assert.NotNull(net1.Interfaces);

        // Повторний виклик для перевірки розрахунку швидкості
        var net2 = provider.GetNetworkMetrics();
        Assert.NotNull(net2);
        Assert.True(net2.TotalBytesReceived >= 0);
        Assert.True(net2.TotalBytesSent >= 0);
        Assert.True(net2.RxSpeedBytesPerSecond >= 0);
        Assert.True(net2.TxSpeedBytesPerSecond >= 0);
    }

    [Fact]
    public void BclTelemetryProvider_CollectsCpuMetrics()
    {
        using var provider = new BclTelemetryProvider();

        var cpu = provider.GetCpuMetrics();

        Assert.NotNull(cpu);
        Assert.InRange(cpu.TotalUsagePercentage, 0.0, 100.0);
        Assert.Equal(Environment.ProcessorCount, cpu.CoreUsagesPercentage.Count);
    }

    [Fact]
    public async Task BclTelemetryProvider_AsyncMethods_CompleteSuccessfully()
    {
        using var provider = new BclTelemetryProvider();

        var snapshot = await provider.GetSnapshotAsync();
        Assert.NotNull(snapshot);

        var cpu = await provider.GetCpuMetricsAsync();
        Assert.NotNull(cpu);

        var ram = await provider.GetRamMetricsAsync();
        Assert.NotNull(ram);

        var disk = await provider.GetDiskMetricsAsync();
        Assert.NotNull(disk);

        var net = await provider.GetNetworkMetricsAsync();
        Assert.NotNull(net);
    }
}
