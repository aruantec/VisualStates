using System.Text.Json;
using System.Text.Json.Serialization;
using VisualStates.Core.Models;

namespace VisualStates.Core.Serialization;

public static class StateProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task SaveAsync(StateProject project, string path, CancellationToken ct = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, project, Options, ct);
    }

    public static async Task<StateProject> LoadAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<StateProject>(stream, Options, ct);
        return project ?? new StateProject();
    }

    public static string Serialize(StateProject project) =>
        JsonSerializer.Serialize(project, Options);

    public static StateProject Deserialize(string json) =>
        JsonSerializer.Deserialize<StateProject>(json, Options) ?? new StateProject();
}
