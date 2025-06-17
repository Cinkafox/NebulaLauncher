using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ProcessHelper;

[ServiceRegister]
public sealed class GameRunnerPreparer(IServiceProvider provider, ContentService contentService, EngineService engineService, DebugService debugService)
{
    public async Task<ProcessRunHandler<GameProcessStartInfoProvider>> GetGameProcessStartInfoProvider(RobustUrl address, ILoadingHandler loadingHandler, CancellationToken cancellationToken = default)
    {
        var buildInfo = await contentService.GetBuildInfo(address, cancellationToken);
        
        var engine = await engineService.EnsureEngine(buildInfo.BuildInfo.Build.EngineVersion);

        if (engine is null)
            throw new Exception("Engine version not found: " + buildInfo.BuildInfo.Build.EngineVersion);

        await contentService.EnsureItems(buildInfo.RobustManifestInfo, loadingHandler, cancellationToken);
        await engineService.EnsureEngineModules("Robust.Client.WebView", buildInfo.BuildInfo.Build.EngineVersion);

        var gameInfo =
            provider.GetService<GameProcessStartInfoProvider>()!.WithBuildInfo(buildInfo.BuildInfo.Auth.PublicKey,
                address);
        var gameProcessRunHandler = new ProcessRunHandler<GameProcessStartInfoProvider>(gameInfo);
        
        return gameProcessRunHandler;
    }
}