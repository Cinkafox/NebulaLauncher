using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ProcessHelper;

[ServiceRegister(isSingleton:false)]
public sealed class GameProcessStartInfoProvider(DotnetResolverService resolverService, AccountInfoViewModel accountInfoViewModel) : 
    DotnetProcessStartInfoProviderBase(resolverService)
{
    private string? _publicKey;
    private RobustUrl _address = default!;
    
    protected override string GetDllPath()
    {
        var path = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
        return Path.Join(path, "Nebula.Runner.dll");
    }

    public GameProcessStartInfoProvider WithBuildInfo(string publicKey, RobustUrl address)
    {
        _publicKey = publicKey;
        _address = address;

        return this;
    }

    public override async Task<ProcessStartInfo> GetProcessStartInfo()
    {
        var baseStart = await base.GetProcessStartInfo();
        
        var authProv = accountInfoViewModel.Credentials;
        if(authProv is null) 
            throw new Exception("Client is without selected auth");

        baseStart.EnvironmentVariables["ROBUST_AUTH_USERID"] = authProv.UserId.ToString();
        baseStart.EnvironmentVariables["ROBUST_AUTH_TOKEN"] = authProv.Token.Token;
        baseStart.EnvironmentVariables["ROBUST_AUTH_SERVER"] = authProv.AuthServer;
        baseStart.EnvironmentVariables["AUTH_LOGIN"] = authProv.Login;
        baseStart.EnvironmentVariables["ROBUST_AUTH_PUBKEY"] = _publicKey;
        baseStart.EnvironmentVariables["GAME_URL"] = _address.ToString();
        
        return baseStart;
    }
}