using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace RimForge.App.Collections;

/// <summary>
/// Observable collection with an atomic replacement operation. The bound view receives
/// one Reset notification instead of one notification per item, which is essential for
/// large WPF projections such as the Mod Sorter and profile lists.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var materialized = items as IReadOnlyList<T> ?? items.ToArray();

        CheckReentrancy();
        Items.Clear();
        foreach (var item in materialized)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
