using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Models;
using Nebula.Launcher.Services;
using Nebula.Launcher.Utils;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared.FileApis;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;
using Robust.LoaderApi;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(ContentBrowserView))]
[ConstructGenerator]
public sealed partial class ContentBrowserViewModel : ViewModelBase, IContentHolder
{
    [ObservableProperty] private string _serverText = "";
    [ObservableProperty] private string _searchText = "";
    [GenerateProperty] private ContentService ContentService { get; } = default!;
    [GenerateProperty] private FileService FileService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupService { get; } = default!;
    [GenerateProperty] private IServiceProvider ServiceProvider { get; }
    [GenerateProperty] private CancellationService CancellationService { get; set; } = default!;
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] private DecompilerService DecompilerService { get; } = default!;

    public static readonly string[] AllowedExtToOpen = [".yaml", ".yml", ".txt", ".html", ".mp3", ".ogg", ".json"];

    public IContentEntry CurrentEntry
    {
        get;
        set
        {
            if(ProcessContent(value))
                return;

            OnPropertyChanging();
            field = value;
            OnPropertyChanged();
            
            SearchText = value.FullPath.ToString();
            if (value.GetRoot() is ServerFolderContentEntry serverEntry)
            {
                ServerText = serverEntry.ServerUrl.ToString();
            }
        }
    }

    public void OnBackEnter()
    {
        if (CurrentEntry.Parent is null)
        {
            SetHubRoot();
            return;
        }
        CurrentEntry.Parent?.GoCurrent();
    }

    public void OnUnpack()
    {
        if(CurrentEntry is not ServerFolderContentEntry serverEntry) 
            return;

        serverEntry.UnpackServerFiles();
    }
    
    private async void ExecuteFile(FileContentEntry file, CancellationToken cancellationToken = default)
    {
        var fullPath = ((IContentEntry)file).FullPath;

        await using var stream = await file.OpenFile(cancellationToken);
        
        try
        {
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
        catch (Exception e)
        {
            PopupService.Popup(e);
        }
    }

    public async Task<IContentEntry?> GetEntry(ContentPath path)
    {
        var cur = ServiceProvider.GetService<ServerFolderContentEntry>()!;
        cur.Init(this, ServerText.ToRobustUrl());
        return await cur.Go(path, CancellationService.Token);
    }

    public async void OnGoEnter()
    {
        if (string.IsNullOrWhiteSpace(ServerText))
        {
            SetHubRoot();
            SearchText = string.Empty;
            return;
        }

        try
        {
            CurrentEntry = await GetEntry(new ContentPath(SearchText))?? throw new NullReferenceException($"{SearchText} not found in {ServerText}");
        }
        catch (Exception e)
        {
            PopupService.Popup(e);
            ServerText = string.Empty;
            SearchText = string.Empty;
            SetHubRoot();
        }
    }
    
    private bool ProcessContent(IContentEntry entry)
    {
        var ext = Path.GetExtension(entry.Name);
        
        if (entry is FileContentEntry fileEntry)
        {
            if (AllowedExtToOpen.Contains(ext))
            {
                ExecuteFile(fileEntry, CancellationService.Token);
                return true;
            }
            
            switch (ext)
            {
                case ".dll":
                    DecompilerService.OpenServerDecompiler(ServerText.ToRobustUrl(), CancellationService.Token);
                    return true;
                case ".rsic":
                case ".png":
                    Task.Run(async () =>
                    {
                        await using var stream = await fileEntry.OpenFile();

                        var rsicShowViewModel = ViewHelperService.GetViewModel<ImageShowViewModel>();

                        if (ext == ".rsic")
                            rsicShowViewModel.Image =
                                ViewHelperService.GetViewModel<RsiImageViewModel>().LoadFromStream(stream);
                        else
                            rsicShowViewModel.Image =
                                ViewHelperService.GetViewModel<StaticImageViewModel>().LoadFromStream(stream);

                        PopupService.Popup(rsicShowViewModel);
                    });
                    return true;
            }
        }
        
        if (entry is BaseFolderContentEntry && ext == ".rsi")
        {
            Task.Run(async () =>
            {
                var imageView = ViewHelperService.GetViewModel<ImageShowViewModel>();
                PopupService.Popup(imageView);
                
                imageView.Image =
                    await ViewHelperService.GetViewModel<RsiImageViewModel>().LoadFromDirectory(entry);
            });
            return true;
        }

        return false;
    }
    
    protected override void InitialiseInDesignMode()
    {
        var root = ViewHelperService.GetViewModel<FolderContentEntry>();
        root.Init(this);
        var child = root.AddFolder("Biba");
        child.AddFolder("Boba");
        child.AddFolder("Buba");
        CurrentEntry = root;
    }

    protected override void Initialise()
    {
        SetHubRoot();
    }

    public void SetHubRoot()
    {
        ServerText = string.Empty;
        SearchText = string.Empty;
        var root = ViewHelperService.GetViewModel<ServerListContentEntry>();
        root.InitHubList(this);
        CurrentEntry = root;
    }

    public void Go(RobustUrl url, ContentPath path)
    {
        ServerText = url.ToString();
        SearchText = path.ToString();
        OnGoEnter();
    }
}

