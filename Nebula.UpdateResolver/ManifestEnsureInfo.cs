using System.Collections.Generic;
using Nebula.SharedModels;

namespace Nebula.UpdateResolver;

public record struct ManifestEnsureInfo(HashSet<LauncherManifestEntry> ToDownload, HashSet<LauncherManifestEntry> ToDelete, HashSet<LauncherManifestEntry> FilesExist);