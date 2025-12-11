using System;
using Nebula.Shared.Services;

namespace Nebula.Launcher.ViewModels.Popup;

public abstract class PopupViewModelBase : ViewModelBase, IDisposable
{
    public abstract PopupMessageService PopupMessageService { get; }

    public abstract string Title { get; }
    public abstract bool IsClosable { get; }
    public Action<PopupViewModelBase>? OnDisposing;

    public void Dispose()
    {
        OnDispose();
        OnDisposing?.Invoke(this);
        PopupMessageService.ClosePopup(this);
    }

    protected virtual void OnDispose(){}
}