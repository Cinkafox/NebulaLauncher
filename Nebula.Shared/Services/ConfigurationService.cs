using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Services.Logging;
using Robust.LoaderApi;

namespace Nebula.Shared.Services;

public class ConVar<T>
{
    internal ConfigurationService.OnConfigurationChangedDelegate<T?>? OnValueChanged;
    
    public ConVar(string name, T? defaultValue = default)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public Type Type => typeof(T);
    public T? DefaultValue { get; }
}

public static class ConVarBuilder
{
    public static ConVar<T> Build<T>(string name, T? defaultValue = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("ConVar name cannot be null or whitespace.", nameof(name));

        return new ConVar<T>(name, defaultValue);
    }
}

[ServiceRegister]
public class ConfigurationService
{
    public delegate void OnConfigurationChangedDelegate<in T>(T value);
    
    public IReadWriteFileApi ConfigurationApi { get; init; }
    
    private readonly ILogger _logger;

    public ConfigurationService(FileService fileService, DebugService debugService)
    {
        _logger = debugService.GetLogger(this);
        ConfigurationApi = fileService.CreateFileApi("config");
    }

    public ConfigChangeSubscriberDisposable<T> SubscribeVarChanged<T>(ConVar<T> convar, OnConfigurationChangedDelegate<T?> @delegate, bool invokeNow = false)
    {
        convar.OnValueChanged += @delegate;
        if (invokeNow)
        {
            @delegate(GetConfigValue(convar));
        }
        
        return new ConfigChangeSubscriberDisposable<T>(convar, @delegate);
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

    public void SetConfigValue<T>(ConVar<T> conVar, T value)
    {
        if (value == null)
        {
            ConfigurationApi.Remove(GetFileName(conVar));
            conVar.OnValueChanged?.Invoke(conVar.DefaultValue);
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

public sealed class ConfigChangeSubscriberDisposable<T> : IDisposable
{
    private readonly ConVar<T> _convar;
    private readonly ConfigurationService.OnConfigurationChangedDelegate<T> _delegate;

    public ConfigChangeSubscriberDisposable(ConVar<T> convar, ConfigurationService.OnConfigurationChangedDelegate<T> @delegate)
    {
        _convar = convar;
        _delegate = @delegate;
    }
    public void Dispose()
    {
        _convar.OnValueChanged -= _delegate;
    }
}