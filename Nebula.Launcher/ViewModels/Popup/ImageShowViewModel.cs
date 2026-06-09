using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ConstructGenerator, ViewModelRegister(typeof(ImageShowView))]
public sealed partial class ImageShowViewModel : PopupViewModelBase
{
    public override string Title => LocalizationService.GetString("popup-rsic-show");
    public override bool IsClosable => true;
    
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public DebugService DebugService { get; }

    [ObservableProperty] private IImageInput _image;

    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
}

public interface IImageInput;