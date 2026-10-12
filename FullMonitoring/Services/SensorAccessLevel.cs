namespace FullMonitoring.Services;

/// <summary>
/// Рівень доступу до апаратних сенсорів (Issue #9).
/// Використовується статус-баром для інформування про обмеження політики безпеки ОС.
/// </summary>
public enum SensorAccessLevel
{
    /// <summary>
    /// Доступ до сенсорів обмежено: температура та оберти кулера недоступні (Safe Mode).
    /// </summary>
    Restricted = 0,

    /// <summary>
    /// Частковий доступ: доступний лише один із сенсорів (температура або кулер).
    /// </summary>
    Limited = 1,

    /// <summary>
    /// Повний доступ до всіх сенсорів.
    /// </summary>
    Full = 2
}
