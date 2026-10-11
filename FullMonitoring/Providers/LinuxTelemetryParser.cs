using System;
using System.Collections.Generic;
using System.Globalization;

namespace FullMonitoring.Providers;

/// <summary>
/// Низькорівневий парсер текстових псевдофайлів ядра Linux (procfs / sysfs).
/// Для мінімізації аллокацій у купі (GC) весь розбір виконується над <see cref="ReadOnlySpan{T}"/>,
/// без створення проміжних рядків та підрядків.
/// </summary>
internal static class LinuxTelemetryParser
{
    /// <summary>
    /// Зріз показників часу CPU у тіках ядра (jiffies): час простою та сумарний час.
    /// </summary>
    internal readonly record struct CpuTimeSample(long Idle, long Total);

    /// <summary>
    /// Знімок показників файлу <c>/proc/stat</c> для агрегованого CPU та кожного логічного ядра.
    /// </summary>
    internal sealed class CpuStatSnapshot
    {
        /// <summary>
        /// Показники агрегованого рядка <c>cpu</c> (усі ядра разом).
        /// </summary>
        public CpuTimeSample Aggregate { get; set; }

        /// <summary>
        /// Показники окремих логічних ядер <c>cpuN</c> у порядку їх появи у файлі.
        /// </summary>
        public CpuTimeSample[] Cores { get; set; } = Array.Empty<CpuTimeSample>();
    }

    /// <summary>
    /// Розбирає вміст <c>/proc/stat</c>, виділяючи сумарний час CPU та час кожного логічного ядра.
    /// </summary>
    /// <param name="content">Повний текст файлу <c>/proc/stat</c>.</param>
    /// <param name="snapshot">Розібраний знімок показників.</param>
    /// <returns><c>true</c>, якщо агрегований рядок <c>cpu</c> було успішно розібрано.</returns>
    internal static bool TryParseCpuStat(ReadOnlySpan<char> content, out CpuStatSnapshot snapshot)
    {
        snapshot = new CpuStatSnapshot();

        var cores = new List<CpuTimeSample>();
        var aggregate = default(CpuTimeSample);
        bool hasAggregate = false;
        bool started = false;

        while (!content.IsEmpty)
        {
            int newLineIndex = content.IndexOf('\n');
            ReadOnlySpan<char> line = newLineIndex >= 0 ? content[..newLineIndex] : content;
            content = newLineIndex >= 0 ? content[(newLineIndex + 1)..] : ReadOnlySpan<char>.Empty;

            line = line.Trim();
            if (line.IsEmpty)
            {
                continue;
            }

            // Рядки CPU завжди йдуть першими. Будь-який інший розділ (intr, ctxt, btime, processes...)
            // означає, що блок CPU завершився, тому подальше читання не має сенсу.
            if (!line.StartsWith("cpu", StringComparison.Ordinal))
            {
                if (started)
                {
                    break;
                }

                continue;
            }

            started = true;
            if (!TryParseCpuLine(line, out var sample, out bool isCore))
            {
                continue;
            }

            if (isCore)
            {
                cores.Add(sample);
            }
            else
            {
                aggregate = sample;
                hasAggregate = true;
            }
        }

        snapshot.Aggregate = aggregate;
        snapshot.Cores = cores.ToArray();
        return hasAggregate;
    }

