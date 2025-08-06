using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nebula.Shared.Configurations;

public abstract class ComplexConVarBinder<T> : INotifyPropertyChanged, INotifyPropertyChanging
{
    private readonly ConVarObserver<T> _baseConVar;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _valueChangeSemaphore = new(1, 1);

    public T? Value
    {
        get
        {
            lock (_lock)
            {
                return _baseConVar.Value;
            }
        }
        set
        {
            _ = SetValueAsync(value);
        }
    }

    protected ComplexConVarBinder(ConVarObserver<T> baseConVar)
    {
        _baseConVar = baseConVar ?? throw new ArgumentNullException(nameof(baseConVar));
        _baseConVar.PropertyChanged += BaseConVarOnPropertyChanged;
        _baseConVar.PropertyChanging += BaseConVarOnPropertyChanging;
    }
    

    private async Task SetValueAsync(T? value)
    {
        await _valueChangeSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            var newValue = await OnValueChange(value).ConfigureAwait(false);

            lock (_lock)
            {
                _baseConVar.Value = newValue;
            }
        }
        finally
        {
            _valueChangeSemaphore.Release();
        }
    }

    protected abstract Task<T?> OnValueChange(T? newValue);

    private void BaseConVarOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
    
    private void BaseConVarOnPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
    }
    

    public event PropertyChangedEventHandler? PropertyChanged;

    public event PropertyChangingEventHandler? PropertyChanging;
}
