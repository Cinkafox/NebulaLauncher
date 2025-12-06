using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nebula.Shared.Configurations;
using Nebula.Shared.Configurations.Migrations;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Nebula.Shared.Services.Logging;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class ConfigurationService
{
    private readonly IServiceProvider _serviceProvider;
    private static List<IConfigurationMigration> _migrations = [];

    public static void AddConfigurationMigration(IConfigurationMigration configurationMigration)
    {
        _migrations.Add(configurationMigration);
    }
    
    public delegate void OnConfigurationChangedDelegate<in T>(T value);
    
    public IReadWriteFileApi ConfigurationApi { get; }
    
    private readonly ILogger _logger;

    public ConfigurationService(FileService fileService, DebugService debugService, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = debugService.GetLogger(this);
        ConfigurationApi = fileService.CreateFileApi("config");
    }

    public void MigrateConfigs(ILoadingHandler loadingHandler)
    {
        Task.Run(async () =>
        {
            foreach (var migration in _migrations)
            {
                await migration.DoMigrate(this, _serviceProvider, loadingHandler);
            }

            loadingHandler.Dispose();
        });
    }

    public ConVarObserver<T> SubscribeVarChanged<T>(ConVar<T> convar, OnConfigurationChangedDelegate<T?> @delegate, bool invokeNow = false)
    {
        convar.OnValueChanged += @delegate;
        if (invokeNow)
        {
            @delegate(GetConfigValue(convar));
        }

        var delegation = SubscribeVarChanged<T>(convar);
        delegation.PropertyChanged += (_, _) => @delegate(delegation.Value);
        return delegation;
    }

    public ConVarObserver<T> SubscribeVarChanged<T>(ConVar<T> convar)
    {
        return new ConVarObserver<T>(convar, this);
    }
    
    public T? GetConfigValue<T>(ConVar<T> conVar)
    {
        ArgumentNullException.ThrowIfNull(conVar);

        try
        {
            if (ConfigurationApi.TryOpen(GetFileName(conVar), out var stream))
                using (stream)
                {
                    var obj = JsonSerializer.Deserialize<T>(stream);
                    if (obj != null)
                    {
                        _logger.Log($"Successfully loaded config: {conVar.Name}");
                        return obj;
                    }
                }
        }
        catch (Exception e)
        {
            _logger.Error($"Error loading config for {conVar.Name}: {e.Message}");
        }

        _logger.Log($"Using default value for config: {conVar.Name}");
        return conVar.DefaultValue;
    }
    
    public bool TryGetConfigValue<T>(ConVar<T> conVar,
        [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(conVar);
        value = default;
        try
        {
            if (ConfigurationApi.TryOpen(GetFileName(conVar), out var stream))
                using (stream)
                {
                    var obj = JsonSerializer.Deserialize<T>(stream);
                    if (obj != null)
                    {
                        _logger.Log($"Successfully loaded config: {conVar.Name}");
                        value = obj;
                        return true;
                    }
                }
        }
        catch (Exception e)
        {
            _logger.Error($"Error loading config for {conVar.Name}: {e.Message}");
        }

        _logger.Log($"Using default value for config: {conVar.Name}");
        return false;
    }

    public void ClearConfigValue<T>(ConVar<T> conVar)
    {
        ConfigurationApi.Remove(GetFileName(conVar));
        conVar.OnValueChanged?.Invoke(conVar.DefaultValue);
    }

    public void SetConfigValue<T>(ConVar<T> conVar, T? value)
    {
        if (value == null)
        {
            ClearConfigValue(conVar);
            return;
        }

        if (!conVar.Type.IsInstanceOfType(value))
        {
            _logger.Error(
                $"Type mismatch for config {conVar.Name}. Expected {conVar.Type}, got {value.GetType()}.");
            return;
        }

        try
        {
            _logger.Log($"Saving config: {conVar.Name}");
            var serializedData = JsonSerializer.Serialize(value);

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            writer.Write(serializedData);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            ConfigurationApi.Save(GetFileName(conVar), stream);
            conVar.OnValueChanged?.Invoke(value);
        }
        catch (Exception e)
        {
            _logger.Error($"Error saving config for {conVar.Name}: {e.Message}");
        }
    }

    private static string GetFileName<T>(ConVar<T> conVar)
    {
        return $"{conVar.Name}.json";
    }
}