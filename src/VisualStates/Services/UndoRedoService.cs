using VisualStates.Core.Commands;

namespace VisualStates.Services;

public sealed class UndoRedoService : IUndoRedoService
{
    private readonly UndoRedoStack _stack = new();

    public bool CanUndo => _stack.CanUndo;
    public bool CanRedo => _stack.CanRedo;
    public event EventHandler? StateChanged;

    public UndoRedoService()
    {
        _stack.Changed += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Execute(IUndoableCommand command) => _stack.Execute(command);
    public void Undo() => _stack.Undo();
    public void Redo() => _stack.Redo();
    public void Clear() => _stack.Clear();
}
