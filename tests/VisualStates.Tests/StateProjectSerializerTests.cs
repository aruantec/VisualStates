using VisualStates.Core.Models;
using VisualStates.Core.Serialization;

namespace VisualStates.Tests;

public sealed class StateProjectSerializerTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTripsProject()
    {
        var original = new StateProject
        {
            Name = "Demo",
            GeneratedClassName = "DemoMachine",
            Namespace = "Demo.Ns",
            Zones =
            [
                new Zone { Id = "z1", Name = "Main", X = 10, Y = 20, Width = 300, Height = 200, BorderColor = "#3498DB" }
            ],
            Boxes =
            [
                new StateBox
                {
                    Id = "b1",
                    Name = "Entry",
                    IsEntry = true,
                    X = 40,
                    Y = 50,
                    Width = 220,
                    HeaderColor = "#E74C3C",
                    ZoneId = "z1",
                    Steps =
                    [
                        new StateStep
                        {
                            Id = "s1",
                            Name = "Init",
                            Kind = StepKind.CallEvent,
                            EventName = "OnEnter"
                        }
                    ]
                }
            ],
            Connections =
            [
                new StateConnection
                {
                    Id = "c1",
                    SourceBoxId = "b1",
                    SourceStepId = "s1",
                    SourceSide = PinSide.Right,
                    TargetBoxId = "b1",
                    TargetSide = PinSide.Left,
                    IsError = false
                }
            ],
            Variables =
            [
                new StateVariable { Id = "v1", Name = "Counter", TypeName = "int", DefaultValue = "0" }
            ]
        };

        var json = StateProjectSerializer.Serialize(original);
        var restored = StateProjectSerializer.Deserialize(json);

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.GeneratedClassName, restored.GeneratedClassName);
        Assert.Equal(original.Namespace, restored.Namespace);
        Assert.Single(restored.Zones);
        Assert.Equal("z1", restored.Zones[0].Id);
        Assert.Single(restored.Boxes);
        Assert.Equal("Entry", restored.Boxes[0].Name);
        Assert.True(restored.Boxes[0].IsEntry);
        Assert.Single(restored.Boxes[0].Steps);
        Assert.Equal(StepKind.CallEvent, restored.Boxes[0].Steps[0].Kind);
        Assert.Single(restored.Connections);
        Assert.Equal(PinSide.Right, restored.Connections[0].SourceSide);
        Assert.Single(restored.Variables);
        Assert.Equal("Counter", restored.Variables[0].Name);
    }

    [Fact]
    public async Task SaveAsync_LoadAsync_RoundTripsToDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"visualstates-{Guid.NewGuid():N}.state");
        try
        {
            var project = new StateProject { Name = "DiskRoundTrip" };
            await StateProjectSerializer.SaveAsync(project, path, TestContext.Current.CancellationToken);

            var loaded = await StateProjectSerializer.LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal("DiskRoundTrip", loaded.Name);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Deserialize_ReturnsEmptyProject_ForNullPayload()
    {
        var project = StateProjectSerializer.Deserialize("null");

        Assert.NotNull(project);
        Assert.Equal("Untitled", project.Name);
        Assert.Empty(project.Boxes);
    }
}
