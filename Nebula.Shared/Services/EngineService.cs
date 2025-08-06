using System.Diagnostics.CodeAnalysis;
using Nebula.Shared.Configurations;
using Nebula.Shared.FileApis;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;

namespace Nebula.Shared.Services;

[ServiceRegister]
public sealed class EngineService
{
    private readonly AssemblyService _assemblyService;
    private readonly FileService _fileService;
    private readonly RestService _restService;
    private readonly ConfigurationService _varService;
    private readonly Task _currInfoTask;
    private readonly IReadWriteFileApi _engineFileApi;
    private readonly ILogger _logger;
    
    private ModulesInfo? _modulesInfo;
    private Dictionary<string, EngineVersionInfo>? _versionsInfo;

    public EngineService(RestService restService, DebugService debugService, ConfigurationService varService,
        FileService fileService, AssemblyService assemblyService)
    {
        _restService = restService;
        _logger = debugService.GetLogger(this);
        _varService = varService;
        _fileService = fileService;
        _assemblyService = assemblyService;

        _engineFileApi = fileService.CreateFileApi("engine");
        _currInfoTask = Task.Run(() => LoadEngineManifest(CancellationToken.None));
    }

    public void GetEngineInfo(out ModulesInfo modulesInfo, out Dictionary<string, EngineVersionInfo> versionsInfo)
    {
        if(!_currInfoTask.IsCompleted) _currInfoTask.Wait();
        if(_currInfoTask.Exception != null) throw new Exception("Error while loading engine manifest:",_currInfoTask.Exception);
        
        if(_modulesInfo == null || _versionsInfo == null) throw new NullReferenceException("Engine manifest is null");
        
        modulesInfo = _modulesInfo;
        versionsInfo = _versionsInfo;
    }

    public async Task LoadEngineManifest(CancellationToken cancellationToken)
    {
        _logger.Log("start fetching engine manifest");
        _versionsInfo = await LoadExacManifest(CurrentConVar.EngineManifestUrl, CurrentConVar.EngineManifestBackup, cancellationToken);
        _modulesInfo = await LoadExacManifest(CurrentConVar.EngineModuleManifestUrl, CurrentConVar.ModuleManifestBackup, cancellationToken);
        _logger.Log("fetched engine manifest successfully");
    }

    private async Task<T> LoadExacManifest<T>(ConVar<string[]> conVar,ConVar<T> backup,CancellationToken cancellationToken)
    {
        var manifestUrls = _varService.GetConfigValue(conVar)!;

        foreach (var manifestUrl in manifestUrls)
        {
            try
            {
                _logger.Log("Fetching engine manifest from: " + manifestUrl);
                var info = await _restService.GetAsync<T>(
                    new Uri(manifestUrl), cancellationToken);
            
                _varService.SetConfigValue(backup, info);
                return info;
            }
            catch (Exception e)
            {
                _logger.Error($"error while attempt fetch engine manifest: {e.Message}");
            }
        }
        
        _logger.Debug("Trying fallback module manifest...");
        if (!_varService.TryGetConfigValue(backup, out var moduleInfo))
        {
            throw new Exception("No module info data available");
        }
        
        return moduleInfo;
    }

    public EngineBuildInfo? GetVersionInfo(string version)
    {
        GetEngineInfo(out var modulesInfo, out var engineVersionInfo);

        if (!engineVersionInfo.TryGetValue(version, out var foundVersion))
            return null;

        if (foundVersion.RedirectVersion != null)
            return GetVersionInfo(foundVersion.RedirectVersion);

        var bestRid = RidUtility.FindBestRid(foundVersion.Platforms.Keys);
        if (bestRid == null) bestRid = "linux-x64";

        _logger.Log("Selecting RID" + bestRid);

        return foundVersion.Platforms[bestRid];
    }

    public bool TryGetVersionInfo(string version, [NotNullWhen(true)] out EngineBuildInfo? info)
    {
        info = GetVersionInfo(version);
        return info != null;
    }

    public async Task<AssemblyApi?> EnsureEngine(string version)
    {
        _logger.Log("Ensure engine " + version);

        if (!TryOpen(version)) await DownloadEngine(version);

        try
        {
            var api = _fileService.OpenZip(version, _engineFileApi);
            if (api != null) return _assemblyService.Mount(api);
        }
        catch (Exception)
        {
            _engineFileApi.Remove(version);
            throw;
        }

        return null;
    }

    public async Task DownloadEngine(string version)
    {
        if (!TryGetVersionInfo(version, out var info))
            return;

        _logger.Log("Downloading engine version " + version);
        using var client = new HttpClient();
        var s = await client.GetStreamAsync(info.Url);
        _engineFileApi.Save(version, s);
        await s.DisposeAsync();
    }

    public bool TryOpen(string version, [NotNullWhen(true)] out Stream? stream)
    {
        return _engineFileApi.TryOpen(version, out stream);
    }

    public bool TryOpen(string version)
    {
        var a = TryOpen(version, out var stream);
        if (a) stream!.Close();
        return a;
    }

    public EngineBuildInfo? GetModuleBuildInfo(string moduleName, string version)
    {
        GetEngineInfo(out var modulesInfo, out var engineVersionInfo);

        if (!modulesInfo.Modules.TryGetValue(moduleName, out var module) ||
            !module.Versions.TryGetValue(version, out var value))
            return null;

        var bestRid = RidUtility.FindBestRid(value.Platforms.Keys);
        if (bestRid == null) throw new Exception("No engine version available for our platform!");

        return value.Platforms[bestRid];
    }

    public bool TryGetModuleBuildInfo(string moduleName, string version, [NotNullWhen(true)] out EngineBuildInfo? info)
    {
        info = GetModuleBuildInfo(moduleName, version);
        return info != null;
    }

    public string ResolveModuleVersion(string moduleName, string engineVersion)
    {
        GetEngineInfo(out var modulesInfo, out var engineVersionInfo);

        var engineVersionObj = Version.Parse(engineVersion);
        var module = modulesInfo.Modules[moduleName];
        var selectedVersion = module.Versions.Select(kv => new { Version = Version.Parse(kv.Key), kv.Key, kv })
            .Where(kv => engineVersionObj >= kv.Version)
            .MaxBy(kv => kv.Version);

        if (selectedVersion == null) throw new Exception();

        return selectedVersion.Key;
    }

    public async Task<AssemblyApi?> EnsureEngineModules(string moduleName, string engineVersion)
    {
        var moduleVersion = ResolveModuleVersion(moduleName, engineVersion);
        if (!TryGetModuleBuildInfo(moduleName, moduleVersion, out var buildInfo))
            return null;

        var fileName = ConcatName(moduleName, moduleVersion);

        if (!TryOpen(fileName)) await DownloadEngineModule(moduleName, moduleVersion);

        try
        {
            return _assemblyService.Mount(
                _fileService.OpenZip(fileName, _engineFileApi) ?? 
                throw new InvalidOperationException($"{fileName} is not exist!"));
        }
        catch (Exception)
        {
            _engineFileApi.Remove(fileName);
            throw;
        }
    }

    public async Task DownloadEngineModule(string moduleName, string moduleVersion)
    {
        if (!TryGetModuleBuildInfo(moduleName, moduleVersion, out var info))
            return;

        _logger.Log("Downloading engine module version " + moduleVersion);
        using var client = new HttpClient();
        var s = await client.GetStreamAsync(info.Url);
        _engineFileApi.Save(ConcatName(moduleName, moduleVersion), s);
        await s.DisposeAsync();
    }

    public string ConcatName(string moduleName, string moduleVersion)
    {
        return moduleName + "" + moduleVersion;
    }
}