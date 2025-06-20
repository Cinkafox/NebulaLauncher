using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nebula.Shared.Models.Auth;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class AuthService(
    RestService restService,
    DebugService debugService,
    CancellationService cancellationService)
{
    private readonly HttpClient _httpClient = new();
    public CurrentAuthInfo? SelectedAuth { get; private set; }
    private readonly ILogger _logger = debugService.GetLogger("AuthService");

    public async Task Auth(AuthLoginPassword authLoginPassword, string? code = null)
    {
        var authServer = authLoginPassword.AuthServer;
        var login = authLoginPassword.Login;
        var password = authLoginPassword.Password;

        _logger.Debug($"Auth to {authServer}api/auth/authenticate {login}");

        var authUrl = new Uri($"{authServer}api/auth/authenticate");

        try
        {
            var result =
                await restService.PostAsync<AuthenticateResponse, AuthenticateRequest>(
                    new AuthenticateRequest(login, null, password, code), authUrl, cancellationService.Token);

            SelectedAuth = new CurrentAuthInfo(result.UserId,
                new LoginToken(result.Token, result.ExpireTime), authLoginPassword.Login, authLoginPassword.AuthServer);
        }
        catch (RestRequestException e)
        {
            Console.WriteLine(e.Content);
            if (e.StatusCode != HttpStatusCode.Unauthorized) throw;
            var err = await e.Content.AsJson<AuthDenyError>();
            
            if (err is null) throw;
            throw new AuthException(err);
        }
    }

    public void ClearAuth()
    {
        SelectedAuth = null;
    }

    public async Task SetAuth(CurrentAuthInfo info)
    {
        SelectedAuth = info;
        await EnsureToken();
    }

    public async Task EnsureToken()
    {
        if (SelectedAuth is null) throw new Exception("Auth info is not set!");

        var authUrl = new Uri($"{SelectedAuth.AuthServer}api/auth/ping");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, authUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("SS14Auth", SelectedAuth.Token.Token);
        using var resp = await _httpClient.SendAsync(requestMessage, cancellationService.Token);
    }
}

public sealed record CurrentAuthInfo(Guid UserId, LoginToken Token, string Login, string AuthServer);

public record AuthLoginPassword(string Login, string Password, string AuthServer);

public sealed record AuthDenyError(string[] Errors, AuthenticateDenyCode Code);

public sealed class AuthException(AuthDenyError error) : Exception
{
    public AuthDenyError Error { get; } = error;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthenticateDenyCode
{
        None               =  0,
        InvalidCredentials =  1,
        AccountUnconfirmed =  2,
        TfaRequired        =  3,
        TfaInvalid         =  4,
        AccountLocked      =  5,
}
