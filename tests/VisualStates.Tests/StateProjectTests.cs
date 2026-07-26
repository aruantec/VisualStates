using VisualStates.Core.Models;

namespace VisualStates.Tests;

public sealed class StateProjectTests
{
    [Fact]
    public void FindZone_FindBox_FindStep_ResolveKnownIds()
    {
        var project = new StateProject
        {
            Zones = [new Zone { Id = "z1", Name = "Zone" }],
            Boxes =
            [
                new StateBox
                {
                    Id = "b1",
                    Name = "Box",
                    Steps = [new StateStep { Id = "s1", Name = "Step" }]
                }
            ]
        };

        Assert.Equal("Zone", project.FindZone("z1")!.Name);
        Assert.Equal("Box", project.FindBox("b1")!.Name);
        Assert.Equal("Step", project.FindStep("b1", "s1")!.Name);
    }

    [Fact]
    public void FindHelpers_ReturnNull_WhenMissing()
    {
        var project = new StateProject();

        Assert.Null(project.FindZone("missing"));
        Assert.Null(project.FindBox("missing"));
        Assert.Null(project.FindStep("missing", "also-missing"));
    }
}
