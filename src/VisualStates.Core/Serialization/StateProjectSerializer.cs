using System.Text.Json;
using System.Text.Json.Serialization;
using VisualStates.Core.Models;

namespace VisualStates.Core.Serialization;

/// <summary>
/// JSON serialization helpers for <see cref="StateProject"/> documents
/// (the <c>.state</c> file format).
/// </summary>
public static class StateProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Writes <paramref name="project"/> as indented camelCase JSON to
    /// <paramref name="path"/>.
    /// </summary>
    /// <param name="project">Project to save.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(StateProject project, string path, CancellationToken ct = default)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, project, Options, ct);
    }

    /// <summary>
    /// Loads a <see cref="StateProject"/> from the JSON file at
    /// <paramref name="path"/>. Returns a new empty project when deserialization
    /// yields null.
    /// </summary>
    /// <param name="path">Source file path.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<StateProject> LoadAsync(string path, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<StateProject>(stream, Options, ct);
        return project ?? new StateProject();
    }

    /// <summary>Serializes <paramref name="project"/> to an indented JSON string.</summary>
    /// <param name="project">Project to serialize.</param>
    public static string Serialize(StateProject project) =>
        JsonSerializer.Serialize(project, Options);

    /// <summary>
    /// Deserializes a <see cref="StateProject"/> from <paramref name="json"/>.
    /// Returns a new empty project when the payload is null.
    /// </summary>
    /// <param name="json">JSON document text.</param>
    public static StateProject Deserialize(string json) =>
        JsonSerializer.Deserialize<StateProject>(json, Options) ?? new StateProject();
}
