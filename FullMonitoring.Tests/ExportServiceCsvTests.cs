using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Models;
using FullMonitoring.Services;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для перевірки CSV-серіалізації (RFC 4180), UTF-8 BOM та екранування у ExportService (Issue #12 DoD).
/// </summary>
public class ExportServiceCsvTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ExportService _exportService;

    public ExportServiceCsvTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FullMonitoring_CsvTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _exportService = new ExportService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ігноруємо помилки очищення тимчасових файлів
        }
    }

    [Fact]
    public async Task ExportToCsvAsync_Processes_IncludesUtf8BomAndValidHeaders()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "processes.csv");
        var processes = new List<ProcessItem>
        {
            new(1001, "explorer.exe", 120L * 1024 * 1024, 18, "Виконується"),
            new(1002, "FullMonitoring.exe", 85L * 1024 * 1024, 8, "Виконується")
        };

        // Act
        await _exportService.ExportToCsvAsync(filePath, processes);

        // Assert - DoD: перевірка наявності UTF-8 BOM для сумісності з Excel
        byte[] bytes = await File.ReadAllBytesAsync(filePath);
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        string[] lines = (await File.ReadAllLinesAsync(filePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        Assert.Equal(3, lines.Length);
        Assert.Equal("PID,Name,RAM (Bytes),RAM (MB),Threads,Status", lines[0]);

        // Рядок 1
        var row1Parts = lines[1].Split(',');
        Assert.Equal("1001", row1Parts[0]);
        Assert.Equal("explorer.exe", row1Parts[1]);
        Assert.Equal((120L * 1024 * 1024).ToString(), row1Parts[2]);
        Assert.Equal("120.00", row1Parts[3]);
        Assert.Equal("18", row1Parts[4]);
        Assert.Equal("Виконується", row1Parts[5]);
    }

    [Fact]
    public async Task ExportToCsvAsync_Processes_EscapesSpecialCharactersPerRfc4180()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "escaped_processes.csv");
        var processes = new List<ProcessItem>
        {
            new(500, "Service, With Comma", 50L * 1024 * 1024, 4, "Статус, 1"),
            new(600, "App \"With Quotes\"", 60L * 1024 * 1024, 6, "Статус \"ОК\""),
            new(700, "Process;Semicolon", 70L * 1024 * 1024, 2, "OK")
        };

        // Act
        await _exportService.ExportToCsvAsync(filePath, processes);

        // Assert
        string text = await File.ReadAllTextAsync(filePath);
        Assert.Contains("\"Service, With Comma\"", text);
        Assert.Contains("\"Статус, 1\"", text);
        Assert.Contains("\"App \"\"With Quotes\"\"\"", text);
        Assert.Contains("\"Статус \"\"ОК\"\"\"", text);
        Assert.Contains("\"Process;Semicolon\"", text);
    }

    [Fact]
    public void ExportToCsv_Sync_Processes_WritesValidFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "processes_sync.csv");
        var processes = new List<ProcessItem>
        {
            new(1, "system", 1024, 1, "Виконується")
        };

        // Act
        _exportService.ExportToCsv(filePath, processes);

        // Assert
        Assert.True(File.Exists(filePath));
        string[] lines = File.ReadAllLines(filePath);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("PID,Name,", lines[0]);
        Assert.StartsWith("1,system,", lines[1]);
    }

    [Fact]
    public async Task ExportToCsvAsync_Incidents_OutputsCorrectColumns()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "incidents.csv");
        var incidents = new List<AlertIncident>
        {
            new(AlertMetricType.CpuUsage, 96.5, 90.0, TimeSpan.FromSeconds(2.5), AlertSeverity.Critical, new DateTimeOffset(2026, 10, 12, 14, 30, 0, TimeSpan.Zero), "Перевищення ліміту CPU")
        };

        // Act
        await _exportService.ExportToCsvAsync(filePath, incidents);

        // Assert
        Assert.True(File.Exists(filePath));
        string[] lines = await File.ReadAllLinesAsync(filePath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("ID,Timestamp (UTC),Timestamp (Local),Metric,Severity,Actual Value,Threshold Value,Duration (sec),Message", lines[0]);

        string dataRow = lines[1];
        Assert.Contains("Critical", dataRow);
        Assert.Contains("96.50", dataRow);
        Assert.Contains("90.00", dataRow);
        Assert.Contains("2.5", dataRow);
        Assert.Contains("Перевищення ліміту CPU", dataRow);
    }

    [Fact]
    public async Task ExportToCsvAsync_TelemetryReport_OutputsAllSections()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "full_report.csv");
        var snapshot = new SystemTelemetrySnapshot
        {
            MachineName = "SRV-TEST",
            OsDescription = "macOS Sonoma 14.5",
            Architecture = "Arm64",
            Uptime = TimeSpan.FromDays(2),
            Cpu = new CpuMetrics { TotalUsagePercentage = 33.3, TemperatureCelsius = 45.0 },
            Ram = new RamMetrics(32L * 1024 * 1024 * 1024, 16L * 1024 * 1024 * 1024, 16L * 1024 * 1024 * 1024, 50.0),
            Disk = new DiskMetrics(Array.Empty<DriveItemMetrics>()),
            Network = new NetworkMetrics(Array.Empty<NetworkInterfaceMetrics>())
        };

        var processes = new[] { new ProcessItem(10, "launchd", 2048, 1) };
        var incidents = new[] { new AlertIncident(AlertMetricType.RamUsage, 91.0, 90.0, TimeSpan.FromSeconds(1)) };
        var cpuHistory = new[] { 10.0, 20.0, 33.3 };

        var report = TelemetryReport.Create(snapshot, cpuHistory, processes, incidents);

        // Act
        await _exportService.ExportToCsvAsync(filePath, report);

        // Assert
        Assert.True(File.Exists(filePath));
        string content = await File.ReadAllTextAsync(filePath);

        Assert.Contains("# СИСТЕМНА ІНФОРМАЦІЯ ТА ЗРІЗ ТЕЛЕМЕТРІЇ", content);
        Assert.Contains("SRV-TEST", content);
        Assert.Contains("macOS Sonoma 14.5", content);
        Assert.Contains("Arm64", content);
        Assert.Contains("CpuUsagePercentage,33.3", content);

        Assert.Contains("# ІСТОРІЯ ЗАВАНТАЖЕННЯ CPU", content);
        Assert.Contains("SecondsAgo,CpuUsagePercentage", content);

        Assert.Contains("# ТОП АКТИВНИХ ПРОЦЕСІВ", content);
        Assert.Contains("10,launchd", content);

        Assert.Contains("# ЖУРНАЛ ПОДІЙ ТА ПОРОГОВИХ СПОВІЩЕНЬ", content);
        Assert.Contains("RamUsage", content);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("hello,world", "\"hello,world\"")]
    [InlineData("hello\"world", "\"hello\"\"world\"")]
    [InlineData("hello;world", "\"hello;world\"")]
    [InlineData("hello\nworld", "\"hello\nworld\"")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void EscapeCsv_HandlesAllCasesCorrectly(string? input, string expected)
    {
        string actual = ExportService.EscapeCsv(input);
        Assert.Equal(expected, actual);
    }
}
