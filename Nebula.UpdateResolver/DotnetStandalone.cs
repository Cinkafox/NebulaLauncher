using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Nebula.SharedModels;
using Nebula.UpdateResolver.Rest;

namespace Nebula.UpdateResolver;

public static class DotnetStandalone
{
    private static readonly HttpClient HttpClient = new();

    private static string GetExecutePath(LauncherRuntimeInfo runtimeInfo)
    {
        return Path.Join(MainWindow.RootPath, 
            $"dotnet.{runtimeInfo.RuntimeVersion}",
            DotnetUrlHelper.GetRuntimeIdentifier(),
            $"dotnet{DotnetUrlHelper.GetExtension()}");
    }

    public static async Task<Process?> Run(LauncherRuntimeInfo runtimeInfo, string dllPath)
    {
        await EnsureDotnet(runtimeInfo);

        return Process.Start(new ProcessStartInfo
        {
            FileName = GetExecutePath(runtimeInfo),
            Arguments = dllPath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        });
    }

    private static async Task EnsureDotnet(LauncherRuntimeInfo runtimeInfo)
    {
        if (!Directory.Exists(GetExecutePath(runtimeInfo)))
            await Download(runtimeInfo);
    }

    private static async Task Download(LauncherRuntimeInfo runtimeInfo)
    {
        LogStandalone.Log($"Downloading dotnet {DotnetUrlHelper.GetRuntimeIdentifier()}...");

        var fullPath = GetExecutePath(runtimeInfo);

        var url = DotnetUrlHelper.GetCurrentPlatformDotnetUrl(runtimeInfo.DotnetRuntimes);

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync();
        await using var tempStream = new MemoryStream();
        stream.CopyTo(tempStream,"dotnet", response.Content.Headers.ContentLength ?? 0);
        await stream.DisposeAsync();

        Directory.CreateDirectory(fullPath);

        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await using var zipArchive = new ZipArchive(tempStream);
            await zipArchive.ExtractToDirectoryAsync(fullPath, true);
        }
        else if (url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                 || url.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            TarUtils.ExtractTarGz(tempStream, fullPath);
        }
        else
        {
            throw new NotSupportedException("Unsupported archive format.");
        }

        LogStandalone.Log("Downloading dotnet complete.");
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