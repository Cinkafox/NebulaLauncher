using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Robust.LoaderApi;

namespace Nebula.Runner.Services;

[ServiceRegister]
public sealed class RunnerService(
    ContentService contentService,
    DebugService debugService,
    ConfigurationService varService,
    EngineService engineService,
    AssemblyService assemblyService, 
    ReflectionService reflectionService, 
    HarmonyService harmonyService)
{
    private ILogger _logger = debugService.GetLogger("RunnerService");
    private bool MetricEnabled = false; //TODO: ADD METRIC THINKS LATER

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
        
        if(!assemblyService.TryOpenAssembly("Prometheus.NetStandard", engine, out var prometheusAssembly))
            return;
        
        reflectionService.RegisterRobustAssemblies(engine);
        harmonyService.CreateInstance();
        
        IDisposable? metricServer = null;

        if (MetricEnabled)
        {
            MetricsEnabledPatcher.ApplyPatch(reflectionService, harmonyService);
            metricServer = RunHelper.RunMetric(prometheusAssembly);
        }
       
        
        await Task.Run(() => loader.Main(args), cancellationToken);
        
        metricServer?.Dispose();
    }
}

public static class MetricsEnabledPatcher
{
    public static void ApplyPatch(ReflectionService reflectionService, HarmonyService harmonyService)
    {
        var harmony = harmonyService.Instance.Harmony;
        
        var targetType = reflectionService.GetType("Robust.Shared.GameObjects.EntitySystemManager");
        var targetMethod = targetType.GetProperty("MetricsEnabled").GetGetMethod();
        
        var prefix = typeof(MetricsEnabledPatcher).GetMethod(nameof(MetricsEnabledGetterPrefix),
            BindingFlags.Static | BindingFlags.NonPublic);
        
        var prefixMethod = new HarmonyMethod(prefix);
        
        harmony.Patch(targetMethod, prefix: prefixMethod);
    }
    
    private static bool MetricsEnabledGetterPrefix(ref bool __result)
    {
        __result = true;
        return false; // Skip original method
    }
}

public static class RunHelper
{
    public static IDisposable RunMetric(Assembly prometheusAssembly)
    {
        var metricServerType = prometheusAssembly.GetType("Prometheus.MetricServer");
        var collectorRegistryType = prometheusAssembly.GetType("Prometheus.CollectorRegistry");
        
        var ctor = metricServerType!.GetConstructor(new Type[]
        {
            typeof(string),
            typeof(int),
            typeof(string),
            collectorRegistryType!,
            typeof(bool)
        });
        
        var hostname = "localhost";
        var port = 51235;
        var url = "metrics/";
        object? registry = null; 
        var useHttps = false;
        
        var metricServerInstance = ctor!.Invoke(new object[] { hostname, port, url, registry!, useHttps });
        metricServerType.GetMethod("Start")!.Invoke(metricServerInstance, BindingFlags.Default, null, null, CultureInfo.CurrentCulture);

        return (IDisposable)metricServerInstance;
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