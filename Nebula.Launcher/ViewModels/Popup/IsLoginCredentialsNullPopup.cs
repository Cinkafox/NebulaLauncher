using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ConstructGenerator, ViewModelRegister(typeof(IsLoginCredentialsNullPopupView))]
public partial class IsLoginCredentialsNullPopupViewModel : PopupViewModelBase
{
    private ServerEntryModelView _entry;
    
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; }
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }

    public IsLoginCredentialsNullPopupViewModel WithServerEntry(ServerEntryModelView entryModelView)
    {
        _entry = entryModelView;
        return this;
    }

    public void Proceed()
    {
        _entry.RunInstanceIgnoreAuth();
        Dispose();
    }

    public void Cancel()
    {
        Dispose();
    }

    public void GotoAuthPage()
    {
        ViewHelperService.GetViewModel<MainViewModel>().RequirePage<AccountInfoViewModel>();
        Dispose();
    }
    
    public override string Title => LocalisationService.GetString("popup-login-credentials-warning");
    public override bool IsClosable => true;
}