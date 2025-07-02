using System.Text.Json.Serialization;

namespace Nebula.Shared.Models;

public sealed record AuthInfo(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("public_key")]
    string PublicKey);

public sealed record BuildInfo(
    [property: JsonPropertyName("engine_version")]
    string EngineVersion,
    [property: JsonPropertyName("fork_id")]
    string ForkId,
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("download_url")]
    string DownloadUrl,
    [property: JsonPropertyName("manifest_download_url")]
    string ManifestDownloadUrl,
    [property: JsonPropertyName("manifest_url")]
    string ManifestUrl,
    [property: JsonPropertyName("acz")] bool Acz,
    [property: JsonPropertyName("hash")] string Hash,
    [property: JsonPropertyName("manifest_hash")]
    string ManifestHash);

public sealed record ServerLink(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("icon")] string Icon,
    [property: JsonPropertyName("url")] string Url);

public sealed record ServerInfo(
    [property: JsonPropertyName("connect_address")]
    string ConnectAddress,
    [property: JsonPropertyName("auth")] AuthInfo Auth,
    [property: JsonPropertyName("build")] BuildInfo Build,
    [property: JsonPropertyName("desc")] string Desc,
    [property: JsonPropertyName("links")] List<ServerLink>? Links);

public sealed record EngineVersionInfo(
    [property: JsonPropertyName("insecure")]
    bool Insecure,
    [property: JsonPropertyName("redirect")]
    string? RedirectVersion,
    [property: JsonPropertyName("platforms")]
    Dictionary<string, EngineBuildInfo> Platforms);

public sealed class EngineBuildInfo
{
    [JsonInclude] [JsonPropertyName("sha256")]
    public string Sha256 = default!;

    [JsonInclude] [JsonPropertyName("sig")]
    public string Signature = default!;

    [JsonInclude] [JsonPropertyName("url")]
    public string Url = default!;
}

public sealed record ServerHubInfo(
    [property: JsonPropertyName("address")]
    string Address,
    [property: JsonPropertyName("statusData")]
    ServerStatus StatusData,
    [property: JsonPropertyName("inferredTags")]
    List<string> InferredTags);

public sealed record ServerStatus(
    [property: JsonPropertyName("map")] string Map,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("tags")] List<string> Tags,
    [property: JsonPropertyName("preset")] string Preset,
    [property: JsonPropertyName("players")]
    int Players,
    [property: JsonPropertyName("round_id")]
    int RoundId,
    [property: JsonPropertyName("run_level")]
    int RunLevel,
    [property: JsonPropertyName("panic_bunker")]
    bool PanicBunker,
    [property: JsonPropertyName("round_start_time")]
    DateTime? RoundStartTime,
    [property: JsonPropertyName("soft_max_players")]
    int SoftMaxPlayers);

public sealed record ModulesInfo(
    [property: JsonPropertyName("modules")]
    Dictionary<string, Module> Modules);

public sealed record Module(
    [property: JsonPropertyName("versions")]
    Dictionary<string, ModuleVersionInfo> Versions);

public sealed record ModuleVersionInfo(
    [property: JsonPropertyName("platforms")]
    Dictionary<string, EngineBuildInfo> Platforms);