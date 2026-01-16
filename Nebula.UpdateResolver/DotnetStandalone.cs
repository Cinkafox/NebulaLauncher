using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Nebula.UpdateResolver.Configuration;

namespace Nebula.UpdateResolver;

public static class DotnetStandalone
{
    private static readonly HttpClient HttpClient = new();

    private static readonly string FullPath =
        Path.Join(MainWindow.RootPath, $"dotnet.{ConfigurationStandalone.GetConfigValue(UpdateConVars.DotnetVersion)}", 
            DotnetUrlHelper.GetRuntimeIdentifier());

    private static readonly string ExecutePath = Path.Join(FullPath, "dotnet" + DotnetUrlHelper.GetExtension());

    public static async Task<Process?> Run(string dllPath)
    {
        await EnsureDotnet();

        return Process.Start(new ProcessStartInfo
        {
            FileName = ExecutePath,
            Arguments = dllPath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        });
    }

    private static async Task EnsureDotnet()
    {
        if (!Directory.Exists(FullPath))
            await Download();
    }

    private static async Task Download()
    {
        LogStandalone.Log($"Downloading dotnet {DotnetUrlHelper.GetRuntimeIdentifier()}...");

        var url = DotnetUrlHelper.GetCurrentPlatformDotnetUrl(
            ConfigurationStandalone.GetConfigValue(UpdateConVars.DotnetUrl)!
        );

        using var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();

        Directory.CreateDirectory(FullPath);

        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zipArchive = new ZipArchive(stream);
            zipArchive.ExtractToDirectory(FullPath, true);
        }
        else if (url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                 || url.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            TarUtils.ExtractTarGz(stream, FullPath);
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
        if (OperatingSystem.IsWindows()) return ".exe";
        return "";
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
            return Environment.Is64BitProcess ? "win-x64" : "win-x86";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux-x64";

        throw new PlatformNotSupportedException("Unsupported operating system");
    }
}