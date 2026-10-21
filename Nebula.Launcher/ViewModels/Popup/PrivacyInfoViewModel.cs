using System.Threading.Tasks;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ConstructGenerator, ViewModelRegister(typeof(PrivacyInfoView))]
public sealed partial class PrivacyInfoViewModel : PopupViewModelBase
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    public override string Title => LocalizationService.GetString("privacy-info-popup-title");
    public override bool IsClosable => true;

    private TaskCompletionSource<bool>? _currPrivacyWaitTask;
    private string _privacyUrl = "";
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }

    public Task<bool> AskPrivacyAccepting(BuildPrivacyInfo info)
    {
        _currPrivacyWaitTask?.SetResult(false);
        _currPrivacyWaitTask = new TaskCompletionSource<bool>();

        _privacyUrl = info.Link;
        
        return _currPrivacyWaitTask.Task;
    }

    public void Accept()
    {
        _currPrivacyWaitTask?.SetResult(true);
        _currPrivacyWaitTask = null;
    }
    
    public void Decline()
    {
        _currPrivacyWaitTask?.SetResult(false);
        _currPrivacyWaitTask = null;
    }

    public void GoToPrivacy()
    {
        Helper.SafeOpenBrowser(_privacyUrl);
    }
}