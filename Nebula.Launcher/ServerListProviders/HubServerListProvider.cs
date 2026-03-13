using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ServerListProviders;

[ServiceRegister(null, false), ConstructGenerator]
public sealed partial class HubServerListProvider : IServerListProvider, IDisposable
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

    public void LoadServerList(
        AvaloniaList<IListEntryModelView> servers, 
        AvaloniaList<Exception> exceptions)
    {
        servers.Add(new LoadingServerEntry());
        Task.Run(() => LoadServerListAsync(servers, exceptions));
    }

    private void SyncServers(List<IListEntryModelView> servers, 
        AvaloniaList<IListEntryModelView> collection)
    {
        collection.Clear();
        collection.AddRange(servers);
    }

    private async Task LoadServerListAsync(
        AvaloniaList<IListEntryModelView> servers, 
        AvaloniaList<Exception> exceptions)
    {
        CancellationTokenSource localCts;
        
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

            Dispatcher.UIThread.Invoke(() =>
            {
                var serverList = new List<IListEntryModelView>();
                
                foreach (var info in serversRaw)
                {
                    serverList.Add(ServerViewContainer.Get(info.Address, info.StatusData));
                }
                SyncServers(serverList, servers);
            });
        }
        catch (OperationCanceledException)
        {
            // Ignore cancel think
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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

    public void Dispose()
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