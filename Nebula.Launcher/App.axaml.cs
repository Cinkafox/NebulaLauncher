using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.MessageBox;
using Nebula.Launcher.Views;
using Nebula.Shared;
using Nebula.Shared.Services;

namespace Nebula.Launcher;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (!Program.IsNewInstance)
        {
            IMessageContainerProvider? provider = null;
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    DisableAvaloniaDataAnnotationValidation();
                    desktop.MainWindow = (Window)(provider = new MessageWindow());
                    break;
                case ISingleViewApplicationLifetime singleViewPlatform:
                    singleViewPlatform.MainView = (Control)(provider = new MessageView());
                    break;
            }
            
            provider?.ShowMessage(
                "Error: An instance of the application is already running. Please close the existing instance before launching a new one.", 
                "Duplicate instance detected.");
            
            return;
        }
        
        if (Design.IsDesignMode)
        {
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    DisableAvaloniaDataAnnotationValidation();
                    desktop.MainWindow = new MainWindow();
                    break;
                case ISingleViewApplicationLifetime singleViewPlatform:
                    singleViewPlatform.MainView = new MainView();
                    break;
            }
        }
        else
        {
            DebugService.DoFileLog = true;
            var services = new ServiceCollection();
            services.AddAvaloniaServices();
            services.AddServices();
            services.AddViews();

            var serviceProvider = services.BuildServiceProvider();
            
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    DisableAvaloniaDataAnnotationValidation();
                    desktop.MainWindow = serviceProvider.GetService<MainWindow>();
                    break;
                case ISingleViewApplicationLifetime singleViewPlatform:
                    singleViewPlatform.MainView = serviceProvider.GetRequiredService<MainView>();
                    break;
            }
        }
        

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) BindingPlugins.DataValidators.Remove(plugin);
    }
}