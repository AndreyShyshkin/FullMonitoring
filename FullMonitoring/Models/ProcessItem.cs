namespace FullMonitoring.Models;

/// <summary>
/// Модель даних для збереження інформації про активний процес.
/// </summary>
public sealed record ProcessItem
{
    /// <summary>
    /// Ідентифікатор процесу (PID).
    /// </summary>
    public int Pid { get; init; }

    /// <summary>
    /// Назва процесу.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Обсяг оперативної пам'яті у байтах.
    /// </summary>
    public long RamUsageBytes { get; init; }

    /// <summary>
    /// Кількість потоків процесу.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Поточний стан процесу.
    /// </summary>
    public string Status { get; init; } = "Виконується";
}
