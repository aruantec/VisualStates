using VisualStates.Core.Commands;

namespace VisualStates.Tests;

public sealed class UndoRedoStackTests
{
    [Fact]
    public void Execute_RunsCommand_AndEnablesUndo()
    {
        var value = 0;
        var stack = new UndoRedoStack();
        var command = new ActionCommand("inc", () => value++, () => value--);

        stack.Execute(command);

        Assert.Equal(1, value);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Undo_RevertsAndEnablesRedo()
    {
        var value = 0;
        var stack = new UndoRedoStack();
        stack.Execute(new ActionCommand("inc", () => value++, () => value--));

        stack.Undo();

        Assert.Equal(0, value);
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public void Redo_ReappliesUndoneCommand()
    {
        var value = 0;
        var stack = new UndoRedoStack();
        stack.Execute(new ActionCommand("inc", () => value++, () => value--));
        stack.Undo();

        stack.Redo();

        Assert.Equal(1, value);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var value = 0;
        var stack = new UndoRedoStack();
        stack.Execute(new ActionCommand("inc", () => value++, () => value--));
        stack.Undo();
        Assert.True(stack.CanRedo);

        stack.Execute(new ActionCommand("inc", () => value++, () => value--));

        Assert.False(stack.CanRedo);
        Assert.Equal(1, value);
    }

    [Fact]
    public void Clear_ResetsBothStacks()
    {
        var value = 0;
        var stack = new UndoRedoStack();
        stack.Execute(new ActionCommand("inc", () => value++, () => value--));
        stack.Undo();

        stack.Clear();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Changed_IsRaised_OnStackMutations()
    {
        var stack = new UndoRedoStack();
        var raised = 0;
        stack.Changed += (_, _) => raised++;

        stack.Execute(new ActionCommand("noop", () => { }, () => { }));
        stack.Undo();
        stack.Redo();
        stack.Clear();

        Assert.Equal(4, raised);
    }

    [Fact]
    public void Undo_AndRedo_AreNoOps_WhenEmpty()
    {
        var stack = new UndoRedoStack();
        var raised = 0;
        stack.Changed += (_, _) => raised++;

        stack.Undo();
        stack.Redo();

        Assert.Equal(0, raised);
    }
}
