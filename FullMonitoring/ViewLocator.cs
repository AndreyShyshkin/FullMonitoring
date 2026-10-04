using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FullMonitoring.ViewModels;

namespace FullMonitoring;

/// <summary>
/// За заданою моделлю представлення (ViewModel) повертає відповідне представлення (View), якщо це можливо.
/// </summary>
[RequiresUnreferencedCode(
    "Типова реалізація ViewLocator використовує рефлексію, яка може бути оптимізована компілятором під час тримінгу.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Не знайдено: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}