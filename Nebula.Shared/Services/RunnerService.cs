﻿using Nebula.Shared.Models;
using Robust.LoaderApi;

namespace Nebula.Shared.Services;

[ServiceRegister]
public sealed class RunnerService(
    ContentService contentService,
    DebugService debugService,
    ConfigurationService varService,
    EngineService engineService,
    AssemblyService assemblyService)
{
    public async Task PrepareRun(RobustBuildInfo buildInfo, ILoadingHandler loadingHandler,
        CancellationToken cancellationToken)
    {
        debugService.Log("Prepare Content!");

        var engine = await engineService.EnsureEngine(buildInfo.BuildInfo.Build.EngineVersion);

        if (engine is null)
            throw new Exception("Engine version not found: " + buildInfo.BuildInfo.Build.EngineVersion);

        await contentService.EnsureItems(buildInfo.RobustManifestInfo, loadingHandler, cancellationToken);
        await engineService.EnsureEngineModules("Robust.Client.WebView", buildInfo.BuildInfo.Build.EngineVersion);
    }

    public async Task Run(string[] runArgs, RobustBuildInfo buildInfo, IRedialApi redialApi,
        ILoadingHandler loadingHandler,
        CancellationToken cancellationToken)
    {
        debugService.Log("Start Content!");

        var engine = await engineService.EnsureEngine(buildInfo.BuildInfo.Build.EngineVersion);

        if (engine is null)
            throw new Exception("Engine version not found: " + buildInfo.BuildInfo.Build.EngineVersion);

        var hashApi = await contentService.EnsureItems(buildInfo.RobustManifestInfo, loadingHandler, cancellationToken);

        var extraMounts = new List<ApiMount>
        {
            new(hashApi, "/")
        };

        var module =
            await engineService.EnsureEngineModules("Robust.Client.WebView", buildInfo.BuildInfo.Build.EngineVersion);
        if (module is not null)
            extraMounts.Add(new ApiMount(module, "/"));

        var args = new MainArgs(runArgs, engine, redialApi, extraMounts);

        if (!assemblyService.TryOpenAssembly(varService.GetConfigValue(CurrentConVar.RobustAssemblyName)!, engine,
                out var clientAssembly))
            throw new Exception("Unable to locate Robust.Client.dll in engine build!");

        if (!assemblyService.TryGetLoader(clientAssembly, out var loader))
            return;

        await Task.Run(() => loader.Main(args), cancellationToken);
    }
}