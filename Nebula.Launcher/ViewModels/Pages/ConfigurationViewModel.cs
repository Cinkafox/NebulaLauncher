using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared;
using Nebula.Shared.Configurations;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(ConfigurationView))]
[ConstructGenerator]
public partial class ConfigurationViewModel : ViewModelBase
{
    public ObservableCollection<IConfigControl> ConfigurationVerbose { get; } = new();
    
    [GenerateProperty] private ConfigurationService ConfigurationService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupService { get; } = default!;
    [GenerateProperty] private FileService FileService { get; set; } = default!;
    [GenerateProperty] private ContentService ContentService { get; set; } = default!;
    [GenerateProperty] private CancellationService CancellationService { get; set; } = default!;
    [GenerateProperty] private ViewHelperService ViewHelperService { get; set; } = default!;


    private readonly List<(object, Type)> _conVarList = new();

    public void AddCvarConf<T>(ConVar<T> cvar)
    {
        ConfigurationVerbose.Add(
            ConfigControlHelper.GetConfigControl(cvar.Name, ConfigurationService.GetConfigValue(cvar)!));
        _conVarList.Add((cvar, cvar.Type));
    }

    public void InvokeUpdateConfiguration()
    {
        for (int i = 0; i < ConfigurationVerbose.Count; i++)
        {
            var conVarControl = ConfigurationVerbose[i];
            if(!conVarControl.Dirty) 
                continue;
            
            var conVar = _conVarList[i];
            var methodInfo = ConfigurationService.GetType().GetMethod("SetConfigValue")!.MakeGenericMethod(conVar.Item2);
            methodInfo.Invoke(ConfigurationService, [conVar.Item1, conVarControl.GetValue()]);
        }
    }

    public void ResetConfig()
    {
        foreach (var conVar in _conVarList)
        {
            var methodInfo = ConfigurationService.GetType().GetMethod("SetConfigValue")!.MakeGenericMethod(conVar.Item2);
            methodInfo.Invoke(ConfigurationService, [conVar.Item1, null]);
        }
        
        _conVarList.Clear();
        ConfigurationVerbose.Clear();

        InitConfiguration();
        
        PopupService.Popup("Configuration has been reset.");
    }

    public void OpenDataFolder()
    {
        ExplorerHelper.OpenFolder(FileService.RootPath);
    }

    public void ExportLogs()
    {
        var logPath = Path.Join(FileService.RootPath, "log");
        var path = Path.Combine(Path.GetTempPath(), "tempThink"+Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        
        ZipFile.CreateFromDirectory(logPath, Path.Join(path, DateTime.Now.ToString("yyyy-MM-dd") + ".zip"));
        ExplorerHelper.OpenFolder(path);
    }

    public void RemoveAllContent()
    {
        Task.Run(() =>
        {
            using var loader = ViewHelperService.GetViewModel<LoadingContextViewModel>();
            loader.LoadingName = "Removing content";
            PopupService.Popup(loader);
            ContentService.RemoveAllContent(loader.CreateLoadingContext(), CancellationService.Token);
        });
    }

    private void InitConfiguration()
    {
        AddCvarConf(LauncherConVar.ILSpyUrl);
        AddCvarConf(LauncherConVar.Hub);
        AddCvarConf(LauncherConVar.AuthServers);
        AddCvarConf(CurrentConVar.EngineManifestUrl);
        AddCvarConf(CurrentConVar.RobustAssemblyName);
        AddCvarConf(CurrentConVar.ManifestDownloadProtocolVersion);
    }
    
    protected override void InitialiseInDesignMode()
    {
        InitConfiguration();
    }

    protected override void Initialise()
    {
        InitConfiguration();
    }
}