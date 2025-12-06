using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Robust.LoaderApi;

namespace Nebula.Shared.FileApis;

public class HashApi : IFileApi
{
    private readonly IReadWriteFileApi _fileApi;
    private readonly Dictionary<string, RobustManifestItem> _manifest;
    public IReadOnlyDictionary<string, RobustManifestItem> Manifest => _manifest;

    public HashApi(List<RobustManifestItem> manifest, IReadWriteFileApi fileApi)
    {
        _fileApi = fileApi;
        _manifest = new Dictionary<string, RobustManifestItem>();
        foreach (var item in manifest) _manifest.TryAdd(item.Path, item);
    }

    public bool TryOpen(string path,[NotNullWhen(true)] out Stream? stream)
    {
        if (path[0] == '/') path = path.Substring(1);

        if (_manifest.TryGetValue(path, out var a) && _fileApi.TryOpen(GetManifestPath(a), out stream))
            return true;

        stream = null;
        return false;
    }

    public bool TryOpen(RobustManifestItem item ,[NotNullWhen(true)] out Stream? stream){
        if(_fileApi.TryOpen(GetManifestPath(item), out stream))
            return true;

        stream = null;
        return false;
    }

    public bool TryOpenByHash(string hash ,[NotNullWhen(true)] out Stream? stream){
        if(_fileApi.TryOpen(GetManifestPath(hash), out stream))
            return true;

        stream = null;
        return false;
    }

    public bool Save(RobustManifestItem item, Stream stream, ILoadingHandler? loadingHandler){
        return _fileApi.Save(GetManifestPath(item), stream, loadingHandler);
    }

    public bool Has(RobustManifestItem item){
        return _fileApi.Has(GetManifestPath(item));
    }

    public bool Remove(RobustManifestItem item)
    {
        return _fileApi.Remove(GetManifestPath(item));
    }

    private string GetManifestPath(RobustManifestItem item){
        return GetManifestPath(item.Hash);
    }

    public static string GetManifestPath(string hash){
        return hash[0].ToString() + hash[1].ToString() + '/' + hash;
    }

    public IEnumerable<string> AllFiles => _manifest.Keys;
}