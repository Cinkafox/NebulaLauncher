using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Models;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.ContentBrowser;

[ViewModelRegister(typeof(FileContentEntryView), false), ConstructGenerator]
public sealed partial class ServerListContentEntry : BaseFolderContentEntry
{
    [GenerateProperty, DesignConstruct] public override ViewHelperService ViewHelperService { get; } = default!;
    [GenerateProperty] public ConfigurationService ConfigurationService { get; } = default!;
    [GenerateProperty] public IServiceProvider ServiceProvider { get; } = default!;
    [GenerateProperty] public RestService RestService { get; } = default!;
    
    public void InitHubList(IContentHolder holder)
    {
        base.Init(holder);

        var servers = ConfigurationService.GetConfigValue(LauncherConVar.Hub)!;

        foreach (var server in servers)
        {
            var serverFolder = ServiceProvider.GetService<ServerListContentEntry>()!;
            var serverLazy = new LazyContentEntry(Holder, server.Name , serverFolder, () => serverFolder.InitServerList(Holder, server));
            AddChild(serverLazy);
        }
    }

    public async void InitServerList(IContentHolder holder, ServerHubRecord hubRecord)
    {
        base.Init(holder, hubRecord.Name);

        IsLoading = true;
        var servers =
            await RestService.GetAsync<List<ServerHubInfo>>(new Uri(hubRecord.MainUrl), CancellationToken.None);

        foreach (var server in servers)
        {
            var serverFolder = ServiceProvider.GetService<ServerFolderContentEntry>()!;
            var serverLazy = new LazyContentEntry(Holder, server.StatusData.Name , serverFolder, () => serverFolder.Init(Holder, server.Address.ToRobustUrl()));
            AddChild(serverLazy);
        }

        IsLoading = true;
    }

    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
}