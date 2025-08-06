using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Shared.Configurations.Migrations;

public class MigrationQueue(List<IConfigurationMigration> migrations) : IConfigurationMigration
{
    public async Task DoMigrate(ConfigurationService configurationService, IServiceProvider serviceProvider , ILoadingHandler loadingHandler)
    {
        foreach (var migration in migrations)
        {
            await migration.DoMigrate(configurationService, serviceProvider, loadingHandler);
        }
    }
}