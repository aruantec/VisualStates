namespace VisualStates.Core.Commands;

public interface IUndoableCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? Changed;

    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        var command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class ActionCommand : IUndoableCommand
{
    private readonly Action _execute;
    private readonly Action _undo;

    public ActionCommand(string description, Action execute, Action undo)
    {
        Description = description;
        _execute = execute;
        _undo = undo;
    }

    public string Description { get; }

    public void Execute() => _execute();
    public void Undo() => _undo();
}
