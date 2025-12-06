using System.Buffers.Binary;
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using Nebula.Shared.FileApis;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Nebula.Shared.Utils;

namespace Nebula.Shared.Services;

public partial class ContentService
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
    
    public HashApi CreateHashApi(List<RobustManifestItem> manifestItems)
    {
        return new HashApi(manifestItems, ContentFileApi);
    }

    public async Task<HashApi> EnsureItems(ManifestReader manifestReader, Uri downloadUri,
        ILoadingHandlerFactory loadingFactory,
        CancellationToken cancellationToken)
    {
        List<RobustManifestItem> allItems = [];

        while (manifestReader.TryReadItem(out var item))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            
            allItems.Add(item.Value);
        }

        var hashApi = CreateHashApi(allItems);

        var items = allItems.Where(a=> !hashApi.Has(a)).ToList();
        
        _logger.Log("Download Count:" + items.Count);
        await Download(downloadUri, items, hashApi, loadingFactory, cancellationToken);

        return hashApi;
    }

    public async Task<HashApi> EnsureItems(RobustManifestInfo info, ILoadingHandlerFactory loadingFactory,
        CancellationToken cancellationToken)
    {
        _logger.Log("Getting manifest: " + info.Hash);
        var loadingHandler = loadingFactory.CreateLoadingContext(new FileLoadingFormater());
        loadingHandler.SetLoadingMessage("Loading manifest");

        if (ManifestFileApi.TryOpen(info.Hash, out var stream))
        {
            _logger.Log("Loading manifest from disk");
            loadingHandler.Dispose();
            return await EnsureItems(new ManifestReader(stream), info.DownloadUri, loadingFactory, cancellationToken);
        }
        
        SetServerHash(info.ManifestUri.ToString(), info.Hash);

        _logger.Log("Fetching manifest from: " + info.ManifestUri);
        loadingHandler.SetLoadingMessage("Fetching manifest from: " + info.ManifestUri.Host);

        var response = await _http.GetAsync(info.ManifestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
    
        loadingHandler.SetJobsCount(response.Content.Headers.ContentLength ?? 0);
        await using var streamContent = await response.Content.ReadAsStreamAsync(cancellationToken);
        ManifestFileApi.Save(info.Hash, streamContent, loadingHandler);
        loadingHandler.Dispose();
        streamContent.Seek(0, SeekOrigin.Begin);
        
        using var manifestReader = new ManifestReader(streamContent);
        return await EnsureItems(manifestReader, info.DownloadUri, loadingFactory, cancellationToken);
    }

    public void Unpack(HashApi hashApi, IWriteFileApi otherApi, ILoadingHandler loadingHandler)
    {
        _logger.Log("Unpack manifest files");
        var items = hashApi.Manifest.Values.ToList();
        loadingHandler.AppendJob(items.Count);
        
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 10
        };

        Parallel.ForEach(items, options, item =>
        {
            if (hashApi.TryOpen(item, out var stream))
            {
                _logger.Log($"Unpack {item.Hash} to: {item.Path}");
                otherApi.Save(item.Path, stream);
                stream.Close();
            }
            else
            {
                _logger.Error("Error while unpacking thinks " + item.Path);
            }

            loadingHandler.AppendResolvedJob();
        });

       loadingHandler.Dispose();
    }

    private async Task Download(Uri contentCdn, List<RobustManifestItem> toDownload, HashApi hashApi, ILoadingHandlerFactory loadingHandlerFactory,
        CancellationToken cancellationToken)
    {
        if (toDownload.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            _logger.Log("Nothing to download! Skip!");
            return;
        }
        
        _logger.Log("Downloading from: " + contentCdn);

        var requestBody = new byte[toDownload.Count * 4];
        var reqI = 0;
        foreach (var item in toDownload)
        {
            BinaryPrimitives.WriteInt32LittleEndian(requestBody.AsSpan(reqI, 4), item.Id);
            reqI += 4;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, contentCdn);
        request.Headers.Add(
            "X-Robust-Download-Protocol",
            varService.GetConfigValue(CurrentConVar.ManifestDownloadProtocolVersion)
                .ToString(CultureInfo.InvariantCulture));

        request.Content = new ByteArrayContent(requestBody);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("zstd"));
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.Log("Downloading cancelled!");
            return;
        }

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var bandwidthStream = new BandwidthStream(stream);
        stream = bandwidthStream;
        if (response.Content.Headers.ContentEncoding.Contains("zstd"))
            stream = new ZStdDecompressStream(stream);

        await using var streamDispose = stream;
        
        var streamHeader = await stream.ReadExactAsync(4, cancellationToken);
        var streamFlags = (DownloadStreamHeaderFlags)BinaryPrimitives.ReadInt32LittleEndian(streamHeader);
        var preCompressed = (streamFlags & DownloadStreamHeaderFlags.PreCompressed) != 0;
        
        var compressContext = preCompressed ? null : new ZStdCCtx();
        var decompressContext = preCompressed ? new ZStdDCtx() : null;
        
        var fileHeader = new byte[preCompressed ? 8 : 4];
        
        var mainLoadingHandler = loadingHandlerFactory.CreateLoadingContext();
        mainLoadingHandler.SetLoadingMessage("Downloading from: " + contentCdn.Host);
        
        try
        {
            var compressBuffer = new byte[1024];
            var readBuffer = new byte[1024];

            var i = 0;

            mainLoadingHandler.AppendJob(toDownload.Count);

            foreach (var item in toDownload)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (loadingHandlerFactory is IConnectionSpeedHandler speedHandler)
                    speedHandler.PasteSpeed((int)bandwidthStream.CalcCurrentAvg());

                // Read file header.
                await stream.ReadExactAsync(fileHeader, cancellationToken);

                var length = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(0, 4));
                
                var fileLoadingHandler = loadingHandlerFactory.CreateLoadingContext(new FileLoadingFormater());
                fileLoadingHandler.SetLoadingMessage(item.Path.Split("/").Last());

                var blockFileLoadHandle = length <= 100000;
                
                EnsureBuffer(ref readBuffer, length);
                var data = readBuffer.AsMemory(0, length);

                if (preCompressed)
                {
                    // Compressed length from extended header.
                    var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(4, 4));

                    if (compressedLength > 0)
                    {
                        fileLoadingHandler.AppendJob(compressedLength);
                        EnsureBuffer(ref compressBuffer, compressedLength);
                        var compressedData = compressBuffer.AsMemory(0, compressedLength);
                        await stream.ReadExactAsync(compressedData, cancellationToken, blockFileLoadHandle ? null : fileLoadingHandler);

                        // Decompress so that we can verify hash down below.

                        var decompressedLength = decompressContext!.Decompress(data.Span, compressedData.Span);

                        if (decompressedLength != data.Length)
                            throw new Exception($"Compressed blob {i} had incorrect decompressed size!");
                    }
                    else
                    {
                        fileLoadingHandler.AppendJob(length);
                        await stream.ReadExactAsync(data, cancellationToken, blockFileLoadHandle ? null : fileLoadingHandler);
                    }
                }
                else
                {
                    fileLoadingHandler.AppendJob(length);
                    await stream.ReadExactAsync(data, cancellationToken, blockFileLoadHandle ? null : fileLoadingHandler);
                }

                using var fileStream = new MemoryStream(data.ToArray());
                hashApi.Save(item, fileStream, null);

                _logger.Log("file saved:" + item.Path);
                mainLoadingHandler.AppendResolvedJob();
                fileLoadingHandler.Dispose();
                i += 1;
            }
        }
        finally
        {
            mainLoadingHandler.Dispose();
            decompressContext?.Dispose();
            compressContext?.Dispose();
        }
    }


    private static void EnsureBuffer(ref byte[] buf, int needsFit)
    {
        if (buf.Length >= needsFit)
            return;

        var newLen = 2 << BitOperations.Log2((uint)needsFit - 1);

        buf = new byte[newLen];
    }
}