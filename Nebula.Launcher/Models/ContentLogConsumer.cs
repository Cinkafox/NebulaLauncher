using System;
using System.Collections.Generic;
using Nebula.Launcher.ProcessHelper;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Models;

public sealed class ContentLogConsumer : IProcessLogConsumer
{
    private readonly PopupMessageService _popupMessageService;
    private readonly List<string> _outMessages = [];
    
    private LogPopupModelView? _currentLogPopup;
    
    public int MaxMessages { get; set; } = 100;

    public ContentLogConsumer(PopupMessageService popupMessageService)
    {
        _popupMessageService = popupMessageService;
    }

    public void Popup()
    {
        if(_currentLogPopup is not null) 
            return;
        
        _currentLogPopup = new LogPopupModelView(_popupMessageService);
        _currentLogPopup.OnDisposing += OnLogPopupDisposing;
        
        foreach (var message in _outMessages.ToArray())
        {
            _currentLogPopup.Append(message);
        }
        
        _popupMessageService.Popup(_currentLogPopup);
    }

    private void OnLogPopupDisposing(PopupViewModelBase obj)
    {
        if(_currentLogPopup == null) 
            return;
        
        _currentLogPopup.OnDisposing -= OnLogPopupDisposing;
        _currentLogPopup = null;
    }

    public void Out(string text)
    {
        _outMessages.Add(text);
        if(_outMessages.Count >= MaxMessages)
            _outMessages.RemoveAt(0);
        
        _currentLogPopup?.Append(text);
    }

    public void Error(string text)
    {
        Out(text);
    }

    public void Fatal(string text)
    {
        _popupMessageService.Popup(new ExceptionCompound("Error while running program", text));
    }
}