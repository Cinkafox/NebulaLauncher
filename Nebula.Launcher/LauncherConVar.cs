using System.Collections.Generic;
using Nebula.Launcher.Models;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Shared.Services;

namespace Nebula.Launcher;

public static class LauncherConVar
{
    public static readonly ConVar<ProfileAuthCredentials[]> AuthProfiles =
        ConVarBuilder.Build<ProfileAuthCredentials[]>("auth.profiles.v2", []);

    public static readonly ConVar<CurrentAuthInfo?> AuthCurrent =
        ConVarBuilder.Build<CurrentAuthInfo?>("auth.current.v2");
    
    public static readonly ConVar<string[]> Favorites =
        ConVarBuilder.Build<string[]>("server.favorites", []);
    
    public static readonly ConVar<AuthServerCredentials[]> AuthServers = ConVarBuilder.Build<AuthServerCredentials[]>("launcher.authServers", [
        new AuthServerCredentials(
            "WizDen", 
            [
                "https://harpy.durenko.tatar/auth-api/",
                "https://auth.fallback.spacestation14.com/",
            ])
    ]);
    
    public static readonly ConVar<ServerHubRecord[]> Hub = ConVarBuilder.Build<ServerHubRecord[]>("launcher.hub.v2", [
        new ServerHubRecord("WizDen", "https://harpy.durenko.tatar/hub-api/api/servers", null),
        new ServerHubRecord("AltHub","https://web.networkgamez.com/api/servers",null)
    ]);

    public static readonly ConVar<string> CurrentLang = ConVarBuilder.Build<string>("launcher.language", "en-US");
    public static readonly ConVar<string> ILSpyUrl = ConVarBuilder.Build<string>("decompiler.url",
    "https://github.com/icsharpcode/ILSpy/releases/download/v9.0/ILSpy_binaries_9.0.0.7889-x64.zip");
    
    
}