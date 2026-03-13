using System;
using System.Collections.ObjectModel;
using Avalonia.Collections;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.ServerListProviders;

public sealed class TestServerList : IServerListProvider
{
    public  void LoadServerList(
        AvaloniaList<IListEntryModelView> servers, 
        AvaloniaList<Exception> exceptions)
    {
        
        servers.Add(new ServerEntryViewModel());
        servers.Add(new ServerEntryViewModel());
        
        exceptions.Add(new Exception("Oh no!"));
    }
}