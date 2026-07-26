namespace VisualStates.Core.Commands;

/// <summary>
/// A reversible editor command that can be pushed onto an <see cref="UndoRedoStack"/>.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Short human-readable description shown in status / history UIs.</summary>
    string Description { get; }

    /// <summary>Applies the change.</summary>
    void Execute();

    /// <summary>Reverts the change previously applied by <see cref="Execute"/>.</summary>
    void Undo();
}

/// <summary>
/// Linear undo/redo history of <see cref="IUndoableCommand"/> instances.
/// Executing a new command clears the redo stack.
/// </summary>
public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    /// <summary>True when at least one command can be undone.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>True when at least one command can be redone.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Raised whenever the undo or redo stacks change.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Executes <paramref name="command"/>, pushes it onto the undo stack,
    /// and clears the redo stack.
    /// </summary>
    /// <param name="command">Command to run and record.</param>
    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the most recent command, if any.</summary>
    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Redoes the most recently undone command, if any.</summary>
    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        var command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears both undo and redo history.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Lightweight <see cref="IUndoableCommand"/> that wraps a pair of
/// execute/undo <see cref="Action"/> delegates.
/// </summary>
public sealed class ActionCommand : IUndoableCommand
{
    private readonly Action _execute;
    private readonly Action _undo;

    /// <summary>
    /// Creates a command from a description and a pair of actions.
    /// </summary>
    /// <param name="description">Human-readable label.</param>
    /// <param name="execute">Action that applies the change.</param>
    /// <param name="undo">Action that reverts the change.</param>
    public ActionCommand(string description, Action execute, Action undo)
    {
        Description = description;
        _execute = execute;
        _undo = undo;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public void Execute() => _execute();

    /// <inheritdoc />
    public void Undo() => _undo();
}
