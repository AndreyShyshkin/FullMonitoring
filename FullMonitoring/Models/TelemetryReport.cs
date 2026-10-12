using System;
using System.Collections.Generic;

namespace FullMonitoring.Models;

/// <summary>
/// Комплексний звіт телеметрії системи для експорту в формати JSON та CSV (Issue #12).
/// Містить повний зріз апаратних показників, історію навантаження CPU, перелік процесів та журнал інцидентів.
/// </summary>
public sealed record TelemetryReport
{
    /// <summary>
    /// Версія формату експортованого звіту.
    /// </summary>
    public string ReportVersion { get; init; } = "1.0";

    /// <summary>
    /// Часова мітка UTC моменту формування звіту.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Мережеве ім'я хостового комп'ютера.
    /// </summary>
    public string MachineName { get; init; } = Environment.MachineName;

    /// <summary>
    /// Опис операційної системи.
    /// </summary>
    public string OsDescription { get; init; } = string.Empty;

    /// <summary>
    /// Архітектура процесу / процесора.
    /// </summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>
    /// Тривалість безперервної роботи системи (uptime).
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// Деталізований знімок метрик телеметрії (CPU, RAM, Disk, Network) на момент формування звіту.
    /// </summary>
    public SystemTelemetrySnapshot? Snapshot { get; init; }

    /// <summary>
    /// Історія значень завантаження CPU (наприклад, останні 60 секунд моніторингу).
    /// </summary>
    public IReadOnlyList<double> CpuHistory { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Зріз активних процесів системи на момент формування звіту.
    /// </summary>
    public IReadOnlyList<ProcessItem> Processes { get; init; } = Array.Empty<ProcessItem>();

    /// <summary>
    /// Зафіксовані інциденти перевантаження та порогові сповіщення.
    /// </summary>
    public IReadOnlyList<AlertIncident> Incidents { get; init; } = Array.Empty<AlertIncident>();

    public TelemetryReport() { }

    /// <summary>
    /// Створює звіт телеметрії на основі поточного знімка системи та необов'язкових супутніх даних.
    /// </summary>
    public static TelemetryReport Create(
        SystemTelemetrySnapshot? snapshot,
        IEnumerable<double>? cpuHistory = null,
        IEnumerable<ProcessItem>? processes = null,
        IEnumerable<AlertIncident>? incidents = null)
    {
        var listCpuHistory = cpuHistory != null ? new List<double>(cpuHistory) : (IReadOnlyList<double>)Array.Empty<double>();
        var listProcesses = processes != null ? new List<ProcessItem>(processes) : (IReadOnlyList<ProcessItem>)Array.Empty<ProcessItem>();
        var listIncidents = incidents != null ? new List<AlertIncident>(incidents) : (IReadOnlyList<AlertIncident>)Array.Empty<AlertIncident>();

        return new TelemetryReport
        {
            GeneratedAt = snapshot?.Timestamp ?? DateTimeOffset.UtcNow,
            MachineName = snapshot?.MachineName ?? Environment.MachineName,
            OsDescription = snapshot?.OsDescription ?? string.Empty,
            Architecture = snapshot?.Architecture ?? string.Empty,
            Uptime = snapshot?.Uptime ?? TimeSpan.Zero,
            Snapshot = snapshot,
            CpuHistory = listCpuHistory,
            Processes = listProcesses,
            Incidents = listIncidents
        };
    }
}
