namespace Nebula.Shared.Models;

public record struct RobustManifestInfo(Uri ManifestUri, Uri DownloadUri, string Hash);
public record struct RobustZipContentInfo(Uri DownloadUri, string Hash);