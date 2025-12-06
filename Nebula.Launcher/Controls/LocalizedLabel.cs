using Avalonia;
using Avalonia.Controls;
using Nebula.Launcher.Services;

namespace Nebula.Launcher.Controls;

public class LocalizedLabel : Label
{
    public static readonly StyledProperty<string> LocalIdProperty = AvaloniaProperty.Register<LocalizedLabel, string>(nameof(LocalId));

    public string LocalId
    {
        get => GetValue(LocalIdProperty);
        set
        {
            SetValue(LocalIdProperty, value);
            Content = LocalizationService.GetString(value);
        }
    }
}