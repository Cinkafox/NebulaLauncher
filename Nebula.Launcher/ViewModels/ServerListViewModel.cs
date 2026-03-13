using System;
using System.Collections.ObjectModel;
using Avalonia.Collections;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerListView), false)]
public class ServerListViewModel : ViewModelBase
{
    public AvaloniaList<IListEntryModelView> ServerList { get; private set; } = new();
    public AvaloniaList<Exception> ErrorList { get; private set; } = new();
    public IServerListProvider? Provider { get; private set; }

    public void ClearProvider()
    {
        foreach (var serverEntry in ServerList)
        {
            if (serverEntry is IDisposable disposable)
            {
                disposable.Dispose();  
            }
        }
        
        ServerList.Clear();
        ErrorList.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void SetProvider(IServerListProvider provider)
    {
        Provider = provider;
        
        OnPropertyChanged(nameof(ServerList));
        OnPropertyChanged(nameof(ErrorList));
        
        RefreshFromProvider();
    }
    
    public void RefreshFromProvider()
    {
        Provider?.LoadServerList(ServerList, ErrorList);
    }
    
    protected override void InitialiseInDesignMode()
    {
        SetProvider(new TestServerList());
    }

    protected override void Initialise()
    {
    }
}