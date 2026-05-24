using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using Nebula.Shared.FileApis;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Nebula.Shared.Utils;
using Robust.LoaderApi;

namespace Nebula.Shared.Services;

public partial class ContentService
{
    public readonly IReadWriteFileApi ZipContentApi = fileService.CreateFileApi("zipContent");
    
    public async Task<IFileApi> EnsureItems(RobustBuildInfo info, ILoadingHandlerFactory loadingFactory,
        CancellationToken cancellationToken)
    {
        if (info.RobustManifestInfo.HasValue)
            return await EnsureItems(info.RobustManifestInfo.Value, loadingFactory, cancellationToken);
        
        if (info.DownloadUri.HasValue)
            return await GetZipFileApi(info.DownloadUri.Value, loadingFactory, cancellationToken);
        
        throw new InvalidOperationException("DownloadUri is null");
    }

    public async Task<IFileApi> GetAllItems(RobustBuildInfo info, ILoadingHandlerFactory loadingFactory,
        CancellationToken cancellationToken)
    {
        if (info.RobustManifestInfo.HasValue)
            return await GetAllItems(info.RobustManifestInfo.Value, loadingFactory, cancellationToken);
        
        if (info.DownloadUri.HasValue)
            return await GetZipFileApi(info.DownloadUri.Value, loadingFactory, cancellationToken);
        
        throw new InvalidOperationException("DownloadUri is null");
    }
    
    private async Task<HashApi> GetAllItems(
        RobustManifestInfo info, 
        ILoadingHandlerFactory loadingFactory, 
        CancellationToken cancellationToken)
    {
        var manifestReader = await GetManifest(info, loadingFactory, cancellationToken);
        return CreateHashApi(manifestReader, info.DownloadUri);
    }

    private async Task<HashApi> EnsureItems(RobustManifestInfo info, ILoadingHandlerFactory loadingFactory,
        CancellationToken cancellationToken)
    {
        var hashApi = await GetAllItems(info, loadingFactory, cancellationToken);

        var missingFiles = hashApi.GetMissingFiles().ToList();
        
        _logger.Log("Download Count:" + missingFiles.Count);
        await Download(missingFiles, hashApi, loadingFactory, cancellationToken);

        return hashApi;
    }

    public async Task Download(List<RobustManifestItem> toDownload, HashApi hashApi, ILoadingHandlerFactory loadingHandlerFactory,
        CancellationToken cancellationToken)
    {
        if (toDownload.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            _logger.Log("Nothing to download! Skip!");
            return;
        }

        var contentCdn = hashApi.DownloadUri;
        
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
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (response.Content.Headers.ContentEncoding.Contains("zstd"))
            stream = new ZStdDecompressStream(stream);

        await using var streamDispose = stream;
        
        var streamHeader = await stream.ReadExactAsync(4, cancellationToken);
        var streamFlags = (DownloadStreamHeaderFlags)BinaryPrimitives.ReadInt32LittleEndian(streamHeader);
        var preCompressed = (streamFlags & DownloadStreamHeaderFlags.PreCompressed) != 0;
        
        var compressContext = preCompressed ? null : new ZStdCCtx();
        var decompressContext = preCompressed ? new ZStdDCtx() : null;
        
        var fileHeader = new byte[preCompressed ? 8 : 4];

        var downloadLoadHandler = loadingHandlerFactory.CreateLoadingContext();
        downloadLoadHandler.SetJobsCount(toDownload.Count);
        downloadLoadHandler.SetLoadingMessage("Fetching files...");

        if (loadingHandlerFactory is IConnectionSpeedHandler speedHandlerStart && toDownload.Count > 1)
            speedHandlerStart.PasteSpeed(0);
        
        try
        {
            var compressBuffer = new byte[1024];
            var readBuffer = new byte[1024];

            var i = 0;
            var downloadWatchdog = new Stopwatch();
            var lengthAcc = 0;
            var timeAcc = 0L;

            foreach (var item in toDownload)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                downloadWatchdog.Restart();
                
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
                fileLoadingHandler.Dispose();
                downloadLoadHandler.AppendResolvedJob();
                i += 1;
                
                if (loadingHandlerFactory is not IConnectionSpeedHandler speedHandler) 
                    continue;

                if (downloadWatchdog.ElapsedMilliseconds + timeAcc < 1000)
                {
                    timeAcc += downloadWatchdog.ElapsedMilliseconds;
                    lengthAcc += length;
                    continue;
                }

                if (timeAcc != 0)
                {
                    timeAcc += downloadWatchdog.ElapsedMilliseconds;
                    lengthAcc += length;
                    
                    speedHandler.PasteSpeed((int)(lengthAcc / (timeAcc / 1000)));

                    timeAcc = 0;
                    lengthAcc = 0;
                    
                    continue;
                }
                
                speedHandler.PasteSpeed((int)(length / (downloadWatchdog.ElapsedMilliseconds / 1000)));
            }
        }
        finally
        {
            downloadLoadHandler.Dispose();
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