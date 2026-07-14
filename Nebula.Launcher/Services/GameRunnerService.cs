using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Launcher.ProcessHelper;
using Nebula.Launcher.ServerListProviders;
using Nebula.Launcher.ViewModels;
using Nebula.Launcher.ViewModels.Pages;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Shared;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;

namespace Nebula.Launcher.Services;

[ServiceRegister]
public class GameRunnerService
{
    private readonly PopupMessageService _popupMessageService;
    private readonly ViewHelperService _viewHelperService;
    private readonly GameRunnerPreparer _gameRunnerPreparer;
    private readonly InstanceRunningContainer _instanceRunningContainer;
    private readonly AccountInfoViewModel _accountInfoViewModel;
    private readonly FavoriteServerListProvider _favoriteServerListProvider;
    private readonly ILogger _logger;

    private Dictionary<RobustUrl, InstanceKey> _instanceDictionary = [];

    public GameRunnerService(PopupMessageService popupMessageService, 
        DebugService debugService, 
        ViewHelperService viewHelperService,
        GameRunnerPreparer gameRunnerPreparer, 
        InstanceRunningContainer instanceRunningContainer, 
        AccountInfoViewModel accountInfoViewModel, 
        FavoriteServerListProvider favoriteServerListProvider)
    {
        _popupMessageService = popupMessageService;
        _viewHelperService = viewHelperService;
        _gameRunnerPreparer = gameRunnerPreparer;
        _instanceRunningContainer = instanceRunningContainer;
        _accountInfoViewModel = accountInfoViewModel;
        _favoriteServerListProvider = favoriteServerListProvider;
        _logger = debugService.GetLogger("GameRunnerService");
    }

    public void StopInstance(InstanceKey instanceKey)
    {
        _instanceRunningContainer.Stop(instanceKey);
    }

    public void ReadInstanceLog(InstanceKey instanceKey)
    {
        _instanceRunningContainer.Popup(instanceKey);
    }
    
    public void OpenContentViewer(RobustUrl robustUrl)
    {
        _viewHelperService.GetViewModel<MainViewModel>().RequirePage<ContentBrowserViewModel>().Go(robustUrl, ContentPath.Empty);
    }

    public void AddFavorite(RobustUrl robustUrl)
    {
        _favoriteServerListProvider.AddFavorite(robustUrl);
    }
    
    public void RemoveFavorite(RobustUrl robustUrl)
    {
        _favoriteServerListProvider.RemoveFavorite(robustUrl);
    }
    
    public void EditName(RobustUrl robustUrl, string? oldName)
    {
        var popup = _viewHelperService.GetViewModel<EditServerNameViewModel>();
        popup.IpInput = robustUrl.ToString();
        popup.NameInput = oldName ?? string.Empty;
        _popupMessageService.Popup(popup);
    }
    
    public async Task RunInstanceAsync(RobustUrl robustUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var viewModelLoading = _viewHelperService.GetViewModel<LoadingContextViewModel>();
            viewModelLoading.LoadingName = "Loading instance...";

            _popupMessageService.Popup(viewModelLoading);
            var currProcessStartProvider = 
                await _gameRunnerPreparer.GetGameProcessStartInfoProvider(robustUrl, viewModelLoading, cancellationToken);
            _logger.Log("Preparing instance...");
            var instance = _instanceRunningContainer.RegisterInstance(currProcessStartProvider);
            _instanceRunningContainer.Run(instance);
            _logger.Log($"Starting instance... {instance} ");
        }
        catch (Exception e)
        {
            var error = new Exception("Error while attempt run instance", e);
            _logger.Error(error);
            _popupMessageService.Popup(error);
        }
    }

    public async Task RunInstanceAsync(ServerEntryViewModel serverEntryViewModel, CancellationToken cancellationToken, bool ignoreLoginCredentials = false)
    {
        _logger.Log("Running instance..." + serverEntryViewModel.RealName);
        if (!ignoreLoginCredentials && _accountInfoViewModel.Credentials.Value is null)
        {
            var warningContext = _viewHelperService.GetViewModel<IsLoginCredentialsNullPopupViewModel>()
                .WithServerEntry(serverEntryViewModel);
            
            _popupMessageService.Popup(warningContext);
            return;
        }

        try
        {
            using var viewModelLoading = _viewHelperService.GetViewModel<LoadingContextViewModel>();
            viewModelLoading.LoadingName = "Loading instance...";

            _popupMessageService.Popup(viewModelLoading);
            var currProcessStartProvider = 
                await _gameRunnerPreparer.GetGameProcessStartInfoProvider(serverEntryViewModel.Address, viewModelLoading, cancellationToken);
            _logger.Log("Preparing instance...");
            _instanceRunningContainer.RegisterInstance(serverEntryViewModel, currProcessStartProvider);
            _instanceRunningContainer.Run(serverEntryViewModel);
            _logger.Log($"Starting instance... {serverEntryViewModel.InstanceKey.Id} ");
        }
        catch (Exception e)
        {
            var error = new Exception("Error while attempt run instance", e);
            _logger.Error(error);
            _popupMessageService.Popup(error);
        }
    }
}