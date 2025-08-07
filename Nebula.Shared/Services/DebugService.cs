using System.Collections.Concurrent;
using System.Reflection;
using Nebula.Shared.Services.Logging;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class DebugService : IDisposable
{
    public static bool DoFileLog;

    private readonly string _path =
        Path.Combine(FileService.RootPath, "log", Assembly.GetEntryAssembly()?.GetName().Name ?? "App");

    public DebugService()
    {
        ClearLog();
        Root = new ServiceLogger("Root", _path);
        Root.GetLogger("DebugService")
            .Log("Initializing debug service " + (DoFileLog ? "with file logging" : "without file logging"));
    }

    private ServiceLogger Root { get; }

    public void Dispose()
    {
        Root.Dispose();
    }

    public ILogger GetLogger(string loggerName)
    {
        return Root.GetLogger(loggerName);
    }

    public ILogger GetLogger(object objectToLog)
    {
        return Root.GetLogger(objectToLog.GetType().Name);
    }

    private void ClearLog()
    {
        if (!Directory.Exists(_path))
            return;
        var di = new DirectoryInfo(_path);

        foreach (var file in di.GetFiles()) file.Delete();
        foreach (var dir in di.GetDirectories()) dir.Delete(true);
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
    private readonly string _path;

    public ServiceLogger(string category, string directory)
    {
        _directory = directory;
        Category = category;

        if (!DebugService.DoFileLog) return;

        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        _path = Path.Combine(directory, $"{Category}.log");

        File.Create(_path).Dispose();
    }

    public ServiceLogger? Root { get; private set; }

    public string Category { get; init; }
    private ConcurrentDictionary<string, ServiceLogger> Childs { get; } = new();

    public void Log(LoggerCategory loggerCategory, string message)
    {
        var output = DebugService.DoFileLog
            ? $"[{DateTime.Now.ToUniversalTime():yyyy-MM-dd HH:mm:ss}][{Enum.GetName(loggerCategory)}][{Category}]: {message}"
            : message;

        Console.WriteLine(output);

        LogToFile(output);
    }

    public void Dispose()
    {
        if (!DebugService.DoFileLog) return;

        foreach (var (_, child) in Childs)
        {
            child.Dispose();
        }

        Childs.Clear(); // Not strictly necessary, but keeps intent clear
    }

    public ServiceLogger GetLogger(string category)
    {
        return Childs.GetOrAdd(category, key =>
        {
            var logger = new ServiceLogger(key, _directory)
            {
                Root = this
            };
            return logger;
        });
    }

    private void LogToFile(string output)
    {
        if (!DebugService.DoFileLog) return;

        try
        {
            Root?.LogToFile(output); // Log to parent first

            using var fileStream = File.Open(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var streamWriter = new StreamWriter(fileStream);
            streamWriter.WriteLine(output);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[Logging Error] Failed to write log: {ex.Message}");
        }
    }
}

public static class LoggerExtensions
{
    public static void Debug(this ILogger logger, string message)
    {
        logger.Log(LoggerCategory.Debug, message);
    }

    public static void Error(this ILogger logger, string message)
    {
        logger.Log(LoggerCategory.Error, message);
    }

    public static void Log(this ILogger logger, string message)
    {
        logger.Log(LoggerCategory.Log, message);
    }

    public static void Error(this ILogger logger, Exception e)
    {
        Error(logger, e.Message + "\r\n" + e.StackTrace);
        if (e.InnerException != null)
            Error(logger, e.InnerException);
    }
}