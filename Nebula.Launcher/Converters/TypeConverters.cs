using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Nebula.Launcher.Utils;
using Nebula.Launcher.ViewModels.Pages;
using Color = System.Drawing.Color;

namespace Nebula.Launcher.Converters;

public static class TypeConverters
{
    public static FuncValueConverter<string, IImage?> IconConverter { get; } =
        new(iconKey =>
        {
            if (iconKey == null) return null;
            return new Bitmap(AssetLoader.Open(new Uri($"avares://Nebula.Launcher/Assets/icon/{iconKey}.png")));
        });
    
    public static FuncValueConverter<string, IImage?> ImageConverter { get; } =
        new(iconKey =>
        {
            if (iconKey == null) return null;
            return new Bitmap(AssetLoader.Open(new Uri($"avares://Nebula.Launcher{iconKey}")));
        });
    
    public static FuncValueConverter<string, Avalonia.Media.Color> NameColorRepresentation { get; } =
        new((str)=>ColorUtils.GetColorFromString(str ?? throw new ArgumentNullException(nameof(str),"Name of color is null!")));
    
    public static FuncValueConverter<string, bool> StringIsNotEmpty { get; } =
        new(iconKey => !string.IsNullOrEmpty(iconKey));

    public static FuncValueConverter<bool, int> BoolToRowSpan { get; } =
        new(isTrue => isTrue ? 2 : 3);
}