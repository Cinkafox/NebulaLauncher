using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform;
using Nebula.Launcher.ViewModels.Pages;
using Color = System.Drawing.Color;

namespace Nebula.Launcher.Converters;

public static class TypeConverters
{
    public static FuncValueConverter<string, string?> IconConverter { get; } =
        new(iconKey =>
        {
            if (iconKey == null) return null;
            return $"/Assets/svg/{iconKey}.svg";
        });
    
    public static FuncValueConverter<string, IImage?> ImageConverter { get; } =
        new(iconKey =>
        {
            if (iconKey == null) return null;
            return new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri($"avares://Nebula.Launcher/Assets/error_presentation/{iconKey}.png")));
        });

    public static FuncValueConverter<string, Avalonia.Media.Color> NameColorRepresentation { get; } =
        new((str)=>ColorUtils.GetColorFromString(str ?? throw new ArgumentNullException(nameof(str),"Name of color is null!")));
    
    public static FuncValueConverter<string, bool> StringIsNotEmpty { get; } =
        new(iconKey => !string.IsNullOrEmpty(iconKey));
}