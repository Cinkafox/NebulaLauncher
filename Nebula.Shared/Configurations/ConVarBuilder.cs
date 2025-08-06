using Nebula.Shared.Configurations.Migrations;
using Nebula.Shared.Services;

namespace Nebula.Shared.Configurations;

public static class ConVarBuilder
{
    public static ConVar<T> Build<T>(string name, T? defaultValue = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("ConVar name cannot be null or whitespace.", nameof(name));

        return new ConVar<T>(name, defaultValue);
    }

    public static ConVar<T> BuildWithMigration<T>(string name, IConfigurationMigration migration, T? defaultValue = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("ConVar name cannot be null or whitespace.", nameof(name));
        
        ConfigurationService.AddConfigurationMigration(migration);

        return new ConVar<T>(name, defaultValue);
    }
}