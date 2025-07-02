using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;
using AddFavoriteView = Nebula.Launcher.Views.Popup.AddFavoriteView;

namespace Nebula.Launcher.ViewModels.Popup;

[ViewModelRegister(typeof(AddFavoriteView), false)]
[ConstructGenerator]
public partial class AddFavoriteViewModel : PopupViewModelBase
{
    private ILogger _logger;
    
    protected override void InitialiseInDesignMode()
    {
    }

    protected override void Initialise()
    {
        _logger = DebugService.GetLogger(this);
    }

    [GenerateProperty] 
    public override PopupMessageService PopupMessageService { get; }
    [GenerateProperty] private ServerOverviewModel ServerOverviewModel { get; }
    [GenerateProperty] private DebugService DebugService { get; }
    [GenerateProperty] private FavoriteServerListProvider FavoriteServerListProvider { get; }
    public override string Title => LocalisationService.GetString("popup-add-favorite");
    public override bool IsClosable => true;

    [ObservableProperty] private string _ipInput;
    [ObservableProperty] private string _error = "";

    public void OnEnter()
    {
        try
        {
            var uri = IpInput.ToRobustUrl();
            FavoriteServerListProvider.AddFavorite(uri);
            Dispose();
        }
        catch (Exception e)
        {
            Error = e.Message;
            _logger.Error(e);
        }
    }
}