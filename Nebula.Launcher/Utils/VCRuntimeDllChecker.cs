using System;
using System.IO;

namespace Nebula.Launcher.Utils;

public static class VCRuntimeDllChecker
{
    public static bool AreVCRuntimeDllsPresent()
    {
        if (!OperatingSystem.IsWindows()) return true;
        
        string systemDir = Environment.SystemDirectory;
        string[] requiredDlls = {
            "msvcp140.dll",
            "vcruntime140.dll"
        };

        foreach (var dll in requiredDlls)
        {
            var path = Path.Combine(systemDir, dll);
            if (!File.Exists(path))
            {
                return false;
            }
        }

        return true;
    }
}