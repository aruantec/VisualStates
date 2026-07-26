using VisualStates.Core.Generation;
using VisualStates.Core.Models;

namespace VisualStates.Tests;

public sealed class StateMachineCodeGeneratorTests
{
    private readonly StateMachineCodeGenerator _generator = new();

    [Fact]
    public void Generate_EmitsNamespaceClass_AndVariables()
    {
        var project = new StateProject
        {
            Namespace = "Demo.Generated",
            GeneratedClassName = "DemoMachine",
            Variables =
            [
                new StateVariable { Name = "Count", TypeName = "int", DefaultValue = "0" },
                new StateVariable { Name = "Label", TypeName = "string" }
            ],
            Boxes =
            [
                new StateBox
                {
                    Id = "b1",
                    Name = "Start",
                    IsEntry = true,
                    Steps =
                    [
                        new StateStep
                        {
                            Id = "s1",
                            Kind = StepKind.SetVariable,
                            TargetName = "Count",
                            Expression = "1"
                        }
                    ]
                }
            ]
        };

        var code = _generator.Generate(project);

        Assert.Contains("namespace Demo.Generated;", code);
        Assert.Contains("public sealed class DemoMachine : IGeneratedStateMachine", code);
        Assert.Contains("public int Count { get; set; } = 0;", code);
        Assert.Contains("public string Label { get; set; }", code);
        Assert.Contains("Count = 1;", code);
    }

    [Fact]
    public void Generate_EmitsCallEvent_AndCallMethod()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "b1",
                    Name = "Actions",
                    Steps =
                    [
                        new StateStep
                        {
                            Id = "e1",
                            Kind = StepKind.CallEvent,
                            EventName = "OnReady"
                        },
                        new StateStep
                        {
                            Id = "m1",
                            Kind = StepKind.CallMethod,
                            MethodName = "DoWork",
                            Arguments = "42"
                        }
                    ]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "b1",
                    SourceStepId = "e1",
                    TargetBoxId = "b1",
                    TargetStepId = "m1"
                }
            ]
        };

        var code = _generator.Generate(project);

        Assert.Contains("RaiseEventAsync(\"OnReady\"", code);
        Assert.Contains("InvokeMethodAsync(\"DoWork\", 42", code);
    }

    [Fact]
    public void Generate_WrapsErrorHandler_ForErrorPinConnection()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "main",
                    Name = "Main",
                    Steps =
                    [
                        new StateStep { Id = "work", Kind = StepKind.CallMethod, MethodName = "Work" }
                    ]
                },
                new StateBox
                {
                    Id = "handler",
                    Name = "Handler",
                    Steps =
                    [
                        new StateStep { Id = "recover", Kind = StepKind.CallEvent, EventName = "OnError" }
                    ]
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

        var code = _generator.Generate(project);

        Assert.Contains("try", code);
        Assert.Contains("catch (Exception)", code);
        Assert.Contains("return false;", code);
    }

    [Fact]
    public void Generate_EmptyProject_EmitsCompletedTask()
    {
        var code = _generator.Generate(new StateProject());

        Assert.Contains("await Task.CompletedTask;", code);
    }
}

public sealed class ExecutionOrderBuilderTests
{
    [Fact]
    public void Build_FollowsHappyPathConnections()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "a",
                    Name = "A",
                    IsEntry = true,
                    Steps = [new StateStep { Id = "a1", Kind = StepKind.CallEvent, EventName = "A" }]
                },
                new StateBox
                {
                    Id = "b",
                    Name = "B",
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

        var order = ExecutionOrderBuilder.Build(project);

        Assert.Equal(2, order.Count);
        Assert.Equal(new ExecutionNode("a", "a1"), order[0]);
        Assert.Equal(new ExecutionNode("b", "b1"), order[1]);
    }

    [Fact]
    public void Build_SkipsErrorConnections()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox
                {
                    Id = "a",
                    Steps = [new StateStep { Id = "a1", Kind = StepKind.CallEvent, EventName = "A" }]
                },
                new StateBox
                {
                    Id = "err",
                    Steps = [new StateStep { Id = "e1", Kind = StepKind.CallEvent, EventName = "Err" }]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    SourceBoxId = "a",
                    SourceStepId = "a1",
                    SourceSide = PinSide.Error,
                    TargetBoxId = "err",
                    TargetStepId = "e1",
                    IsError = true
                }
            ]
        };

        var order = ExecutionOrderBuilder.Build(project);

        // Both nodes appear, but error edge does not force err after a.
        Assert.Contains(new ExecutionNode("a", "a1"), order);
        Assert.Contains(new ExecutionNode("err", "e1"), order);
    }

    [Fact]
    public void Build_ChainsZoneChildrenInVisualOrder()
    {
        var project = new StateProject
        {
            Zones = [new Zone { Id = "z1" }],
            Boxes =
            [
                new StateBox
                {
                    Id = "top",
                    ZoneId = "z1",
                    X = 0,
                    Y = 0,
                    Steps = [new StateStep { Id = "t1", Kind = StepKind.CallEvent, EventName = "Top" }]
                },
                new StateBox
                {
                    Id = "bottom",
                    ZoneId = "z1",
                    X = 0,
                    Y = 100,
                    Steps = [new StateStep { Id = "b1", Kind = StepKind.CallEvent, EventName = "Bottom" }]
                }
            ]
        };

        var order = ExecutionOrderBuilder.Build(project);

        var topIndex = order.ToList().FindIndex(n => n.BoxId == "top");
        var bottomIndex = order.ToList().FindIndex(n => n.BoxId == "bottom");
        Assert.True(topIndex >= 0);
        Assert.True(bottomIndex > topIndex);
    }
}
