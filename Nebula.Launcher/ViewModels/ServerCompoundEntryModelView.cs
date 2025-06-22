using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using BindingFlags = System.Reflection.BindingFlags;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerCompoundEntryView), false)]
[ConstructGenerator]
public sealed partial class ServerCompoundEntryViewModel : 
    ViewModelBase, IFavoriteEntryModelView, IFilterConsumer, IListEntryModelView
{
    [ObservableProperty] private ServerEntryModelView _currentEntry;
    [ObservableProperty] private Control? _entryControl;
    [ObservableProperty] private string _name = "Loading...";
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _loading = true;
    
    [GenerateProperty] private RestService RestService { get; }
    [GenerateProperty] private IServiceProvider ServiceProvider{ get; }
    [GenerateProperty] private FavoriteServerListProvider FavoriteServerListProvider { get; }
    
    private RobustUrl? _url;
    

    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }

    public ServerCompoundEntryViewModel LoadServerEntry(RobustUrl url, CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            try
            {
                _url = url;
                Name = $"Loading {url}...";
                var status = await RestService.GetAsync<ServerStatus>(url.StatusUri, cancellationToken);
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CurrentEntry = ServiceProvider.GetService<ServerEntryModelView>()!.WithData(url, status);
                    CurrentEntry.IsFavorite = IsFavorite;
                    CurrentEntry.Loading = false;
                    Loading = false;
                });
            }
            catch (Exception e)
            {
                var error = new Exception("Unable to load server entry", e);
                Name = e.Message;
            }
        }, cancellationToken);

        return this;
    }

    public void ToggleFavorites()
    {
        if (_url == null) 
            return;
        IsFavorite = !IsFavorite;
        if(IsFavorite)
            FavoriteServerListProvider.AddFavorite(_url);
        else
            FavoriteServerListProvider.RemoveFavorite(_url);
    }
    
    
    public void ProcessFilter(ServerFilter? serverFilter)
    {
        if(CurrentEntry is IFilterConsumer filterConsumer) 
            filterConsumer.ProcessFilter(serverFilter);
    }
}