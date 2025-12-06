using System;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ConstructGenerator, ViewModelRegister(typeof(TfaView))]
public partial class TfaViewModel : PopupViewModelBase
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public AccountInfoViewModel AccountInfo { get; }
    public override string Title => LocalizationService.GetString("popup-twofa");
    public override bool IsClosable => true;
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }

    public void OnTfaEnter(string code)
    {
        AccountInfo.DoAuth(code);
        Dispose();
    }
}