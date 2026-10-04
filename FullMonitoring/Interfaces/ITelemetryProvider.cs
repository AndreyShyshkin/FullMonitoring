using System;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Models;

namespace FullMonitoring.Interfaces;

/// <summary>
/// Базовий контракт для кросплатформних провайдерів телеметрії.
/// Забезпечує слабку зв'язаність (Decoupling) між інтерфейсом користувача та сервісами системного моніторингу.
/// </summary>
public interface ITelemetryProvider : IDisposable
{
    /// <summary>
    /// Отримує назву провайдера та тип його реалізації.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Вказує, чи підтримується цей провайдер телеметрії поточною операційною системою.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Синхронно фіксує повний знімок усіх показників системної телеметрії.
    /// </summary>
    SystemTelemetrySnapshot GetSnapshot();

    /// <summary>
    /// Асинхронно фіксує повний знімок усіх показників системної телеметрії.
    /// </summary>
    Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно отримує поточні показники навантаження CPU та температури.
    /// </summary>
    CpuMetrics GetCpuMetrics();

    /// <summary>
    /// Асинхронно отримує поточні показники навантаження CPU та температури.
    /// </summary>
    Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно отримує поточні показники оперативної пам'яті (RAM).
    /// </summary>
    RamMetrics GetRamMetrics();

    /// <summary>
    /// Асинхронно отримує поточні показники оперативної пам'яті (RAM).
    /// </summary>
    Task<RamMetrics> GetRamMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно отримує метрики змонтованих накопичувачів та файлових систем.
    /// </summary>
    DiskMetrics GetDiskMetrics();

    /// <summary>
    /// Асинхронно отримує метрики змонтованих накопичувачів та файлових систем.
    /// </summary>
    Task<DiskMetrics> GetDiskMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно отримує лічильники трафіку та поточну швидкість мережевих інтерфейсів.
    /// </summary>
    NetworkMetrics GetNetworkMetrics();

    /// <summary>
    /// Асинхронно отримує лічильники трафіку та поточну швидкість мережевих інтерфейсів.
    /// </summary>
    Task<NetworkMetrics> GetNetworkMetricsAsync(CancellationToken cancellationToken = default);
}
