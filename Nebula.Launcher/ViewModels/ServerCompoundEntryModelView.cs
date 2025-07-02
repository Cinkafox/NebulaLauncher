using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Models;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;
using BindingFlags = System.Reflection.BindingFlags;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerCompoundEntryView), false)]
[ConstructGenerator]
public sealed partial class ServerCompoundEntryViewModel : 
    ViewModelBase, IFavoriteEntryModelView, IFilterConsumer, IListEntryModelView, IEntryNameHolder
{
    [ObservableProperty] private ServerEntryModelView? _currentEntry;
    [ObservableProperty] private Control? _entryControl;
    [ObservableProperty] private string _message = "Loading server entry...";
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _loading = true;
    
    private string? _name;
    private RobustUrl? _url;
    private ServerFilter? _currentFilter;

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

    public ServerCompoundEntryViewModel LoadServerEntry(RobustUrl url,string? name, CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            _url = url;
            try
            {
                Message = "Loading server entry...";
                var status = await RestService.GetAsync<ServerStatus>(_url.StatusUri, cancellationToken);
                
                CurrentEntry = ServiceProvider.GetService<ServerEntryModelView>()!.WithData(_url,name, status);
                CurrentEntry.IsFavorite = IsFavorite;
                CurrentEntry.Loading = false;
                CurrentEntry.ProcessFilter(_currentFilter);
                Loading = false;
            }
            catch (Exception e)
            {
                Message = e.Message;
            }
        }, cancellationToken);

        return this;
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
}