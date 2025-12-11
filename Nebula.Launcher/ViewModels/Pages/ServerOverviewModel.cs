using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Controls;
using Nebula.Launcher.Models;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(ServerOverviewView))]
[ConstructGenerator]
public partial class ServerOverviewModel : ViewModelBase
{
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isFilterVisible;
    
    public readonly ServerFilter CurrentFilter = new();
    [GenerateProperty] private IServiceProvider ServiceProvider { get; }
    [GenerateProperty] private ConfigurationService ConfigurationService { get; }
    [GenerateProperty] private FavoriteServerListProvider FavoriteServerListProvider { get; }
    public ObservableCollection<ServerListTabTemplate> Items { get; private set; }
    [ObservableProperty] private ServerListTabTemplate _selectedItem;
    [GenerateProperty, DesignConstruct] private ServerViewContainer ServerViewContainer { get; } 
    [GenerateProperty, DesignConstruct] public ServerListViewModel CurrentServerList { get; }
    

    //Design think
    protected override void InitialiseInDesignMode()
    {
        Items = new ObservableCollection<ServerListTabTemplate>([
            new ServerListTabTemplate(new TestServerList(), "Test think"),
            new ServerListTabTemplate(new TestServerList(), "Test think2")
        ]);
        SelectedItem = Items[0];
    }

    //real think
    protected override void Initialise()
    {
        ConfigurationService.SubscribeVarChanged(LauncherConVar.Hub, OnHubListChanged, true);
    }

    private void OnHubListChanged(ServerHubRecord[]? value)
    {
        var tempItems = new List<ServerListTabTemplate>();
        
        foreach (var record in value ?? [])
        {
            tempItems.Add(new ServerListTabTemplate(ServiceProvider.GetService<HubServerListProvider>()!.With(record.MainUrl), record.Name));
        }
        
        tempItems.Add(new ServerListTabTemplate(FavoriteServerListProvider, LocalizationService.GetString("tab-favorite")));
        
        Items = new ObservableCollection<ServerListTabTemplate>(tempItems);
        
        SelectedItem = Items[0];
    }

    partial void OnSearchTextChanged(string value)
    {
        CurrentFilter.SearchText = value;
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        foreach (var entry in ServerViewContainer.Items)
        {
            if(entry is IFilterConsumer filterConsumer)
                filterConsumer.ProcessFilter(CurrentFilter);
        }
    }
    
    public void OnFilterChanged(FilterBoxChangedEventArgs args)
    {
        if (args.Checked)
            CurrentFilter.Tags.Add(args.Tag);
        else
            CurrentFilter.Tags.Remove(args.Tag);
        ApplyFilter();
    }
    
    public void FilterRequired()
    {
        IsFilterVisible = !IsFilterVisible;
    }

    public void UpdateRequired()
    {
        ServerViewContainer.Clear();
        CurrentServerList.RefreshFromProvider();
        CurrentServerList.ApplyFilter(CurrentFilter);
    }

    partial void OnSelectedItemChanged(ServerListTabTemplate value)
    {
        CurrentServerList.Provider = value.ServerListProvider;
        ApplyFilter();
    }
    
}

[ServiceRegister]
public sealed class ServerViewContainer
{
    private readonly ViewHelperService _viewHelperService;
    private readonly List<string> _favorites = [];
    private readonly Dictionary<string, string> _customNames = [];

    public ServerViewContainer()
    {
        _viewHelperService = new ViewHelperService();
    }

    [UsedImplicitly]
    public ServerViewContainer(ViewHelperService viewHelperService, ConfigurationService configurationService)
    {
        _viewHelperService = viewHelperService;
        configurationService.SubscribeVarChanged(LauncherConVar.Favorites, OnFavoritesChange, true);
        configurationService.SubscribeVarChanged(LauncherConVar.ServerCustomNames, OnCustomNamesChanged, true);
    }

