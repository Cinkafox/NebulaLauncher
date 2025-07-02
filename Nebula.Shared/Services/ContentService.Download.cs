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
        ILoadingHandler loadingHandler,
        CancellationToken cancellationToken)
    {
        List<RobustManifestItem> allItems = [];
        List<RobustManifestItem> items = [];

        while (manifestReader.TryReadItem(out var item))
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            
            allItems.Add(item.Value);
        }

        var hashApi = CreateHashApi(allItems);

        items = allItems.Where(a=> !hashApi.Has(a)).ToList();

        _logger.Log("Download Count:" + items.Count);
        await Download(downloadUri, items, hashApi, loadingHandler, cancellationToken);

        return hashApi;
    }

    public async Task<HashApi> EnsureItems(RobustManifestInfo info, ILoadingHandler loadingHandler,
        CancellationToken cancellationToken)
    {
        _logger.Log("Getting manifest: " + info.Hash);

        if (ManifestFileApi.TryOpen(info.Hash, out var stream))
        {
            _logger.Log("Loading manifest from: " + info.Hash);
            return await EnsureItems(new ManifestReader(stream), info.DownloadUri, loadingHandler, cancellationToken);
        }
        
        SetServerHash(info.ManifestUri.ToString(), info.Hash);

        _logger.Log("Fetching manifest from: " + info.ManifestUri);

        var response = await _http.GetAsync(info.ManifestUri, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new Exception();

        await using var streamContent = await response.Content.ReadAsStreamAsync(cancellationToken);
        ManifestFileApi.Save(info.Hash, streamContent);
        streamContent.Seek(0, SeekOrigin.Begin);
        using var manifestReader = new ManifestReader(streamContent);
        return await EnsureItems(manifestReader, info.DownloadUri, loadingHandler, cancellationToken);
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
                _logger.Error("OH FUCK!! " + item.Path);
            }

            loadingHandler.AppendResolvedJob();
        });

        if (loadingHandler is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public async Task Download(Uri contentCdn, List<RobustManifestItem> toDownload, HashApi hashApi, ILoadingHandler loadingHandler,
        CancellationToken cancellationToken)
    {
        if (toDownload.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            _logger.Log("Nothing to download! Fuck this!");
            return;
        }

        var downloadJobWatch = loadingHandler.GetQueryJob();

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
            _logger.Log("Downloading is cancelled!");
            return;
        }

        downloadJobWatch.Dispose();

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        var bandwidthStream = new BandwidthStream(stream);
        stream = bandwidthStream;
        if (response.Content.Headers.ContentEncoding.Contains("zstd"))
            stream = new ZStdDecompressStream(stream);

        await using var streamDispose = stream;

        // Read flags header
        var streamHeader = await stream.ReadExactAsync(4, null);
        var streamFlags = (DownloadStreamHeaderFlags)BinaryPrimitives.ReadInt32LittleEndian(streamHeader);
        var preCompressed = (streamFlags & DownloadStreamHeaderFlags.PreCompressed) != 0;

        // compressContext.SetParameter(ZSTD_cParameter.ZSTD_c_nbWorkers, 4);
        // If the stream is pre-compressed we need to decompress the blobs to verify BLAKE2B hash.
        // If it isn't, we need to manually try re-compressing individual files to store them.
        var compressContext = preCompressed ? null : new ZStdCCtx();
        var decompressContext = preCompressed ? new ZStdDCtx() : null;

        // Normal file header:
        // <int32> uncompressed length
        // When preCompressed is set, we add:
        // <int32> compressed length
        var fileHeader = new byte[preCompressed ? 8 : 4];


        try
        {
            // Buffer for storing compressed ZStd data.
            var compressBuffer = new byte[1024];

            // Buffer for storing uncompressed data.
            var readBuffer = new byte[1024];

            var i = 0;

            loadingHandler.AppendJob(toDownload.Count);

            foreach (var item in toDownload)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Log("Downloading is cancelled!");
                    decompressContext?.Dispose();
                    compressContext?.Dispose();
                    return;
                }

                // Read file header.
                await stream.ReadExactAsync(fileHeader, null);

                var length = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(0, 4));

                EnsureBuffer(ref readBuffer, length);
                var data = readBuffer.AsMemory(0, length);

                if (preCompressed)
                {
                    // Compressed length from extended header.
                    var compressedLength = BinaryPrimitives.ReadInt32LittleEndian(fileHeader.AsSpan(4, 4));

                    if (compressedLength > 0)
                    {
                        EnsureBuffer(ref compressBuffer, compressedLength);
                        var compressedData = compressBuffer.AsMemory(0, compressedLength);
                        await stream.ReadExactAsync(compressedData, null);

                        // Decompress so that we can verify hash down below.

                        var decompressedLength = decompressContext!.Decompress(data.Span, compressedData.Span);

                        if (decompressedLength != data.Length)
                            throw new Exception($"Compressed blob {i} had incorrect decompressed size!");
                    }
                    else
                    {
                        await stream.ReadExactAsync(data, null);
                    }
                }
                else
                {
                    await stream.ReadExactAsync(data, null);
                }

                using var fileStream = new MemoryStream(data.ToArray());
                hashApi.Save(item, fileStream);

                _logger.Log("file saved:" + item.Path);
                loadingHandler.AppendResolvedJob();
                i += 1;
            }
        }
        finally
        {
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