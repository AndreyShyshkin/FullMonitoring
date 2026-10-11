namespace FullMonitoring.Models;

/// <summary>
/// Рівень серйозності інциденту порогового сповіщення.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Попереджувальний рівень.
    /// </summary>
    Warning,

    /// <summary>
    /// Критичний рівень навантаження чи температури.
    /// </summary>
    Critical
}
