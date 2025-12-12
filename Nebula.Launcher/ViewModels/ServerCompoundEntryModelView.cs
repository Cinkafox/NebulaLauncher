using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Models;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerCompoundEntryView), false)]
[ConstructGenerator]
public sealed partial class ServerCompoundEntryViewModel : 
    ViewModelBase, IFavoriteEntryModelView, IFilterConsumer, IListEntryModelView, IEntryNameHolder
{
    private ServerEntryModelView? _currentEntry;
    [ObservableProperty] private string _message = "Loading server entry...";
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _loading = true;
    
    private string? _name;
    private RobustUrl? _url;
    private ServerFilter? _currentFilter;

    public ServerEntryModelView? CurrentEntry
    {
        get => _currentEntry;
        set
        {
            if (value == _currentEntry) return;
            
            _currentEntry = value;

            if (_currentEntry != null)
            {
                _currentEntry.IsFavorite = IsFavorite;
                _currentEntry.Name = Name;
                _currentEntry.ProcessFilter(_currentFilter);
            }
            
            Loading = _currentEntry == null;
            
            OnPropertyChanged();
        }
    }

    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
            
            if (CurrentEntry != null) 
                CurrentEntry.Name = value;
        }
    }
    
    [GenerateProperty] private RestService RestService { get; }
    [GenerateProperty] private IServiceProvider ServiceProvider{ get; }
    [GenerateProperty] private FavoriteServerListProvider FavoriteServerListProvider { get; }
    
    protected override void InitialiseInDesignMode()
    {
        Name = "TEST.TEST";
    }

    protected override void Initialise()
    {
    }

    public ServerCompoundEntryViewModel LoadWithEntry(ServerEntryModelView? entry)
    {
        CurrentEntry = entry;
        return this;
    }

    public ServerCompoundEntryViewModel LoadServerEntry(RobustUrl url, string? name, CancellationToken cancellationToken)
    {
        _url = url;
        _name = name; 
        Task.Run(LoadServer, cancellationToken);
        return this;
    }

    private async Task LoadServer()
    {
        if (_url is null)
        {
            Message = "Url is not set";
            return;
        }
        
        try
        {
            Message = "Loading server entry...";
            var status = await RestService.GetAsync<ServerStatus>(_url.StatusUri, CancellationToken.None);
                
            CurrentEntry = ServiceProvider.GetService<ServerEntryModelView>()!.WithData(_url, null, status);
            
            Loading = false;
        }
        catch (Exception e)
        {
            Message = "Error while fetching data from " + _url + " : " + e.Message;
        }
    }

    public void ToggleFavorites()
    {
        if (CurrentEntry is null && _url is not null)
        {
            IsFavorite = !IsFavorite;
            if(IsFavorite) FavoriteServerListProvider.AddFavorite(_url);
            else FavoriteServerListProvider.RemoveFavorite(_url);
        }
     
        CurrentEntry?.ToggleFavorites();
    }
    
    
    public void ProcessFilter(ServerFilter? serverFilter)
    {
        _currentFilter = serverFilter;
        if(CurrentEntry is IFilterConsumer filterConsumer) 
            filterConsumer.ProcessFilter(serverFilter);
    }

    public void Dispose()
    {
        CurrentEntry?.Dispose();
    }
}