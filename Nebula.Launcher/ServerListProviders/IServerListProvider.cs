using System;
using Avalonia.Collections;
using Nebula.Launcher.ViewModels.Pages;

namespace Nebula.Launcher.ServerListProviders;

public interface IServerListProvider
{
    public void LoadServerList(
        AvaloniaList<IListEntryModelView> servers,
        AvaloniaList<Exception> exceptions);
}