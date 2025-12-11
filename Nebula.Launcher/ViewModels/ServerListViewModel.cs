using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Models;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerListView), false)]
public partial class ServerListViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isLoading;

    public ServerListViewModel()
    {
        if (Design.IsDesignMode)
        {
            Provider = new TestServerList();
        }
    }
    
    private IServerListProvider? _provider;
    
    public ObservableCollection<IListEntryModelView> ServerList { get; } = new();
    public ObservableCollection<Exception> ErrorList { get; } = new();

    public IServerListProvider Provider
    {
        get => _provider ?? throw new Exception();

        set
        {
            _provider = value;
            _provider.OnDisposed += OnProviderDisposed;
            if (_provider is IServerListDirtyInvoker invoker)
            {
                invoker.Dirty += OnDirty;
            }
            
            if(!_provider.IsLoaded)
                RefreshFromProvider();
            else
            {
                Clear();
                PasteServersFromList();
            }
        }
    }

    private void OnProviderDisposed()
    {
        Provider.OnLoaded -= RefreshRequired;
        Provider.OnDisposed -= OnProviderDisposed;
        if (Provider is IServerListDirtyInvoker invoker)
        {
            invoker.Dirty -= OnDirty;
        }
        
        _provider = null;
    }

    private ServerFilter? _currentFilter;

    public void RefreshFromProvider()
    {
        if (IsLoading) 
            return;
        
        Clear();
        StartLoading();
        
        Provider.LoadServerList();
        
        if (Provider.IsLoaded) PasteServersFromList();
        else Provider.OnLoaded += RefreshRequired;
    }

    public void ApplyFilter(ServerFilter? filter)
    {
        _currentFilter = filter;
        
        if(IsLoading) 
            return;
        
        foreach (var serverView in ServerList)
        {
            if(serverView is IFilterConsumer filterConsumer)
                filterConsumer.ProcessFilter(filter);
        }
    }

    private void OnDirty()
    {
        RefreshFromProvider();
    }
    
    private void Clear()
    {
        ErrorList.Clear();
        ServerList.Clear();
    }
    
    private void PasteServersFromList()
    {
        foreach (var serverEntry in Provider.GetServers())
        {
            ServerList.Add(serverEntry);
            if(serverEntry is IFilterConsumer serverFilter)
                serverFilter.ProcessFilter(_currentFilter);
        }
        
        foreach (var error in Provider.GetErrors())
        {
            ErrorList.Add(error);
        }
        
        EndLoading();
    }
    
    private void RefreshRequired()
    {
        PasteServersFromList();
        Provider.OnLoaded -= RefreshRequired;
    }

    private void StartLoading()
    {
        Clear();
        IsLoading = true;
    }

    private void EndLoading()
    {
        IsLoading = false;
    }

    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
}