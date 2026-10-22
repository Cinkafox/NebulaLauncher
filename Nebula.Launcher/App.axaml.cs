using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Launcher.MessageBox;
using Nebula.Launcher.Services;
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
        var services = new ServiceCollection();
        
        if (!Program.IsNewInstance)
        {
            IMessageContainerProvider? provider = null;
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    desktop.MainWindow = (Window)(provider = new MessageWindow());
                    break;
                case ISingleViewApplicationLifetime singleViewPlatform:
                    singleViewPlatform.MainView = (Control)(provider = new MessageView());
                    break;
            }

            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<FileService>();
            services.AddSingleton<DebugService>();
            services.AddSingleton<LocalizationService>();
            
            var serviceProvider = services.BuildServiceProvider();
            var localizationService = serviceProvider.GetRequiredService<LocalizationService>();
            localizationService.LoadCurrentLanguage();
            
            provider?.ShowMessage(
                LocalizationService.GetString("error-app-already-running-message"), 
                LocalizationService.GetString("error-app-already-running-title"));
            
            return;
        }
        
        if (Design.IsDesignMode)
        {
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
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
           
            services.AddAvaloniaServices();
            services.AddServices();
            services.AddViews();

            var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetRequiredService<LocalizationService>().LoadCurrentLanguage();
            
            switch (ApplicationLifetime)
            {
                case IClassicDesktopStyleApplicationLifetime desktop:
                    desktop.MainWindow = serviceProvider.GetService<MainWindow>();
                    serviceProvider.GetRequiredService<FileFormService>().SetMainWindow(desktop.MainWindow!);
                    break;
                case ISingleViewApplicationLifetime singleViewPlatform:
                    singleViewPlatform.MainView = serviceProvider.GetRequiredService<MainView>();
                    break;
            }
        }
        

        base.OnFrameworkInitializationCompleted();
    }
}