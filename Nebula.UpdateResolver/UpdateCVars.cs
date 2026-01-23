using Nebula.SharedModels;
using Nebula.UpdateResolver.Configuration;

namespace Nebula.UpdateResolver;

public static class UpdateConVars
{
    public static readonly ConVar<string> UpdateCacheUrl =
        ConVarBuilder.Build<string>("update.url","https://durenko.tatar/nebula/manifest/");
    public static readonly ConVar<LauncherManifest> CurrentLauncherManifest = 
        ConVarBuilder.Build<LauncherManifest>("update.manifest");
}