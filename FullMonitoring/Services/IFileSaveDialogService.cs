using System.Threading.Tasks;

namespace FullMonitoring.Services;

/// <summary>
/// Підтримувані формати для діалогу збереження файлу.
/// </summary>
public enum ExportFormatOption
{
    Json,
    Csv
}

/// <summary>
/// Параметри виклику системного діалогу вибору місця збереження файлу експорту.
/// </summary>
public sealed record FileSaveDialogOptions
{
    public string Title { get; init; } = "Зберегти файл";
    public string DefaultExtension { get; init; } = "json";
    public string SuggestedFileName { get; init; } = "export";
    public ExportFormatOption Format { get; init; } = ExportFormatOption.Json;
}

/// <summary>
/// Інтерфейс для взаємодії з нативним системним діалогом вибору файлу для збереження (Issue #12).
/// Дозволяє абстрагувати Avalonia StorageProvider для повноцінного модульного тестування ViewModels.
/// </summary>
public interface IFileSaveDialogService
{
    /// <summary>
    /// Відкриває нативний діалог збереження файлу та повертає обраний локальний шлях, або null, якщо скасовано.
    /// </summary>
    Task<string?> ShowSaveFileDialogAsync(FileSaveDialogOptions options);
}
