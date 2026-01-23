using System.Text.Json.Serialization;

namespace Nebula.SharedModels;

public record struct LauncherManifest(
    [property: JsonPropertyName("entries")] HashSet<LauncherManifestEntry> Entries,
    [property: JsonPropertyName("runtime_info")] LauncherRuntimeInfo RuntimeInfo
);