    /// <summary>
    /// Розбирає один рядок <c>/proc/stat</c> (наприклад, <c>cpu0 user nice system idle ...</c>).
    /// </summary>
    private static bool TryParseCpuLine(ReadOnlySpan<char> line, out CpuTimeSample sample, out bool isCore)
    {
        sample = default;
        isCore = false;

        // Пропускаємо префікс "cpu" та необов'язковий числовий індекс ядра ("cpu0", "cpu15").
        int index = 3;
        while (index < line.Length && line[index] >= '0' && line[index] <= '9')
        {
            index++;
        }

        isCore = index > 3;

        long total = 0;
        long idle = 0;
        int field = 0;

        while (index < line.Length)
        {
            while (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
            {
                index++;
            }

            if (index >= line.Length)
            {
                break;
            }

            int start = index;
            while (index < line.Length && line[index] != ' ' && line[index] != '\t')
            {
                index++;
            }

            if (!long.TryParse(line[start..index], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                continue;
            }

            // Перші 8 полів (user, nice, system, idle, iowait, irq, softirq, steal) формують сумарний час.
            // Поля guest та guest_nice враховані всередині user/nice, тому їх не додаємо повторно.
            if (field < 8)
            {
                total += value;
            }

            // Простій враховує idle (поле 3) та очікування вводу-виводу iowait (поле 4).
            if (field == 3)
            {
                idle = value;
            }
            else if (field == 4)
            {
                idle += value;
            }

            field++;
        }

        if (field < 4)
        {
            return false;
        }

        sample = new CpuTimeSample(idle, total);
        return true;
    }

    /// <summary>
    /// Розбирає вміст <c>/proc/meminfo</c> та повертає загальний і доступний обсяг пам'яті в байтах.
    /// </summary>
    /// <param name="content">Повний текст файлу <c>/proc/meminfo</c>.</param>
    /// <param name="totalBytes">Загальний обсяг фізичної RAM у байтах.</param>
    /// <param name="availableBytes">Обсяг доступної пам'яті у байтах.</param>
    /// <returns><c>true</c>, якщо вдалося визначити загальний обсяг пам'яті.</returns>
    internal static bool TryParseMemInfo(ReadOnlySpan<char> content, out long totalBytes, out long availableBytes)
    {
        totalBytes = 0;
        availableBytes = 0;

        long freeBytes = 0;
        long buffersBytes = 0;
        long cachedBytes = 0;
        bool hasAvailable = false;

        while (!content.IsEmpty)
        {
            int newLineIndex = content.IndexOf('\n');
            ReadOnlySpan<char> line = newLineIndex >= 0 ? content[..newLineIndex] : content;
            content = newLineIndex >= 0 ? content[(newLineIndex + 1)..] : ReadOnlySpan<char>.Empty;

            line = line.Trim();
            if (line.IsEmpty)
            {
                continue;
            }

            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                continue;
            }

            ReadOnlySpan<char> key = line[..colonIndex].Trim();
            ReadOnlySpan<char> valuePart = line[(colonIndex + 1)..].Trim();

            // Значення у meminfo подаються у кілобайтах.
            if (!TryParseLeadingInt64(valuePart, out long kilobytes))
            {
                continue;
            }

            long bytes = kilobytes * 1024L;

            if (key.SequenceEqual("MemTotal"))
            {
                totalBytes = bytes;
            }
            else if (key.SequenceEqual("MemAvailable"))
            {
                availableBytes = bytes;
                hasAvailable = true;
            }
            else if (key.SequenceEqual("MemFree"))
            {
                freeBytes = bytes;
            }
            else if (key.SequenceEqual("Buffers"))
            {
                buffersBytes = bytes;
            }
            else if (key.SequenceEqual("Cached"))
            {
                cachedBytes = bytes;
            }
        }

        if (!hasAvailable)
        {
            // Ядра до 3.14 не містять MemAvailable. Наближаємо доступну пам'ять так само, як це робить утиліта free.
            availableBytes = freeBytes + buffersBytes + cachedBytes;
        }

        return totalBytes > 0;
    }

    /// <summary>
    /// Розбирає значення датчика <c>temp*_input</c> (у міліградусах Цельсія) та конвертує його в градуси.
    /// </summary>
    internal static bool TryParseTemperatureCelsius(ReadOnlySpan<char> content, out double celsius)
    {
        celsius = 0;
        if (!TryParseLeadingInt64(content, out long millidegrees))
        {
            return false;
        }

        celsius = millidegrees / 1000.0;
        return true;
    }

    /// <summary>
    /// Розбирає значення датчика <c>fan*_input</c> (у обертах за хвилину).
    /// </summary>
    internal static bool TryParseFanRpm(ReadOnlySpan<char> content, out double rpm)
    {
        rpm = 0;
        if (!TryParseLeadingInt64(content, out long value))
        {
            return false;
        }

        rpm = value;
        return true;
    }

    /// <summary>
    /// Посимвольно виділяє ціле число з початку фрагмента без створення проміжного рядка.
    /// </summary>
    private static bool TryParseLeadingInt64(ReadOnlySpan<char> text, out long value)
    {
        value = 0;
        text = text.TrimStart();
        if (text.IsEmpty)
        {
            return false;
        }

        int length = 0;
        while (length < text.Length && text[length] >= '0' && text[length] <= '9')
        {
            length++;
        }

        if (length == 0)
        {
            return false;
        }

        return long.TryParse(text[..length], NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
