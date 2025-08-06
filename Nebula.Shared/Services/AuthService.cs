using System.Net;
using System.Net.Http.Headers;
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
    private readonly ILogger _logger = debugService.GetLogger("AuthService");

    public async Task<AuthTokenCredentials> Auth(string login, string password, string authServer, string? code = null)
    {
        _logger.Debug($"Auth to {authServer}api/auth/authenticate {login}");

        var authUrl = new Uri($"{authServer}api/auth/authenticate");

        try
        {
            var result =
                await restService.PostAsync<AuthenticateResponse, AuthenticateRequest>(
                    new AuthenticateRequest(login, null, password, code), authUrl, cancellationService.Token);

            return new AuthTokenCredentials(result.UserId,
                new LoginToken(result.Token, result.ExpireTime), login, authServer);
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

    public async Task EnsureToken(AuthTokenCredentials tokenCredentials)
    {
        var authUrl = new Uri($"{tokenCredentials.AuthServer}api/auth/ping");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, authUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("SS14Auth", tokenCredentials.Token.Token);
        using var resp = await _httpClient.SendAsync(requestMessage, cancellationService.Token);
    }
    
    public async Task Logout(AuthTokenCredentials tokenCredentials)
    {
        var authUrl = new Uri($"{tokenCredentials.AuthServer}api/auth/logout");
        await restService.PostAsync<NullResponse, TokenRequest>(TokenRequest.From(tokenCredentials), authUrl, cancellationService.Token);
    }

    public async Task<AuthTokenCredentials> Refresh(AuthTokenCredentials tokenCredentials)
    {
        var authUrl = new Uri($"{tokenCredentials.AuthServer}api/auth/refresh");
        var newToken = await restService.PostAsync<LoginToken, TokenRequest>(
            TokenRequest.From(tokenCredentials), authUrl, cancellationService.Token);
        
        return tokenCredentials with { Token = newToken };
    }
}

public sealed record AuthTokenCredentials(Guid UserId, LoginToken Token, string Login, string AuthServer);

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

public sealed record TokenRequest(string Token)
{
    public static TokenRequest From(AuthTokenCredentials authTokenCredentials)
    {
        return new TokenRequest(authTokenCredentials.Token.Token);
    }
    
    public static TokenRequest Empty { get; } = new TokenRequest("");
    
}