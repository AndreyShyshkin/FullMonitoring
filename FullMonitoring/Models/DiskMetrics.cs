using System;
using System.Collections.Generic;

namespace FullMonitoring.Models;

/// <summary>
/// Метрики телеметрії для окремого дискового тому або точки монтування.
/// </summary>
public sealed record DriveItemMetrics
{
    /// <summary>
    /// Ідентифікатор або шлях до диска (наприклад, "C:\", "/dev/sda1", "/").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Мітка тому або зручна назва.
    /// </summary>
    public string VolumeLabel { get; init; } = string.Empty;

    /// <summary>
    /// Шлях до кореневої директорії точки монтування.
    /// </summary>
    public string RootDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Тип накопичувача (наприклад, Fixed, Removable, Network).
    /// </summary>
    public string DriveType { get; init; } = string.Empty;

    /// <summary>
    /// Формат файлової системи (наприклад, NTFS, ext4, APFS).
    /// </summary>
    public string DriveFormat { get; init; } = string.Empty;

    /// <summary>
    /// Загальна місткість накопичувача в байтах.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Доступний вільний простір для поточного користувача в байтах.
    /// </summary>
    public long AvailableFreeBytes { get; init; }

    /// <summary>
    /// Загальний вільний простір на накопичувачі в байтах.
    /// </summary>
    public long TotalFreeBytes { get; init; }

    /// <summary>
    /// Зайнятий дисковий простір у байтах.
    /// </summary>
    public long UsedBytes => TotalBytes >= AvailableFreeBytes ? TotalBytes - AvailableFreeBytes : 0;

    /// <summary>
    /// Відсоток заповнення дискового простору (0.0 до 100.0).
    /// </summary>
    public double UsedPercentage => TotalBytes > 0
        ? Math.Clamp((double)UsedBytes / TotalBytes * 100.0, 0.0, 100.0)
        : 0.0;

    /// <summary>
    /// Вказує, чи змонтований накопичувач і готовий до операцій вводу/виводу.
    /// </summary>
    public bool IsReady { get; init; } = true;
}

/// <summary>
/// Агреговані метрики телеметрії для всіх накопичувачів системи.
/// </summary>
public sealed record DiskMetrics
{
    /// <summary>
    /// Колекція окремих змонтованих накопичувачів.
    /// </summary>
    public IReadOnlyList<DriveItemMetrics> Drives { get; init; } = Array.Empty<DriveItemMetrics>();

    /// <summary>
    /// Загальна місткість по всіх активних накопичувачах у байтах.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Загальний зайнятий простір по всіх активних накопичувачах у байтах.
    /// </summary>
    public long UsedBytes { get; init; }

    /// <summary>
    /// Загальний вільний простір по всіх активних накопичувачах у байтах.
    /// </summary>
    public long FreeBytes { get; init; }

    /// <summary>
    /// Загальний відсоток використання накопичувачів (0.0 до 100.0).
    /// </summary>
    public double OverallUsedPercentage => TotalBytes > 0
        ? Math.Clamp((double)UsedBytes / TotalBytes * 100.0, 0.0, 100.0)
        : 0.0;

    public DiskMetrics() { }

    public DiskMetrics(IReadOnlyList<DriveItemMetrics> drives)
    {
        Drives = drives ?? Array.Empty<DriveItemMetrics>();
        long total = 0;
        long used = 0;
        long free = 0;

        foreach (var d in Drives)
        {
            if (d.IsReady && d.TotalBytes > 0)
            {
                total += d.TotalBytes;
                used += d.UsedBytes;
                free += d.AvailableFreeBytes;
            }
        }

        TotalBytes = total;
        UsedBytes = used;
        FreeBytes = free;
    }
}
