using VisualStates.Core.Models;
using VisualStates.Runtime;

namespace VisualStates.Tests;

public sealed class StateMachineExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_RunsHappyPathInOrder()
    {
        var context = new RecordingContext();
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "a",
                    IsEntry = true,
                    Steps =
                    [
                        new StateStep { Id = "a1", Kind = StepKind.CallEvent, EventName = "First" }
                    ]
                },
                new StateBox
                {
                    Id = "b",
                    Steps =
                    [
                        new StateStep { Id = "b1", Kind = StepKind.CallMethod, MethodName = "Second" }
                    ]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "a",
                    SourceStepId = "a1",
                    TargetBoxId = "b",
                    TargetStepId = "b1"
                }
            ]
        };

        await new StateMachineExecutor().ExecuteAsync(project, context, TestContext.Current.CancellationToken);

        Assert.Equal(["event:First", "method:Second"], context.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRunErrorHandler_WhenNoException()
    {
        var context = new RecordingContext();
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "main",
                    IsEntry = true,
                    Steps =
                    [
                        new StateStep { Id = "work", Kind = StepKind.CallMethod, MethodName = "Work" }
                    ]
                },
                new StateBox
                {
                    Id = "next",
                    Steps =
                    [
                        new StateStep { Id = "nextStep", Kind = StepKind.CallEvent, EventName = "Next" }
                    ]
                },
                new StateBox
                {
                    Id = "handler",
                    Steps =
                    [
                        new StateStep { Id = "recover", Kind = StepKind.CallEvent, EventName = "Recovered" }
                    ]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "main",
                    SourceStepId = "work",
                    TargetBoxId = "next",
                    TargetStepId = "nextStep"
                },
                new StateConnection
                {
                    SourceBoxId = "main",
                    SourceStepId = "work",
                    SourceSide = PinSide.Error,
                    TargetBoxId = "handler",
                    TargetStepId = "recover",
                    IsError = true
                }
            ]
        };

        await new StateMachineExecutor().ExecuteAsync(project, context, TestContext.Current.CancellationToken);

        Assert.Equal(["method:Work", "event:Next"], context.Calls);
        Assert.DoesNotContain("event:Recovered", context.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DivertsToErrorHandler_WhenStepThrows()
    {
        var context = new RecordingContext { ThrowOnMethod = "Boom" };
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "main",
                    IsEntry = true,
                    Steps =
                    [
                        new StateStep { Id = "work", Kind = StepKind.CallMethod, MethodName = "Boom" },
                        new StateStep { Id = "after", Kind = StepKind.CallEvent, EventName = "ShouldNotRun" }
                    ]
                },
                new StateBox
                {
                    Id = "handler",
                    Steps =
                    [
                        new StateStep { Id = "recover", Kind = StepKind.CallEvent, EventName = "Recovered" }
                    ]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "main",
                    SourceStepId = "work",
                    TargetBoxId = "main",
                    TargetStepId = "after"
                },
                new StateConnection
                {
                    SourceBoxId = "main",
                    SourceStepId = "work",
                    SourceSide = PinSide.Error,
                    TargetBoxId = "handler",
                    TargetStepId = "recover",
                    IsError = true
                }
            ]
        };

        await new StateMachineExecutor().ExecuteAsync(project, context, TestContext.Current.CancellationToken);

        Assert.Equal(["method:Boom", "event:Recovered"], context.Calls);
        Assert.DoesNotContain("event:ShouldNotRun", context.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_UsesZoneErrorHandler_ForNestedBox()
    {
        var context = new RecordingContext { ThrowOnMethod = "Fail" };
        var project = new StateProject
        {
            Zones = [new Zone { Id = "z1" }],
            Boxes =
            [
                new StateBox
                {
                    Id = "child",
                    ZoneId = "z1",
                    IsEntry = true,
                    Steps =
                    [
                        new StateStep { Id = "work", Kind = StepKind.CallMethod, MethodName = "Fail" }
                    ]
                },
                new StateBox
                {
                    Id = "handler",
                    Steps =
                    [
                        new StateStep { Id = "recover", Kind = StepKind.CallEvent, EventName = "ZoneRecovered" }
                    ]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceZoneId = "z1",
                    SourceSide = PinSide.Error,
                    TargetBoxId = "handler",
                    TargetStepId = "recover",
                    IsError = true
                }
            ]
        };

        await new StateMachineExecutor().ExecuteAsync(project, context, TestContext.Current.CancellationToken);

        Assert.Equal(["method:Fail", "event:ZoneRecovered"], context.Calls);
    }
}

