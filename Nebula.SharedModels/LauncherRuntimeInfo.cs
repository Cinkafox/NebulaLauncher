using System.Text.Json.Serialization;

namespace Nebula.SharedModels;

public record struct LauncherRuntimeInfo(
    [property: JsonPropertyName("version")] string RuntimeVersion,
    [property: JsonPropertyName("runtimes")] Dictionary<string, string> DotnetRuntimes);
    
public static class LauncherManifestEntryHelper
{
    public static string GetFullPath(this LauncherRuntimeInfo runtimeInfo)
    {
        return Path.Join(AppDataPath.RootPath,
            $"dotnet.{runtimeInfo.RuntimeVersion}",
            DotnetUrlHelper.GetRuntimeIdentifier());
    }

    public static string GetExecutePath(this LauncherRuntimeInfo runtimeInfo )
    {
        return Path.Join(GetFullPath(runtimeInfo),
            $"dotnet{DotnetUrlHelper.GetExtension()}");
    }
}