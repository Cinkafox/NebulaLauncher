using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nebula.Launcher.Models.Auth;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared.Models.Auth;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher.ViewModels.Pages;

[ViewModelRegister(typeof(AccountInfoView))]
[ConstructGenerator]
public partial class AccountInfoViewModel : ViewModelBase
{
    [ObservableProperty] private bool _authMenuExpand;

    [ObservableProperty] private bool _authUrlConfigExpand;

    [ObservableProperty] private int _authViewSpan = 1;

    [ObservableProperty] private string _currentAuthServer = string.Empty;

    [ObservableProperty] private string _currentLogin = string.Empty;

    [ObservableProperty] private string _currentPassword = string.Empty;

    [ObservableProperty] private bool _isLogged;
    [ObservableProperty] private bool _doRetryAuth;
    [ObservableProperty] private AuthTokenCredentials? _credentials;

    private bool _isProfilesEmpty;
    [GenerateProperty] private PopupMessageService PopupMessageService { get; } = default!;
    [GenerateProperty] private ConfigurationService ConfigurationService { get; } = default!;
    [GenerateProperty] private DebugService DebugService { get; }
    [GenerateProperty] private AuthService AuthService { get; } = default!;
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; } = default!;

    public ObservableCollection<ProfileAuthCredentials> Accounts { get; } = new();
    public ObservableCollection<AuthServerCredentials> AuthUrls { get; } = new();

    [ObservableProperty] private AuthServerCredentials _authItemSelect;

    private ILogger _logger;

    //Design think
    protected override void InitialiseInDesignMode()
    {
        AddAccount(new AuthTokenCredentials(Guid.Empty, LoginToken.Empty, "Binka", ""));
        AddAccount(new AuthTokenCredentials(Guid.Empty, LoginToken.Empty, "Binka", ""));
        
        AuthUrls.Add(new AuthServerCredentials("Test",["example.com"]));
    }

    //Real think
    protected override void Initialise()
    {
        _logger = DebugService.GetLogger(this);
        Task.Run(ReadAuthConfig);
    }

    public async void AuthByProfile(ProfileAuthCredentials credentials)
    {
        var message = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        message.InfoText = LocalisationService.GetString("auth-try-auth-profile");
        message.IsInfoClosable = false;
        PopupMessageService.Popup(message);
        
        try
        {
            await CatchAuthError(async () => await TryAuth(credentials.Credentials), () => message.Dispose());
        }
        catch (Exception ex)
        {
            CurrentLogin = credentials.Credentials.Login;
            CurrentAuthServer = credentials.Credentials.AuthServer;
            
            var unexpectedError = new Exception(LocalisationService.GetString("auth-error"), ex);
            _logger.Error(unexpectedError);
            PopupMessageService.Popup(unexpectedError);
            return;
        }
        
        ConfigurationService.SetConfigValue(LauncherConVar.AuthCurrent, Credentials);
        
        message.Dispose();
    }

    public void DoAuth(string? code = null)
    {
        var message = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        message.InfoText = LocalisationService.GetString("auth-processing");
        message.IsInfoClosable = false;
        PopupMessageService.Popup(message);

        var serverCandidates = new List<string>();

        if (string.IsNullOrWhiteSpace(CurrentAuthServer))
            serverCandidates.AddRange(AuthItemSelect.Servers);
        else
            serverCandidates.Add(CurrentAuthServer);

        Task.Run(async () =>
        {
            Exception? exception = null;
            foreach (var server in serverCandidates)
            {
                try
                {
                    await CatchAuthError(async () => await TryAuth(CurrentLogin, CurrentPassword, server, code), ()=> message.Dispose());
                    break;
                }
                catch (Exception ex)
                {
                    var unexpectedError = new Exception(LocalisationService.GetString("auth-error"), ex);
                    _logger.Error(unexpectedError);
                    PopupMessageService.Popup(unexpectedError);
                }
            }
            
            message.Dispose();

            if (!IsLogged)
            {
                PopupMessageService.Popup(exception ?? new Exception(LocalisationService.GetString("auth-error")));
            }
        });
    }

    private async Task TryAuth(AuthTokenCredentials authTokenCredentials)
    {
        CurrentLogin = authTokenCredentials.Login;
        CurrentAuthServer = authTokenCredentials.AuthServer;
        await SetAuth(authTokenCredentials);
        IsLogged = true;
    }

    private async Task SetAuth(AuthTokenCredentials authTokenCredentials)
    {
        await AuthService.EnsureToken(authTokenCredentials);
        Credentials = authTokenCredentials;
    }

    private async Task TryAuth(string login, string password, string authServer, string? code)
    {
        Credentials = await AuthService.Auth(login, password, authServer, code);
        CurrentLogin = login;
        CurrentPassword = password;
        CurrentAuthServer = authServer;
        IsLogged = true;
        ConfigurationService.SetConfigValue(LauncherConVar.AuthCurrent, Credentials);
    }

    private async Task CatchAuthError(Func<Task> a, Action? onError)
    {
        DoRetryAuth = false;

        try
        {
            await a();
        }
        catch (AuthException e)
        {
            onError?.Invoke();
            switch (e.Error.Code)
            {
                case AuthenticateDenyCode.TfaRequired:
                case AuthenticateDenyCode.TfaInvalid:
                    var p = ViewHelperService.GetViewModel<TfaViewModel>();
                    p.OnTfaEntered += OnTfaEntered;
                    PopupMessageService.Popup(p);
                    _logger.Log("TFA required");
                    break;
                case AuthenticateDenyCode.InvalidCredentials:
                    PopupError(LocalisationService.GetString("auth-invalid-credentials"), e);
                    break;
                default:
                    throw;
            }
        }
        catch (HttpRequestException e)
        {
            onError?.Invoke();
            switch (e.HttpRequestError)
            {
                case HttpRequestError.ConnectionError:
                    PopupError(LocalisationService.GetString("auth-connection-error"), e);
                    DoRetryAuth = true;
                    break;

                case HttpRequestError.NameResolutionError:
                    PopupError(LocalisationService.GetString("auth-name-resolution-error"), e);
                    DoRetryAuth = true;
                    break;
                
                case HttpRequestError.SecureConnectionError:
                    PopupError(LocalisationService.GetString("auth-secure-error"), e);
                    DoRetryAuth = true;
                    break;

                default:
                    var authError = new Exception(LocalisationService.GetString("auth-error"), e);
                    _logger.Error(authError);
                    PopupMessageService.Popup(authError);
                    break;
            }
        }
    }

    private void OnTfaEntered(string code)
    {
        DoAuth(code);
    }

    public void Logout()
    {
        IsLogged = false;
        Credentials = null;
    }

    private void UpdateAuthMenu()
    {
        if (AuthMenuExpand || _isProfilesEmpty)
            AuthViewSpan = 2;
        else
            AuthViewSpan = 1;
    }

    private void AddAccount(AuthTokenCredentials credentials)
    {
        var onDelete = new DelegateCommand<ProfileAuthCredentials>(OnDeleteProfile);
        var onSelect = new DelegateCommand<ProfileAuthCredentials>(AuthByProfile);

        var alpm = new ProfileAuthCredentials(
            credentials,
            onSelect,
            onDelete);

        onDelete.TRef.Value = alpm;
        onSelect.TRef.Value = alpm;

        Accounts.Add(alpm);
    }

    private void ReadAuthConfig()
    {
        var message = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        message.InfoText = LocalisationService.GetString("auth-config-read");
        message.IsInfoClosable = false;
        PopupMessageService.Popup(message);
        foreach (var profile in
                 ConfigurationService.GetConfigValue(LauncherConVar.AuthProfiles)!)
            AddAccount(profile);

        if (Accounts.Count == 0) UpdateAuthMenu();

        AuthUrls.Clear();
        var authUrls = ConfigurationService.GetConfigValue(LauncherConVar.AuthServers)!;
        foreach (var url in authUrls) AuthUrls.Add(url);
        if(authUrls.Length > 0) AuthItemSelect = authUrls[0];
        message.Dispose();

        DoCurrentAuth();
    }

    public async void DoCurrentAuth()
    {
        var message = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        message.InfoText = LocalisationService.GetString("auth-try-auth-config");
        message.IsInfoClosable = false;
        PopupMessageService.Popup(message);

        var currProfile = ConfigurationService.GetConfigValue(LauncherConVar.AuthCurrent);

        if (currProfile != null)
        {
            try
            {
                await CatchAuthError(async () => await TryAuth(currProfile), () => message.Dispose());
            }
            catch (Exception ex)
            {
                var unexpectedError = new Exception(LocalisationService.GetString("auth-error"), ex);
                _logger.Error(unexpectedError);
                PopupMessageService.Popup(unexpectedError);
                return;
            }
        }

        message.Dispose();
    }

    [RelayCommand]
    private void OnSaveProfile()
    {
        if(Credentials is null) return;
        
        AddAccount(Credentials);
        _isProfilesEmpty = Accounts.Count == 0;
        UpdateAuthMenu();
        DirtyProfile();
    }

    private void OnDeleteProfile(ProfileAuthCredentials account)
    {
        Accounts.Remove(account);
        _isProfilesEmpty = Accounts.Count == 0;
        UpdateAuthMenu();
        DirtyProfile();
    }

    private void PopupError(string message, Exception e)
    {
        message = LocalisationService.GetString("auth-error-occured") + message;
        _logger.Error(new Exception(message, e));

        var messageView = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        messageView.InfoText = message;
        messageView.IsInfoClosable = true;
        PopupMessageService.Popup(messageView);
    }

    [RelayCommand]
    private void OnExpandAuthUrl()
    {
        AuthUrlConfigExpand = !AuthUrlConfigExpand;
    }

    [RelayCommand]
    private void OnExpandAuthView()
    {
        AuthMenuExpand = !AuthMenuExpand;
        UpdateAuthMenu();
    }

    private void DirtyProfile()
    {
        ConfigurationService.SetConfigValue(LauncherConVar.AuthProfiles,
            Accounts.Select(a => a.Credentials).ToArray());
    }
}