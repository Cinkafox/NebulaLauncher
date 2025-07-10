using System.IO.Compression;
using Nebula.Shared.FileApis;
using Nebula.Shared.FileApis.Interfaces;
using Nebula.Shared.Models;
using Nebula.Shared.Services.Logging;
using Robust.LoaderApi;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class FileService
{
    public static string RootPath = Path.Join(Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData), "Datum");

    private readonly ILogger _logger;
    
    public FileService(DebugService debugService)
    {
        _logger = debugService.GetLogger(this);

        if(!Directory.Exists(RootPath)) 
            Directory.CreateDirectory(RootPath);
    }
    
    public IReadWriteFileApi CreateFileApi(string path)
    {
        _logger.Debug($"Creating file api for {path}");
        return new FileApi(Path.Join(RootPath, path));
    }
    
    public IReadWriteFileApi EnsureTempDir(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), "tempThink"+Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        _logger.Debug($"Ensuring temp directory for {path}");
        return new FileApi(path);
    }

    public ZipFileApi? OpenZip(string path, IFileApi fileApi)
    {
        Stream? zipStream = null;
        try
        {
            if (!fileApi.TryOpen(path, out zipStream))
                return null;

            var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            return new ZipFileApi(zipArchive, "");
        }
        catch (Exception)
        {
            zipStream?.Dispose();
            throw;
        }
    }
    
    public void RemoveAllFiles(string fileApiName,ILoadingHandler loadingHandler, CancellationToken cancellationToken)
    {
        _logger.Debug($"Deleting files from {fileApiName}");
        var path = Path.Combine(RootPath, fileApiName);
        
        var di = new DirectoryInfo(path);

        var files = di.GetFiles();
        var dirs = di.GetDirectories();
        
        loadingHandler.AppendJob(files.Length);
        loadingHandler.AppendJob(dirs.Length);
        
        if(cancellationToken.IsCancellationRequested)
            return;
        
        foreach (var file in files)
        {
            if(cancellationToken.IsCancellationRequested)
                return;
            file.Delete(); 
            loadingHandler.AppendResolvedJob();
        }
        foreach (var dir in dirs)
        {
            if(cancellationToken.IsCancellationRequested)
                return;
            dir.Delete(true); 
            loadingHandler.AppendResolvedJob();
        }
    }
}

public sealed class ConsoleLoadingHandler : ILoadingHandler
{
    private int _currJobs;

    private float _percent;
    private int _resolvedJobs;

    public void SetJobsCount(int count)
    {
        _currJobs = count;

        UpdatePercent();
        Draw();
    }

    public int GetJobsCount()
    {
        return _currJobs;
    }

    public void SetResolvedJobsCount(int count)
    {
        _resolvedJobs = count;

        UpdatePercent();
        Draw();
    }

    public int GetResolvedJobsCount()
    {
        return _resolvedJobs;
    }

    public void SetLoadingMessage(string message)
    {
        
    }

    private void UpdatePercent()
    {
        if (_currJobs == 0)
        {
            _percent = 0;
            return;
        }

        if (_resolvedJobs > _currJobs) return;

        _percent = _resolvedJobs / (float)_currJobs;
    }

    private void Draw()
    {
        var barCount = 10;
        var fullCount = (int)(barCount * _percent);
        var emptyCount = barCount - fullCount;

        Console.Write("\r");

        for (var i = 0; i < fullCount; i++) Console.Write("#");

        for (var i = 0; i < emptyCount; i++) Console.Write(" ");

        Console.Write($"\t {_resolvedJobs}/{_currJobs}");
    }
}