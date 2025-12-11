using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;

namespace Nebula.Launcher.ServerListProviders;

[ServiceRegister, ConstructGenerator]
public sealed partial class FavoriteServerListProvider : IServerListProvider, IServerListDirtyInvoker
{
    [GenerateProperty] private ConfigurationService ConfigurationService { get; }
    [GenerateProperty] private IServiceProvider ServiceProvider { get; }
    [GenerateProperty] private ServerViewContainer ServerViewContainer { get; }

    private List<IListEntryModelView> _serverLists = [];
    private string[] rawServerLists = [];
    
    public bool IsLoaded { get; private set; }
    public Action? OnLoaded { get; set; }
    public Action? OnDisposed { get; set; }
    public Action? Dirty { get; set; }
    public IEnumerable<IListEntryModelView> GetServers()
    {
        return _serverLists;
    }

    public IEnumerable<Exception> GetErrors()
    {
        return [];
    }

    public void LoadServerList()
    {
        IsLoaded = false;
        _serverLists.Clear();
        var servers = GetFavoriteEntries();

        var serverEntries = servers.Select(s =>
            ServerViewContainer.Get(s.ToRobustUrl())
        );
        
        _serverLists.AddRange(serverEntries);
        
        _serverLists.Add(new AddFavoriteButton(ServiceProvider));
        
        IsLoaded = true;
        OnLoaded?.Invoke();
    }
    
    public void AddFavorite(ServerEntryModelView entryModelView)
    {
        AddFavorite(entryModelView.Address);
    }

    public void AddFavorite(RobustUrl robustUrl)
    {
        var servers = GetFavoriteEntries();
        servers.Add(robustUrl.ToString());
        ConfigurationService.SetConfigValue(LauncherConVar.Favorites, servers.ToArray());
        if(ServerViewContainer.Get(robustUrl) is IFavoriteEntryModelView favoriteView) favoriteView.IsFavorite = true;
    }

    public void RemoveFavorite(ServerEntryModelView entryModelView)
    {
        var servers = GetFavoriteEntries();
        servers.Remove(entryModelView.Address.ToString());
        ConfigurationService.SetConfigValue(LauncherConVar.Favorites, servers.ToArray());
    }
    
    public void RemoveFavorite(RobustUrl url)
    {
        var servers = GetFavoriteEntries();
        servers.Remove(url.ToString());
        ConfigurationService.SetConfigValue(LauncherConVar.Favorites, servers.ToArray());
    }

    private List<string> GetFavoriteEntries()
    {
        return rawServerLists.ToList();
    }

    private void Initialise()
    {
        ConfigurationService.SubscribeVarChanged(LauncherConVar.Favorites, OnVarChanged, true);
    }

    private void OnVarChanged(string[]? value)
    {
        if (value == null)
        {
            rawServerLists = [];
            Dirty?.Invoke();
            return;
        }

        rawServerLists = value;
        Dirty?.Invoke();
    }

    private void InitialiseInDesignMode(){}

    public void Dispose()
    {
        OnDisposed?.Invoke();
    }
}

public sealed class AddFavoriteButton: Border, IListEntryModelView{

    private Button _addFavoriteButton = new Button();
    public AddFavoriteButton(IServiceProvider serviceProvider)
    {
        Margin = new Thickness(5, 5, 5, 20);
        Background = new SolidColorBrush(Color.Parse("#222222"));
        CornerRadius = new CornerRadius(20f);
        _addFavoriteButton.HorizontalAlignment = HorizontalAlignment.Center;
        _addFavoriteButton.Click += (sender, args) =>
        {
            serviceProvider.GetService<PopupMessageService>()!.Popup(
                serviceProvider.GetService<AddFavoriteViewModel>()!);
        };
        _addFavoriteButton.Content = "Add Favorite";
        Child = _addFavoriteButton;
    }
    public bool IsFavorite { get; set; }

    public void Dispose()
    {
        
    }
}