using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Models;

namespace FullMonitoring.Interfaces;

/// <summary>
/// Інтерфейс сервісу експорту метрик телеметрії, списку процесів та журналу подій у формати JSON і CSV (Issue #12).
/// Забезпечує асинхронний неблокуючий запис файлів та синхронні методи.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Асинхронно серіалізує та зберігає комплексний звіт телеметрії у файл JSON.
    /// </summary>
    /// <param name="filePath">Цільовий шлях до файлу збереження.</param>
    /// <param name="report">Звіт системної телеметрії.</param>
    /// <param name="cancellationToken">Токен скасування асинхронної операції.</param>
    Task ExportToJsonAsync(string filePath, TelemetryReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно серіалізує та зберігає довільний об'єкт даних у форматі JSON.
    /// </summary>
    Task ExportToJsonAsync<T>(string filePath, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно серіалізує та зберігає комплексний звіт телеметрії у файл JSON.
    /// </summary>
    void ExportToJson(string filePath, TelemetryReport report);

    /// <summary>
    /// Синхронно серіалізує та зберігає довільний об'єкт даних у форматі JSON.
    /// </summary>
    void ExportToJson<T>(string filePath, T data);

    /// <summary>
    /// Асинхронно експортує перелік активних процесів у табличний формат CSV (RFC 4180).
    /// </summary>
    Task ExportToCsvAsync(string filePath, IEnumerable<ProcessItem> processes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно експортує журнал зафіксованих інцидентів та сповіщень у формат CSV.
    /// </summary>
    Task ExportToCsvAsync(string filePath, IEnumerable<AlertIncident> incidents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно експортує зведений звіт телеметрії та історію показників у формат CSV.
    /// </summary>
    Task ExportToCsvAsync(string filePath, TelemetryReport report, CancellationToken cancellationToken = default);

    /// <summary>
    /// Синхронно експортує перелік активних процесів у табличний формат CSV (RFC 4180).
    /// </summary>
    void ExportToCsv(string filePath, IEnumerable<ProcessItem> processes);

    /// <summary>
    /// Синхронно експортує журнал зафіксованих інцидентів та сповіщень у формат CSV.
    /// </summary>
    void ExportToCsv(string filePath, IEnumerable<AlertIncident> incidents);

    /// <summary>
    /// Синхронно експортує зведений звіт телеметрії та історію показників у формат CSV.
    /// </summary>
    void ExportToCsv(string filePath, TelemetryReport report);

    /// <summary>
    /// Асинхронно експортує перелік активних процесів у формат JSON.
    /// </summary>
    Task ExportProcessesToJsonAsync(string filePath, IEnumerable<ProcessItem> processes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно експортує журнал інцидентів у формат JSON.
    /// </summary>
    Task ExportIncidentsToJsonAsync(string filePath, IEnumerable<AlertIncident> incidents, CancellationToken cancellationToken = default);
}
