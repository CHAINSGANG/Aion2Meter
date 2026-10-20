using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Aion2Meter.ViewModels;

/// <summary>
/// Keeps a WPF-bound collection in snapshot order without recreating unchanged rows.
/// Duplicate keys cannot leak into the visual tree, even if an upstream snapshot is bad.
/// </summary>
internal static class KeyedCollectionSynchronizer
{
    public static void Reconcile<TSource, TRow, TKey>(
        ObservableCollection<TRow> rows,
        IReadOnlyList<TSource> source,
        Func<TSource, TKey> sourceKey,
        Func<TRow, TKey> rowKey,
        Func<TSource, TRow> createRow,
        Action<TRow, TSource> updateRow,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        comparer ??= EqualityComparer<TKey>.Default;
        var desiredKeys = new HashSet<TKey>(comparer);
        var desired = new List<TSource>(source.Count);
        foreach (TSource item in source)
        {
            if (desiredKeys.Add(sourceKey(item)))
                desired.Add(item);
        }

        var existing = new Dictionary<TKey, TRow>(comparer);
        for (int index = 0; index < rows.Count; index++)
        {
            TRow row = rows[index];
            if (existing.TryAdd(rowKey(row), row))
                continue;

            rows.RemoveAt(index);
            index--;
        }

        for (int index = 0; index < desired.Count; index++)
        {
            TSource item = desired[index];
            TKey key = sourceKey(item);
            if (!existing.TryGetValue(key, out TRow? row))
            {
                row = createRow(item);
                rows.Insert(index, row);
            }
            else
            {
                int oldIndex = rows.IndexOf(row);
                if (oldIndex != index)
                    rows.Move(oldIndex, index);
            }

            updateRow(row, item);
        }

        for (int index = rows.Count - 1; index >= 0; index--)
        {
            if (!desiredKeys.Contains(rowKey(rows[index])))
                rows.RemoveAt(index);
        }
    }
}
