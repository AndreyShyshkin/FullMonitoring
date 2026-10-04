using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FullMonitoring.Services;
using Xunit;

namespace FullMonitoring.Tests;

/// <summary>
/// Набір модульних тестів для перевірки сервісу ProcessManagerService.
/// </summary>
public class ProcessManagerServiceTests
{
    private readonly ProcessManagerService _service = new();

    [Fact]
    public void GetRunningProcesses_ReturnsNonEmptyList()
    {
        var processes = _service.GetRunningProcesses();

        Assert.NotNull(processes);
        Assert.NotEmpty(processes);
    }

    [Fact]
    public void GetRunningProcesses_DefaultLimit_DoesNotExceedFifty()
    {
        var processes = _service.GetRunningProcesses();

        Assert.True(processes.Count <= 50, "Кількість процесів за замовчуванням не повинна перевищувати 50");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetRunningProcesses_CustomLimit_IsRespected(int limit)
    {
        var processes = _service.GetRunningProcesses(limit);

        Assert.NotNull(processes);
        Assert.True(processes.Count <= limit);
    }

    [Fact]
    public void GetRunningProcesses_IsSortedByRamUsageDescending()
    {
        var processes = _service.GetRunningProcesses(50);

        Assert.NotEmpty(processes);

        for (int i = 0; i < processes.Count - 1; i++)
        {
            Assert.True(
                processes[i].RamUsageBytes >= processes[i + 1].RamUsageBytes,
                $"Процеси мають бути відсортовані за спаданням пам'яті: {processes[i].RamUsageBytes} < {processes[i + 1].RamUsageBytes} (індекси {i} та {i + 1})"
            );
        }
    }

    [Fact]
    public void GetRunningProcesses_ItemsHaveValidProperties()
    {
        var processes = _service.GetRunningProcesses(20);

        foreach (var proc in processes)
        {
            Assert.True(proc.Pid >= 0);
            Assert.False(string.IsNullOrWhiteSpace(proc.Name));
            Assert.True(proc.RamUsageBytes >= 0);
            Assert.True(proc.ThreadCount >= 0);
            Assert.False(string.IsNullOrWhiteSpace(proc.Status));
        }
    }

    [Fact]
    public async Task GetRunningProcessesAsync_ReturnsValidProcesses()
    {
        var processes = await _service.GetRunningProcessesAsync(15);

        Assert.NotNull(processes);
        Assert.NotEmpty(processes);
        Assert.True(processes.Count <= 15);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void KillProcess_InvalidPid_ReturnsFalse(int invalidPid)
    {
        bool result = _service.KillProcess(invalidPid);

        Assert.False(result);
    }

    [Fact]
    public void KillProcess_NonExistentPid_ReturnsFalse()
    {
        // Неіснуючий PID не повинен викликати падіння застосунку
        int nonExistentPid = 99999999;

        bool result = _service.KillProcess(nonExistentPid);

        Assert.False(result);
    }

    [Fact]
    public void KillProcess_SystemProcess_SafelyHandlesAccessDenied()
    {
        // PID 4 зазвичай є системним процесом (System) на Windows
        // Сервіс повинен безпечно перехопити Win32Exception і повернути false
        int systemPid = 4;

        bool result = _service.KillProcess(systemPid);

        Assert.False(result);
    }

    [Fact]
    public void KillProcess_ValidActiveProcess_TerminatesSuccessfully()
    {
        // Запускаємо тестовий дочірній процес, який чекає
        using var testProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping 127.0.0.1 -n 15 > nul",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Assert.NotNull(testProcess);
        int pid = testProcess.Id;

        try
        {
            bool killed = _service.KillProcess(pid);
            Assert.True(killed, "Метод KillProcess повинен повернути true для успішно завершеного процесу");

            bool exited = testProcess.WaitForExit(3000);
            Assert.True(exited, "Процес мав фактично завершити виконання");
        }
        finally
        {
            try
            {
                if (!testProcess.HasExited)
                {
                    testProcess.Kill();
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task KillProcessAsync_InvalidPid_ReturnsFalse()
    {
        bool result = await _service.KillProcessAsync(-5);

        Assert.False(result);
    }
}
