using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Nebula.Launcher.ViewModels;
using Nebula.Shared;

namespace Nebula.Launcher.Services;

[ServiceRegister, ConstructGenerator]
public sealed partial class ViewHelperService
{
    [GenerateProperty] private IServiceProvider ServiceProvider { get; } = default!;
    
    public bool TryGetViewModel(Type type, [NotNullWhen(true)] out ViewModelBase? viewModelBase)
    {
        viewModelBase = null;
        var vm = Design.IsDesignMode
            ? Activator.CreateInstance(type)
            : ServiceProvider.GetService(type);

        if (vm is not ViewModelBase vmb) return false;

        viewModelBase = vmb;
        return true;
    }
    
    public bool TryGetViewModel<T>(Type type, [NotNullWhen(true)] out T? viewModelBase) where T : ViewModelBase
    {
        var success = TryGetViewModel(type, out var vmb);
        viewModelBase = (T?)vmb;
        return success;
    }

    public bool TryGetViewModel<T>([NotNullWhen(true)] out T? viewModelBase) where T : ViewModelBase
    {
        var success = TryGetViewModel(typeof(T), out var vmb);
        viewModelBase = (T?)vmb;
        return success;
    }

    public T GetViewModel<T>() where T : ViewModelBase
    {
        TryGetViewModel<T>(out var viewModelBase);
        return viewModelBase!;
    }

    private void Initialise(){}
    private void InitialiseInDesignMode(){}
}