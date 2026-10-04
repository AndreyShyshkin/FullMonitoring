using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Interfaces;
using FullMonitoring.Models;

namespace FullMonitoring.Services;

/// <summary>
/// Сервіс моніторингу та керування процесами операційної системи.
/// </summary>
public class ProcessManagerService : IProcessManagerService
{
    private const int DefaultLimit = 50;

    public IReadOnlyList<ProcessItem> GetRunningProcesses(int limit = DefaultLimit)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return Array.Empty<ProcessItem>();
        }

        var result = new List<ProcessItem>(processes.Length);
        foreach (var process in processes)
        {
            try
            {
                int pid = process.Id;
                string name;
                try
                {
                    name = process.ProcessName;
                }
                catch (Exception)
                {
                    name = $"Процес #{pid}";
                }

                long ramBytes = 0;
                try
                {
                    ramBytes = process.WorkingSet64;
                }
                catch (Exception)
                {
                    ramBytes = 0;
                }

                int threads = 0;
                try
                {
                    threads = process.Threads.Count;
                }
                catch (Exception)
                {
                    threads = 0;
                }

                string status = "Виконується";
                try
                {
                    if (process.HasExited)
                    {
                        status = "Завершено";
                    }
                    else if (!process.Responding)
                    {
                        status = "Не відповідає";
                    }
                }
                catch (Exception)
                {
                    status = "Виконується";
                }

                result.Add(new ProcessItem(pid, name, ramBytes, threads, status));
            }
            catch (Exception)
            {
                // Безпечний пропуск процесів, які завершилися під час збору метрик
                continue;
            }
            finally
            {
                // Обов'язкове звільнення системного дескриптора процесу
                try
                {
                    process.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }

        int effectiveLimit = limit > 0 ? limit : DefaultLimit;

        return result
            .OrderByDescending(p => p.RamUsageBytes)
            .Take(effectiveLimit)
            .ToList();
    }

    public Task<IReadOnlyList<ProcessItem>> GetRunningProcessesAsync(int limit = DefaultLimit, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProcessItem>>(Array.Empty<ProcessItem>());
    }

    public bool KillProcess(int pid)
    {
        return false;
    }

    public Task<bool> KillProcessAsync(int pid, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
