using System.Text.Json.Serialization;

namespace Nebula.SharedModels;

public record struct LauncherManifestEntry(
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("path")] string Path
);