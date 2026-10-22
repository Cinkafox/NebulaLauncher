using System;
using System.IO;
using Nebula.Launcher.Models.ContentBrowser;
using Nebula.Launcher.Services;
using Nebula.Launcher.Utils;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;
using Robust.LoaderApi;

namespace Nebula.Launcher.ViewModels.ContentBrowser;

[ViewModelRegister(typeof(FileContentEntryView), false), ConstructGenerator]
public sealed partial class ServerFolderContentEntry : BaseFolderContentEntry
{
    [GenerateProperty, DesignConstruct] public override ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] public ContentService ContentService { get; } = default!;
    [GenerateProperty] public CancellationService CancellationService { get; } = default!;
    [GenerateProperty] public PopupMessageService PopupService { get; } = default!;
    [GenerateProperty] public DecompilerService DecompilerService { get; } = default!;
    [GenerateProperty] public FileService FileService { get; } = default!;
    [GenerateProperty] private FileFormService FileFormService { get; } = default!;
    [GenerateProperty] private DebugService DebugService { get; } = default!;
    [GenerateProperty] private FileContentLinkService FileContentLinkService { get; } = default!;
    
    public RobustUrl ServerUrl { get; private set; }
    private IFileApi FileApi { get; set; } = default!;
    private RobustBuildInfo BuildInfo { get; set; } = default!;
    
    public async void Init(IContentHolder holder, RobustUrl serverUrl)
    {
        base.Init(holder);
        
        IsLoading = true;
        var loading = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loading.LoadingName = "Loading entry";
        PopupService.Popup(loading);
        ServerUrl = serverUrl;

        BuildInfo = await ContentService.GetBuildInfo(serverUrl, CancellationService.Token);
        FileApi = await ContentService.GetAllItems(BuildInfo, loading.CreateContextEntry(),
            CancellationService.Token);

        foreach (var path in FileApi.AllFiles)
        {
            CreateContent(new ContentPath(path));
        }
            
        IsLoading = false;
        loading.Dispose();
    }

    private BaseFileContentEntry CreateContent(ContentPath path)
    {
        var pathDir = path.GetDirectory();
        BaseFolderContentEntry parent = this;
        
        while (pathDir.TryNext(out var dirPart))
        {
            if (!parent.TryGetChild(dirPart, out var folderContentEntry))
            {
                folderContentEntry = ViewHelperService.GetViewModel<FolderContentEntry>();
                ((FolderContentEntry)folderContentEntry).Init(Holder, dirPart);
                parent.AddChild(folderContentEntry);
            }
            
            parent = folderContentEntry as BaseFolderContentEntry ?? throw new InvalidOperationException();
        }
        
        BaseFileContentEntry fileContentEntry;

        if (FileContentLinkService.TryGetFileContent(Path.GetExtension(path.GetName()), out var fileContentType))
        {
            if(!ViewHelperService.TryGetViewModel(fileContentType, out fileContentEntry!))
                throw new InvalidOperationException();
        }
        else
        {
            fileContentEntry = ViewHelperService.GetViewModel<OpeningFileContentEntry>();
        }
        
        fileContentEntry.Init(Holder, FileApi, path.GetName(), ContentService, ViewHelperService, PopupService);
        
        parent.AddChild(fileContentEntry);
        
        return fileContentEntry;
    }

    public async void UnpackServerFiles()
    {
        var path = await FileFormService.ShowOpenFileDialogAsync();
        if(path is null) return;
        
        
        var loading = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loading.LoadingName = "Unpacking entry";
        PopupService.Popup(loading);

        try
        {
            var ensuredFileApi = await ContentService.EnsureItems(
                BuildInfo,
                loading.CreateContextEntry(),
                CancellationService.Token);
            ContentService.Unpack(ensuredFileApi, path, loading.CreateContextEntry().CreateLoadingContext());
            ExplorerUtils.OpenFolder(path.RootPath);
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception e)
        {
            DebugService.GetLogger(this).Error(e);
        }
        
        loading.Dispose();
    }
    
    protected override void InitialiseInDesignMode() { }
    protected override void Initialise() { }
}