using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nebula.Launcher.Services;
using Nebula.Launcher.ViewModels.Popup;
using Nebula.Launcher.Views.Pages;
using Nebula.Shared;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;

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
        AddAccount(new AuthLoginPassword("Binka", "12341", ""));
        AddAccount(new AuthLoginPassword("Binka", "12341", ""));
        
        AuthUrls.Add(new AuthServerCredentials("Test",["example.com"]));
    }

    //Real think
    protected override void Initialise()
    {
        _logger = DebugService.GetLogger(this);
        ReadAuthConfig();
    }

    public void AuthByProfile(ProfileAuthCredentials credentials)
    {
        CurrentLogin = credentials.Login;
        CurrentPassword = credentials.Password;
        CurrentAuthServer = credentials.AuthServer;
        
        DoAuth();
    }

    public void DoAuth(string? code = null)
    {
        var message = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        message.InfoText = "Auth think, please wait...";
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
                    await TryAuth(CurrentLogin, CurrentPassword, server,code);
                    break;
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }
            
            message.Dispose();

            if (!IsLogged)
            {
                PopupMessageService.Popup(new Exception("No one of auth server is available.", exception));
            }
        });
    }

    private async Task TryAuth(string login, string password, string authServer,string? code)
    {
        try
        {
            await AuthService.Auth(new AuthLoginPassword(login, password, authServer), code);
            CurrentLogin = login;
            CurrentPassword = password;
            CurrentAuthServer = authServer;
            IsLogged = true;
            ConfigurationService.SetConfigValue(LauncherConVar.AuthCurrent, AuthService.SelectedAuth);
        }
        catch (AuthException e)
        {
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
                    PopupMessageService.Popup("Invalid Credentials!");
                    _logger.Error($"Invalid credentials");
                    break;
                default:
                    throw;
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
        AuthService.ClearAuth();
    }

    private void UpdateAuthMenu()
    {
        if (AuthMenuExpand || _isProfilesEmpty)
            AuthViewSpan = 2;
        else
            AuthViewSpan = 1;
    }

    private void AddAccount(AuthLoginPassword authLoginPassword)
    {
        var onDelete = new DelegateCommand<ProfileAuthCredentials>(OnDeleteProfile);
        var onSelect = new DelegateCommand<ProfileAuthCredentials>(AuthByProfile);

        var alpm = new ProfileAuthCredentials(
            authLoginPassword.Login,
            authLoginPassword.Password,
            authLoginPassword.AuthServer,
            onSelect,
            onDelete);

        onDelete.TRef.Value = alpm;
        onSelect.TRef.Value = alpm;

        Accounts.Add(alpm);
    }

    private async void ReadAuthConfig()
    {
        var message = ViewHelperService.GetViewModel<InfoPopupViewModel>();
        message.InfoText = "Read configuration file, please wait...";
        message.IsInfoClosable = false;
        PopupMessageService.Popup(message);
        foreach (var profile in
                 ConfigurationService.GetConfigValue(LauncherConVar.AuthProfiles)!)
            AddAccount(new AuthLoginPassword(profile.Login, profile.Password, profile.AuthServer));

        if (Accounts.Count == 0) UpdateAuthMenu();

        AuthUrls.Clear();
        var authUrls = ConfigurationService.GetConfigValue(LauncherConVar.AuthServers)!;
        foreach (var url in authUrls) AuthUrls.Add(url);
        if(authUrls.Length > 0) AuthItemSelect = authUrls[0];
        
        var currProfile = ConfigurationService.GetConfigValue(LauncherConVar.AuthCurrent);

        if (currProfile != null)
        {
            try
            {
                CurrentLogin = currProfile.Login;
                CurrentAuthServer = currProfile.AuthServer;

                IsLogged = await AuthService.SetAuth(currProfile);
            }
            catch (Exception e)
            {
                message.Dispose();
                PopupMessageService.Popup(e);
            }
        }
        
        message.Dispose();
    }

    [RelayCommand]
    private void OnSaveProfile()
    {
        AddAccount(new AuthLoginPassword(CurrentLogin, CurrentPassword, CurrentAuthServer));
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
            Accounts.ToArray());
    }
}

public sealed record ProfileAuthCredentials(
    string Login,
    string Password,
    string AuthServer,
    [property: JsonIgnore] ICommand OnSelect = default!,
    [property: JsonIgnore] ICommand OnDelete = default!);
    
public sealed record AuthServerCredentials(
    string Name, 
    string[] Servers
);