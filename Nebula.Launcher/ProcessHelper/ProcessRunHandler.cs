using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;

namespace Nebula.Launcher.ProcessHelper;

public class ProcessRunHandler : IDisposable
{
    private ProcessStartInfo? _processInfo;
    private Task<ProcessStartInfo>? _processInfoTask;
    
    private Process? _process;
    private readonly IProcessLogConsumer _logConsumer;
    
    private string _lastError = string.Empty;
    private readonly IProcessStartInfoProvider _currentProcessStartInfoProvider;
    
    public IProcessStartInfoProvider GetCurrentProcessStartInfo() => _currentProcessStartInfoProvider;
    public bool IsRunning => _processInfo is not null;
    public Action<ProcessRunHandler>? OnProcessExited;
    
    public bool Disposed { get; private set; }
    
    public ProcessRunHandler(IProcessStartInfoProvider processStartInfoProvider, IProcessLogConsumer logConsumer)
    {
        _currentProcessStartInfoProvider = processStartInfoProvider;
        _logConsumer = logConsumer;
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

    private void CheckIfDisposed()
    {
        if (!Disposed) return;
        throw new ObjectDisposedException(nameof(ProcessRunHandler));
    }

    public void Start()
    {
        CheckIfDisposed();
        if(_process is not null) 
            throw new InvalidOperationException("Already running");
            
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
        CheckIfDisposed();
        Dispose();
    }
    
    private void OnExited(object? sender, EventArgs e)
    {
        if (_process is null) return;

        _process.OutputDataReceived -= OnOutputDataReceived;
        _process.ErrorDataReceived -= OnErrorDataReceived;
        _process.Exited -= OnExited;
        

        if (_process.ExitCode != 0)
            _logConsumer.Fatal(_lastError);
        
        _process.Dispose();
        _process = null;
        
        OnProcessExited?.Invoke(this);
        Dispose();
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            _lastError = e.Data;
            _logConsumer.Error(e.Data);
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            _logConsumer.Out(e.Data);
        }
    }

    public void Dispose()
    {
        if (_process is not null)
        {
            _process.CloseMainWindow();
            return;
        }
        
        CheckIfDisposed();
    
        _processInfoTask?.Dispose();
        Disposed = true;
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