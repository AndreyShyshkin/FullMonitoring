using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.ViewModels;
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
        using var provider = new HangingDiskProvider
        {
            SectionTimeoutOverride = TimeSpan.FromMilliseconds(300),
            SectionCooldownOverride = TimeSpan.FromMinutes(5)
        };

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

    [Fact]
    public void GetSnapshot_TimedOutSection_IsSkippedDuringCooldown()
    {
        using var provider = new HangingDiskProvider
        {
            SectionTimeoutOverride = TimeSpan.FromMilliseconds(200),
            SectionCooldownOverride = TimeSpan.FromMinutes(5)
        };

        _ = provider.GetSnapshot();

        // Повторний виклик під час cooldown не чекає таймаут повторно.
        var sw = Stopwatch.StartNew();
        _ = provider.GetSnapshot();
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
            $"Повторний знімок чекав ({sw.Elapsed.TotalMilliseconds:F0} мс) замість пропуску секції на cooldown");
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

    [Fact]
    public async Task OverlappingRefreshMetricsAsync_BothCompleteWithoutDeadlock()
    {
        // Регресія зависання на повільних дисках: два паралельні асинхронні збори
        // (тік таймера + фоновий сервіс сповіщень) завершуються без взаємного
        // блокування, а перший знімок застосовується до ViewModel.
        using var provider = new GateSnapshotProvider();
        var vm = new OverviewViewModel(provider, autoStartPolling: false, deferInitialCollection: true);
        try
        {
            var first = vm.RefreshMetricsAsync();
            var second = vm.RefreshMetricsAsync();

            // Відкриваємо ворота лише після старту обох зборів.
            await Task.Delay(200);
            provider.Gate.Set();

            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(vm.IsFirstSnapshotLoaded);
        }
        finally
        {
            vm.Dispose();
        }
    }

    /// <summary>
    /// Провайдер із керованою затримкою знімка для перевірки паралельних зборів.
    /// </summary>
    private sealed class GateSnapshotProvider : BclTelemetryProvider
    {
        public readonly ManualResetEventSlim Gate = new(false);

        public override SystemTelemetrySnapshot GetSnapshot()
        {
            Gate.Wait(TimeSpan.FromSeconds(15));
            return base.GetSnapshot();
        }
    }
}
