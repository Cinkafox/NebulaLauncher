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
    public Uri DownloadUri { get; private set; }

    public HashApi(List<RobustManifestItem> manifest, IReadWriteFileApi fileApi, Uri downloadUri)
    {
        _fileApi = fileApi;
        DownloadUri = downloadUri;
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

    public bool Has(string path)
    {
        return _manifest.TryGetValue(path, out var item) && Has(item);
    }

    public bool FileDownloaded(string path)
    {
        if(!_manifest.TryGetValue(path, out var item))
            throw new FileNotFoundException(path);
        
        return Has(item);
    }

    public IEnumerable<RobustManifestItem> GetMissingFiles()
    {
        foreach (var item in _manifest.Values)
        {
            if(Has(item)) 
                continue;
            
            yield return item;
        }
    }

    public HashApiFilter GetFilter()
    {
        return new HashApiFilter(this);
    }

    public IEnumerable<string> AllFiles => _manifest.Keys;
}


public class HashApiFilter
{
    private readonly HashApi _api;
    private List<RobustManifestItem> _filtered;

    public HashApiFilter(HashApi filter)
    {
        _api = filter;
        _filtered = _api.Manifest.Values.ToList();
    }
    
    private HashApiFilter(HashApi filter, List<RobustManifestItem> filtered)
    {
        _api = filter;
        _filtered = filtered;
    }

    public HashApiFilter WithMissingFiles()
    {
        var newFiltered = new List<RobustManifestItem>();
        foreach (var item in _filtered)
        {
            if( _api.Has(item)) 
                continue;
            
            newFiltered.Add(item);
        }
        return new HashApiFilter(_api, newFiltered);
    }

    public HashApiFilter WithExt(string ext)
    {
        var newFiltered = new List<RobustManifestItem>();
        foreach (var item in _filtered)
        {
            if (Path.GetExtension(item.Path) != ext) 
                continue;
            
            newFiltered.Add(item);
        }
        return new HashApiFilter(_api, newFiltered);
    }

    public List<RobustManifestItem> GetFiltered()
    {
        return _filtered;
    }
}