using VisualStates.Core.Commands;

namespace VisualStates.Services;

/// <summary>
/// Default <see cref="IUndoRedoService"/> wrapping a single <see cref="UndoRedoStack"/>.
/// </summary>
public sealed class UndoRedoService : IUndoRedoService
{
    private readonly UndoRedoStack _stack = new();

    /// <summary>True when at least one command can be undone.</summary>
    public bool CanUndo => _stack.CanUndo;

    /// <summary>True when at least one command can be redone.</summary>
    public bool CanRedo => _stack.CanRedo;

    /// <summary>Raised whenever undo/redo availability changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Creates a service that forwards stack changes to <see cref="StateChanged"/>.</summary>
    public UndoRedoService()
    {
        _stack.Changed += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Executes and records <paramref name="command"/> on the undo stack.</summary>
    /// <param name="command">Command to run.</param>
    public void Execute(IUndoableCommand command) => _stack.Execute(command);

    /// <summary>Undoes the most recent command, if any.</summary>
    public void Undo() => _stack.Undo();

    /// <summary>Redoes the most recently undone command, if any.</summary>
    public void Redo() => _stack.Redo();

    /// <summary>Clears the entire undo/redo history.</summary>
    public void Clear() => _stack.Clear();
}
