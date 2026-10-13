using System;
using System.Diagnostics;
using System.Threading;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Тести стійкості збору знімка: одна підвисла секція (диск, мережа) не повинна
/// блокувати появу вікна та інші секції (таймаут + cooldown у BclTelemetryProvider).
/// </summary>
public class SnapshotResilienceTests
{
    [Fact]
    public void GetSnapshot_HangingDiskSection_ReturnsOtherSectionsWithinTimeout()
    {
        var previousTimeout = BclTelemetryProvider.SnapshotSectionTimeout;
        var previousCooldown = BclTelemetryProvider.SnapshotSectionCooldown;
        BclTelemetryProvider.SnapshotSectionTimeout = TimeSpan.FromMilliseconds(300);
        BclTelemetryProvider.SnapshotSectionCooldown = TimeSpan.FromMinutes(5);
        try
        {
            using var provider = new HangingDiskProvider();

            var sw = Stopwatch.StartNew();
            var snapshot = provider.GetSnapshot();
            sw.Stop();

            // Знімок повернувся завдяки таймауту, а не завис разом із секцією диска.
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20),
                $"Знімок збирався задовго ({sw.Elapsed.TotalSeconds:F1} с) — таймаут не спрацював");
            Assert.NotNull(snapshot.Cpu);
            Assert.NotNull(snapshot.Ram);
            Assert.NotNull(snapshot.Disk);
            Assert.NotNull(snapshot.Network);
            // Швидкі секції все одно зібрані з реальними даними.
            Assert.True(snapshot.Ram.TotalBytes > 0);
        }
        finally
        {
            BclTelemetryProvider.SnapshotSectionTimeout = previousTimeout;
            BclTelemetryProvider.SnapshotSectionCooldown = previousCooldown;
        }
    }

    [Fact]
    public void GetSnapshot_TimedOutSection_IsSkippedDuringCooldown()
    {
        var previousTimeout = BclTelemetryProvider.SnapshotSectionTimeout;
        var previousCooldown = BclTelemetryProvider.SnapshotSectionCooldown;
        BclTelemetryProvider.SnapshotSectionTimeout = TimeSpan.FromMilliseconds(200);
        BclTelemetryProvider.SnapshotSectionCooldown = TimeSpan.FromMinutes(5);
        try
        {
            using var provider = new HangingDiskProvider();

            _ = provider.GetSnapshot();

            // Повторний виклик під час cooldown не чекає таймаут повторно.
            var sw = Stopwatch.StartNew();
            _ = provider.GetSnapshot();
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200),
                $"Повторний знімок чекав ({sw.Elapsed.TotalMilliseconds:F0} мс) замість пропуску секції на cooldown");
        }
        finally
        {
            BclTelemetryProvider.SnapshotSectionTimeout = previousTimeout;
            BclTelemetryProvider.SnapshotSectionCooldown = previousCooldown;
        }
    }

    /// <summary>
    /// Провайдер із навмисно підвислою секцією диска (імітація заблокованого
    /// системного виклику на кшталт statvfs для недоступного монтування).
    /// </summary>
    private sealed class HangingDiskProvider : BclTelemetryProvider
    {
        public override DiskMetrics GetDiskMetrics()
        {
            Thread.Sleep(Timeout.Infinite);
            return new DiskMetrics();
        }
    }
}
