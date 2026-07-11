using System;
using System.Collections.Generic;
using Nebula.Launcher.Models;
using Nebula.Launcher.ProcessHelper;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Shared;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Services;

[ServiceRegister]
public sealed class InstanceRunningContainer(
    PopupMessageService popupMessageService,
    DebugService debugService
    )
{
    private readonly InstanceKeyPool _keyPool = new();
    private readonly Dictionary<InstanceKey, ProcessRunHandler> _processCache = [];
    private readonly Dictionary<InstanceKey, ContentLogConsumer> _contentLoggerCache = [];
    private readonly Dictionary<ProcessRunHandler, InstanceKey> _keyCache = [];
    private readonly Dictionary<InstanceKey, IInstanceKeyHolder> _holders = [];

    public void RegisterInstance(IInstanceKeyHolder holder, IProcessStartInfoProvider provider)
    {
        holder.InstanceKey = RegisterInstance(provider);
        _holders[holder.InstanceKey] = holder;
    }
    
    public InstanceKey RegisterInstance(IProcessStartInfoProvider provider)
    {
        var id = _keyPool.Take();
        
        var currentContentLogConsumer = new ContentLogConsumer(popupMessageService);
        var logBridge = new DebugLoggerBridge(debugService.GetLogger("PROCESS_"+id.Id));
        var logContainer = new ProcessLogConsumerCollection();
        logContainer.RegisterLogger(currentContentLogConsumer);
        logContainer.RegisterLogger(logBridge);
        
        var handler = new ProcessRunHandler(provider, logContainer);
        handler.OnProcessExited += OnProcessExited;
        
        _processCache[id] = handler;
        _contentLoggerCache[id] = currentContentLogConsumer;
        _keyCache[handler] = id;
        
        return id;
    }
    
    public void Popup(InstanceKey instanceKey)
    {
        if(!_contentLoggerCache.TryGetValue(instanceKey, out var handler)) 
            return;
        
        handler.Popup();
    }

    public bool Run(IInstanceKeyHolder instanceKeyHolder)
    {
        if(!Run(instanceKeyHolder.InstanceKey))
            return false;

        instanceKeyHolder.IsInstanceRunning = true;
        return true;
    }

    public bool Run(InstanceKey instanceKey)
    {
        if(!_processCache.TryGetValue(instanceKey, out var process)) 
            return false;
        
        process.Start();
        return true;
    }

    public void Stop(IInstanceKeyHolder instanceKeyHolder)
    {
        Stop(instanceKeyHolder.InstanceKey);
    }

    public void Stop(InstanceKey instanceKey)
    {
        if(!_processCache.TryGetValue(instanceKey, out var process)) 
            return;
        
        process.Stop();
    }

    public bool IsRunning(IInstanceKeyHolder instanceKeyHolder)
    {
        return IsRunning(instanceKeyHolder.InstanceKey);
    }
    
    public bool IsRunning(InstanceKey instanceKey)
    {
        return _processCache.ContainsKey(instanceKey);
    }

    private void RemoveProcess(ProcessRunHandler handler)
    {
        if(handler.Disposed) return;
        
        var key = _keyCache[handler];

        if (_holders.TryGetValue(key, out var holder))
        {
            holder.IsInstanceRunning = false;
            holder.InstanceKey = default;
            _holders.Remove(key);
        }
        
        _processCache.Remove(key);
        _keyCache.Remove(handler);
        _contentLoggerCache.Remove(key);
    }

    private void OnProcessExited(ProcessRunHandler obj)
    {
        obj.OnProcessExited -= OnProcessExited;
        RemoveProcess(obj);
    }
}

public interface IInstanceKeyHolder
{
    public InstanceKey InstanceKey { get; set; }
    public bool IsInstanceRunning { get; set; }
}
