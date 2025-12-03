using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Nebula.Shared.Utils;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class DotnetResolverService(DebugService debugService, ConfigurationService configurationService)
{
    private static readonly string FullPath =
        Path.Join(FileService.RootPath, "dotnet", DotnetUrlHelper.GetRuntimeIdentifier());

    private static readonly string ExecutePath = Path.Join(FullPath, "dotnet" + DotnetUrlHelper.GetExtension());
    private readonly HttpClient _httpClient = new();

    public async Task<string> EnsureDotnet()
    {
        if (!Directory.Exists(FullPath))
            await Download();

        return ExecutePath;
    }

    private async Task Download()
    {
        var debugLogger = debugService.GetLogger(this);
        debugLogger.Log($"Downloading dotnet {DotnetUrlHelper.GetRuntimeIdentifier()}...");

        var url = DotnetUrlHelper.GetCurrentPlatformDotnetUrl(
            configurationService.GetConfigValue(CurrentConVar.DotnetUrl)!
        );

        using var response = await _httpClient.GetAsync(url);
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

        debugLogger.Log("Downloading dotnet complete.");
    }
}

public static class DotnetUrlHelper
{
    [Obsolete("FOR TEST USING ONLY!")]
    public static string? RidOverrideTest = null; // FOR TEST PURPOSES ONLY!!!
    
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
        if(RidOverrideTest != null) return RidOverrideTest;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.Is64BitProcess ? "win-x64" : "win-x86";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux-x64";

        throw new PlatformNotSupportedException("Unsupported operating system");
    }
}