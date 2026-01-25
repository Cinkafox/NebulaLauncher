using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Nebula.Shared.Utils;
using Nebula.SharedModels;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class DotnetResolverService(DebugService debugService, ConfigurationService configurationService)
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> EnsureDotnet(CancellationToken cancellationToken = default)
    {
        var dotnetEntry = new LauncherRuntimeInfo(
            configurationService.GetConfigValue(CurrentConVar.DotnetVersion)!,
            configurationService.GetConfigValue(CurrentConVar.DotnetUrl)!
            );
        
        if (!File.Exists(dotnetEntry.GetExecutePath()))
            await Download(dotnetEntry, cancellationToken);

        return dotnetEntry.GetExecutePath();
    }

    private async Task Download(LauncherRuntimeInfo runtimeInfo, CancellationToken cancellationToken = default)
    {
        var debugLogger = debugService.GetLogger(this);
        debugLogger.Log($"Downloading dotnet {DotnetUrlHelper.GetRuntimeIdentifier()}...");

        var url = DotnetUrlHelper.GetCurrentPlatformDotnetUrl(runtimeInfo.DotnetRuntimes);
        
        var fullPath = runtimeInfo.GetFullPath();
        
        UrlValidator.EnsureDomainValid(url, "microsoft.com");

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        Directory.CreateDirectory(fullPath);

        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await using var zipArchive = new ZipArchive(stream);
            await zipArchive.ExtractToDirectoryAsync(fullPath, true, cancellationToken);
        }
        else if (url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                 || url.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            TarUtils.ExtractTarGz(stream, fullPath);
        }
        else
        {
            throw new NotSupportedException("Unsupported archive format.");
        }

        debugLogger.Log("Downloading dotnet complete.");
    }
}
