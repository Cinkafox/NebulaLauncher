using System.ComponentModel;
using System.Runtime.CompilerServices;
using Nebula.Shared.Services;

namespace Nebula.Shared.Configurations;

public sealed class ConVarObserver<T> : IDisposable, INotifyPropertyChanged, INotifyPropertyChanging
{
    private readonly ConVar<T> _convar;
    private readonly ConfigurationService _configurationService;
    
    private T? _value;
    private ConfigurationService.OnConfigurationChangedDelegate<T> _delegate;

    public bool HasValue => Value != null;

    public T? Value
    {
        get => _value; 
        set => _configurationService.SetConfigValue(_convar, value);
    }

    public ConVarObserver(ConVar<T> convar, ConfigurationService configurationService)
    {
        _convar = convar;
        _convar.OnValueChanged += OnValueChanged;
        _configurationService = configurationService;
        _delegate += OnValueChanged;
        
        OnValueChanged(configurationService.GetConfigValue(_convar));
    }

    private void OnValueChanged(T? value)
    {
        OnPropertyChanging(nameof(Value));
        OnPropertyChanging(nameof(HasValue));
        
        if(value is null && _value is null)
            return;
        if (_value is not null && _value.Equals(value))
            return;
        
        _value = value;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(HasValue));
    }

    public void Dispose()
    {
        _convar.OnValueChanged -= OnValueChanged;
    }
    
    public static implicit operator T? (ConVarObserver<T> convar) => convar.Value;

    public event PropertyChangingEventHandler? PropertyChanging;
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    private void OnPropertyChanging([CallerMemberName] string? propertyName = null)
    {
        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
    }
    
}