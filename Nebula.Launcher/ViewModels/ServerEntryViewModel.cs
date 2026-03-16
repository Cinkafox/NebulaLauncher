using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Models;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerEntryView), false)]
public sealed partial class ServerEntryViewModel(
    RestService restService, 
    CancellationService cancellationService, 
    GameRunnerService gameRunnerService
    ) : 
    ViewModelBase, 
    IFilterConsumer,
    IListEntryModelView, 
    IFavoriteEntryModelView,
    IEntryNameHolder, 
    IRunningSignalConsumer
{
    [ObservableProperty] private string _description = "Fetching info...";
    [ObservableProperty] private bool _expandInfo;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _runVisible = true;
    [ObservableProperty] private string _realName = string.Empty;
    
    public string? Name
    {
        get => RealName;
        set => RealName = value ?? Status.Name;
    }
    
    private ServerInfo? _serverInfo;

    public RobustUrl Address { get; private set; }

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
        if (_serverInfo != null) 
            return _serverInfo;
        
        try
        {
            _serverInfo = await restService.GetAsync<ServerInfo>(Address.InfoUri, cancellationService.Token);
        }
        catch (Exception e)
        {
            Description = e.Message;
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
    
    public ServerEntryViewModel WithData(RobustUrl url, string? name, ServerStatus serverStatus)
    {
        Address = url;
        SetStatus(serverStatus);
        Name = name;
        return this;
    }

    public void OpenContentViewer()
    {
        gameRunnerService.OpenContentViewer(Address);
    }

    public void ToggleFavorites()
    {
        IsFavorite = !IsFavorite;
        if(IsFavorite)
            gameRunnerService.AddFavorite(Address);
        else
            gameRunnerService.RemoveFavorite(Address);
    }

    public void RunInstance()
    { 
        Task.Run(async ()=> await gameRunnerService.RunInstanceAsync(this, cancellationService.Token));
    }

    public void RunInstanceIgnoreAuth()
    {
        Task.Run(async ()=> await gameRunnerService.RunInstanceAsync(this, cancellationService.Token, true));
    }

    public void StopInstance()
    {
        gameRunnerService.StopInstance(Address);
    }
    
    public void ReadLog()
    {
        gameRunnerService.ReadInstanceLog(Address);
    }

    public void EditName()
    {
        gameRunnerService.EditName(Address, Name);
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
    

    public void ProcessRunningSignal(bool isRunning)
    {
        RunVisible = !isRunning;
    }
}

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