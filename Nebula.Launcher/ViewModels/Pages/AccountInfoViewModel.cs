using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Nebula.Launcher.Configurations;
using Nebula.Launcher.Models.Auth;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared.Configurations;
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
    [ObservableProperty] private AuthServerCredentials _authItemSelect;

    private bool _isProfilesEmpty;
    [GenerateProperty] private PopupMessageService PopupMessageService { get; } = default!;
    [GenerateProperty] private ConfigurationService ConfigurationService { get; } = default!;
    [GenerateProperty] private DebugService DebugService { get; }
    [GenerateProperty] private AuthService AuthService { get; } = default!;
    [GenerateProperty, DesignConstruct] private ViewHelperService ViewHelperService { get; } = default!;

    public ObservableCollection<ProfileAuthCredentials> Accounts { get; } = new();
    public ObservableCollection<AuthServerCredentials> AuthUrls { get; } = new();

    public ComplexConVarBinder<AuthTokenCredentials?> Credentials { get; private set; }

    private ILogger _logger;
    
    //Design think
    protected override void InitialiseInDesignMode()
    {
        AuthUrls.Add(new AuthServerCredentials("Test",["example.com"]));
        
        AddAccount(new AuthTokenCredentials(Guid.Empty, LoginToken.Empty, "Binka", "example.com"));
        AddAccount(new AuthTokenCredentials(Guid.Empty, LoginToken.Empty, "Binka", ""));
    }
    
    //Real think
    protected override void Initialise()
    {
        _logger = DebugService.GetLogger(this);
        Credentials = new AuthTokenCredentialsVar(this);
        Task.Run(ReadAuthConfig);
        Credentials.Value = Credentials.Value;
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
                    await CatchAuthError(async() =>
                    {
                        Credentials.Value = await AuthService.Auth(CurrentLogin, CurrentPassword, server, code);
                    }, ()=> message.Dispose());        
                    break;
                }
                catch (Exception ex)
                {
                    exception = new Exception(LocalisationService.GetString("auth-error"), ex);
                }
            }
            
            message.Dispose();

            if (exception != null)
            {
                PopupMessageService.Popup(new Exception("Error while auth", exception));
            }
        });
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
                    PopupMessageService.Popup(p);
                    _logger.Log("TFA required");
                    break;
                case AuthenticateDenyCode.InvalidCredentials:
                    PopupError(LocalisationService.GetString("auth-invalid-credentials"), e);
                    break;
                case AuthenticateDenyCode.AccountLocked:
                    PopupError(LocalisationService.GetString("auth-account-locked"), e);
                    break;
                case AuthenticateDenyCode.AccountUnconfirmed:
                    PopupError(LocalisationService.GetString("auth-account-unconfirmed"), e);
                    break;
                case AuthenticateDenyCode.None:
                    PopupError(LocalisationService.GetString("auth-none"),e);
                    break;
                default:
                    PopupError(LocalisationService.GetString("auth-error-fuck"), e);
                    break;
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
                case HttpRequestError.UserAuthenticationError:
                    PopupError(LocalisationService.GetString("auth-user-authentication-error"), e);
                    break;
                case HttpRequestError.Unknown:
                    PopupError(LocalisationService.GetString("auth-unknown"), e);
                    break;
                case HttpRequestError.HttpProtocolError:
                    PopupError(LocalisationService.GetString("auth-http-protocol-error"), e);
                    break;
                case HttpRequestError.ExtendedConnectNotSupported:
                    PopupError(LocalisationService.GetString("auth-extended-connect-not-support"), e);
                    break;
                case HttpRequestError.VersionNegotiationError:
                    PopupError(LocalisationService.GetString("auth-version-negotiation-error"), e);
                    break;
                case HttpRequestError.ProxyTunnelError:
                    PopupError(LocalisationService.GetString("auth-proxy-tunnel-error"), e);
                    break;
                case HttpRequestError.InvalidResponse:
                    PopupError(LocalisationService.GetString("auth-invalid-response"), e);
                    break;
                case HttpRequestError.ResponseEnded:
                    PopupError(LocalisationService.GetString("auth-response-ended"), e);
                    break;
                case HttpRequestError.ConfigurationLimitExceeded:
                    PopupError(LocalisationService.GetString("auth-configuration-limit-exceeded"), e);
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
        Credentials.Value = null;
        CurrentAuthServer = "";
    }

    public string GetServerAuthName(AuthTokenCredentials? credentials)
    {
        if (credentials is null) return "";
        return AuthUrls.FirstOrDefault(p => p.Servers.Contains(credentials.AuthServer))?.Name ?? "CustomAuth";
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
        var onSelect = new DelegateCommand<ProfileAuthCredentials>((p) => Credentials.Value = p.Credentials);
        
        var serverName = GetServerAuthName(credentials);

        var alpm = new ProfileAuthCredentials(
            credentials,
            serverName, 
            onSelect,
            onDelete);

        onDelete.TRef.Value = alpm;
        onSelect.TRef.Value = alpm;

        Accounts.Add(alpm);
    }

    private async Task ReadAuthConfig()
    {
        var message = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        message.InfoText = LocalisationService.GetString("auth-config-read");
        message.IsInfoClosable = false;
        PopupMessageService.Popup(message);
        
        _logger.Log("Reading auth config");
        
        AuthUrls.Clear();
        var authUrls = ConfigurationService.GetConfigValue(LauncherConVar.AuthServers)!;
        foreach (var url in authUrls) AuthUrls.Add(url);
        if(authUrls.Length > 0) AuthItemSelect = authUrls[0];
        
        var profileCandidates = new List<AuthTokenCredentials>();

        foreach (var profile in
                 ConfigurationService.GetConfigValue(LauncherConVar.AuthProfiles)!)
        {
            _logger.Log($"Reading profile {profile.Login}");
            var checkedCredit = await CheckOrRenewToken(profile);
            if(checkedCredit is null)
            {
                _logger.Error($"Profile {profile.Login} is not available");
                continue;
            }
            
            _logger.Log($"Profile {profile.Login} is available");
            profileCandidates.Add(checkedCredit);
            AddAccount(checkedCredit);
        }
        
        ConfigurationService.SetConfigValue(LauncherConVar.AuthProfiles, profileCandidates.ToArray());

        if (Accounts.Count == 0) UpdateAuthMenu();
        
        message.Dispose();
    }

    public void DoCurrentAuth()
    {
        DoAuth();
    }

    private async Task<AuthTokenCredentials?> CheckOrRenewToken(AuthTokenCredentials? authTokenCredentials)
    {
        if(authTokenCredentials is null) 
            return null;

        var daysLeft = (int)(authTokenCredentials.Token.ExpireTime - DateTime.Now).TotalDays;
        
        if(daysLeft >= 4)
        {
            _logger.Log("Token " + authTokenCredentials.Login + " is active, "+daysLeft+" days left, undo renewing!");
            return authTokenCredentials;
        }
        
        try
        {
            _logger.Log($"Renewing token for {authTokenCredentials.Login}");
            return await ExceptionHelper.TryRun(() => AuthService.Refresh(authTokenCredentials),3, (attempt, e) =>
            {
                _logger.Error(new Exception("Error while renewing, attempts: " + attempt, e));
            });
        }
        catch (Exception e)
        {
            var unexpectedError = new Exception(LocalisationService.GetString("auth-error"), e);
            _logger.Error(unexpectedError);
            return authTokenCredentials;
        }
    }
    
    public void OnSaveProfile()
    {
        if(Credentials.Value is null) return;
        
        AddAccount(Credentials.Value);
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
    
    public void OnExpandAuthUrl()
    {
        AuthUrlConfigExpand = !AuthUrlConfigExpand;
    }
    
    public void OnExpandAuthView()
    {
        AuthMenuExpand = !AuthMenuExpand;
        UpdateAuthMenu();
    }

    private void DirtyProfile()
    {
        ConfigurationService.SetConfigValue(LauncherConVar.AuthProfiles,
            Accounts.Select(a => a.Credentials).ToArray());
    }
    
    public sealed class AuthTokenCredentialsVar(AccountInfoViewModel accountInfoViewModel)
        : ComplexConVarBinder<AuthTokenCredentials?>(
            accountInfoViewModel.ConfigurationService.SubscribeVarChanged(LauncherConVar.AuthCurrent))
    {
        protected override async Task<AuthTokenCredentials?> OnValueChange(AuthTokenCredentials? currProfile)
        {
            if (currProfile is null)
            {
                accountInfoViewModel.IsLogged = false;
                accountInfoViewModel._logger.Log("clearing credentials");
                return null;
            }
        
            var message = accountInfoViewModel.ViewHelperService.GetViewModel<InfoPopupViewModel>();
            message.InfoText = LocalisationService.GetString("auth-try-auth-config");
            message.IsInfoClosable = false;
            accountInfoViewModel.PopupMessageService.Popup(message);
      
            accountInfoViewModel._logger.Log($"trying auth with {currProfile.Login}");

            var errorRun = false;

            currProfile = await accountInfoViewModel.CheckOrRenewToken(currProfile);

            if (currProfile is null)
            {
                message.Dispose();
                
                accountInfoViewModel._logger.Log("profile credentials update required!");
                
                accountInfoViewModel.PopupMessageService.Popup("profile credentials update required!");
                
                accountInfoViewModel.IsLogged = false;
                return null;
            }
            
            try
            {
                await accountInfoViewModel.CatchAuthError(async () =>
                {
                    await accountInfoViewModel.AuthService.EnsureToken(currProfile);
                }, () =>
                {
                    message.Dispose();
                    errorRun = true;
                });
                message.Dispose();
            }
            catch (Exception ex)
            {
                accountInfoViewModel.CurrentLogin = currProfile.Login;
                accountInfoViewModel.CurrentAuthServer = currProfile.AuthServer;
                var unexpectedError = new Exception(LocalisationService.GetString("auth-error"), ex);
                accountInfoViewModel._logger.Error(unexpectedError);
                accountInfoViewModel.PopupMessageService.Popup(unexpectedError);
                errorRun = true;
            }

            if (errorRun)
            {
                accountInfoViewModel.IsLogged = false;
                return null;
            }
        
            accountInfoViewModel.IsLogged = true;
            
            return currProfile;
        }
    }
}

