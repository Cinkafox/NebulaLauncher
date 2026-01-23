using System.Text.Json.Serialization;

namespace Nebula.SharedModels;

public record struct LauncherRuntimeInfo(
    [property: JsonPropertyName("version")] string RuntimeVersion,
    [property: JsonPropertyName("runtimes")] Dictionary<string, string> DotnetRuntimes);