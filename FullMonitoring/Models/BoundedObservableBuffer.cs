using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FullMonitoring.Models;

/// <summary>
/// Спостережувана колекція фіксованого розміру (кільцевий буфер / ковзне вікно) для точок графіків.
/// Під час додавання нового елемента понад <see cref="MaxCapacity"/> автоматично видаляє найстаріший елемент,
/// що гарантує стабільне споживання пам'яті без витоків при тривалій роботі застосунку.
/// </summary>
/// <typeparam name="T">Тип елементів буфера.</typeparam>
public sealed class BoundedObservableBuffer<T> : ObservableCollection<T>
{
    /// <summary>
    /// Максимальна кількість елементів, яку може одночасно зберігати буфер.
    /// </summary>
    public int MaxCapacity { get; }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="BoundedObservableBuffer{T}"/> із заданою максимальною місткістю.
    /// </summary>
    /// <param name="maxCapacity">Максимальна кількість точок у буфері (має бути більшою за 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Виникає, якщо <paramref name="maxCapacity"/> менша або дорівнює 0.</exception>
    public BoundedObservableBuffer(int maxCapacity = 60)
    {
        if (maxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCapacity),
                maxCapacity,
                "Максимальна місткість буфера точок має бути більшою за нуль.");
        }

        MaxCapacity = maxCapacity;
    }

    /// <summary>
    /// Ініціалізує новий екземпляр <see cref="BoundedObservableBuffer{T}"/> із початковим набором значень.
    /// </summary>
    /// <param name="maxCapacity">Максимальна кількість точок у буфері.</param>
    /// <param name="initialItems">Початкова послідовність елементів.</param>
    public BoundedObservableBuffer(int maxCapacity, IEnumerable<T> initialItems)
        : this(maxCapacity)
    {
        ArgumentNullException.ThrowIfNull(initialItems);

        foreach (var item in initialItems)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Додає нову точку в кінець буфера з автоматичним витісненням найстарішої точки при досягненні <see cref="MaxCapacity"/>.
    /// </summary>
    /// <param name="item">Нове значення для додавання в буфер.</param>
    public void Push(T item)
    {
        Add(item);
    }

    /// <summary>
    /// Заповнює буфер заданим значенням за замовчуванням до повної місткості <see cref="MaxCapacity"/>.
    /// </summary>
    /// <param name="defaultValue">Значення для початкового заповнення.</param>
    public void FillWithDefault(T defaultValue)
    {
        Clear();
        for (int i = 0; i < MaxCapacity; i++)
        {
            base.InsertItem(Count, defaultValue);
        }
    }

    /// <inheritdoc />
    protected override void InsertItem(int index, T item)
    {
        while (Count >= MaxCapacity)
        {
            RemoveAt(0);
            if (index > 0)
            {
                index--;
            }
        }

        if (index > Count)
        {
            index = Count;
        }

        base.InsertItem(index, item);
    }
}
