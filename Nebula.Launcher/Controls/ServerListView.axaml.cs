using Avalonia.Controls;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.Controls;

public partial class ServerListView : UserControl
{
    private IServerListProvider _provider = default!;
    private ServerFilter? _currentFilter;
    
    public bool IsLoading { get; private set; }
    
    public ServerListView()
    {
        InitializeComponent();
    }

    public static ServerListView TakeFrom(IServerListProvider provider)
    {
        var serverListView = new ServerListView();
        if (provider is IServerListDirtyInvoker invoker)
        {
            invoker.Dirty += serverListView.OnDirty;
        }
        serverListView._provider = provider;
        serverListView.RefreshFromProvider();
        return serverListView;
    }

    public void RefreshFromProvider()
    {
        if (IsLoading) 
            return;
        
        Clear();
        StartLoading();
        
        _provider.LoadServerList();
        
        if (_provider.IsLoaded) PasteServersFromList();
        else _provider.OnLoaded += RefreshRequired;
    }

    public void RequireStatusUpdate()
    {
        foreach (var rawView in ServerList.Items)
        {
            if (rawView is ServerEntryModelView serverEntryModelView)
            {
                serverEntryModelView.UpdateStatusIfNecessary();
            }
        }
    }

    public void ApplyFilter(ServerFilter? filter)
    {
        _currentFilter = filter;
        
        if(IsLoading) 
            return;
        
        foreach (IFilterConsumer? serverView in ServerList.Items)
        {
            serverView?.ProcessFilter(filter);
        }
    }

    private void OnDirty()
    {
        RefreshFromProvider();
    }
    
    private void Clear()
    {
        ErrorList.Items.Clear();
        ServerList.Items.Clear();
    }
    
    private void PasteServersFromList()
    {
        foreach (var serverEntry in _provider.GetServers())
        {
            ServerList.Items.Add(serverEntry);
            serverEntry.ProcessFilter(_currentFilter);
        }
        
        foreach (var error in _provider.GetErrors())
        {
            ErrorList.Items.Add(error);
        }
        
        EndLoading();
    }
    
    private void RefreshRequired()
    {
        PasteServersFromList();
        _provider.OnLoaded -= RefreshRequired;
    }

    private void StartLoading()
    {
        Clear();
        IsLoading = true;
        LoadingLabel.IsVisible = true;
    }

    private void EndLoading()
    {
        IsLoading = false;
        LoadingLabel.IsVisible = false;
    }
}