public interface IContentHolder
{
    public IContentEntry CurrentEntry { get; set; }
}

public interface IContentEntry
{
    public IContentHolder Holder { get; }
    
    public IContentEntry? Parent { get; set; }
    public string? Name { get; }
    public string IconPath { get; }
    public ContentPath FullPath => Parent?.FullPath.With(Name) ?? new ContentPath(Name);
    
    public Task<IContentEntry?> Go(ContentPath path, CancellationToken cancellationToken = default);
    
    public async void GoCurrent()
    {
        var entry = await Go(ContentPath.Empty, CancellationToken.None);
        if(entry is not null) Holder.CurrentEntry = entry;
    }
    
    public IContentEntry GetRoot()
    {
        if (Parent is null) return this;
        return Parent.GetRoot();
    }
}


public sealed class LazyContentEntry : IContentEntry
{
    public IContentHolder Holder { get; set; }
    public IContentEntry? Parent { get; set; }
    public string? Name { get; }
    public string IconPath { get; }

    private readonly IContentEntry _lazyEntry;
    private readonly Action _lazyEntryInit;

    public LazyContentEntry (IContentHolder holder,string name, IContentEntry entry, Action lazyEntryInit)
    {
        Holder = holder;
        Name = name;
        IconPath = entry.IconPath;
        _lazyEntry = entry;
        _lazyEntryInit = lazyEntryInit;
    }
    public async Task<IContentEntry?> Go(ContentPath path, CancellationToken cancellationToken)
    {
        _lazyEntryInit?.Invoke();
        return _lazyEntry;
    }
}

public sealed partial class FileContentEntry : IContentEntry
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
            
            await _contentService.Download([file], hashApi, loading, cancellationToken);

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
            return this;
        return null;
    }
}

[ViewModelRegister(typeof(FileContentEntryView), false), ConstructGenerator]
public sealed partial class FolderContentEntry : BaseFolderContentEntry
{
    [GenerateProperty, DesignConstruct] public override ViewHelperService ViewHelperService { get; } = default!;
    
    public FolderContentEntry AddFolder(string folderName)
    {
        var folder = ViewHelperService.GetViewModel<FolderContentEntry>();
        folder.Init(Holder, folderName);
        return AddChild(folder);
    }

    protected override void InitialiseInDesignMode() { }
    protected override void Initialise() { }
}

[ViewModelRegister(typeof(FileContentEntryView), false), ConstructGenerator]
public sealed partial class ServerFolderContentEntry : BaseFolderContentEntry
{
    [GenerateProperty, DesignConstruct] public override ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] public ContentService ContentService { get; } = default!;
    [GenerateProperty] public CancellationService CancellationService { get; } = default!;
    [GenerateProperty] public PopupMessageService PopupService { get; } = default!;
    [GenerateProperty] public DecompilerService DecompilerService { get; } = default!;
    [GenerateProperty] public FileService FileService { get; } = default!;
    
    public RobustUrl ServerUrl { get; private set; }
    private IFileApi FileApi { get; set; } = default!;
    
    public async void Init(IContentHolder holder, RobustUrl serverUrl)
    {
        base.Init(holder);
        
        IsLoading = true;
        var loading = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loading.LoadingName = "Loading entry";
        PopupService.Popup(loading);
        ServerUrl = serverUrl;

        var buildInfo = await ContentService.GetBuildInfo(serverUrl, CancellationService.Token);
        FileApi = await ContentService.GetAllItems(buildInfo, loading,
            CancellationService.Token);

        foreach (var path in FileApi.AllFiles)
        {
            CreateContent(new ContentPath(path));
        }
            
        IsLoading = false;
        loading.Dispose();
    }

    public FileContentEntry CreateContent(ContentPath path)
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
        
        var manifestContent = new FileContentEntry();
        manifestContent.Init(Holder, FileApi, path.GetName(), ContentService, ViewHelperService, PopupService);
        
        parent.AddChild(manifestContent);
        
        return manifestContent;
    }

    public void UnpackServerFiles()
    {
        var myTempDir = FileService.EnsureTempDir(out var tmpDir);
        
        var loading = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loading.LoadingName = "Unpacking entry";
        PopupService.Popup(loading);

        Task.Run(() =>
        {
            ContentService.Unpack(FileApi, myTempDir, loading.CreateLoadingContext());
            loading.Dispose();
        });
        ExplorerUtils.OpenFolder(tmpDir);
    }
    
    protected override void InitialiseInDesignMode() { }
    protected override void Initialise() { }
}

