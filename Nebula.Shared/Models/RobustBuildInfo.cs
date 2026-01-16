namespace Nebula.Shared.Models;

public class RobustBuildInfo
{
    public ServerInfo BuildInfo = default!;
    public RobustManifestInfo? RobustManifestInfo;
    public RobustZipContentInfo? DownloadUri;
    public RobustUrl Url = default!;
}