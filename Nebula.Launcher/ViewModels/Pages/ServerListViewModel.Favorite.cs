using System.Collections.ObjectModel;
using System.Linq;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;

namespace Nebula.Launcher.ViewModels.Pages;

public partial class ServerListViewModel
{
    [GenerateProperty] private ConfigurationService ConfigurationService { get; }
    [GenerateProperty] private RestService RestService { get; }
    
    public ObservableCollection<ServerEntryModelView> FavoriteServers { get; } = [];
    
    private void UpdateFavoriteEntries()
    {
        foreach(var fav in FavoriteServers.ToList()){
            FavoriteServers.Remove(fav);
        }
        
        var servers = ConfigurationService.GetConfigValue(LauncherConVar.Favorites);
        if (servers is null || servers.Length == 0)
        {
            return;
        }
        
        foreach (var server in servers)
        {
            var s =  ServerViewContainer.Get(server.ToRobustUrl());
            s.IsFavorite = true;
            FavoriteServers.Add(s);
        }
        
        ApplyFilter();
    }

    public void AddFavorite(ServerEntryModelView entryModelView)
    {
        entryModelView.IsFavorite = true;
        AddFavorite(entryModelView.Address);
    }

    public void AddFavorite(RobustUrl robustUrl)
    {
        var servers = (ConfigurationService.GetConfigValue(LauncherConVar.Favorites) ?? []).ToList();
        servers.Add(robustUrl.ToString());
        ConfigurationService.SetConfigValue(LauncherConVar.Favorites, servers.ToArray());
        UpdateFavoriteEntries();
    }

    public void RemoveFavorite(ServerEntryModelView entryModelView)
    {
        var servers = (ConfigurationService.GetConfigValue(LauncherConVar.Favorites) ?? []).ToList();
        servers.Remove(entryModelView.Address.ToString());
        ConfigurationService.SetConfigValue(LauncherConVar.Favorites, servers.ToArray());
        entryModelView.IsFavorite = false;
        UpdateFavoriteEntries();
    }
}