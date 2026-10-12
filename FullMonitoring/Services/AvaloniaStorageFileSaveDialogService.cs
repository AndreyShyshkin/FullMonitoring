using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace FullMonitoring.Services;

/// <summary>
/// Реалізація сервісу вибору файлу для збереження на основі нативного Avalonia StorageProvider (Issue #12).
/// Викликає TopLevel.GetTopLevel(window).StorageProvider.SaveFilePickerAsync відповідно до вимог архітектури Avalonia 11/12.
/// </summary>
public class AvaloniaStorageFileSaveDialogService : IFileSaveDialogService
{
    private readonly Func<TopLevel?> _topLevelProvider;

    /// <summary>
    /// Ініціалізує новий екземпляр із можливістю надання власного постачальника TopLevel для UI-тестів.
    /// </summary>
    public AvaloniaStorageFileSaveDialogService(Func<TopLevel?>? topLevelProvider = null)
    {
        _topLevelProvider = topLevelProvider ?? GetDefaultTopLevel;
    }

    /// <summary>
    /// Отримує поточний кореневий контейнер TopLevel з активного головного вікна додатку.
    /// </summary>
    public static TopLevel? GetDefaultTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is not null)
            {
                return TopLevel.GetTopLevel(desktop.MainWindow);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<string?> ShowSaveFileDialogAsync(FileSaveDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var topLevel = _topLevelProvider();
        if (topLevel?.StorageProvider is null || !topLevel.StorageProvider.CanSave)
        {
            return null;
        }

        var jsonType = new FilePickerFileType("JSON файли (*.json)")
        {
            Patterns = new[] { "*.json" },
            MimeTypes = new[] { "application/json" }
        };

        var csvType = new FilePickerFileType("CSV таблиці (*.csv)")
        {
            Patterns = new[] { "*.csv" },
            MimeTypes = new[] { "text/csv" }
        };

        var allType = new FilePickerFileType("Всі файли (*.*)")
        {
            Patterns = new[] { "*.*" }
        };

        var fileTypeChoices = options.Format == ExportFormatOption.Csv
            ? new[] { csvType, jsonType, allType }
            : new[] { jsonType, csvType, allType };

        var pickerOptions = new FilePickerSaveOptions
        {
            Title = options.Title,
            DefaultExtension = options.DefaultExtension,
            SuggestedFileName = options.SuggestedFileName,
            FileTypeChoices = fileTypeChoices,
            ShowOverwritePrompt = true
        };

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(pickerOptions).ConfigureAwait(false);
        if (file is null)
        {
            return null;
        }

        // Отримуємо локальний шлях файлу
        string? localPath = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(localPath))
        {
            return localPath;
        }

        return file.Path?.LocalPath;
    }
}
