using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;

namespace Nebula.Launcher.ServerListProviders;

[ServiceRegister(null, false), ConstructGenerator]
public sealed partial class HubServerListProvider : IServerListProvider
{
    [GenerateProperty] private RestService RestService { get; }
    [GenerateProperty] private ServerViewContainer ServerViewContainer { get; }
    
    public string HubUrl { get; set; }
    
    public bool IsLoaded { get; private set; }
    public Action? OnLoaded { get; set; }

    private CancellationTokenSource? _cts;
    private readonly List<IListEntryModelView> _servers = [];
    private readonly List<Exception> _errors = [];

    public HubServerListProvider With(string hubUrl)
    {
        HubUrl = hubUrl;
        return this;
    }
    
    public IEnumerable<IListEntryModelView> GetServers()
    {
        return _servers;
    }

    public IEnumerable<Exception> GetErrors()
    {
        return _errors;
    }

    public async void LoadServerList()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts = null;
        }
        
        _servers.Clear();
        _errors.Clear();
        IsLoaded = false;
        _cts = new CancellationTokenSource();

        try
        {
            var servers =
                await RestService.GetAsync<List<ServerHubInfo>>(new Uri(HubUrl), _cts.Token);
        
            servers.Sort(new ServerComparer());
        
            if(_cts.Token.IsCancellationRequested) return;
        
            _servers.AddRange(
                servers.Select(h=> 
                    ServerViewContainer.Get(h.Address.ToRobustUrl(), h.StatusData)
                )
            );
        }
        catch (Exception e)
        {
            _errors.Add(new Exception($"Some error while loading server list from {HubUrl}. See inner exception", e));
        }
        
        IsLoaded = true;
        OnLoaded?.Invoke();
    }
    
    private void Initialise(){}
    private void InitialiseInDesignMode(){}
}