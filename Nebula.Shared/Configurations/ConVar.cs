using Nebula.Shared.Services;

namespace Nebula.Shared.Configurations;

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