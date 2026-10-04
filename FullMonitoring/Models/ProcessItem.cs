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

    /// <summary>
    /// Обсяг оперативної пам'яті у мегабайтах (для зручного відображення).
    /// </summary>
    public double RamUsageMb => Math.Round(RamUsageBytes / (1024.0 * 1024.0), 2);

    public ProcessItem() { }

    public ProcessItem(int pid, string name, long ramUsageBytes, int threadCount, string status = "Виконується")
    {
        Pid = pid;
        Name = string.IsNullOrWhiteSpace(name) ? "Невідомо" : name.Trim();
        RamUsageBytes = Math.Max(0, ramUsageBytes);
        ThreadCount = Math.Max(0, threadCount);
        Status = string.IsNullOrWhiteSpace(status) ? "Виконується" : status.Trim();
    }
}
