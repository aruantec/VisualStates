using VisualStates.Core.Commands;

namespace VisualStates.Services;

public interface IUndoRedoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    event EventHandler? StateChanged;

    void Execute(IUndoableCommand command);
    void Undo();
    void Redo();
    void Clear();
}
