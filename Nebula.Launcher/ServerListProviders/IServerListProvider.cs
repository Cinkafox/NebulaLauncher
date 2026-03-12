using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.ServerListProviders;

public abstract class BaseServerListProvider : ObservableObject, IDisposable
{
    public virtual void LoadServerList(
        ObservableCollection<IListEntryModelView> servers, 
        ObservableCollection<Exception> exceptions)
    {
        servers.Clear();
        exceptions.Clear();
    }

    public abstract void Dispose();
}