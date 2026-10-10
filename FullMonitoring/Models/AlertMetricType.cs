namespace FullMonitoring.Models;

/// <summary>
/// Тип метрики телеметрії, для якої зареєстровано критичне перевищення порогового значення.
/// </summary>
public enum AlertMetricType
{
    /// <summary>
    /// Навантаження центрального процесора (CPU usage percentage).
    /// </summary>
    CpuUsage,

    /// <summary>
    /// Температура центрального процесора (CPU temperature).
    /// </summary>
    CpuTemperature,

    /// <summary>
    /// Заповнення оперативної пам'яті (RAM usage percentage).
    /// </summary>
    RamUsage
}
