using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Controls;
using Nebula.Launcher.Models;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(ServerOverviewView))]
[ConstructGenerator]
public partial class ServerOverviewModel : ViewModelBase
{
    [ObservableProperty] private string _searchText = string.Empty;
    
    [ObservableProperty] private bool _isFilterVisible;

    [ObservableProperty] private ServerListView _currentServerList = new ServerListView();
    
    public readonly ServerFilter CurrentFilter = new ServerFilter();
    
    public Action? OnSearchChange;
    
    [GenerateProperty] private PopupMessageService PopupMessageService { get; }
    [GenerateProperty] private CancellationService CancellationService { get; }
    [GenerateProperty] private DebugService DebugService { get; }
    [GenerateProperty] private IServiceProvider ServiceProvider { get; }
    [GenerateProperty] private ConfigurationService ConfigurationService { get; }
    [GenerateProperty] private FavoriteServerListProvider FavoriteServerListProvider { get; }
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; }
    
    public ObservableCollection<ServerListTabTemplate> Items { get; private set; }
    [ObservableProperty] private ServerListTabTemplate _selectedItem;
    
    [GenerateProperty, DesignConstruct] private ServerViewContainer ServerViewContainer { get; set; } 
    
    private Dictionary<string, ServerListView> _viewCache = [];
    

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
        var tempItems = new List<ServerListTabTemplate>();
        foreach (var record in ConfigurationService.GetConfigValue(LauncherConVar.Hub) ?? [])
        {
            tempItems.Add(new ServerListTabTemplate(ServiceProvider.GetService<HubServerListProvider>()!.With(record.MainUrl), record.Name));
        }
        
        tempItems.Add(new ServerListTabTemplate(FavoriteServerListProvider, "Favorite"));
        
        Items = new ObservableCollection<ServerListTabTemplate>(tempItems);
        
        SelectedItem = Items[0];
        
        OnSearchChange += SearchChangeEvent;
    }

    private void SearchChangeEvent()
    {
        CurrentFilter.SearchText = SearchText;
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        foreach (var entry in ServerViewContainer.Items)
        {
            entry.ProcessFilter(CurrentFilter);
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
        CurrentServerList.RefreshFromProvider();
    }

    partial void OnSelectedItemChanged(ServerListTabTemplate value)
    {
        if (!_viewCache.TryGetValue(value.TabName, out var view))
        {
            view = ServerListView.TakeFrom(value.ServerListProvider);
            _viewCache[value.TabName] = view;
        }
        
        CurrentServerList = view;
    }
    
}

[ServiceRegister]
public class ServerViewContainer
{
    private readonly ViewHelperService _viewHelperService;

    public ServerViewContainer()
    {
        _viewHelperService = new ViewHelperService();
    }

    public ServerViewContainer(ViewHelperService viewHelperService)
    {
        _viewHelperService = viewHelperService;
    }
    
    private readonly Dictionary<string, ServerEntryModelView> _entries = new();
    
    public ICollection<ServerEntryModelView> Items => _entries.Values;

    public void Clear()
    {
        _entries.Clear();
    }

    public ServerEntryModelView Get(RobustUrl url, ServerStatus? serverStatus = null)
    {
        ServerEntryModelView? entry;
        
        lock (_entries)
        {
            if (_entries.TryGetValue(url.ToString(), out entry))
            {
                return entry;
            }

            entry = _viewHelperService.GetViewModel<ServerEntryModelView>().WithData(url, serverStatus);
            
            _entries.Add(url.ToString(), entry);
        }
        
        return entry;
    }
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