using System;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media;

namespace Nebula.Launcher.ViewModels.Pages;

public static class ColorUtils
{
    public static Color GetColorFromString(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        
        var r = byte.Clamp(hash[0], 10, 200);
        var g = byte.Clamp(hash[1], 10, 100);
        var b = byte.Clamp(hash[2], 10, 100);

        return Color.FromArgb(Byte.MaxValue, r, g, b);
    }
}