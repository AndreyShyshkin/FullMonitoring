using System;
using System.Collections.Generic;
using FullMonitoring.Models;
using Xunit;

namespace FullMonitoring.Tests;

public class TelemetryModelsTests
{
    [Fact]
    public void CpuMetrics_ClampsUsagePercentage_Between0And100()
    {
        var overCpu = new CpuMetrics(150.0);
        var underCpu = new CpuMetrics(-20.0);
        var validCpu = new CpuMetrics(45.5, new[] { 40.0, 51.0 }, 60.5, 1500.0);

        Assert.Equal(100.0, overCpu.TotalUsagePercentage);
        Assert.Equal(0.0, underCpu.TotalUsagePercentage);
        Assert.Equal(45.5, validCpu.TotalUsagePercentage);
        Assert.Equal(2, validCpu.CoreUsagesPercentage.Count);
        Assert.Equal(60.5, validCpu.TemperatureCelsius);
        Assert.Equal(1500.0, validCpu.FanSpeedRpm);
    }

    [Fact]
    public void RamMetrics_CalculatesUsedPercentageAccurately()
    {
        long total = 16L * 1024 * 1024 * 1024;
        long used = 8L * 1024 * 1024 * 1024;
        long free = total - used;

        var ram = new RamMetrics(total, used, free);

        Assert.Equal(total, ram.TotalBytes);
        Assert.Equal(used, ram.UsedBytes);
        Assert.Equal(free, ram.FreeBytes);
        Assert.Equal(50.0, ram.UsedPercentage, 1);
    }

    [Fact]
    public void RamMetrics_HandlesZeroTotalBytesGracefully()
    {
        var ram = new RamMetrics(0, 0, 0);

        Assert.Equal(0, ram.TotalBytes);
        Assert.Equal(0, ram.UsedBytes);
        Assert.Equal(0, ram.FreeBytes);
        Assert.Equal(0.0, ram.UsedPercentage);
    }

    [Fact]
    public void DiskMetrics_AggregatesOnlyReadyDrives()
    {
        var drive1 = new DriveItemMetrics
        {
            Name = "C:\\",
            VolumeLabel = "System",
            RootDirectory = "C:\\",
            DriveType = "Fixed",
            DriveFormat = "NTFS",
            TotalBytes = 500_000_000_000,
            AvailableFreeBytes = 200_000_000_000,
            TotalFreeBytes = 200_000_000_000,
            IsReady = true
        };

        var drive2 = new DriveItemMetrics
        {
            Name = "D:\\",
            VolumeLabel = "Optical",
            RootDirectory = "D:\\",
            DriveType = "CDRom",
            DriveFormat = "",
            TotalBytes = 0,
            AvailableFreeBytes = 0,
            TotalFreeBytes = 0,
            IsReady = false
        };

        var diskMetrics = new DiskMetrics(new[] { drive1, drive2 });

        Assert.Equal(2, diskMetrics.Drives.Count);
        Assert.Equal(500_000_000_000, diskMetrics.TotalBytes);
        Assert.Equal(300_000_000_000, diskMetrics.UsedBytes);
        Assert.Equal(200_000_000_000, diskMetrics.FreeBytes);
        Assert.Equal(60.0, diskMetrics.OverallUsedPercentage, 1);
    }

    [Fact]
    public void NetworkMetrics_AggregatesTotalsAndRates()
    {
        var nic1 = new NetworkInterfaceMetrics
        {
            Id = "eth0",
            Name = "eth0",
            BytesReceived = 1000,
            BytesSent = 500,
            RxSpeedBytesPerSecond = 100.0,
            TxSpeedBytesPerSecond = 50.0
        };

        var nic2 = new NetworkInterfaceMetrics
        {
            Id = "wlan0",
            Name = "wlan0",
            BytesReceived = 2000,
            BytesSent = 1500,
            RxSpeedBytesPerSecond = 200.0,
            TxSpeedBytesPerSecond = 150.0
        };

        var netMetrics = new NetworkMetrics(new[] { nic1, nic2 });

        Assert.Equal(3000, netMetrics.TotalBytesReceived);
        Assert.Equal(2000, netMetrics.TotalBytesSent);
        Assert.Equal(300.0, netMetrics.RxSpeedBytesPerSecond);
        Assert.Equal(200.0, netMetrics.TxSpeedBytesPerSecond);
    }

    [Fact]
    public void SystemTelemetrySnapshot_InitializesWithDefaultValues()
    {
        var snapshot = new SystemTelemetrySnapshot();

        Assert.NotNull(snapshot.Cpu);
        Assert.NotNull(snapshot.Ram);
        Assert.NotNull(snapshot.Disk);
        Assert.NotNull(snapshot.Network);
        Assert.True(snapshot.Timestamp <= DateTimeOffset.UtcNow);
    }
}
