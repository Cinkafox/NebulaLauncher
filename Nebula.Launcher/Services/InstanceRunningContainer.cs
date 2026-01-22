using System;
using System.Collections.Generic;
using Nebula.Launcher.Models;
using Nebula.Launcher.ProcessHelper;
using Nebula.Launcher.ViewModels;
using Nebula.Shared;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Services;

[ServiceRegister]
public sealed class InstanceRunningContainer(PopupMessageService popupMessageService, DebugService debugService)
{
    private readonly InstanceKeyPool _keyPool = new();
    private readonly Dictionary<InstanceKey, ProcessRunHandler> _processCache = new();
    private readonly Dictionary<InstanceKey, ContentLogConsumer> _contentLoggerCache = new();
    private readonly Dictionary<ProcessRunHandler, InstanceKey> _keyCache = new();

    public Action<InstanceKey, bool>? IsRunningChanged;

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

    public void Run(InstanceKey instanceKey)
    {
        if(!_processCache.TryGetValue(instanceKey, out var process)) 
            return;
        
        process.Start();
        IsRunningChanged?.Invoke(instanceKey, true);
    }

    public void Stop(InstanceKey instanceKey)
    {
        if(!_processCache.TryGetValue(instanceKey, out var process)) 
            return;
        
        process.Stop();
    }

    public bool IsRunning(InstanceKey instanceKey)
    {
        return _processCache.ContainsKey(instanceKey);
    }

    private void RemoveProcess(ProcessRunHandler handler)
    {
        if(handler.Disposed) return;
        
        var key = _keyCache[handler];
        IsRunningChanged?.Invoke(key, false);
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