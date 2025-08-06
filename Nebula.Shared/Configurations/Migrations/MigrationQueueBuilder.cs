namespace Nebula.Shared.Configurations.Migrations;

public class MigrationQueueBuilder
{
    public static MigrationQueueBuilder Instance => new();
    
    private readonly List<IConfigurationMigration> _migrations = [];

    public MigrationQueueBuilder With(IConfigurationMigration migration)
    {
        _migrations.Add(migration);
        return this;
    }

    public MigrationQueue Build()
    {
        return new MigrationQueue(_migrations);
    }
}