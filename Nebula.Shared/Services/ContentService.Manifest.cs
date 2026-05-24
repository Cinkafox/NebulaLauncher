using Nebula.Shared.FileApis;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Nebula.Shared.Utils;

namespace Nebula.Shared.Services;

public sealed partial class ContentService
{
    public readonly IReadWriteFileApi ContentFileApi = fileService.CreateFileApi("content");
    public readonly IReadWriteFileApi ManifestFileApi = fileService.CreateFileApi("manifest");
    
    public void SetServerHash(string address, string hash)
    {
        var dict = varService.GetConfigValue(CurrentConVar.ServerManifestHash)!;
        if (dict.TryGetValue(address, out var oldHash))
        {
            if(oldHash == hash) return;
            
            ManifestFileApi.Remove(oldHash);
        }
        
        dict[address] = hash;
        varService.SetConfigValue(CurrentConVar.ServerManifestHash, dict);
    }
    
    public HashApi CreateHashApi(List<RobustManifestItem> manifestItems, Uri downloadUri)
    {
        return new HashApi(manifestItems, ContentFileApi, downloadUri);
    }
    
    private async Task<List<RobustManifestItem>> GetManifest(RobustManifestInfo info, ILoadingHandlerFactory loadingFactory,
        CancellationToken cancellationToken)
    {
        _logger.Log("Getting manifest: " + info.Hash);
        var loadingHandler = loadingFactory.CreateLoadingContext(new FileLoadingFormater());
        loadingHandler.SetLoadingMessage("Loading manifest");

        if (ManifestFileApi.TryOpen(info.Hash, out var stream))
        {
            _logger.Log("Loading manifest from disk");
            loadingHandler.Dispose();
            var list = new ManifestReader(stream).ReadAllItems(cancellationToken);
            await stream.DisposeAsync();
            return list;
        }
        
        SetServerHash(info.ManifestUri.ToString(), info.Hash);

        _logger.Log("Fetching manifest from: " + info.ManifestUri);
        loadingHandler.SetLoadingMessage("Fetching manifest from: " + info.ManifestUri.Host);

        var response = await _http.GetAsync(info.ManifestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
    
        loadingHandler.SetJobsCount(response.Content.Headers.ContentLength ?? 0);
        var streamContent = await response.Content.ReadAsStreamAsync(cancellationToken);
        ManifestFileApi.Save(info.Hash, streamContent, loadingHandler);
        loadingHandler.Dispose();
        streamContent.Seek(0, SeekOrigin.Begin);
        
        var listContent = new ManifestReader(streamContent).ReadAllItems(cancellationToken);
        await streamContent.DisposeAsync();
        
        return listContent;
    }
}