using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Nebula.Launcher.Models;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;

namespace Nebula.Launcher.ServerListProviders;

[ServiceRegister(null, false), ConstructGenerator]
public sealed partial class HubServerListProvider : BaseServerListProvider
{
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    
    [GenerateProperty] private RestService RestService { get; }
    [GenerateProperty] private ServerViewContainer ServerViewContainer { get; }

    private string _hubUrl;

    public HubServerListProvider With(string hubUrl)
    {
        _hubUrl = hubUrl;
        return this;
    }

    public override void LoadServerList(
        ObservableCollection<IListEntryModelView> servers, 
        ObservableCollection<Exception> exceptions)
    {
        base.LoadServerList(servers, exceptions);
        
        servers.Add(new LoadingServerEntry());
        Task.Run(() => LoadServerListAsync(servers, exceptions));
    }

    private void SyncServers(List<IListEntryModelView> servers, 
        ObservableCollection<IListEntryModelView> collection)
    {
        collection.Clear();
        foreach (var server in servers)
        {
            collection.Add(server);
        }
    }

    private async Task LoadServerListAsync(
        ObservableCollection<IListEntryModelView> servers, 
        ObservableCollection<Exception> exceptions)
    {
        CancellationTokenSource localCts;
        
        var serverList = new List<IListEntryModelView>();

        await _loadLock.WaitAsync();
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();

            _cts = new CancellationTokenSource();
            localCts = _cts;
        }
        finally
        {
            _loadLock.Release();
        }

        try
        {
            var serversRaw = await RestService.GetAsync<List<ServerHubInfo>>(
                new Uri(_hubUrl),
                localCts.Token
            );

            serversRaw.Sort(new ServerComparer());

            localCts.Token.ThrowIfCancellationRequested();
            
            foreach (var info in serversRaw)
            {
                var viewContainer =
                    ServerViewContainer.Get(info.Address.ToRobustUrl(), info.StatusData);

                serverList.Add(viewContainer);
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                SyncServers(serverList, servers);
            });
        }
        catch (OperationCanceledException)
        {
            
        }
        catch (Exception e)
        {
            exceptions.Add(
                new Exception(
                    $"Some error while loading server list from {_hubUrl}. See inner exception",
                    e
                )
            );
        }
    }
    
    private void Initialise(){}
    private void InitialiseInDesignMode(){}

    public override void Dispose()
    {
        _cts?.Dispose();
    }
}

public sealed class LoadingServerEntry : Label, IListEntryModelView
{
    public LoadingServerEntry()
    {
        Content = LocalizationService.GetString("server-list-loading");
    }
    public void Dispose()
    {}
}