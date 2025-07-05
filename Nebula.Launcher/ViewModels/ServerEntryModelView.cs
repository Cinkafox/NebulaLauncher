using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
public partial class ServerEntryModelView : ViewModelBase, IFilterConsumer, IListEntryModelView, IFavoriteEntryModelView, IEntryNameHolder
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
    private ContentLogConsumer _currentContentLogConsumer;
    private ProcessRunHandler<GameProcessStartInfoProvider>? _currentInstance;

    public LogPopupModelView CurrLog;
    public RobustUrl Address { get; private set; }
    [GenerateProperty] private AuthService AuthService { get; }
    [GenerateProperty] private CancellationService CancellationService { get; } = default!;
    [GenerateProperty] private DebugService DebugService { get; } = default!;
    [GenerateProperty] private PopupMessageService PopupMessageService { get; } = default!;
    [GenerateProperty] private ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] private RestService RestService { get; } = default!;
    [GenerateProperty] private MainViewModel MainViewModel { get; } = default!;
    [GenerateProperty] private FavoriteServerListProvider FavoriteServerListProvider { get; } = default!;
    [GenerateProperty] private GameRunnerPreparer GameRunnerPreparer { get; } = default!;

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
        Address = "ss14://localhost".ToRobustUrl();
    }

    protected override void Initialise()
    {
        _logger = DebugService.GetLogger(this);
        CurrLog = ViewHelperService.GetViewModel<LogPopupModelView>();
        _currentContentLogConsumer = new(CurrLog, PopupMessageService);
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
        CurrLog.Clear();
        Task.Run(async ()=> await RunInstanceAsync());
    }

    public void RunInstanceIgnoreAuth()
    {
        CurrLog.Clear();
        Task.Run(async ()=> await RunInstanceAsync(true));
    }

    private async Task RunInstanceAsync(bool ignoreLoginCredentials = false)
    {
        if (!ignoreLoginCredentials && AuthService.SelectedAuth is null)
        {
            var warningContext = ViewHelperService.GetViewModel<IsLoginCredentialsNullPopupViewModel>()
                .WithServerEntry(this);
            
            PopupMessageService.Popup(warningContext);
            return;
        }
        
        using var loadingContext = ViewHelperService.GetViewModel<LoadingContextViewModel>();
        loadingContext.LoadingName = "Loading instance...";
        ((ILoadingHandler)loadingContext).AppendJob();

        PopupMessageService.Popup(loadingContext);
        _currentInstance = 
            await GameRunnerPreparer.GetGameProcessStartInfoProvider(Address, loadingContext, CancellationService.Token);
            
        _currentInstance.RegisterLogger(_currentContentLogConsumer);
        _currentInstance.RegisterLogger(new DebugLoggerBridge(DebugService.GetLogger($"PROCESS_{Random.Shared.Next(65535)}")));
        _currentInstance.OnProcessExited += OnProcessExited;
        RunVisible = false;
        _currentInstance.Start();
    }

    private void OnProcessExited(ProcessRunHandler<GameProcessStartInfoProvider> obj)
    {
        RunVisible = true;
        if (_currentInstance == null) return;
        
        _currentInstance.OnProcessExited -= OnProcessExited;
        _currentInstance.Dispose();
        _currentInstance = null;
    }

    public void StopInstance()
    {
        _currentInstance?.Stop();   
    }
    
    public void ReadLog()
    {
        PopupMessageService.Popup(CurrLog);
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
}

public class LinkGoCommand : ICommand
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