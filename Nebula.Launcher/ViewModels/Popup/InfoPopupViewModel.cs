using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(InfoPopupView), false)]
[ConstructGenerator]
public partial class InfoPopupViewModel : PopupViewModelBase
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }

    [ObservableProperty] private string _infoText = "Test";

    public override string Title => LocalizationService.GetString("popup-information");
    public bool IsInfoClosable { get; set; } = true;
    public override bool IsClosable => IsInfoClosable;

    protected override void Initialise()
    {
    }

    protected override void InitialiseInDesignMode()
    {
    }
}