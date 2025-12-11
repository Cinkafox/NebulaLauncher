using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Models;
using Nebula.Launcher.ProcessHelper;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerEntryView), false)]
[ConstructGenerator]
public sealed partial class ServerEntryModelView : ViewModelBase, IFilterConsumer, IListEntryModelView, IFavoriteEntryModelView, IEntryNameHolder
{
    [ObservableProperty] private string _description = "Fetching info...";
    [ObservableProperty] private bool _expandInfo;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _runVisible = true;
    [ObservableProperty] private bool _tagDataVisible;
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private string _realName;

    public string? Name
    {
        get => RealName;
        set => RealName = value ?? Status.Name;
    }
    
    private ILogger _logger;
    private ServerInfo? _serverInfo;
    private InstanceKey _instanceKey;
    public RobustUrl Address { get; private set; }
    [GenerateProperty] private AccountInfoViewModel AccountInfoViewModel { get; }
    [GenerateProperty] private CancellationService CancellationService { get; } = default!;
    [GenerateProperty] private DebugService DebugService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupMessageService { get; } = default!;
    [GenerateProperty] private ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] private RestService RestService { get; } = default!;
    [GenerateProperty] private MainViewModel MainViewModel { get; } = default!;
    [GenerateProperty] private FavoriteServerListProvider FavoriteServerListProvider { get; } = default!;
    [GenerateProperty] private GameRunnerPreparer GameRunnerPreparer { get; } = default!;
    [GenerateProperty] private InstanceRunningContainer InstanceRunningContainer { get; } = default!;

    public ServerStatus Status { get; private set; } =
        new(
            "Fetching data...",
            "Loading...", [],
            "",
            -1,
            -1,
            -1,
            false,
            DateTime.Now,
            -1
        );

    public ObservableCollection<ServerLink> Links { get; } = new();
    public ObservableCollection<string> Tags { get; } = [];
    public ICommand OnLinkGo { get; } = new LinkGoCommand();

    public async Task<ServerInfo?> GetServerInfo()
    {
        if (_serverInfo == null)
            try
            {
                _serverInfo = await RestService.GetAsync<ServerInfo>(Address.InfoUri, CancellationService.Token);
            }
            catch (Exception e)
            {
                Description = e.Message;
                _logger.Error(e);
            }

        return _serverInfo;
    }

    protected override void InitialiseInDesignMode()
    {
        IsVisible = true;
        RealName = "TEST.TEST";
        Description = "Server of meow girls! Nya~ \nNyaMeow\nOOOINK!!";
        Links.Add(new ServerLink("Discord", "discord", "https://cinka.ru"));
        Status = new ServerStatus("Ameba",
            "Locala meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow meow ",
            ["rp:hrp", "18+"],
            "Antag", 15, 5, 1, false
            , DateTime.Now, 100);
        Address = "ss14://localhost";
    }

    protected override void Initialise()
    {
        _logger = DebugService.GetLogger(this);
        InstanceRunningContainer.IsRunningChanged += IsRunningChanged;
    }

    private void IsRunningChanged(InstanceKey arg1, bool isRunning)
    {
        if(arg1.Equals(_instanceKey)) 
            RunVisible = !isRunning;
    }

    public void ProcessFilter(ServerFilter? serverFilter)
    {
        if (serverFilter == null)
        {
            IsVisible = true;
            return;
        }
        
        IsVisible = serverFilter.IsMatch(Status.Name, Tags);
    }

    public void SetStatus(ServerStatus serverStatus)
    {
        Status = serverStatus;
        Tags.Clear();
        foreach (var tag in Status.Tags) Tags.Add(tag);
        OnPropertyChanged(nameof(Status));
    }
    
    public ServerEntryModelView WithData(RobustUrl url, string? name,ServerStatus serverStatus)
    {
        Address = url;
        SetStatus(serverStatus);
        Name = name;
        return this;
    }

    public void EditName()
    {
        var popup = ViewHelperService.GetViewModel<EditServerNameViewModel>();
        popup.IpInput = Address.ToString();
        popup.NameInput = Name ?? string.Empty;
        PopupMessageService.Popup(popup);
    }

    public void OpenContentViewer()
    {
        MainViewModel.RequirePage<ContentBrowserViewModel>().Go(Address, ContentPath.Empty);
    }

    public void ToggleFavorites()
    {
        IsFavorite = !IsFavorite;
        if(IsFavorite)
            FavoriteServerListProvider.AddFavorite(this);
        else
            FavoriteServerListProvider.RemoveFavorite(this);
    }

    public void RunInstance()
    { 
        Task.Run(async ()=> await RunInstanceAsync());
    }

    public void RunInstanceIgnoreAuth()
    {
        Task.Run(async ()=> await RunInstanceAsync(true));
    }

    private async Task RunInstanceAsync(bool ignoreLoginCredentials = false)
    {
        _logger.Log("Running instance..." + RealName);
        if (!ignoreLoginCredentials && AccountInfoViewModel.Credentials.Value is null)
        {
            var warningContext = ViewHelperService.GetViewModel<IsLoginCredentialsNullPopupViewModel>()
                .WithServerEntry(this);
            
            PopupMessageService.Popup(warningContext);
            return;
        }

        try
        {
            using var viewModelLoading = ViewHelperService.GetViewModel<LoadingContextViewModel>();
            viewModelLoading.LoadingName = "Loading instance...";

            PopupMessageService.Popup(viewModelLoading);
            var currProcessStartProvider = 
                await GameRunnerPreparer.GetGameProcessStartInfoProvider(Address, viewModelLoading, CancellationService.Token);
            _logger.Log("Preparing instance...");
            _instanceKey = InstanceRunningContainer.RegisterInstance(currProcessStartProvider);
            InstanceRunningContainer.Run(_instanceKey);
            _logger.Log("Starting instance..." + RealName);
        }
        catch (Exception e)
        {
            var error = new Exception("Error while attempt run instance", e);
            _logger.Error(error);
            PopupMessageService.Popup(error);
        }
    }

    public void StopInstance()
    {
        InstanceRunningContainer.Stop(_instanceKey);
    }
    
    public void ReadLog()
    {
        InstanceRunningContainer.Popup(_instanceKey);
    }

    public async void ExpandInfoRequired()
    {
        ExpandInfo = !ExpandInfo;
        if (Design.IsDesignMode) return;

        var info = await GetServerInfo();
        if (info == null) return;

        Description = info.Desc;

        Links.Clear();
        if (info.Links is null) return;
        foreach (var link in info.Links) Links.Add(link);
    }

    public void Dispose()
    {
        _logger.Dispose();
    }
}

public sealed class InstanceKeyPool
{
    private int _nextId = 1;

    public InstanceKey Take()
    {
        return new InstanceKey(_nextId++);
    }

    public void Free(InstanceKey id)
    {
        // TODO: make some free logic later
    }
}

public record struct InstanceKey(int Id):
    IEquatable<int>,
    IComparable<InstanceKey>
{
    public static implicit operator InstanceKey(int id) => new InstanceKey(id);
    public static implicit operator int(InstanceKey id) => id.Id;
    public bool Equals(int other) => Id == other;
    public int CompareTo(InstanceKey other) => Id.CompareTo(other.Id);
};

public sealed class LinkGoCommand : ICommand
{
    public LinkGoCommand()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        if (parameter is not string str) return;
        Helper.SafeOpenBrowser(str);
    }

    public event EventHandler? CanExecuteChanged;
}