using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Models;
using FullMonitoring.Services;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для перевірки JSON-серіалізації та валідності збережених файлів у ExportService (Issue #12 DoD).
/// </summary>
public sealed class ExportServiceJsonTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ExportService _exportService;

    public ExportServiceJsonTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FullMonitoring_JsonTests_" + Guid.NewGuid().ToString("N"));
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
            // Ігноруємо помилки очищення тимчасових файлів після тестів
        }
    }

    [Fact]
    public async Task ExportToJsonAsync_TelemetryReport_ProducesValidJsonWithReadableUnicode()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "report.json");
        var snapshot = new SystemTelemetrySnapshot
        {
            OsDescription = "Microsoft Windows 11 Pro 64-bit",
            Architecture = "X64",
            MachineName = "TEST-PC",
            Uptime = TimeSpan.FromHours(5),
            Cpu = new CpuMetrics { TotalUsagePercentage = 42.5, TemperatureCelsius = 58.0 },
            Ram = new RamMetrics(16L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024, 50.0)
        };

        var processes = new List<ProcessItem>
        {
            new(101, "FullMonitoring.exe", 150 * 1024 * 1024, 12, "Виконується"),
            new(202, "dotnet.exe", 80 * 1024 * 1024, 8, "Виконується")
        };

        var incidents = new List<AlertIncident>
        {
            new(AlertMetricType.CpuUsage, 95.0, 90.0, TimeSpan.FromSeconds(3), AlertSeverity.Critical, DateTimeOffset.UtcNow, "Критичне завантаження CPU")
        };

        var report = TelemetryReport.Create(snapshot, new[] { 10.0, 25.5, 42.5 }, processes, incidents);

        // Act
        await _exportService.ExportToJsonAsync(filePath, report);

        // Assert - DoD: Файли зберігаються у валідному форматі (перевірено валідатором JSON)
        Assert.True(File.Exists(filePath));
        string jsonContent = await File.ReadAllTextAsync(filePath);
        Assert.False(string.IsNullOrWhiteSpace(jsonContent));

        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        Assert.Equal("1.0", root.GetProperty("reportVersion").GetString());
        Assert.Equal("TEST-PC", root.GetProperty("machineName").GetString());
        Assert.Equal("Microsoft Windows 11 Pro 64-bit", root.GetProperty("osDescription").GetString());

        var snapshotEl = root.GetProperty("snapshot");
        Assert.Equal(42.5, snapshotEl.GetProperty("cpu").GetProperty("totalUsagePercentage").GetDouble());

        var processesEl = root.GetProperty("processes");
        Assert.Equal(2, processesEl.GetArrayLength());
        Assert.Equal("FullMonitoring.exe", processesEl[0].GetProperty("name").GetString());
        Assert.Equal("Виконується", processesEl[0].GetProperty("status").GetString());

        var incidentsEl = root.GetProperty("incidents");
        Assert.Equal(1, incidentsEl.GetArrayLength());
        Assert.Equal("Критичне завантаження CPU", incidentsEl[0].GetProperty("message").GetString());

        // Перевіряємо, що кирилиця не екранується як \uXXXX завдяки UnsafeRelaxedJsonEscaping
        Assert.Contains("Виконується", jsonContent);
    }

    [Fact]
    public void ExportToJson_Sync_TelemetryReport_WritesValidFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "report_sync.json");
        var report = new TelemetryReport
        {
            MachineName = "SYNC-PC",
            OsDescription = "Linux Ubuntu 24.04",
            CpuHistory = new[] { 12.0, 15.0 }
        };

        // Act
        _exportService.ExportToJson(filePath, report);

        // Assert
        Assert.True(File.Exists(filePath));
        string json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("SYNC-PC", doc.RootElement.GetProperty("machineName").GetString());
    }

    [Fact]
    public async Task ExportProcessesToJsonAsync_ProducesValidJsonArray()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "processes.json");
        var list = new List<ProcessItem>
        {
            new(1000, "code.exe", 250 * 1024 * 1024, 25, "Виконується"),
            new(2000, "chrome.exe", 500 * 1024 * 1024, 40, "Виконується")
        };

        // Act
        await _exportService.ExportProcessesToJsonAsync(filePath, list);

        // Assert
        Assert.True(File.Exists(filePath));
        string json = await File.ReadAllTextAsync(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal(1000, doc.RootElement[0].GetProperty("pid").GetInt32());
    }

    [Fact]
    public async Task ExportIncidentsToJsonAsync_ProducesValidJsonArray()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "incidents.json");
        var incidents = new List<AlertIncident>
        {
            new(AlertMetricType.RamUsage, 92.0, 90.0, TimeSpan.FromSeconds(5), AlertSeverity.Warning)
        };

        // Act
        await _exportService.ExportIncidentsToJsonAsync(filePath, incidents);

        // Assert
        Assert.True(File.Exists(filePath));
        string json = await File.ReadAllTextAsync(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Single(doc.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ExportToJsonAsync_AutoCreatesTargetDirectory_IfMissing()
    {
        // Arrange
        string nestedPath = Path.Combine(_tempDirectory, "nested", "folder", "output.json");
        var report = new TelemetryReport { MachineName = "NESTED" };

        // Act
        await _exportService.ExportToJsonAsync(nestedPath, report);

        // Assert
        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public async Task ExportToJsonAsync_ThrowsOnNullOrEmptyPath()
    {
        var report = new TelemetryReport();
        await Assert.ThrowsAsync<ArgumentException>(() => _exportService.ExportToJsonAsync(string.Empty, report));
        await Assert.ThrowsAsync<ArgumentException>(() => _exportService.ExportToJsonAsync("   ", report));
    }

    [Fact]
    public async Task ExportToJsonAsync_WithCancellation_CancelsOperation()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "cancelled.json");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _exportService.ExportToJsonAsync(filePath, new TelemetryReport(), cts.Token));
    }
}
