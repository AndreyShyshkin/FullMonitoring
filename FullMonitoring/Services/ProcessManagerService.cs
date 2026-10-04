using System;
using System.Collections.Generic;
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
        return Array.Empty<ProcessItem>();
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
