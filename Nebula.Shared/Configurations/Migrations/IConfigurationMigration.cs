using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Shared.Configurations.Migrations;

public interface IConfigurationMigration
{
    public Task DoMigrate(ConfigurationService configurationService, IServiceProvider serviceProvider, ILoadingHandler loadingHandler);
}