using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FullMonitoring.Models;

namespace FullMonitoring.Interfaces;

/// <summary>
/// Інтерфейс сервісу для моніторингу та керування активними процесами.
/// </summary>
public interface IProcessManagerService
{
    /// <summary>
    /// Отримує список активних процесів із зазначеним обмеженням кількості.
    /// </summary>
    /// <param name="limit">Максимальна кількість процесів.</param>
    /// <returns>Список процесів.</returns>
    IReadOnlyList<ProcessItem> GetRunningProcesses(int limit = 50);

    /// <summary>
    /// Асинхронно отримує список активних процесів.
    /// </summary>
    /// <param name="limit">Максимальна кількість процесів.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>Список процесів.</returns>
    Task<IReadOnlyList<ProcessItem>> GetRunningProcessesAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершує процес за вказаним ідентифікатором (PID).
    /// </summary>
    /// <param name="pid">Ідентифікатор процесу.</param>
    /// <returns>True, якщо процес зупинено або він не існує; інакше False.</returns>
    bool KillProcess(int pid);

    /// <summary>
    /// Асинхронно завершує процес за вказаним ідентифікатором (PID).
    /// </summary>
    /// <param name="pid">Ідентифікатор процесу.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns>True, якщо процес зупинено; інакше False.</returns>
    Task<bool> KillProcessAsync(int pid, CancellationToken cancellationToken = default);
}
