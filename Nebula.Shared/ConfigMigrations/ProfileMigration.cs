using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Shared.ConfigMigrations;

public class ProfileMigrationV2(string oldName, string newName)
    : BaseConfigurationMigration<ProfileAuthCredentialsV2[], AuthTokenCredentials[]>(oldName, newName)
{
    protected override async Task<AuthTokenCredentials[]> Migrate(IServiceProvider serviceProvider, ProfileAuthCredentialsV2[] oldValue, ILoadingHandler loadingHandler)
    {
        loadingHandler.SetLoadingMessage("Migrating Profile V2 -> V3");
        var list = new List<AuthTokenCredentials>();
        var authService = serviceProvider.GetRequiredService<AuthService>();
        var logger = serviceProvider.GetRequiredService<DebugService>().GetLogger("ProfileMigrationV2");
        foreach (var oldCredentials in oldValue)
        {
            try
            {
                loadingHandler.SetLoadingMessage($"Migrating {oldCredentials.Login}");
                var newCred = await authService.Auth(oldCredentials.Login, oldCredentials.Password, oldCredentials.AuthServer);
                list.Add(newCred);
            }
            catch (Exception e)
            {
                logger.Error(e);
                loadingHandler.SetLoadingMessage(e.Message);
            }
        }

        loadingHandler.SetLoadingMessage("Migration done!");
        return list.ToArray();
    }
}

public sealed record ProfileAuthCredentialsV2(
    string Login,
    string Password,
    string AuthServer);