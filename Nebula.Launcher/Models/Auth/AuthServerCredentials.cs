namespace Nebula.Launcher.Models.Auth;

public sealed record AuthServerCredentials(
    string Name, 
    string[] Servers
);