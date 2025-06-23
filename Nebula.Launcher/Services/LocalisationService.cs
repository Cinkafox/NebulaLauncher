using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Fluent.Net;
using Nebula.Shared;
using Nebula.Shared.Services;

namespace Nebula.Launcher.Services;

[ConstructGenerator, ServiceRegister]
public partial class LocalisationService
{
    [GenerateProperty] private ConfigurationService ConfigurationService { get; }

    private CultureInfo _currentCultureInfo = CultureInfo.CurrentCulture;
    private static MessageContext? _currentMessageContext;

    private void Initialise()
    {
        LoadLanguage(CultureInfo.GetCultureInfo(ConfigurationService.GetConfigValue(LauncherConVar.CurrentLang)!));
    }

    public void LoadLanguage(CultureInfo cultureInfo)
    {
        try
        {
            _currentCultureInfo = cultureInfo;
            using var fs = AssetLoader.Open(new Uri($@"avares://Nebula.Launcher/Assets/lang/{_currentCultureInfo.Name}.ftl"));
            using var sr = new StreamReader(fs);

            var options = new MessageContextOptions { UseIsolating = false };
            var mc = new MessageContext(cultureInfo.Name, options);
            var errors = mc.AddMessages(sr);
            foreach (var error in errors)
            {
                Console.WriteLine(error);
            }


            _currentMessageContext = mc;
        } catch (Exception e) {
            LoadLanguage(CultureInfo.GetCultureInfo("en-US"));
        }
    }

    private void InitialiseInDesignMode()
    {
        Initialise();
    }

    public static string GetString(string locale)
    {
        if (_currentMessageContext is null)
        {
            Console.WriteLine("ERROR SHIT BITHC!");
            return locale;
        }
        var message = _currentMessageContext.GetMessage(locale);
        if (message == null) return locale;
        return _currentMessageContext.Format(message, new Dictionary<string, object>());
    }
}

public class LocaledText : MarkupExtension
{
    public string Key { get; set; } 

    public LocaledText(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Fetch the localized string using the key
        return LocalisationService.GetString(Key);
    }
}