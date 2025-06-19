using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Nebula.Shared;

namespace Nebula.Launcher.Services;


public static class ExplorerHelper
{
    public static void OpenFolder(string path)
    {
        string command;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            command = "explorer.exe";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            command = "xdg-open";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            command = "open";
        else
            throw new PlatformNotSupportedException("Unsupported OS platform");
        

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = path,
            UseShellExecute = false
        };

        Process.Start(startInfo);
    }
}