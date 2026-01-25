using System.Runtime.InteropServices;

namespace Nebula.SharedModels;

public static class AppDataPath
{
    public static string RootPath { get; private set; } = GetAppDataPath("Datum");

    public static void SetTestRootPath(string rootPath)
    {
        Console.WriteLine($"REWRITE ROOT PATH TO {rootPath}");
        RootPath = rootPath;
    }
    
    public static string GetAppDataPath(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("appName cannot be null or empty.", nameof(appName));

        string basePath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Application Support"
            );
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            basePath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                       ?? Path.Combine(
                           Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                           ".config"
                       );
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported operating system.");
        }

        return Path.Combine(basePath, appName);
    }
}

public static class UrlValidator
{
    public static bool IsInDomainUrl(string url, string allowedDomain)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        
        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;
        
        var host = uri.Host.ToLowerInvariant();
        return host == allowedDomain || host.EndsWith("." + allowedDomain);
    }

    public static void EnsureDomainValid(string url, string allowedDomain)
    {
        if(!IsInDomainUrl(url, allowedDomain))
            throw new InvalidOperationException($"URL {url} is not in domain {allowedDomain}.");
    }
}

public static class DotnetUrlHelper
{
    public static string GetExtension()
    {
        return OperatingSystem.IsWindows() ? ".exe" : string.Empty;
    }
    public static string GetCurrentPlatformDotnetUrl(Dictionary<string, string> dotnetUrl)
    {
        var rid = GetRuntimeIdentifier();

        if (dotnetUrl.TryGetValue(rid, out var url)) return url;

        throw new PlatformNotSupportedException($"No download URL available for the current platform: {rid}");
    }

    public static string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => throw new PlatformNotSupportedException($"Unsupported Windows architecture: {RuntimeInformation.ProcessArchitecture}")
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.X86 => "linux-x86",
                Architecture.Arm => "linux-arm",
                Architecture.Arm64 => "linux-arm64",
                _ => throw new PlatformNotSupportedException($"Unsupported Linux architecture: {RuntimeInformation.ProcessArchitecture}")
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => throw new PlatformNotSupportedException($"Unsupported macOS architecture: {RuntimeInformation.ProcessArchitecture}")
            };
        }

        throw new PlatformNotSupportedException($"Unsupported operating system: {RuntimeInformation.OSDescription}");
    }
}