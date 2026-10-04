using System;
using System.Collections.Generic;

namespace FullMonitoring.Models;

/// <summary>
/// Метрики телеметрії для окремого мережевого інтерфейсу.
/// </summary>
public sealed record NetworkInterfaceMetrics
{
    /// <summary>
    /// Ідентифікатор інтерфейсу (наприклад, GUID або системна назва пристрою).
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Зручна назва інтерфейсу (наприклад, "eth0", "en0", "Wi-Fi").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Опис обладнання або драйвера інтерфейсу.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Тип інтерфейсу (Ethernet, Wireless80211, Loopback тощо).
    /// </summary>
    public string InterfaceType { get; init; } = string.Empty;

    /// <summary>
    /// Операційний стан (Up, Down, Testing, Unknown тощо).
    /// </summary>
    public string OperationalStatus { get; init; } = string.Empty;

    /// <summary>
    /// Швидкість з'єднання в бітах за секунду.
    /// </summary>
    public long SpeedBitsPerSecond { get; init; }

    /// <summary>
    /// Загальна кількість отриманих байтів (RX).
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// Загальна кількість відправлених байтів (TX).
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// Поточна швидкість завантаження в байтах за секунду.
    /// </summary>
    public double RxSpeedBytesPerSecond { get; init; }

    /// <summary>
    /// Поточна швидкість передачі в байтах за секунду.
    /// </summary>
    public double TxSpeedBytesPerSecond { get; init; }
}

/// <summary>
/// Агреговані метрики мережевої активності всієї системи.
/// </summary>
public sealed record NetworkMetrics
{
    /// <summary>
    /// Колекція виявлених мережевих інтерфейсів та їхній поточний стан.
    /// </summary>
    public IReadOnlyList<NetworkInterfaceMetrics> Interfaces { get; init; } = Array.Empty<NetworkInterfaceMetrics>();

    /// <summary>
    /// Загальна кількість отриманих байтів по всіх активних інтерфейсах.
    /// </summary>
    public long TotalBytesReceived { get; init; }

    /// <summary>
    /// Загальна кількість відправлених байтів по всіх активних інтерфейсах.
    /// </summary>
    public long TotalBytesSent { get; init; }

    /// <summary>
    /// Сумарна швидкість завантаження по всіх інтерфейсах у байтах за секунду.
    /// </summary>
    public double RxSpeedBytesPerSecond { get; init; }

    /// <summary>
    /// Сумарна швидкість відправлення по всіх інтерфейсах у байтах за секунду.
    /// </summary>
    public double TxSpeedBytesPerSecond { get; init; }

    public NetworkMetrics() { }

    public NetworkMetrics(IReadOnlyList<NetworkInterfaceMetrics> interfaces)
    {
        Interfaces = interfaces ?? Array.Empty<NetworkInterfaceMetrics>();
        long rx = 0;
        long tx = 0;
        double rxSpeed = 0;
        double txSpeed = 0;

        foreach (var nic in Interfaces)
        {
            rx += nic.BytesReceived;
            tx += nic.BytesSent;
            rxSpeed += nic.RxSpeedBytesPerSecond;
            txSpeed += nic.TxSpeedBytesPerSecond;
        }

        TotalBytesReceived = rx;
        TotalBytesSent = tx;
        RxSpeedBytesPerSecond = rxSpeed;
        TxSpeedBytesPerSecond = txSpeed;
    }
}
