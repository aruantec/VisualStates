using VisualStates.Core;
using VisualStates.Core.Models;

namespace VisualStates.Tests;

public sealed class ZoneFlowTests
{
    [Fact]
    public void GetOrderedChildren_OrdersByYThenX()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox { Id = "c", ZoneId = "z1", X = 100, Y = 50 },
                new StateBox { Id = "a", ZoneId = "z1", X = 10, Y = 10 },
                new StateBox { Id = "b", ZoneId = "z1", X = 50, Y = 10 },
                new StateBox { Id = "out", ZoneId = null, X = 0, Y = 0 }
            ]
        };

        var ordered = ZoneFlow.GetOrderedChildren(project, "z1");

        Assert.Equal(["a", "b", "c"], ordered.Select(b => b.Id));
    }

    [Fact]
    public void ResolveEnter_ReturnsFirstChildFirstStep()
    {
        var project = CreateTwoBoxZone();

        var enter = ZoneFlow.ResolveEnter(project, "z1");

        Assert.NotNull(enter);
        Assert.Equal("first", enter.Value.BoxId);
        Assert.Equal("s1", enter.Value.StepId);
    }

    [Fact]
    public void ResolveExit_ReturnsLastChildLastStep()
    {
        var project = CreateTwoBoxZone();

        var exit = ZoneFlow.ResolveExit(project, "z1");

        Assert.NotNull(exit);
        Assert.Equal("second", exit.Value.BoxId);
        Assert.Equal("s2b", exit.Value.StepId);
    }

    [Fact]
    public void ResolveEnter_AndExit_ReturnNull_WhenZoneEmpty()
    {
        var project = new StateProject
        {
            Zones = [new Zone { Id = "empty" }]
        };

        Assert.Null(ZoneFlow.ResolveEnter(project, "empty"));
        Assert.Null(ZoneFlow.ResolveExit(project, "empty"));
    }

    [Fact]
    public void ResolveEnter_UsesNullStep_WhenFirstBoxHasNoSteps()
    {
        var project = new StateProject
        {
            Boxes =
            [
                new StateBox { Id = "lonely", ZoneId = "z1", X = 0, Y = 0 }
            ]
        };

        var enter = ZoneFlow.ResolveEnter(project, "z1");

        Assert.NotNull(enter);
        Assert.Equal("lonely", enter.Value.BoxId);
        Assert.Null(enter.Value.StepId);
    }

    private static StateProject CreateTwoBoxZone() => new()
    {
        Boxes =
        [
            new StateBox
            {
                Id = "first",
                ZoneId = "z1",
                X = 0,
                Y = 0,
                Steps =
                [
                    new StateStep { Id = "s1", Name = "One" },
                    new StateStep { Id = "s1b", Name = "OneB" }
                ]
            },
            new StateBox
            {
                Id = "second",
                ZoneId = "z1",
                X = 0,
                Y = 100,
                Steps =
                [
                    new StateStep { Id = "s2", Name = "Two" },
                    new StateStep { Id = "s2b", Name = "TwoB" }
                ]
            }
        ]
    };
}
