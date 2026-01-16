using System.Collections.Generic;
using System.Globalization;
using Nebula.Launcher.Models;
using Nebula.Launcher.Models.Auth;
using Nebula.Shared.ConfigMigrations;
using Nebula.Shared.Configurations;
using Nebula.Shared.Configurations.Migrations;
using Nebula.Shared.Services;

namespace Nebula.Launcher;

public static class LauncherConVar
{
    public static readonly ConVar<bool> DoMigration =
        ConVarBuilder.Build("migration.doMigrate", true);
    
    public static readonly ConVar<string[]> AuthProfiles =
        ConVarBuilder.BuildWithMigration<string[]>("auth.profiles.v4", 
            MigrationQueueBuilder.Instance
                .With(new ProfileMigrationV2("auth.profiles.v2","auth.profiles.v4"))
                .With(new ProfileMigrationV3V4("auth.profiles.v3","auth.profiles.v4"))
                .Build(), 
            []);

    public static readonly ConVar<AuthTokenCredentials?> AuthCurrent =
        ConVarBuilder.Build<AuthTokenCredentials?>("auth.current.v2");
    
    public static readonly ConVar<string[]> Favorites =
        ConVarBuilder.Build<string[]>("server.favorites", []);
    
    public static readonly ConVar<Dictionary<string,string>> ServerCustomNames = 
        ConVarBuilder.Build<Dictionary<string,string>>("server.names", []);
    
    public static readonly ConVar<AuthServerCredentials[]> AuthServers = 
        ConVarBuilder.Build<AuthServerCredentials[]>("launcher.authServers", [
        new AuthServerCredentials(
            "WizDen", 
            [
                "https://harpy.durenko.tatar/auth-api/",
                "https://auth.spacestation14.com/",
                "https://auth.fallback.spacestation14.com/",
            ]),
        new AuthServerCredentials(
            "SimpleStation", 
            [
                "https://auth.simplestation.org/",
            ])
    ]);
    
    public static readonly ConVar<ServerHubRecord[]> Hub = ConVarBuilder.Build<ServerHubRecord[]>("launcher.hub.v2", [
        new ServerHubRecord("WizDen", "https://harpy.durenko.tatar/hub-api/api/servers"),
        new ServerHubRecord("AltHub","https://hub.singularity14.co.uk/api/servers")
    ]);

    public static readonly ConVar<string> CurrentLang = ConVarBuilder.Build<string>("launcher.language", CultureInfo.CurrentCulture.Name);
    public static readonly ConVar<string> ILSpyUrl = ConVarBuilder.Build<string>("decompiler.url",
    "https://github.com/icsharpcode/ILSpy/releases/download/v10.0-preview2/ILSpy_selfcontained_10.0.0.8282-preview2-x64.zip");
    
    public static readonly ConVar<string> ILSpyVersion = ConVarBuilder.Build<string>("dotnet.version", "10");
}