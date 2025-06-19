using System.Reflection;
using Nebula.Shared.FileApis;
using Nebula.Shared.Services.Logging;
using Robust.LoaderApi;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class DebugService : IDisposable
{
    public static bool DoFileLog;
    private ServiceLogger Root {get; set;}

    private readonly string _path =
        Path.Combine(FileService.RootPath, "log", Assembly.GetEntryAssembly()?.GetName().Name ?? "App");
    
    public DebugService()
    {
        ClearLog();
        Root = new ServiceLogger("Root",_path);
        Root.GetLogger("DebugService").Log("Initializing debug service " + (DoFileLog ? "with file logging" : "without file logging"));
    }

    public ILogger GetLogger(string loggerName)
    {
        return Root.GetLogger(loggerName);
    }

    public ILogger GetLogger(object objectToLog)
    {
        return Root.GetLogger(objectToLog.GetType().Name);
    }

    public void Dispose()
    {
        Root.Dispose();
    }

    private void ClearLog()
    {
        if(!Directory.Exists(_path)) 
            return;
        var di = new DirectoryInfo(_path);

        foreach (var file in di.GetFiles())
        {
            file.Delete(); 
        }
        foreach (var dir in di.GetDirectories())
        {
            dir.Delete(true); 
        }
    }
}

public enum LoggerCategory
{
    Log,
    Debug,
    Error
}

internal class ServiceLogger : ILogger
{
    private readonly string _directory;
    public ServiceLogger? Root { get; private set; }
    public ServiceLogger(string category, string directory)
    {
        _directory = directory;
        Category = category;

        if (!DebugService.DoFileLog) return;
        
        if(!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        _path = Path.Combine(directory, $"{Category}.log");
        
        File.Create(_path).Dispose();
    }

    public string Category { get; init; }
    
    private Dictionary<string, ServiceLogger> Childs { get; init; } = new();
    
    private FileStream? _fileStream;
    private StreamWriter? _streamWriter;
    private readonly string _path;

    public ServiceLogger GetLogger(string category)
    {
        if (Childs.TryGetValue(category, out var logger))
            return logger;
        
        logger = new ServiceLogger(category, _directory);
        logger.Root = this;
        Childs.Add(category, logger);
        return logger;
    }
    
    public void Log(LoggerCategory loggerCategory, string message)
    {
        var output = DebugService.DoFileLog 
            ? $"[{DateTime.Now.ToUniversalTime():yyyy-MM-dd HH:mm:ss}][{Enum.GetName(loggerCategory)}][{Category}]: {message}"
            : message;
        
        Console.WriteLine(output);
        
        LogToFile(output);
    }

    private void LogToFile(string output)
    {
        if(!DebugService.DoFileLog) return;
        _fileStream = File.Open(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _streamWriter = new StreamWriter(_fileStream);
        Root?.LogToFile(output);
        _streamWriter.WriteLine(output);
        _streamWriter.Flush();
        
        _streamWriter.Dispose();
        _fileStream.Dispose();
        
        _fileStream = null;
        _streamWriter = null;
    }

    public void Dispose()
    {
        if (!DebugService.DoFileLog) return;
        
        _streamWriter?.Dispose();
        _fileStream?.Dispose();
        foreach (var (_, child) in Childs)
        {
            child.Dispose();
        }
        Childs.Clear();
    }
}

public static class LoggerExtensions
{
    public static void Debug(this ILogger logger,string message)
    {
        logger.Log(LoggerCategory.Debug, message);
    }

    public static void Error(this ILogger logger,string message)
    {
        logger.Log(LoggerCategory.Error, message);
    }

    public static void Log(this ILogger logger,string message)
    {
        logger.Log(LoggerCategory.Log, message);
    }

    public static void Error(this ILogger logger,Exception e)
    {
        Error(logger,e.Message + "\r\n" + e.StackTrace);
        if (e.InnerException != null)
            Error(logger, e.InnerException);
    }
}