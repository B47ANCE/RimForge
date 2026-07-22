using System;

namespace RimForge.App.Undo;

public interface IUndoService
{
    bool CanUndo { get; }
    string? PendingDescription { get; }
    event EventHandler? StateChanged;
    void Register(string description, Action undo);
    bool TryUndo();
    void Clear();
}

public sealed class UndoService : IUndoService
{
    private readonly object _sync = new();
    private UndoEntry? _pending;

    public bool CanUndo
    {
        get { lock (_sync) return _pending is not null; }
    }

    public string? PendingDescription
    {
        get { lock (_sync) return _pending?.Description; }
    }

    public event EventHandler? StateChanged;

    public void Register(string description, Action undo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(undo);
        lock (_sync) _pending = new UndoEntry(description, undo);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryUndo()
    {
        UndoEntry? entry;
        lock (_sync)
        {
            entry = _pending;
            _pending = null;
        }

        if (entry is null) return false;
        entry.Undo();
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        var changed = false;
        lock (_sync)
        {
            changed = _pending is not null;
            _pending = null;
        }
        if (changed) StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed record UndoEntry(string Description, Action Undo);
}
