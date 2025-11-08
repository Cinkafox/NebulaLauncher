using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared.Configurations.Migrations;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Nebula.Shared.Utils;

namespace Nebula.Shared.ConfigMigrations;

public class ProfileMigrationV2(string oldName, string newName)
    : BaseConfigurationMigration<ProfileAuthCredentials[], string[]>(oldName, newName)
{
    protected override async Task<string[]> Migrate(IServiceProvider serviceProvider, ProfileAuthCredentials[] oldValue, ILoadingHandler loadingHandler)
    {
        loadingHandler.SetLoadingMessage("Migrating Profile V2 -> V4");
        var list = new List<string>();
        var authService = serviceProvider.GetRequiredService<AuthService>();
        var logger = serviceProvider.GetRequiredService<DebugService>().GetLogger("ProfileMigrationV2");
        foreach (var oldCredentials in oldValue)
        {
            try
            {
                loadingHandler.SetLoadingMessage($"Migrating {oldCredentials.Login}");
                await authService.Auth(oldCredentials.Login, oldCredentials.Password, oldCredentials.AuthServer);
                list.Add(CryptographicStore.Encrypt(oldCredentials, CryptographicStore.GetComputerKey()));
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

public class ProfileMigrationV3V4(string oldName, string newName)
    : BaseConfigurationMigration<AuthTokenCredentials[], string[]>(oldName, newName)
{
    protected override Task<string[]> Migrate(IServiceProvider serviceProvider, AuthTokenCredentials[] oldValue, ILoadingHandler loadingHandler)
    {
        Console.WriteLine("Removing profile v3 because no password is provided");
        return Task.FromResult(Array.Empty<string>());
    }
}

