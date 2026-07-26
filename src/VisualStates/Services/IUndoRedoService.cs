using VisualStates.Core.Commands;

namespace VisualStates.Services;

/// <summary>
/// Application-facing undo/redo facade over an <see cref="UndoRedoStack"/>.
/// </summary>
public interface IUndoRedoService
{
    /// <summary>True when at least one command can be undone.</summary>
    bool CanUndo { get; }

    /// <summary>True when at least one command can be redone.</summary>
    bool CanRedo { get; }

    /// <summary>Raised whenever undo/redo availability changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Executes and records <paramref name="command"/>.</summary>
    /// <param name="command">Command to run.</param>
    void Execute(IUndoableCommand command);

    /// <summary>Undoes the most recent command, if any.</summary>
    void Undo();

    /// <summary>Redoes the most recently undone command, if any.</summary>
    void Redo();

    /// <summary>Clears the entire undo/redo history.</summary>
    void Clear();
}