    private void OnCustomNamesChanged(Dictionary<string,string>? value)
    {
        var oldNames = 
            _customNames.ToDictionary(k => k.Key, v => v.Value); //Clone think
        
        _customNames.Clear();
        
        if(value == null)
        {
            foreach (var (ip,_) in oldNames)
            {
                if(!_entries.TryGetValue(ip, out var listEntry) || listEntry is not IEntryNameHolder entryNameHolder) 
                    continue;

                entryNameHolder.Name = null;
            }
            
            return;
        }

        foreach (var (oldIp, oldName) in oldNames)
        {
            if(value.TryGetValue(oldIp, out var newName))
            {
                if (oldName == newName)
                    value.Remove(newName);
                
                continue;
            }
            
            if(!_entries.TryGetValue(oldIp, out var listEntry) || 
               listEntry is not IEntryNameHolder entryNameHolder) 
                continue;
            
            entryNameHolder.Name = null;
        }
        
        foreach (var (ip, name) in value)
        {
            _customNames.Add(ip, name);
            if(!_entries.TryGetValue(ip, out var listEntry) || listEntry is not IEntryNameHolder entryNameHolder) 
                continue;
            
            entryNameHolder.Name = name;
        }
    }

    private void OnFavoritesChange(string[]? value)
    {
        _favorites.Clear();
        if(value == null) return;
        
        foreach (var favorite in value)
        {
            _favorites.Add(favorite);
            if (_entries.TryGetValue(favorite, out var entry) && entry is IFavoriteEntryModelView favoriteView)
            {
                favoriteView.IsFavorite = true;
            }
        }
    }

    private readonly Dictionary<string, IListEntryModelView> _entries = new();
    
    public ICollection<IListEntryModelView> Items => _entries.Values;

    public void Clear()
    {
        foreach (var (_, value) in _entries)
        {
            value.Dispose();
        }
        _entries.Clear();
    }

    public IListEntryModelView Get(RobustUrl url, ServerStatus? serverStatus = null)
    {
        IListEntryModelView? entry;
        
        lock (_entries)
        {
            _customNames.TryGetValue(url.ToString(), out var customName);
            
            if (_entries.TryGetValue(url.ToString(), out entry))
            {
                return entry;
            }

            if (serverStatus is not null)
                entry = _viewHelperService.GetViewModel<ServerEntryModelView>().WithData(url, customName, serverStatus);
            else
                entry = _viewHelperService.GetViewModel<ServerCompoundEntryViewModel>().LoadServerEntry(url, customName, CancellationToken.None);
            
            if(_favorites.Contains(url.ToString()) && 
               entry is IFavoriteEntryModelView favoriteEntryModelView) 
                favoriteEntryModelView.IsFavorite = true;
            
            _entries.Add(url.ToString(), entry);
        }
        
        return entry;
    }
}

public interface IListEntryModelView : IDisposable
{
    
}

public interface IFavoriteEntryModelView
{
    public bool IsFavorite { get; set; }
}

public interface IEntryNameHolder
{
    public string? Name { get; set; }
}

public class ServerComparer : IComparer<ServerHubInfo>, IComparer<ServerStatus>, IComparer<(RobustUrl,ServerStatus)>
{
    public int Compare(ServerHubInfo? x, ServerHubInfo? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (ReferenceEquals(null, y))
            return 1;
        if (ReferenceEquals(null, x))
            return -1;

        return Compare(x.StatusData, y.StatusData);
    }

    public int Compare(ServerStatus? x, ServerStatus? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (ReferenceEquals(null, y))
            return 1;
        if (ReferenceEquals(null, x))
            return -1;

        return y.Players.CompareTo(x.Players);
    }

    public int Compare((RobustUrl, ServerStatus) x, (RobustUrl, ServerStatus) y)
    {
        return Compare(x.Item2, y.Item2);
    }
}

public sealed class ServerFilter
{
    public string SearchText { get; set; } = "";
    public HashSet<string> Tags { get; } = new();
    public bool IsMatchByName(string name)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        return name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsMatchByTags(IEnumerable<string> itemTags)
    {
        if (Tags.Count == 0)
            return true;
        
        var itemTagSet = new HashSet<string>(itemTags);
        return Tags.All(tag => itemTagSet.Contains(tag));
    }

    public bool IsMatch(string name, IEnumerable<string> itemTags)
    {
        return IsMatchByName(name) && IsMatchByTags(itemTags);
    }
}