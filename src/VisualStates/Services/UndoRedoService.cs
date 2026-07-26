using VisualStates.Core.Commands;

namespace VisualStates.Services;

/// <summary>
/// Default <see cref="IUndoRedoService"/> wrapping a single <see cref="UndoRedoStack"/>.
/// </summary>
public sealed class UndoRedoService : IUndoRedoService
{
    private readonly UndoRedoStack _stack = new();

    /// <inheritdoc />
    public bool CanUndo => _stack.CanUndo;

    /// <inheritdoc />
    public bool CanRedo => _stack.CanRedo;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <summary>Creates a service that forwards stack changes to <see cref="StateChanged"/>.</summary>
    public UndoRedoService()
    {
        _stack.Changed += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Execute(IUndoableCommand command) => _stack.Execute(command);

    /// <inheritdoc />
    public void Undo() => _stack.Undo();

    /// <inheritdoc />
    public void Redo() => _stack.Redo();

    /// <inheritdoc />
    public void Clear() => _stack.Clear();
}
