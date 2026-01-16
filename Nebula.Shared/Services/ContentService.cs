using Nebula.Shared.Models;
using Nebula.Shared.Services.Logging;

namespace Nebula.Shared.Services;

[ServiceRegister]
public partial class ContentService(
    RestService restService,
    DebugService debugService,
    ConfigurationService varService,
    FileService fileService)
{
    private readonly HttpClient _http = new();
    private readonly ILogger _logger = debugService.GetLogger("ContentService");

    public async Task<RobustBuildInfo> GetBuildInfo(RobustUrl url, CancellationToken cancellationToken)
    {
        var info = new RobustBuildInfo();
        info.Url = url;
        var bi = await restService.GetAsync<ServerInfo>(url.InfoUri, cancellationToken);
        info.BuildInfo = bi;

        if (info.BuildInfo.Build.Acz is null)
        {
            info.DownloadUri = new RobustZipContentInfo(new Uri(info.BuildInfo.Build.DownloadUrl), info.BuildInfo.Build.Hash);
            return info;
        }
        
        info.RobustManifestInfo = info.BuildInfo.Build.Acz.Value
            ? new RobustManifestInfo(new RobustPath(info.Url, "manifest.txt"), new RobustPath(info.Url, "download"),
                bi.Build.ManifestHash)
            : new RobustManifestInfo(new Uri(info.BuildInfo.Build.ManifestUrl),
                new Uri(info.BuildInfo.Build.ManifestDownloadUrl), bi.Build.ManifestHash);

        return info;
    }

    public void RemoveAllContent(ILoadingHandler loadingHandler, CancellationToken cancellationToken)
    {
        fileService.RemoveAllFiles("content", loadingHandler, cancellationToken);
        fileService.RemoveAllFiles("manifest", loadingHandler, cancellationToken);
    }
}