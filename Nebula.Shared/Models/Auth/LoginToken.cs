namespace Nebula.Shared.Models.Auth;

public sealed record LoginToken(string Token, DateTimeOffset ExpireTime)
{
    public static LoginToken Empty = new(string.Empty, DateTimeOffset.Now);
}