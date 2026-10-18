using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;

namespace FullMonitoring.Services;

/// <summary>
/// Сервіс експорту метрик телеметрії, списку процесів та журналу подій у формати JSON та CSV (Issue #12).
/// Забезпечує неблокуючий асинхронний запис файлів, валідне екранування (RFC 4180) та підтримку кодування UTF-8 з BOM для коректного відкриття в Excel.
/// </summary>
public class ExportService : IExportService
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly SearchValues<char> CsvSpecialChars = SearchValues.Create(",\"\r\n;");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Асинхронно серіалізує та зберігає комплексний звіт телеметрії у файл JSON.
    /// </summary>
    public Task ExportToJsonAsync(string filePath, TelemetryReport report, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(report);

        return ExportToJsonInternalAsync(filePath, report, cancellationToken);
    }

    /// <summary>
    /// Асинхронно серіалізує та зберігає довільний об'єкт даних у форматі JSON.
    /// </summary>
    public Task ExportToJsonAsync<T>(string filePath, T data, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        return ExportToJsonInternalAsync(filePath, data, cancellationToken);
    }

    /// <summary>
    /// Синхронно серіалізує та зберігає комплексний звіт телеметрії у файл JSON.
    /// </summary>
    public void ExportToJson(string filePath, TelemetryReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(report);

        ExportToJsonInternal(filePath, report);
    }

    /// <summary>
    /// Синхронно серіалізує та зберігає довільний об'єкт даних у форматі JSON.
    /// </summary>
    public void ExportToJson<T>(string filePath, T data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        ExportToJsonInternal(filePath, data);
    }

    /// <summary>
    /// Асинхронно експортує перелік активних процесів у формат JSON.
    /// </summary>
    public Task ExportProcessesToJsonAsync(string filePath, IEnumerable<ProcessItem> processes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(processes);

        return ExportToJsonInternalAsync(filePath, processes, cancellationToken);
    }

    /// <summary>
    /// Асинхронно експортує журнал інцидентів у формат JSON.
    /// </summary>
    public Task ExportIncidentsToJsonAsync(string filePath, IEnumerable<AlertIncident> incidents, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(incidents);

        return ExportToJsonInternalAsync(filePath, incidents, cancellationToken);
    }

    /// <summary>
    /// Асинхронно експортує перелік активних процесів у табличний формат CSV (RFC 4180).
    /// </summary>
    public async Task ExportToCsvAsync(string filePath, IEnumerable<ProcessItem> processes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(processes);

        EnsureDirectoryExists(filePath);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using var writer = new StreamWriter(stream, Utf8WithBom);

        await writer.WriteLineAsync("PID,Name,RAM (Bytes),RAM (MB),Threads,Status").ConfigureAwait(false);

        foreach (var process in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{process.Pid},{EscapeCsv(process.Name)},{process.RamUsageBytes},{process.RamUsageMb:F2},{process.ThreadCount},{EscapeCsv(process.Status)}");
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Асинхронно експортує журнал зафіксованих інцидентів у формат CSV.
    /// </summary>
    public async Task ExportToCsvAsync(string filePath, IEnumerable<AlertIncident> incidents, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(incidents);

        EnsureDirectoryExists(filePath);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using var writer = new StreamWriter(stream, Utf8WithBom);

        await writer.WriteLineAsync("ID,Timestamp (UTC),Timestamp (Local),Metric,Severity,Actual Value,Threshold Value,Duration (sec),Message").ConfigureAwait(false);

        foreach (var incident in incidents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{incident.Id},{incident.Timestamp:O},{EscapeCsv(incident.FormattedTimestamp)},{EscapeCsv(incident.MetricDisplayName)},{incident.Severity},{incident.ActualValue:F2},{incident.ThresholdValue:F2},{incident.OverloadDuration.TotalSeconds:F1},{EscapeCsv(incident.Message)}");
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Асинхронно експортує зведений звіт телеметрії у формат CSV.
    /// </summary>
    public async Task ExportToCsvAsync(string filePath, TelemetryReport report, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(report);

        EnsureDirectoryExists(filePath);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await using var writer = new StreamWriter(stream, Utf8WithBom);

        await WriteReportCsvContentAsync(writer, report, cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Синхронно експортує перелік активних процесів у табличний формат CSV (RFC 4180).
    /// </summary>
    public void ExportToCsv(string filePath, IEnumerable<ProcessItem> processes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(processes);

        EnsureDirectoryExists(filePath);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Utf8WithBom);

        writer.WriteLine("PID,Name,RAM (Bytes),RAM (MB),Threads,Status");

        foreach (var process in processes)
        {
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{process.Pid},{EscapeCsv(process.Name)},{process.RamUsageBytes},{process.RamUsageMb:F2},{process.ThreadCount},{EscapeCsv(process.Status)}");
            writer.WriteLine(line);
        }

        writer.Flush();
    }

    /// <summary>
    /// Синхронно експортує журнал зафіксованих інцидентів у формат CSV.
    /// </summary>
    public void ExportToCsv(string filePath, IEnumerable<AlertIncident> incidents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(incidents);

        EnsureDirectoryExists(filePath);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Utf8WithBom);

        writer.WriteLine("ID,Timestamp (UTC),Timestamp (Local),Metric,Severity,Actual Value,Threshold Value,Duration (sec),Message");

        foreach (var incident in incidents)
        {
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{incident.Id},{incident.Timestamp:O},{EscapeCsv(incident.FormattedTimestamp)},{EscapeCsv(incident.MetricDisplayName)},{incident.Severity},{incident.ActualValue:F2},{incident.ThresholdValue:F2},{incident.OverloadDuration.TotalSeconds:F1},{EscapeCsv(incident.Message)}");
            writer.WriteLine(line);
        }

        writer.Flush();
    }

    /// <summary>
    /// Синхронно експортує зведений звіт телеметрії у формат CSV.
    /// </summary>
    public void ExportToCsv(string filePath, TelemetryReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(report);

        EnsureDirectoryExists(filePath);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Utf8WithBom);

        WriteReportCsvContentSync(writer, report);
        writer.Flush();
    }

    /// <summary>
    /// Екранує текстове значення відповідно до стандарту RFC 4180.
    /// </summary>
    public static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool containsSpecialChars = value.AsSpan().IndexOfAny(CsvSpecialChars) >= 0;
        if (!containsSpecialChars)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static async Task ExportToJsonInternalAsync<T>(string filePath, T data, CancellationToken cancellationToken)
    {
        EnsureDirectoryExists(filePath);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static void ExportToJsonInternal<T>(string filePath, T data)
    {
        EnsureDirectoryExists(filePath);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, data, JsonOptions);
    }

    private static async Task WriteReportCsvContentAsync(StreamWriter writer, TelemetryReport report, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync("# СИСТЕМНА ІНФОРМАЦІЯ ТА ЗРІЗ ТЕЛЕМЕТРІЇ").ConfigureAwait(false);
        await writer.WriteLineAsync("Section,Property,Value").ConfigureAwait(false);
        await writer.WriteLineAsync($"System,ReportVersion,{EscapeCsv(report.ReportVersion)}").ConfigureAwait(false);
        await writer.WriteLineAsync($"System,GeneratedAtUtc,{report.GeneratedAt:O}").ConfigureAwait(false);
        await writer.WriteLineAsync($"System,MachineName,{EscapeCsv(report.MachineName)}").ConfigureAwait(false);
        await writer.WriteLineAsync($"System,OS,{EscapeCsv(report.OsDescription)}").ConfigureAwait(false);
        await writer.WriteLineAsync($"System,Architecture,{EscapeCsv(report.Architecture)}").ConfigureAwait(false);
        await writer.WriteLineAsync($"System,Uptime,{report.Uptime}").ConfigureAwait(false);

        if (report.Snapshot is not null)
        {
            var s = report.Snapshot;
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,CpuUsagePercentage,{s.Cpu.TotalUsagePercentage:F1}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,CpuTemperatureCelsius,{(s.Cpu.TemperatureCelsius.HasValue ? s.Cpu.TemperatureCelsius.Value.ToString("F1", CultureInfo.InvariantCulture) : "N/A")}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,RamUsagePercentage,{s.Ram.UsedPercentage:F1}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,RamUsedGb,{s.Ram.UsedBytes / (1024.0 * 1024.0 * 1024.0):F2}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,RamTotalGb,{s.Ram.TotalBytes / (1024.0 * 1024.0 * 1024.0):F2}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,DiskUsagePercentage,{s.Disk.OverallUsedPercentage:F1}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,DiskUsedGb,{s.Disk.UsedBytes / (1024.0 * 1024.0 * 1024.0):F2}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,DiskTotalGb,{s.Disk.TotalBytes / (1024.0 * 1024.0 * 1024.0):F2}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,NetworkRxSpeedBytesPerSec,{s.Network.RxSpeedBytesPerSecond:F1}")).ConfigureAwait(false);
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Metrics,NetworkTxSpeedBytesPerSec,{s.Network.TxSpeedBytesPerSecond:F1}")).ConfigureAwait(false);
        }

        if (report.CpuHistory.Count > 0)
        {
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync("# ІСТОРІЯ ЗАВАНТАЖЕННЯ CPU").ConfigureAwait(false);
            await writer.WriteLineAsync("SecondsAgo,CpuUsagePercentage").ConfigureAwait(false);
            for (int i = 0; i < report.CpuHistory.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int secondsAgo = report.CpuHistory.Count - 1 - i;
                await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{secondsAgo},{report.CpuHistory[i]:F1}")).ConfigureAwait(false);
            }
        }

        if (report.Processes.Count > 0)
        {
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync("# ТОП АКТИВНИХ ПРОЦЕСІВ").ConfigureAwait(false);
            await writer.WriteLineAsync("PID,Name,RAM (Bytes),RAM (MB),Threads,Status").ConfigureAwait(false);
            foreach (var p in report.Processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{p.Pid},{EscapeCsv(p.Name)},{p.RamUsageBytes},{p.RamUsageMb:F2},{p.ThreadCount},{EscapeCsv(p.Status)}")).ConfigureAwait(false);
            }
        }

        if (report.Incidents.Count > 0)
        {
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync("# ЖУРНАЛ ПОДІЙ ТА ПОРОГОВИХ СПОВІЩЕНЬ").ConfigureAwait(false);
            await writer.WriteLineAsync("ID,Timestamp,Metric,Severity,ActualValue,ThresholdValue,DurationSec,Message").ConfigureAwait(false);
            foreach (var inc in report.Incidents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{inc.Id},{inc.Timestamp:O},{EscapeCsv(inc.MetricDisplayName)},{inc.Severity},{inc.ActualValue:F2},{inc.ThresholdValue:F2},{inc.OverloadDuration.TotalSeconds:F1},{EscapeCsv(inc.Message)}")).ConfigureAwait(false);
            }
        }
    }

    private static void WriteReportCsvContentSync(StreamWriter writer, TelemetryReport report)
    {
        writer.WriteLine("# СИСТЕМНА ІНФОРМАЦІЯ ТА ЗРІЗ ТЕЛЕМЕТРІЇ");
        writer.WriteLine("Section,Property,Value");
        writer.WriteLine($"System,ReportVersion,{EscapeCsv(report.ReportVersion)}");
        writer.WriteLine($"System,GeneratedAtUtc,{report.GeneratedAt:O}");
        writer.WriteLine($"System,MachineName,{EscapeCsv(report.MachineName)}");
        writer.WriteLine($"System,OS,{EscapeCsv(report.OsDescription)}");
        writer.WriteLine($"System,Architecture,{EscapeCsv(report.Architecture)}");
        writer.WriteLine($"System,Uptime,{report.Uptime}");

        if (report.Snapshot is not null)
        {
            var s = report.Snapshot;
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,CpuUsagePercentage,{s.Cpu.TotalUsagePercentage:F1}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,CpuTemperatureCelsius,{(s.Cpu.TemperatureCelsius.HasValue ? s.Cpu.TemperatureCelsius.Value.ToString("F1", CultureInfo.InvariantCulture) : "N/A")}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,RamUsagePercentage,{s.Ram.UsedPercentage:F1}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,RamUsedGb,{s.Ram.UsedBytes / (1024.0 * 1024.0 * 1024.0):F2}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,RamTotalGb,{s.Ram.TotalBytes / (1024.0 * 1024.0 * 1024.0):F2}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,DiskUsagePercentage,{s.Disk.OverallUsedPercentage:F1}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,DiskUsedGb,{s.Disk.UsedBytes / (1024.0 * 1024.0 * 1024.0):F2}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,DiskTotalGb,{s.Disk.TotalBytes / (1024.0 * 1024.0 * 1024.0):F2}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,NetworkRxSpeedBytesPerSec,{s.Network.RxSpeedBytesPerSecond:F1}"));
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Metrics,NetworkTxSpeedBytesPerSec,{s.Network.TxSpeedBytesPerSecond:F1}"));
        }

        if (report.CpuHistory.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("# ІСТОРІЯ ЗАВАНТАЖЕННЯ CPU");
            writer.WriteLine("SecondsAgo,CpuUsagePercentage");
            for (int i = 0; i < report.CpuHistory.Count; i++)
            {
                int secondsAgo = report.CpuHistory.Count - 1 - i;
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{secondsAgo},{report.CpuHistory[i]:F1}"));
            }
        }

        if (report.Processes.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("# ТОП АКТИВНИХ ПРОЦЕСІВ");
            writer.WriteLine("PID,Name,RAM (Bytes),RAM (MB),Threads,Status");
            foreach (var p in report.Processes)
            {
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{p.Pid},{EscapeCsv(p.Name)},{p.RamUsageBytes},{p.RamUsageMb:F2},{p.ThreadCount},{EscapeCsv(p.Status)}"));
            }
        }

        if (report.Incidents.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("# ЖУРНАЛ ПОДІЙ ТА ПОРОГОВИХ СПОВІЩЕНЬ");
            writer.WriteLine("ID,Timestamp,Metric,Severity,ActualValue,ThresholdValue,DurationSec,Message");
            foreach (var inc in report.Incidents)
            {
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{inc.Id},{inc.Timestamp:O},{EscapeCsv(inc.MetricDisplayName)},{inc.Severity},{inc.ActualValue:F2},{inc.ThresholdValue:F2},{inc.OverloadDuration.TotalSeconds:F1},{EscapeCsv(inc.Message)}"));
            }
        }
    }
}
