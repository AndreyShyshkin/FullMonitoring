namespace FullMonitoring.Models;

/// <summary>
/// Перелік системних станів навантаження або температури обладнання.
/// </summary>
public enum SystemHealthState
{
    /// <summary>
    /// Нормальний режим роботи системи.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Підвищене навантаження, що потребує уваги (попередження).
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Критичне перевантаження або перегрів вузла системи.
    /// </summary>
    Critical = 2
}