public sealed class ExecutionPlannerTests
{
    [Fact]
    public void Plan_OrdersByConnections()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "a",
                    IsEntry = true,
                    Steps = [new StateStep { Id = "a1", Kind = StepKind.CallEvent, EventName = "A" }]
                },
                new StateBox
                {
                    Id = "b",
                    Steps = [new StateStep { Id = "b1", Kind = StepKind.CallEvent, EventName = "B" }]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "a",
                    SourceStepId = "a1",
                    TargetBoxId = "b",
                    TargetStepId = "b1"
                }
            ]
        };

        var plan = ExecutionPlanner.Plan(project);

        Assert.Equal(2, plan.Count);
        Assert.Equal("a", plan[0].Box.Id);
        Assert.Equal("b", plan[1].Box.Id);
    }

    [Fact]
    public void Plan_ExcludesErrorOnlyTargets_FromHappyPath()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "main",
                    IsEntry = true,
                    Steps = [new StateStep { Id = "work", Kind = StepKind.CallEvent, EventName = "Work" }]
                },
                new StateBox
                {
                    Id = "next",
                    Steps = [new StateStep { Id = "nextStep", Kind = StepKind.CallEvent, EventName = "Next" }]
                },
                new StateBox
                {
                    Id = "handler",
                    Steps = [new StateStep { Id = "recover", Kind = StepKind.CallEvent, EventName = "Recovered" }]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "main",
                    SourceStepId = "work",
                    TargetBoxId = "next",
                    TargetStepId = "nextStep"
                },
                new StateConnection
                {
                    SourceBoxId = "main",
                    SourceStepId = "work",
                    SourceSide = PinSide.Error,
                    TargetBoxId = "handler",
                    TargetStepId = "recover",
                    IsError = true
                }
            ]
        };

        var plan = ExecutionPlanner.Plan(project);

        Assert.Equal(2, plan.Count);
        Assert.Equal("main", plan[0].Box.Id);
        Assert.Equal("next", plan[1].Box.Id);
        Assert.DoesNotContain(plan, step => step.Box.Id == "handler");
    }

    [Fact]
    public void BuildErrorHandlers_MapsStepToHandler()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "main",
                    Steps = [new StateStep { Id = "work", Kind = StepKind.CallMethod, MethodName = "Work" }]
                },
                new StateBox
                {
                    Id = "handler",
                    Steps = [new StateStep { Id = "recover", Kind = StepKind.CallEvent, EventName = "OnError" }]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "main",
                    SourceStepId = "work",
                    SourceSide = PinSide.Error,
                    TargetBoxId = "handler",
                    TargetStepId = "recover",
                    IsError = true
                }
            ]
        };

        var handlers = ExecutionPlanner.BuildErrorHandlers(project);

        Assert.True(handlers.TryGetValue(new RuntimeStepKey("main", "work"), out var handler));
        Assert.Equal("handler", handler.Box.Id);
        Assert.Equal("recover", handler.Step!.Id);
    }
}

/// <summary>
/// Test double that records event/method calls and can throw on demand.
/// </summary>
file sealed class RecordingContext : IStateMachineContext
{
    public List<string> Calls { get; } = [];

    public string? ThrowOnMethod { get; init; }

    public Task RaiseEventAsync(string eventName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"event:{eventName}");
        return Task.CompletedTask;
    }

    public Task InvokeMethodAsync(string methodName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"method:{methodName}");
        if (ThrowOnMethod is not null && ThrowOnMethod == methodName)
            throw new InvalidOperationException($"Forced failure for {methodName}");
        return Task.CompletedTask;
    }

    public T? GetService<T>() where T : class => null;
}
