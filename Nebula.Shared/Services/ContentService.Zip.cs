using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Nebula.Shared.FileApis;
using Nebula.Shared.Models;
using Robust.LoaderApi;

namespace Nebula.Shared.Services;

public partial class ContentService
{
    private bool TryFromFile(IFileApi fileApi, string path, [NotNullWhen(true)] out ZipFileApi? zipFileApi)
    {
        zipFileApi = null;
        if(!fileApi.TryOpen(path, out var zipContent)) 
            return false;
        
        var zip = new ZipArchive(zipContent);
        zipFileApi = new ZipFileApi(zip, null);
        return true;
    }  
    
    private async Task<ZipFileApi> GetZipFileApi(RobustZipContentInfo info, ILoadingHandlerFactory loadingFactory, CancellationToken cancellationToken)
    {
        if (TryFromFile(ZipContentApi, info.Hash, out var zipFile))
            return zipFile;
        
        var loadingHandler = loadingFactory.CreateLoadingContext(new FileLoadingFormater());
        
        var response = await _http.GetAsync(info.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    
        loadingHandler.SetLoadingMessage("Downloading zip content");
        loadingHandler.SetJobsCount(response.Content.Headers.ContentLength ?? 0);
        await using var streamContent = await response.Content.ReadAsStreamAsync(cancellationToken);
        ZipContentApi.Save(info.Hash, streamContent, loadingHandler);
        loadingHandler.Dispose();

        if (TryFromFile(ZipContentApi, info.Hash, out zipFile)) 
            return zipFile;
        
        ZipContentApi.Remove(info.Hash);
        throw new Exception("Failed to load zip file");
    }
}