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

    public static async Task<Process?> Run(LauncherRuntimeInfo runtimeInfo, string dllPath)
    {
        await EnsureDotnet(runtimeInfo);

        return Process.Start(new ProcessStartInfo
        {
            FileName = runtimeInfo.GetExecutePath(),
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
        if (!File.Exists(runtimeInfo.GetExecutePath()))
            await Download(runtimeInfo);
    }

    private static async Task Download(LauncherRuntimeInfo runtimeInfo)
    {
        LogStandalone.Log($"Downloading dotnet {DotnetUrlHelper.GetRuntimeIdentifier()}...");

        var fullPath = runtimeInfo.GetFullPath();

        var url = DotnetUrlHelper.GetCurrentPlatformDotnetUrl(runtimeInfo.DotnetRuntimes);
        
        UrlValidator.EnsureDomainValid(url, "microsoft.com");

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