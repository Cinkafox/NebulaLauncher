using Nebula.Shared.Models;
using Nebula.Shared.Services;

namespace Nebula.Shared.Configurations.Migrations;

public abstract class BaseConfigurationMigration<T1,T2> : IConfigurationMigration
{
    protected ConVar<T1> OldConVar;
    protected ConVar<T2> NewConVar;
    
    public BaseConfigurationMigration(string oldName, string newName)
    {
        OldConVar = ConVarBuilder.Build<T1>(oldName);
        NewConVar = ConVarBuilder.Build<T2>(newName);
    }

    public async Task DoMigrate(ConfigurationService configurationService, IServiceProvider serviceProvider, ILoadingHandler loadingHandler)
    {
        var oldValue = configurationService.GetConfigValue(OldConVar);
        if(oldValue == null) return;
        
        var newValue = await Migrate(serviceProvider, oldValue, loadingHandler);
        configurationService.SetConfigValue(NewConVar, newValue);
        configurationService.ClearConfigValue(OldConVar);
    }

    protected abstract Task<T2> Migrate(IServiceProvider serviceProvider, T1 oldValue, ILoadingHandler loadingHandler);
}