using System;
using System.Threading.Tasks;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Shared;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Services;

[ServiceRegister]
public class BuildPrivacyService(
    ViewHelperService viewHelperService, 
    PopupMessageService popupMessageService,
    ConfigurationService configurationService)
{
    public async Task<bool> IsPrivacyAcceptRequired(ServerEntryViewModel serverEntry)
    {
        var info = await serverEntry.GetServerInfo();
        if(info?.PrivacyPolicy is null) 
            return false;
        
        var privacyAcceptedList = configurationService.GetConfigValue(LauncherConVar.AcceptedPrivacyPolicies)!;
        var privacyKey = info.PrivacyPolicy.Identifier + "-" + info.PrivacyPolicy.Version;
        if (privacyAcceptedList.Contains(privacyKey))
            return false;

        using var privacyPopup = viewHelperService.GetViewModel<PrivacyInfoViewModel>();
        popupMessageService.Popup(privacyPopup);
        if (await privacyPopup.AskPrivacyAccepting(info.PrivacyPolicy))
        {
            configurationService.SetConfigValue(LauncherConVar.AcceptedPrivacyPolicies, [..privacyAcceptedList, privacyKey]);
            return false;
        }
        
        return true;
    }
}