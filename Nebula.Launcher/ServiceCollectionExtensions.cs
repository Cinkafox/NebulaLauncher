using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.Views;
using Nebula.Shared.ViewHelper;

namespace Nebula.Launcher;

public static class ServiceCollectionExtensions
{
    public static void AddAvaloniaServices(this IServiceCollection services)
    {
        services.AddSingleton<IDispatcher>(_ => Dispatcher.UIThread);
        services.AddSingleton(_ =>
            Application.Current?.ApplicationLifetime ??
            throw new InvalidOperationException("No application lifetime is set"));

        services.AddSingleton(sp =>
            sp.GetRequiredService<IApplicationLifetime>() switch
            {
                IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow ??
                                                                   throw new InvalidOperationException(
                                                                       "No main window set"),
                ISingleViewApplicationLifetime singleViewPlatform =>
                    TopLevel.GetTopLevel(singleViewPlatform.MainView) ??
                    throw new InvalidOperationException("Could not find top level element for single view"),
                _ => throw new InvalidOperationException($"Could not find {nameof(TopLevel)} element")
            }
        );

        services.AddSingleton(sp => sp.GetRequiredService<TopLevel>().StorageProvider);
    }

    public static void AddViews(this IServiceCollection services)
    {
        services.AddTransient<MainWindow>();

        foreach (var (viewModel, view, isSingleton) in GetTypesWithHelpAttribute(Assembly.GetExecutingAssembly()))
        {
            if (isSingleton)
            {
                services.AddSingleton(viewModel);
                if (view != null) services.AddSingleton(view);
            }
            else
            {
                services.AddTransient(viewModel);
                if (view != null) services.AddTransient(view);
            }
            
        }
    }

    private static IEnumerable<(Type, Type?, bool)> GetTypesWithHelpAttribute(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var attr = type.GetCustomAttribute<ViewModelRegisterAttribute>();
            if (attr is not null) yield return (type, attr.Type, attr.IsSingleton);
        }
    }
}