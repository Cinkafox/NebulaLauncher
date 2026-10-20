using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Launcher.Models.ContentBrowser;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views;
using Nebula.Shared.FileApis;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;
using Robust.LoaderApi;

namespace Nebula.Launcher.ViewModels.ContentBrowser;

public abstract class BaseFileContentEntry : ViewModelBase, IContentEntry
{
    public IContentHolder Holder { get; set; } = default!;
    public IContentEntry? Parent { get; set; }
    public string? Name { get; set; }
    public string IconPath => "/Assets/svg/file.svg";
    
    private IFileApi _fileApi = default!;
    private ContentService _contentService = default!;
    private ViewHelperService _viewHelperService = default!;
    private PopupMessageService _popupMessageService = default!;

    public void Init(IContentHolder holder, 
        IFileApi api, 
        string fileName,
        ContentService contentService,
        ViewHelperService viewHelperService,
        PopupMessageService popupService
    )
    {
        Holder = holder;
        Name = fileName;
        _fileApi = api;
        _contentService = contentService;
        _viewHelperService = viewHelperService;
        _popupMessageService = popupService;
    }

    public async Task EnsureFile(CancellationToken cancellationToken)
    {
        var fullPath = ((IContentEntry)this).FullPath;
        
        if (_fileApi is HashApi hashApi && !hashApi.FileDownloaded(fullPath.ToString()))
        {
            var file = hashApi.Manifest[fullPath.ToString()];
            var loading = _viewHelperService.GetViewModel<LoadingContextViewModel>();
            loading.LoadingName = "Loading file";
            _popupMessageService.Popup(loading);
            
            await _contentService.Download([file], hashApi, loading.CreateContextEntry(), cancellationToken);

            loading.Dispose();
        }
    }

    public async Task<Stream> OpenFile(CancellationToken cancellationToken = default)
    {
        await EnsureFile(cancellationToken);
        
        var fullPath = ((IContentEntry)this).FullPath;
        
        if (!_fileApi.TryOpen(fullPath.Path, out var stream))
            throw new FileNotFoundException();
        
        return stream;
    }
    
    public async Task<IContentEntry?> Go(ContentPath path, CancellationToken cancellationToken = default)
    {
        if (path.IsEmpty())
        {
            Execute(cancellationToken);
            return this;
        }
        return null;
    }

    public abstract void Execute(CancellationToken cancellationToken = default);
}

[ViewModelRegister(typeof(OpeningFileContentEntryView), false), ConstructGenerator]
public sealed partial class OpeningFileContentEntry : BaseFileContentEntry
{
    [GenerateProperty] private DecompilerService DecompilerService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupMessageService { get; } = default!;

    private static readonly string[] NotAllowedExt = [".exe,.ps1,.sh"];
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
    
    private async void ExecuteFile(BaseFileContentEntry file, CancellationToken cancellationToken = default)
    {
        var fullPath = ((IContentEntry)file).FullPath;

        await using var stream = await file.OpenFile(cancellationToken);
        
        var myTempFile = Path.Combine(Path.GetTempPath(), fullPath.GetName());

        var sw = new FileStream(myTempFile, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(sw, cancellationToken);
        await sw.DisposeAsync();

        var startInfo = new ProcessStartInfo(myTempFile)
        {
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private RobustUrl GetServerUrl()
    {
        var currParent = this.Parent;
        while (currParent != null)
        {
            if(currParent is ServerFolderContentEntry serverFolderContentEntry)
                return serverFolderContentEntry.ServerUrl;
            currParent = currParent.Parent;
        }
        
        throw new Exception($"Parent {typeof(ServerFolderContentEntry)} not found");
    }

    public override void Execute(CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(Name);
        
        if(NotAllowedExt.Contains(ext))
        {
            PopupMessageService.Popup(new Exception($"extension {ext} is not allowed to open!"));
            return;
        }
        
        if (ext == ".dll")
        {
            DecompilerService.OpenServerDecompiler(GetServerUrl(), cancellationToken);
            return;
        }
        
        ExecuteFile(this, cancellationToken);
    }
}