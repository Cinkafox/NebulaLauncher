using System;
using System.Collections.Generic;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.ServerListProviders;

public interface IServerListProvider : IDisposable
{
    public bool IsLoaded { get; }
    public Action? OnLoaded { get; set; }
    public Action? OnDisposed { get; set; }
   
    public IEnumerable<IListEntryModelView> GetServers();
    public IEnumerable<Exception> GetErrors();
   
    public void LoadServerList();
}

public interface IServerListDirtyInvoker
{
    public Action? Dirty { get; set; }
}