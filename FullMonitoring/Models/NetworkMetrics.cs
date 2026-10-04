using System;
using System.Collections.Generic;

namespace FullMonitoring.Models;

/// <summary>
/// Telemetry metrics for an individual network interface.
/// </summary>
public sealed record NetworkInterfaceMetrics
{
    /// <summary>
    /// Interface identifier (e.g., GUID or device name).
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Friendly interface name (e.g., "eth0", "en0", "Wi-Fi").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Interface hardware or driver description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Interface type (Ethernet, Wireless80211, Loopback, etc.).
    /// </summary>
    public string InterfaceType { get; init; } = string.Empty;

    /// <summary>
    /// Operational status (Up, Down, Testing, Unknown, etc.).
    /// </summary>
    public string OperationalStatus { get; init; } = string.Empty;

    /// <summary>
    /// Link speed in bits per second.
    /// </summary>
    public long SpeedBitsPerSecond { get; init; }

    /// <summary>
    /// Total received bytes counter (RX).
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// Total transmitted bytes counter (TX).
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// Current download rate in bytes per second.
    /// </summary>
    public double RxSpeedBytesPerSecond { get; init; }

    /// <summary>
    /// Current upload rate in bytes per second.
    /// </summary>
    public double TxSpeedBytesPerSecond { get; init; }
}

/// <summary>
/// Telemetry metrics aggregating system-wide network activity.
/// </summary>
public sealed record NetworkMetrics
{
    /// <summary>
    /// Collection of detected network interfaces and their current status.
    /// </summary>
    public IReadOnlyList<NetworkInterfaceMetrics> Interfaces { get; init; } = Array.Empty<NetworkInterfaceMetrics>();

    /// <summary>
    /// Total received bytes across all active interfaces.
    /// </summary>
    public long TotalBytesReceived { get; init; }

    /// <summary>
    /// Total transmitted bytes across all active interfaces.
    /// </summary>
    public long TotalBytesSent { get; init; }

    /// <summary>
    /// Combined download speed across all interfaces in bytes per second.
    /// </summary>
    public double RxSpeedBytesPerSecond { get; init; }

    /// <summary>
    /// Combined upload speed across all interfaces in bytes per second.
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
