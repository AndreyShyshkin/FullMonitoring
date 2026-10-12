using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;
using FullMonitoring.Providers;
using FullMonitoring.Services;
using FullMonitoring.ViewModels;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Модульні тести для перевірки робочих процесів експорту (CSV/JSON) у ViewModel-компонентах (Issue #12).
/// Перевіряє неблокуючу поведінку, інтеграцію з сервісом діалогу та обробку скасувань і помилок.
/// </summary>
public class ExportViewModelsTests : IDisposable
{
    private readonly string _tempDirectory;

    public ExportViewModelsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FullMonitoring_VmExport_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
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
            // Ігноруємо помилки очищення після тестів
        }
    }

    private sealed class MockFileSaveDialogService : IFileSaveDialogService
    {
        public string? PathToReturn { get; set; }
        public FileSaveDialogOptions? LastOptions { get; private set; }
        public int CallCount { get; private set; }

        public MockFileSaveDialogService(string? pathToReturn)
        {
            PathToReturn = pathToReturn;
        }

        public Task<string?> ShowSaveFileDialogAsync(FileSaveDialogOptions options)
        {
            CallCount++;
            LastOptions = options;
            return Task.FromResult(PathToReturn);
        }
    }

    private sealed class MockProcessManager : IProcessManagerService
    {
        public IReadOnlyList<ProcessItem> Processes { get; set; } = new List<ProcessItem>
        {
            new(101, "proc1.exe", 100L * 1024 * 1024, 5, "Виконується"),
            new(202, "proc2.exe", 200L * 1024 * 1024, 10, "Виконується")
        };

        public IReadOnlyList<ProcessItem> GetRunningProcesses(int limit = 50) => Processes;
        public Task<IReadOnlyList<ProcessItem>> GetRunningProcessesAsync(int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult(Processes);
        public bool KillProcess(int processId) => true;
        public Task<bool> KillProcessAsync(int processId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task ProcessesViewModel_ExportToCsvCommand_InvokesDialogAndSavesCsv()
    {
        // Arrange
        string targetFile = Path.Combine(_tempDirectory, "out_proc.csv");
        var dialogMock = new MockFileSaveDialogService(targetFile);
        var exportService = new ExportService();
        var procMock = new MockProcessManager();
        var vm = new ProcessesViewModel(procMock, exportService, dialogMock);

        // Act
        await vm.ExportToCsvCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, dialogMock.CallCount);
        Assert.NotNull(dialogMock.LastOptions);
        Assert.Equal(ExportFormatOption.Csv, dialogMock.LastOptions.Format);
        Assert.True(File.Exists(targetFile));

        string[] lines = await File.ReadAllLinesAsync(targetFile);
        Assert.True(lines.Length >= 3);
        Assert.StartsWith("PID,Name,", lines[0]);
        Assert.Contains("proc1.exe", lines[1]);
        Assert.Contains("збережено", vm.StatusMessage);
    }

    [Fact]
    public async Task ProcessesViewModel_ExportToJsonCommand_InvokesDialogAndSavesJson()
    {
        // Arrange
        string targetFile = Path.Combine(_tempDirectory, "out_proc.json");
        var dialogMock = new MockFileSaveDialogService(targetFile);
        var exportService = new ExportService();
        var procMock = new MockProcessManager();
        var vm = new ProcessesViewModel(procMock, exportService, dialogMock);

        // Act
        await vm.ExportToJsonCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, dialogMock.CallCount);
        Assert.NotNull(dialogMock.LastOptions);
        Assert.Equal(ExportFormatOption.Json, dialogMock.LastOptions.Format);
        Assert.True(File.Exists(targetFile));

        string json = await File.ReadAllTextAsync(targetFile);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ProcessesViewModel_WhenDialogCancelled_DoesNotWriteFile()
    {
        // Arrange
        var dialogMock = new MockFileSaveDialogService(null);
        var exportService = new ExportService();
        var procMock = new MockProcessManager();
        var vm = new ProcessesViewModel(procMock, exportService, dialogMock);
        string initialStatus = vm.StatusMessage;

        // Act
        await vm.ExportToCsvCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, dialogMock.CallCount);
        Assert.Equal(initialStatus, vm.StatusMessage);
    }

    [Fact]
    public async Task OverviewViewModel_ExportReportToJsonCommand_SavesValidTelemetryReport()
    {
        // Arrange
        string targetFile = Path.Combine(_tempDirectory, "out_report.json");
        var dialogMock = new MockFileSaveDialogService(targetFile);
        var exportService = new ExportService();
        var telemetryProvider = new MockTelemetryProvider();
        var vm = new OverviewViewModel(telemetryProvider, exportService, dialogMock, autoStartPolling: false);

        // Act
        await vm.ExportReportToJsonCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, dialogMock.CallCount);
        Assert.Equal(ExportFormatOption.Json, dialogMock.LastOptions?.Format);
        Assert.True(File.Exists(targetFile));

        string json = await File.ReadAllTextAsync(targetFile);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("1.0", doc.RootElement.GetProperty("reportVersion").GetString());
        Assert.Contains("успішно збережено", vm.ExportStatusMessage);
    }

    [Fact]
    public async Task OverviewViewModel_ExportReportToCsvCommand_SavesValidCsv()
    {
        // Arrange
        string targetFile = Path.Combine(_tempDirectory, "out_report.csv");
        var dialogMock = new MockFileSaveDialogService(targetFile);
        var exportService = new ExportService();
        var telemetryProvider = new MockTelemetryProvider();
        var vm = new OverviewViewModel(telemetryProvider, exportService, dialogMock, autoStartPolling: false);

        // Act
        await vm.ExportReportToCsvCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, dialogMock.CallCount);
        Assert.Equal(ExportFormatOption.Csv, dialogMock.LastOptions?.Format);
        Assert.True(File.Exists(targetFile));

        string csv = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("# СИСТЕМНА ІНФОРМАЦІЯ ТА ЗРІЗ ТЕЛЕМЕТРІЇ", csv);
    }

    [Fact]
    public async Task EventsLogViewModel_ExportToCsvCommand_SavesIncidentsCsv()
    {
        // Arrange
        string targetFile = Path.Combine(_tempDirectory, "out_events.csv");
        var dialogMock = new MockFileSaveDialogService(targetFile);
        var exportService = new ExportService();
        using var alertService = new AlertService(new ThresholdSettings(90, 85, 90, durationThresholdSeconds: 1.0));
        var baseTime = DateTimeOffset.UtcNow;
        alertService.ProcessMetrics(95, 50, 50, baseTime);
        alertService.ProcessMetrics(95, 50, 50, baseTime.AddSeconds(2));

        var vm = new EventsLogViewModel(alertService, exportService, dialogMock);

        // Act
        await vm.ExportToCsvCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, dialogMock.CallCount);
        Assert.Equal(ExportFormatOption.Csv, dialogMock.LastOptions?.Format);
        Assert.True(File.Exists(targetFile));

        string content = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("Severity", content);
        Assert.Contains("Critical", content);
        Assert.Contains("успішно збережено", vm.StatusMessage);
    }

    [Fact]
    public async Task EventsLogViewModel_ExportToJsonCommand_SavesIncidentsJson()
    {
        // Arrange
        string targetFile = Path.Combine(_tempDirectory, "out_events.json");
        var dialogMock = new MockFileSaveDialogService(targetFile);
        var exportService = new ExportService();
        using var alertService = new AlertService(new ThresholdSettings(90, 85, 90, durationThresholdSeconds: 1.0));
        var baseTime = DateTimeOffset.UtcNow;
        alertService.ProcessMetrics(95, 50, 50, baseTime);
        alertService.ProcessMetrics(95, 50, 50, baseTime.AddSeconds(2));

        var vm = new EventsLogViewModel(alertService, exportService, dialogMock);

        // Act
        await vm.ExportToJsonCommand.ExecuteAsync(null);

        // Assert
        Assert.True(File.Exists(targetFile));
        string json = await File.ReadAllTextAsync(targetFile);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Single(doc.RootElement.EnumerateArray());
    }
}
