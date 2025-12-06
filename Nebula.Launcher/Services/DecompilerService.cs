
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

    private HttpClient _httpClient = new HttpClient();
    private ILogger _logger;

    private static string fullPath = Path.Join(FileService.RootPath,"ILSpy");
    private static string executePath = Path.Join(fullPath, "ILSpy.exe");

    public async void OpenDecompiler(string arguments){
        await EnsureILSpy();
        var startInfo = new ProcessStartInfo(){
            FileName = executePath,
            Arguments = arguments
        };
        Process.Start(startInfo);
    }

    public async void OpenServerDecompiler(RobustUrl url, CancellationToken cancellationToken)
    {
        var myTempDir = FileService.EnsureTempDir(out var tmpDir);

        var loadingHandler = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        var mainLoadingHandler = loadingHandler.CreateLoadingContext();
        mainLoadingHandler.AppendJob(4);
        var buildInfo =
            await ContentService.GetBuildInfo(url, cancellationToken);
        var engine = await EngineService.EnsureEngine(buildInfo.BuildInfo.Build.EngineVersion, loadingHandler, cancellationToken);
        mainLoadingHandler.AppendJob();
        if (engine is null)
            throw new Exception("Engine version not found: " + buildInfo.BuildInfo.Build.EngineVersion);

        foreach (var file in engine.AllFiles)
        {
            if(!file.Contains(".dll") || !engine.TryOpen(file, out var stream)) continue;
            myTempDir.Save(file, stream);
            await stream.DisposeAsync();
        }
        
        mainLoadingHandler.AppendJob();

        var hashApi = await ContentService.EnsureItems(buildInfo.RobustManifestInfo, loadingHandler, cancellationToken);
        mainLoadingHandler.AppendJob();
        foreach (var (file, hash) in hashApi.Manifest)
        {
            if(!file.Contains(".dll") || !hashApi.TryOpen(hash, out var stream)) continue;
            myTempDir.Save(Path.GetFileName(file), stream);
            await stream.DisposeAsync();
        }
        
        loadingHandler.Dispose();
        
        _logger.Log("File extracted. " + tmpDir);
        
        OpenDecompiler(string.Join(' ', myTempDir.AllFiles.Select(f=>Path.Join(tmpDir, f))) + " --newinstance");
    }

    private void Initialise()
    {
        _logger = DebugService.GetLogger(this);
    }
    private void InitialiseInDesignMode(){}

    private async Task EnsureILSpy(){
        if(!Directory.Exists(fullPath))
            await Download();
    }

    private async Task Download(){
        using var loading = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loading.LoadingName = "Download ILSpy";
        loading.CreateLoadingContext().SetJobsCount(1);
        PopupMessageService.Popup(loading);
        using var response = await _httpClient.GetAsync(ConfigurationService.GetConfigValue(LauncherConVar.ILSpyUrl));
        using var zipArchive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        Directory.CreateDirectory(fullPath);
        zipArchive.ExtractToDirectory(fullPath);
    }
}