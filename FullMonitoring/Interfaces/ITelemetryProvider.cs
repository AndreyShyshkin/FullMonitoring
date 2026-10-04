using System;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Models;

namespace FullMonitoring.Interfaces;

/// <summary>
/// Core contract for cross-platform telemetry data providers.
/// Enables decoupled data extraction for UI and background monitoring services.
/// </summary>
public interface ITelemetryProvider : IDisposable
{
    /// <summary>
    /// Gets the human-readable provider name and implementation type.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates whether this telemetry provider is fully supported on the current host OS.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Captures a complete snapshot of all system telemetry metrics synchronously.
    /// </summary>
    SystemTelemetrySnapshot GetSnapshot();

    /// <summary>
    /// Captures a complete snapshot of all system telemetry metrics asynchronously.
    /// </summary>
    Task<SystemTelemetrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current CPU utilization, frequency, and thermal metrics synchronously.
    /// </summary>
    CpuMetrics GetCpuMetrics();

    /// <summary>
    /// Retrieves current CPU utilization, frequency, and thermal metrics asynchronously.
    /// </summary>
    Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current RAM metrics synchronously.
    /// </summary>
    RamMetrics GetRamMetrics();

    /// <summary>
    /// Retrieves current RAM metrics asynchronously.
    /// </summary>
    Task<RamMetrics> GetRamMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves mounted storage volume metrics synchronously.
    /// </summary>
    DiskMetrics GetDiskMetrics();

    /// <summary>
    /// Retrieves mounted storage volume metrics asynchronously.
    /// </summary>
    Task<DiskMetrics> GetDiskMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves network interface counters and throughput rates synchronously.
    /// </summary>
    NetworkMetrics GetNetworkMetrics();

    /// <summary>
    /// Retrieves network interface counters and throughput rates asynchronously.
    /// </summary>
    Task<NetworkMetrics> GetNetworkMetricsAsync(CancellationToken cancellationToken = default);
}