[ViewModelRegister(typeof(FileContentEntryView), false), ConstructGenerator]
public sealed partial class ServerListContentEntry : BaseFolderContentEntry
{
    [GenerateProperty, DesignConstruct] public override ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] public ConfigurationService ConfigurationService { get; } = default!;
    [GenerateProperty] public IServiceProvider ServiceProvider { get; } = default!;
    [GenerateProperty] public RestService RestService { get; } = default!;
    
    public void InitHubList(IContentHolder holder)
    {
        base.Init(holder);

        var servers = ConfigurationService.GetConfigValue(LauncherConVar.Hub)!;

        foreach (var server in servers)
        {
            var serverFolder = ServiceProvider.GetService<ServerListContentEntry>()!;
            var serverLazy = new LazyContentEntry(Holder, server.Name , serverFolder, () => serverFolder.InitServerList(Holder, server));
            AddChild(serverLazy);
        }
    }

    public async void InitServerList(IContentHolder holder, ServerHubRecord hubRecord)
    {
        base.Init(holder, hubRecord.Name);

        IsLoading = true;
        var servers =
            await RestService.GetAsync<List<ServerHubInfo>>(new Uri(hubRecord.MainUrl), CancellationToken.None);

        foreach (var server in servers)
        {
            var serverFolder = ServiceProvider.GetService<ServerFolderContentEntry>()!;
            var serverLazy = new LazyContentEntry(Holder, server.StatusData.Name , serverFolder, () => serverFolder.Init(Holder, server.Address.ToRobustUrl()));
            AddChild(serverLazy);
        }

        IsLoading = true;
    }

    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
}

public abstract class BaseFolderContentEntry : ViewModelBase, IContentEntry
{
    public bool IsLoading { get; set; } = false;
    public abstract ViewHelperService ViewHelperService { get; }
    
    public ObservableCollection<IContentEntry> Entries { get; } = [];

    private Dictionary<string, IContentEntry> _childs = [];

    public string IconPath => "/Assets/svg/folder.svg";
    
    private IContentHolder? _holder = null;
    public IContentHolder Holder
    {
        get
        {
            if(_holder == null) 
                throw new InvalidOperationException(
                    GetType().Name + " was not initialised! Call Init(IContentHolder holder, string? name = null) before using it.");
            
            return _holder;
        }
    }

    public IContentEntry? Parent { get; set; }
    public string? Name { get; private set; }
    
    public async Task<IContentEntry?> Go(ContentPath path, CancellationToken cancellationToken = default)
    {
        if (path.IsEmpty()) return this;
        if (_childs.TryGetValue(path.GetNext(), out var child)) 
            return await child.Go(path, cancellationToken);
        
        return null;
    }

    public void Init(IContentHolder holder, string? name = null)
    {
        Name = name;
        _holder = holder;
    }

    public T AddChild<T>(T child) where T: IContentEntry
    {
        if(child.Name is null) throw new InvalidOperationException();
        
        child.Parent = this;
        
        _childs.Add(child.Name, child);
        Entries.Add(child);

        return child;
    }

    public bool TryGetChild(string name,[NotNullWhen(true)] out IContentEntry? child)
    {
        return _childs.TryGetValue(name, out child);
    }
}


public struct ContentPath : IEquatable<ContentPath>
{
    public static readonly ContentPath Empty = new();
    
    public List<string> Pathes { get; }

    public ContentPath()
    {
        Pathes = [];
    }

    public ContentPath(List<string> pathes)
    {
        Pathes = pathes;
    }

    public ContentPath(string? path)
    {
        Pathes = string.IsNullOrEmpty(path)
            ? new List<string>()
            : path.Split(['/'], StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public ContentPath With(string? name)
    {
        if (name != null) return new ContentPath([..Pathes, name]);
        return new ContentPath(Pathes);
    }

    public ContentPath GetDirectory()
    {
        if (Pathes.Count == 0)
            return this;

        var directoryPathes = Pathes.Take(Pathes.Count - 1).ToList();
        return new ContentPath(directoryPathes);
    }

    public string GetName()
    {
        if (Pathes.Count == 0)
            throw new InvalidOperationException("Cannot get the name of the root path.");

        return Pathes.Last();
    }

    public string GetNext()
    {
        if (Pathes.Count == 0)
            throw new InvalidOperationException("No elements left to retrieve from the root.");

        var nextName = Pathes[0];
        Pathes.RemoveAt(0);

        return string.IsNullOrWhiteSpace(nextName) ? GetNext() : nextName;
    }

    public bool TryNext([NotNullWhen(true)]out string? part)
    {
        part = null;
        if (Pathes.Count == 0) return false;
        part = GetNext();
        return true;
    }

    public ContentPath Clone()
    {
        return new ContentPath(new List<string>(Pathes));
    }

    public string Path => Pathes.Count == 0 ? "/" : string.Join("/", Pathes);

    public override string ToString()
    {
        return Path;
    }

    public bool IsEmpty()
    {
        return Pathes.Count == 0;
    }

    public bool Equals(ContentPath other)
    {
        return Pathes.Equals(other.Pathes);
    }

    public override bool Equals(object? obj)
    {
        return obj is ContentPath other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Pathes.GetHashCode();
    }
}

public sealed class ContentComparer : IComparer<IContentEntry>
{
    public int Compare(IContentEntry? x, IContentEntry? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (y is null) return 1;
        if (x is null) return -1;
        return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
    }
}