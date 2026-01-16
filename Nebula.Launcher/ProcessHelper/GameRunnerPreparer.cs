using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;

namespace Nebula.Launcher.ProcessHelper;

[ServiceRegister]
public sealed class GameRunnerPreparer(IServiceProvider provider, ContentService contentService, EngineService engineService)
{
    public async Task<GameProcessStartInfoProvider> GetGameProcessStartInfoProvider(RobustUrl address, ILoadingHandlerFactory loadingHandlerFactory, CancellationToken cancellationToken = default)
    {
        var buildInfo = await contentService.GetBuildInfo(address, cancellationToken);
        
        var engine = await engineService.EnsureEngine(buildInfo.BuildInfo.Build.EngineVersion, loadingHandlerFactory, cancellationToken);

        if (engine is null)
            throw new Exception("Engine version not found: " + buildInfo.BuildInfo.Build.EngineVersion);

        var hashApi = await contentService.EnsureItems(buildInfo, loadingHandlerFactory, cancellationToken);
        
        if (hashApi.TryOpen("manifest.yml", out var stream))
        {
            var modules = ContentManifestParser.ExtractModules(stream);

            foreach (var moduleStr in modules)
            {
                var module = await engineService.EnsureEngineModules(moduleStr, loadingHandlerFactory, buildInfo.BuildInfo.Build.EngineVersion);
                if(module is null) 
                    throw new Exception("Module not found: " + moduleStr);
            }
            
            await stream.DisposeAsync();
        }

        return 
            provider.GetService<GameProcessStartInfoProvider>()!.WithBuildInfo(buildInfo.BuildInfo.Auth.PublicKey,
                address);
        
    }
}