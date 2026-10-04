using System;
using FullMonitoring.Models;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Набір модульних тестів для перевірки моделі ProcessItem.
/// </summary>
public class ProcessItemTests
{
    [Fact]
    public void ProcessItem_DefaultConstructor_SetsDefaultValues()
    {
        var item = new ProcessItem();

        Assert.Equal(0, item.Pid);
        Assert.Equal(string.Empty, item.Name);
        Assert.Equal(0L, item.RamUsageBytes);
        Assert.Equal(0, item.ThreadCount);
        Assert.Equal("Виконується", item.Status);
        Assert.Equal(0.0, item.RamUsageMb);
    }

    [Fact]
    public void ProcessItem_ParameterizedConstructor_AssignsPropertiesCorrectly()
    {
        var item = new ProcessItem(1234, "explorer.exe", 104857600, 15, "Активний");

        Assert.Equal(1234, item.Pid);
        Assert.Equal("explorer.exe", item.Name);
        Assert.Equal(104857600L, item.RamUsageBytes);
        Assert.Equal(15, item.ThreadCount);
        Assert.Equal("Активний", item.Status);
        Assert.Equal(100.0, item.RamUsageMb);
    }

    [Fact]
    public void ProcessItem_NegativeValues_ClampedToZero()
    {
        var item = new ProcessItem(-10, "test", -5000, -3);

        Assert.Equal(-10, item.Pid);
        Assert.Equal(0L, item.RamUsageBytes);
        Assert.Equal(0, item.ThreadCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessItem_EmptyOrNullName_DefaultsToUnknown(string? invalidName)
    {
        var item = new ProcessItem(100, invalidName!, 1024, 2);

        Assert.Equal("Невідомо", item.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessItem_EmptyOrNullStatus_DefaultsToRunning(string? invalidStatus)
    {
        var item = new ProcessItem(100, "proc", 1024, 2, invalidStatus!);

        Assert.Equal("Виконується", item.Status);
    }

    [Fact]
    public void ProcessItem_RecordEquality_WorksCorrectly()
    {
        var item1 = new ProcessItem(42, "app", 1000, 4, "Виконується");
        var item2 = new ProcessItem(42, "app", 1000, 4, "Виконується");
        var item3 = new ProcessItem(43, "app", 1000, 4, "Виконується");

        Assert.Equal(item1, item2);
        Assert.NotEqual(item1, item3);
    }
}
