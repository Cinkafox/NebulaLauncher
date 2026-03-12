using System;
using System.Collections.ObjectModel;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels;

[ViewModelRegister(typeof(ServerListView), false)]
public partial class ServerListViewModel : ViewModelBase
{
    public ObservableCollection<IListEntryModelView> ServerList { get; private set; } = new();
    public ObservableCollection<Exception> ErrorList { get; private set; } = new();

    private BaseServerListProvider? _provider;

    public void ClearProvider()
    {
        ServerList.Clear();
        ErrorList.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void SetProvider(BaseServerListProvider provider)
    {
        _provider = provider;
        
        OnPropertyChanged(nameof(ServerList));
        OnPropertyChanged(nameof(ErrorList));
        
        RefreshFromProvider();
    }
    
    public void RefreshFromProvider()
    {
        _provider?.LoadServerList(ServerList, ErrorList);
    }
    
    protected override void InitialiseInDesignMode()
    {
        SetProvider(new TestServerList());
    }

    protected override void Initialise()
    {
    }
}