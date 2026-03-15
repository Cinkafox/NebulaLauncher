using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Launcher.Models;
using Nebula.Launcher.ProcessHelper;
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
    private readonly ServerViewContainer _container;
    private readonly ILogger _logger;
    
    private readonly Dictionary<InstanceKey, RobustUrl> _robustUrls = new();
    private readonly Dictionary<RobustUrl, InstanceKey> _robustKeys = new();

    public GameRunnerService(PopupMessageService popupMessageService, 
        DebugService debugService, 
        ViewHelperService viewHelperService,
        GameRunnerPreparer gameRunnerPreparer, 
        InstanceRunningContainer instanceRunningContainer, 
        AccountInfoViewModel accountInfoViewModel, 
        ServerViewContainer container)
    {
        _popupMessageService = popupMessageService;
        _viewHelperService = viewHelperService;
        _gameRunnerPreparer = gameRunnerPreparer;
        _instanceRunningContainer = instanceRunningContainer;
        _accountInfoViewModel = accountInfoViewModel;
        _container = container;

        _logger = debugService.GetLogger("GameRunnerService");
        _instanceRunningContainer.IsRunningChanged += IsRunningChanged;
    }

    private void IsRunningChanged(InstanceKey key, bool isRunning)
    {
        _logger.Debug($"IsRunningChanged {key}: {isRunning}");
        if (!_robustUrls.TryGetValue(key, out var robustUrl)) return;
        
        if (_container.Get(robustUrl) is IRunningSignalConsumer signalConsumer)
        {
            _logger.Debug($"IsRunningChanged conf {robustUrl}: {isRunning}");
            signalConsumer.ProcessRunningSignal(isRunning);
        }
            
        if (!isRunning)
        {
            _robustKeys.Remove(robustUrl);
            _robustUrls.Remove(key);
        }
    }

    public void StopInstance(ServerEntryViewModel serverEntryViewModel)
    {
        if (_robustKeys.TryGetValue(serverEntryViewModel.Address, out var instanceKey))
        {
            _instanceRunningContainer.Stop(instanceKey);
        }
    }

    public void ReadInstanceLog(ServerEntryViewModel serverEntryViewModel)
    {
        if (_robustKeys.TryGetValue(serverEntryViewModel.Address, out var instanceKey))
        {
            _instanceRunningContainer.Popup(instanceKey);
        }
    }

    public async Task<InstanceKey?> RunInstanceAsync(ServerEntryViewModel serverEntryViewModel, CancellationToken cancellationToken, bool ignoreLoginCredentials = false)
    {
        _logger.Log("Running instance..." + serverEntryViewModel.RealName);
        if (!ignoreLoginCredentials && _accountInfoViewModel.Credentials.Value is null)
        {
            var warningContext = _viewHelperService.GetViewModel<IsLoginCredentialsNullPopupViewModel>()
                .WithServerEntry(serverEntryViewModel);
            
            _popupMessageService.Popup(warningContext);
            return null;
        }

        try
        {
            using var viewModelLoading = _viewHelperService.GetViewModel<LoadingContextViewModel>();
            viewModelLoading.LoadingName = "Loading instance...";

            _popupMessageService.Popup(viewModelLoading);
            var currProcessStartProvider = 
                await _gameRunnerPreparer.GetGameProcessStartInfoProvider(serverEntryViewModel.Address, viewModelLoading, cancellationToken);
            _logger.Log("Preparing instance...");
            var instanceKey = _instanceRunningContainer.RegisterInstance(currProcessStartProvider);
            _robustUrls.Add(instanceKey, serverEntryViewModel.Address);
            _robustKeys.Add(serverEntryViewModel.Address, instanceKey);
            _instanceRunningContainer.Run(instanceKey);
            _logger.Log($"Starting instance... {instanceKey.Id} " + serverEntryViewModel.RealName);
            return instanceKey;
        }
        catch (Exception e)
        {
            var error = new Exception("Error while attempt run instance", e);
            _logger.Error(error);
            _popupMessageService.Popup(error);
            return null;
        }
    }
}