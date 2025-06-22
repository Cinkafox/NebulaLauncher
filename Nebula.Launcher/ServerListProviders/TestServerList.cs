using System;
using System.Collections.Generic;
using Nebula.Launcher.Controls;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.ServerListProviders;

public sealed class TestServerList : IServerListProvider
{
    public bool IsLoaded => true;
    public Action? OnLoaded { get; set; }
    public IEnumerable<IListEntryModelView> GetServers()
    {
        return [new ServerEntryModelView(),new ServerEntryModelView()];
    }

    public IEnumerable<Exception> GetErrors()
    {
        return [new Exception("On no!")];
    }

    public void LoadServerList()
    {
        
    }
}