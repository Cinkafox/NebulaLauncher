using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
public sealed partial class FavoriteServerListProvider : BaseServerListProvider
{
    [GenerateProperty] private ConfigurationService ConfigurationService { get; }
    [GenerateProperty] private IServiceProvider ServiceProvider { get; }
    [GenerateProperty] private ServerViewContainer ServerViewContainer { get; }
    //[GenerateProperty] private ServerOverviewModel ServerOverviewModel { get; }
    
    private string[] _rawServerLists = [];
    
    public override void LoadServerList(
        ObservableCollection<IListEntryModelView> servers, 
        ObservableCollection<Exception> exceptions)
    {
        base.LoadServerList(servers, exceptions);
        
        foreach (var server in _rawServerLists)
        {
            var container = ServerViewContainer.Get(server.ToRobustUrl());
            servers.Add(container);
        }
        
        servers.Add(new AddFavoriteButton(ServiceProvider));
    }

    public override void Dispose()
    {
        
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
        return _rawServerLists.ToList();
    }

    private void Initialise()
    {
        ConfigurationService.SubscribeVarChanged(LauncherConVar.Favorites, OnVarChanged, true);
    }

    private void OnVarChanged(string[]? value)
    {
        if (value == null)
        {
            _rawServerLists = [];
            return;
        }

        _rawServerLists = value;
        //ServerOverviewModel.UpdateRequired();
    }

    private void InitialiseInDesignMode(){}
}

public sealed class AddFavoriteButton: Border, IListEntryModelView{

    private readonly Button _addFavoriteButton = new();
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
    public void Dispose(){}
}