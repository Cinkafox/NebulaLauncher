using System;
using System.Collections.ObjectModel;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.ServerListProviders;

public sealed class TestServerList : BaseServerListProvider
{
    public override void LoadServerList(
         ObservableCollection<IListEntryModelView> servers, 
         ObservableCollection<Exception> exceptions)
    {
        base.LoadServerList(servers, exceptions);
        
        servers.Add(new ServerEntryModelView());
        servers.Add(new ServerEntryModelView());
        
        exceptions.Add(new Exception("Oh no!"));
    }

    public override void Dispose()
    {
        
    }
}