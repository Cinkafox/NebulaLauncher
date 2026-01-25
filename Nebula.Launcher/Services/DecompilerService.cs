
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Shared;
using Nebula.Shared.FileApis;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.SharedModels;

namespace Nebula.Launcher.Services;

[ConstructGenerator, ServiceRegister]
public sealed partial class DecompilerService
{
    [GenerateProperty] private ConfigurationService ConfigurationService { get; } 
    [GenerateProperty] private PopupMessageService PopupMessageService {get;}
    [GenerateProperty] private ViewHelperService ViewHelperService {get;}
    [GenerateProperty] private ContentService ContentService {get;}
    [GenerateProperty] private FileService FileService {get;}
    [GenerateProperty] private EngineService EngineService {get;}
    [GenerateProperty] private DebugService DebugService {get;}

    private readonly HttpClient _httpClient = new();
    private ILogger _logger;

    private string FullPath => Path.Join(AppDataPath.RootPath, $"ILSpy.{ConfigurationService.GetConfigValue(LauncherConVar.ILSpyVersion)}");
    private string ExecutePath => Path.Join(FullPath, "ILSpy.exe");

    public async void OpenDecompiler(string arguments){
        await EnsureILSpy();
        var startInfo = new ProcessStartInfo(){
            FileName = ExecutePath,
            Arguments = arguments
        };
        Process.Start(startInfo);
    }

    public async void OpenServerDecompiler(RobustUrl url, CancellationToken cancellationToken)
    {
        var myTempDir = FileService.EnsureTempDir(out var tmpDir);

        using var loadingHandler = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        var buildInfo =
            await ContentService.GetBuildInfo(url, cancellationToken);
        var engine = await EngineService.EnsureEngine(buildInfo.BuildInfo.Build.EngineVersion, loadingHandler, cancellationToken);
        if (engine is null)
            throw new Exception("Engine version not found: " + buildInfo.BuildInfo.Build.EngineVersion);

        foreach (var file in engine.AllFiles)
        {
            if(!file.Contains(".dll") || !engine.TryOpen(file, out var stream)) continue;
            myTempDir.Save(file, stream);
            await stream.DisposeAsync();
        }
        
        var hashApi = await ContentService.EnsureItems(buildInfo, loadingHandler, cancellationToken);
        
        foreach (var file in hashApi.AllFiles)
        {
            if(!file.Contains(".dll") || !hashApi.TryOpen(file, out var stream)) continue;
            myTempDir.Save(Path.GetFileName(file), stream);
            await stream.DisposeAsync();
        }
        
        _logger.Log("File extracted. " + tmpDir);
        
        OpenDecompiler(string.Join(' ', myTempDir.AllFiles.Select(f=>Path.Join(tmpDir, f))) + " --newinstance");
    }

    private void Initialise()
    {
        _logger = DebugService.GetLogger(this);
    }
    private void InitialiseInDesignMode(){}

    private async Task EnsureILSpy(){
        if(!Directory.Exists(FullPath))
            await Download();
    }

    private async Task Download(){
        using var loading = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loading.LoadingName = "Download ILSpy";
        loading.CreateLoadingContext().SetJobsCount(1);
        PopupMessageService.Popup(loading);
        using var response = await _httpClient.GetAsync(ConfigurationService.GetConfigValue(LauncherConVar.ILSpyUrl));
        using var zipArchive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        Directory.CreateDirectory(FullPath);
        zipArchive.ExtractToDirectory(FullPath);
    }
}