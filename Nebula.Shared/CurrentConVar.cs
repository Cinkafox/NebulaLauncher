using Nebula.Shared.Configurations;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Shared;

public static class CurrentConVar
{
    public static readonly ConVar<string[]> EngineManifestUrl =
        ConVarBuilder.Build<string[]>("engine.manifestUrl", [
            "https://harpy.durenko.tatar/manifests/manifest", 
            "https://robust-builds.fallback.cdn.spacestation14.com/manifest.json"
        ]);

    public static readonly ConVar<string[]> EngineModuleManifestUrl =
        ConVarBuilder.Build<string[]>("engine.moduleManifestUrl", 
        [
            "https://harpy.durenko.tatar/manifests/modules",
            "https://robust-builds.fallback.cdn.spacestation14.com/modules.json"
        ]);

    public static readonly ConVar<int> ManifestDownloadProtocolVersion =
        ConVarBuilder.Build("engine.manifestDownloadProtocolVersion", 1);

    public static readonly ConVar<string> RobustAssemblyName =
        ConVarBuilder.Build("engine.robustAssemblyName", "Robust.Client");

    public static readonly ConVar<Dictionary<string, EngineVersionInfo>> EngineManifestBackup =
        ConVarBuilder.Build<Dictionary<string, EngineVersionInfo>>("engine.manifest.backup");
    public static readonly ConVar<ModulesInfo> ModuleManifestBackup =
        ConVarBuilder.Build<ModulesInfo>("module.manifest.backup");
    public static readonly ConVar<Dictionary<string, string>> ServerManifestHash = 
        ConVarBuilder.Build<Dictionary<string, string>>("server.manifest.hash",[]);
    
    public static readonly ConVar<Dictionary<string,string>> DotnetUrl = ConVarBuilder.Build<Dictionary<string,string>>("dotnet.url",
        new(){
            {"win-x64", "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.6/dotnet-runtime-9.0.6-win-x64.zip"},
            {"win-x86", "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.6/dotnet-runtime-9.0.6-win-x86.zip"},
            {"linux-x64", "https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.6/dotnet-runtime-9.0.6-linux-x64.tar.gz"}
        });
}