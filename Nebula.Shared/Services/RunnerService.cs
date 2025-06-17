using Nebula.Shared.Models;
using Nebula.Shared.Services.Logging;
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
    private ILogger _logger = debugService.GetLogger("RunnerService");

    public async Task Run(string[] runArgs, RobustBuildInfo buildInfo, IRedialApi redialApi,
        ILoadingHandler loadingHandler,
        CancellationToken cancellationToken)
    {
        _logger.Log("Start Content!");

        var engine = await engineService.EnsureEngine(buildInfo.BuildInfo.Build.EngineVersion);

        if (engine is null)
            throw new Exception("Engine version not found: " + buildInfo.BuildInfo.Build.EngineVersion);

        var hashApi = await contentService.EnsureItems(buildInfo.RobustManifestInfo, loadingHandler, cancellationToken);

        var extraMounts = new List<ApiMount>
        {
            new(hashApi, "/")
        };

        if (hashApi.TryOpen("manifest.yml", out var stream))
        {
            var modules = ContentManifestParser.ExtractModules(stream);

            foreach (var moduleStr in modules)
            {
                var module =
                    await engineService.EnsureEngineModules(moduleStr, buildInfo.BuildInfo.Build.EngineVersion);
                if (module is not null)
                    extraMounts.Add(new ApiMount(module, "/"));
            }
            
            await stream.DisposeAsync();
        }
        
        var args = new MainArgs(runArgs, engine, redialApi, extraMounts);

        if (!assemblyService.TryOpenAssembly(varService.GetConfigValue(CurrentConVar.RobustAssemblyName)!, engine,
                out var clientAssembly))
            throw new Exception("Unable to locate Robust.Client.dll in engine build!");

        if (!assemblyService.TryGetLoader(clientAssembly, out var loader))
            return;

        await Task.Run(() => loader.Main(args), cancellationToken);
    }
}

public static class ContentManifestParser
{
    public static List<string> ExtractModules(Stream manifestStream)
    {
        using var reader = new StreamReader(manifestStream);
        return ExtractModules(reader.ReadToEnd());
    }
    
    public static List<string> ExtractModules(string manifestContent)
    {
        var modules = new List<string>();
        var lines = manifestContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        bool inModulesSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("modules:"))
            {
                inModulesSection = true;
                continue;
            }

            if (inModulesSection)
            {
                if (line.StartsWith("- "))
                {
                    modules.Add(line.Substring(2).Trim());
                }
                else if (!line.StartsWith(" "))
                {
                    break;
                }
            }
        }

        return modules;
    }
}