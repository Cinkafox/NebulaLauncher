using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;

namespace Nebula.Launcher.ProcessHelper;

public class ProcessRunHandler<T> : IProcessConsumerCollection, IDisposable where T: IProcessStartInfoProvider
{
    private ProcessStartInfo? _processInfo;
    private Task<ProcessStartInfo>? _processInfoTask;
    
    private Process? _process;
    private ProcessLogConsumerCollection _consumerCollection = new();
    
    private string _lastError = string.Empty;
    private readonly T _currentProcessStartInfoProvider;
    
    public T GetCurrentProcessStartInfo() => _currentProcessStartInfoProvider;
    public bool IsRunning => _processInfo is not null;
    public Action<ProcessRunHandler<T>>? OnProcessExited;

    public void RegisterLogger(IProcessLogConsumer consumer)
    {
        _consumerCollection.RegisterLogger(consumer);
    }
    
    public ProcessRunHandler(T processStartInfoProvider)
    {
        _currentProcessStartInfoProvider = processStartInfoProvider;
        _processInfoTask = _currentProcessStartInfoProvider.GetProcessStartInfo();
        _processInfoTask.GetAwaiter().OnCompleted(OnInfoProvided);
    }

    private void OnInfoProvided()
    {
        if (_processInfoTask == null)
            return;
        
        _processInfo = _processInfoTask.GetAwaiter().GetResult();
        _processInfoTask = null;
    }

    public void Start()
    {
        if (_processInfoTask != null)
        {
            _processInfoTask.Wait();
        }
        
        _process = Process.Start(_processInfo!);
        
        if (_process is null) return;
        
        _process.EnableRaisingEvents = true;

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;

        _process.Exited += OnExited;
    }
    
    public void Stop()
    {
        _process?.CloseMainWindow();
    }
    
    private void OnExited(object? sender, EventArgs e)
    {
        if (_process is null) return;

        _process.OutputDataReceived -= OnOutputDataReceived;
        _process.ErrorDataReceived -= OnErrorDataReceived;
        _process.Exited -= OnExited;
        

        if (_process.ExitCode != 0)
            _consumerCollection.Fatal(_lastError);
        
        _process.Dispose();
        _process = null;
        
        OnProcessExited?.Invoke(this);
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            _lastError = e.Data;
            _consumerCollection.Error(e.Data);
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            _consumerCollection.Out(e.Data);
        }
    }

    public void Dispose()
    {
        _processInfoTask?.Dispose();
        _process?.Dispose();
    }
}

public sealed class DebugLoggerBridge : IProcessLogConsumer
{
    private ILogger _logger;

    public DebugLoggerBridge(ILogger logger)
    {
        _logger = logger;
    }

    public void Out(string text)
    {
        _logger.Log(LoggerCategory.Log, text);
    }

    public void Error(string text)
    {
        _logger.Log(LoggerCategory.Error, text);
    }

    public void Fatal(string text)
    {
        _logger.Log(LoggerCategory.Error, text);
    }
}