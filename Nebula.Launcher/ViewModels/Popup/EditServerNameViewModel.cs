using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Services;
using Nebula.Launcher.Views.Popup;
using Nebula.Shared.Services;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(EditServerNameView), false)]
[ConstructGenerator]
public sealed partial class EditServerNameViewModel : PopupViewModelBase
{
    [GenerateProperty] public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] public ConfigurationService ConfigurationService { get; }
    public override string Title => LocalisationService.GetString("popup-edit-name");
    public override bool IsClosable => true;
    
    [ObservableProperty] private string _ipInput;
    [ObservableProperty] private string _nameInput;
    
    public void OnEnter()
    {
        if(string.IsNullOrWhiteSpace(IpInput)) 
            return;

        if (string.IsNullOrWhiteSpace(NameInput))
        {
            OnClear();
            return;
        }
        
        AddServerName();
        Dispose();
    }

    public void OnClear()
    {
        RemoveServerName();
        Dispose();
    }

    private void AddServerName()
    {
        var currentNames = ConfigurationService.GetConfigValue(LauncherConVar.ServerCustomNames)!;
        currentNames.Add(IpInput, NameInput);
        ConfigurationService.SetConfigValue(LauncherConVar.ServerCustomNames, currentNames);
    }

    private void RemoveServerName()
    {
        var currentNames = ConfigurationService.GetConfigValue(LauncherConVar.ServerCustomNames)!;
        currentNames.Remove(IpInput);
        ConfigurationService.SetConfigValue(LauncherConVar.ServerCustomNames, currentNames);
    }
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
    }
}