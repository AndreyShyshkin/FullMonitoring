using System;

namespace FullMonitoring.Models;

/// <summary>
/// Незмінний знімок усіх метрик системної телеметрії у конкретний момент часу.
/// </summary>
public sealed record SystemTelemetrySnapshot
{
    /// <summary>
    /// Часова мітка UTC моменту збору знімка.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Метрики навантаження CPU, розбивки ядер та температурних показників.
    /// </summary>
    public CpuMetrics Cpu { get; init; } = new();

    /// <summary>
    /// Метрики використання фізичної оперативної пам'яті (RAM).
    /// </summary>
    public RamMetrics Ram { get; init; } = new();

    /// <summary>
    /// Метрики томів зберігання, точок монтування та місткості дисків.
    /// </summary>
    public DiskMetrics Disk { get; init; } = new();

    /// <summary>
    /// Метрики мережевих інтерфейсів, лічильників трафіку та швидкості передачі даних.
    /// </summary>
    public NetworkMetrics Network { get; init; } = new();

    /// <summary>
    /// Опис операційної системи (наприклад, "Microsoft Windows 11", "Linux 6.8", "macOS 15.1").
    /// </summary>
    public string OsDescription { get; init; } = string.Empty;

    /// <summary>
    /// Архітектура процесу / процесора (наприклад, X64, Arm64).
    /// </summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>
    /// Мережеве ім'я хостового комп'ютера.
    /// </summary>
    public string MachineName { get; init; } = string.Empty;

    /// <summary>
    /// Тривалість безперервної роботи системи (uptime).
    /// </summary>
    public TimeSpan Uptime { get; init; }
}
