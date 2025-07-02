using System;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ConstructGenerator, ViewModelRegister(typeof(TfaView))]
public partial class TfaViewModel : PopupViewModelBase
{
    public Action<string>? OnTfaEntered;
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }

    public void OnTfaEnter(string code)
    {
        OnTfaEntered?.Invoke(code);
        Dispose();
    }

    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    public override string Title => LocalisationService.GetString("popup-twofa");
    public override bool IsClosable => true;
}