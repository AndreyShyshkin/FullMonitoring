using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FullMonitoring.Providers;
using Xunit;

namespace FullMonitoring.Tests;

public class MockTelemetryProviderTests
{
    [Fact]
    public void MockTelemetryProvider_ReturnsValidData_AcrossMultipleTicksWithoutFailure()
    {
        using var provider = new MockTelemetryProvider(seed: 42);

        long lastEthRx = 0;
        long lastEthTx = 0;

        // Simulate 10 sequential 1-second ticks (DoD: returns valid data every second without runtime crashes)
        for (int i = 0; i < 10; i++)
        {
            var snapshot = provider.GetSnapshot();

            Assert.NotNull(snapshot);
            Assert.Equal("Mock OS 1.0 (Simulation Engine)", snapshot.OsDescription);
            Assert.Equal("Arm64", snapshot.Architecture);
            Assert.Equal("MOCK-DEV-RIG", snapshot.MachineName);

            // CPU validation
            Assert.InRange(snapshot.Cpu.TotalUsagePercentage, 0.0, 100.0);
            Assert.Equal(8, snapshot.Cpu.CoreUsagesPercentage.Count);
            foreach (var coreUsage in snapshot.Cpu.CoreUsagesPercentage)
            {
                Assert.InRange(coreUsage, 0.0, 100.0);
            }
            Assert.NotNull(snapshot.Cpu.TemperatureCelsius);
            Assert.InRange(snapshot.Cpu.TemperatureCelsius.Value, 30.0, 105.0);
            Assert.NotNull(snapshot.Cpu.FanSpeedRpm);
            Assert.InRange(snapshot.Cpu.FanSpeedRpm.Value, 500.0, 4000.0);

            // RAM validation
            Assert.True(snapshot.Ram.TotalBytes > 0);
            Assert.True(snapshot.Ram.UsedBytes > 0);
            Assert.True(snapshot.Ram.FreeBytes >= 0);
            Assert.True(snapshot.Ram.UsedBytes <= snapshot.Ram.TotalBytes);
            Assert.InRange(snapshot.Ram.UsedPercentage, 0.0, 100.0);

            // Disk validation
            Assert.NotEmpty(snapshot.Disk.Drives);
            Assert.True(snapshot.Disk.TotalBytes > 0);
            Assert.True(snapshot.Disk.UsedBytes > 0);
            Assert.True(snapshot.Disk.FreeBytes > 0);
            Assert.InRange(snapshot.Disk.OverallUsedPercentage, 0.0, 100.0);

            // Network validation
            Assert.Equal(2, snapshot.Network.Interfaces.Count);
            Assert.True(snapshot.Network.TotalBytesReceived > 0);
            Assert.True(snapshot.Network.TotalBytesSent > 0);
            Assert.True(snapshot.Network.RxSpeedBytesPerSecond >= 0);
            Assert.True(snapshot.Network.TxSpeedBytesPerSecond >= 0);

            var eth = snapshot.Network.Interfaces[0];
            Assert.Equal("mock-eth0", eth.Id);
            Assert.True(eth.BytesReceived >= lastEthRx);
            Assert.True(eth.BytesSent >= lastEthTx);
            lastEthRx = eth.BytesReceived;
            lastEthTx = eth.BytesSent;
        }
    }

    [Fact]
    public async Task MockTelemetryProvider_IndividualAsyncMethods_ReturnValidMetrics()
    {
        using var provider = new MockTelemetryProvider(seed: 123);

        var cpu = await provider.GetCpuMetricsAsync();
        Assert.InRange(cpu.TotalUsagePercentage, 0.0, 100.0);
        Assert.NotEmpty(cpu.CoreUsagesPercentage);

        var ram = await provider.GetRamMetricsAsync();
        Assert.True(ram.TotalBytes > 0);
        Assert.InRange(ram.UsedPercentage, 0.0, 100.0);

        var disk = await provider.GetDiskMetricsAsync();
        Assert.NotEmpty(disk.Drives);

        var net = await provider.GetNetworkMetricsAsync();
        Assert.NotEmpty(net.Interfaces);

        var snapshot = await provider.GetSnapshotAsync();
        Assert.NotNull(snapshot);
    }

    [Fact]
    public void MockTelemetryProvider_ThreadSafe_UnderConcurrentLoad()
    {
        using var provider = new MockTelemetryProvider();

        Parallel.For(0, 100, _ =>
        {
            var snap = provider.GetSnapshot();
            Assert.NotNull(snap);
            Assert.InRange(snap.Cpu.TotalUsagePercentage, 0.0, 100.0);
            Assert.True(snap.Ram.UsedBytes > 0);

            var cpu = provider.GetCpuMetrics();
            Assert.NotNull(cpu);

            var ram = provider.GetRamMetrics();
            Assert.NotNull(ram);

            var disk = provider.GetDiskMetrics();
            Assert.NotNull(disk);

            var net = provider.GetNetworkMetrics();
            Assert.NotNull(net);
        });
    }

    [Fact]
    public async Task MockTelemetryProvider_ReturnsValidData_EverySecondInRealtime()
    {
        using var provider = new MockTelemetryProvider();

        // DoD verification: provider returns valid data every second without runtime crashes
        for (int i = 0; i < 3; i++)
        {
            await Task.Delay(1000);
            var snapshot = provider.GetSnapshot();

            Assert.NotNull(snapshot);
            Assert.InRange(snapshot.Cpu.TotalUsagePercentage, 0.0, 100.0);
            Assert.True(snapshot.Ram.TotalBytes > 0);
            Assert.InRange(snapshot.Ram.UsedPercentage, 0.0, 100.0);
            Assert.NotEmpty(snapshot.Disk.Drives);
            Assert.NotEmpty(snapshot.Network.Interfaces);
        }
    }
}
