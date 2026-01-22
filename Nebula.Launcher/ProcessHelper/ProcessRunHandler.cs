using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;

namespace Nebula.Launcher.ProcessHelper;

public class ProcessRunHandler : IDisposable
{
    private Process? _process;
    private readonly IProcessLogConsumer _logConsumer;
    
    private StringBuilder _lastErrorBuilder = new StringBuilder();
    
    public bool IsRunning => _process is not null;
    public Action<ProcessRunHandler>? OnProcessExited;

    public AsyncValueCache<ProcessStartInfo> ProcessStartInfoProvider { get; }
    
    public bool Disposed { get; private set; }
    
    public ProcessRunHandler(IProcessStartInfoProvider processStartInfoProvider, IProcessLogConsumer logConsumer)
    {
        _logConsumer = logConsumer;

        ProcessStartInfoProvider = new AsyncValueCache<ProcessStartInfo>(processStartInfoProvider.GetProcessStartInfo);
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
        
        _process = Process.Start(ProcessStartInfoProvider.GetValue());
        
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
            _logConsumer.Fatal(_lastErrorBuilder.ToString());
        
        _process.Dispose();
        _process = null;
        
        OnProcessExited?.Invoke(this);
        Dispose();
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;

        if (!e.Data.StartsWith("  "))
            _lastErrorBuilder.Clear();
        
        _lastErrorBuilder.AppendLine(e.Data);
        _logConsumer.Error(e.Data);
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
        
        ProcessStartInfoProvider.Invalidate();
        
        CheckIfDisposed();
        
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

public class AsyncValueCache<T>
{
    private readonly Func<CancellationToken, Task<T>> _valueFactory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _cacheCts = new();
    
    private Lazy<Task<T>> _lazyTask = null!;
    private T _cachedValue = default!;
    private bool _isCacheValid;

    public AsyncValueCache(Func<CancellationToken, Task<T>> valueFactory)
    {
        _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        ResetLazyTask();
    }

    public T GetValue()
    {
        if (_isCacheValid) return _cachedValue;

        try
        {
            _semaphore.Wait();
            if (_isCacheValid) return _cachedValue;

            _cachedValue = _lazyTask.Value
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
            
            _isCacheValid = true;
            return _cachedValue;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Invalidate()
    {
        using var cts = new CancellationTokenSource();
        try
        {
            _semaphore.Wait();
            _isCacheValid = false;
            _cacheCts.Cancel();
            _cacheCts.Dispose();
            ResetLazyTask();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ResetLazyTask()
    {
        _lazyTask = new Lazy<Task<T>>(() => 
            _valueFactory(_cacheCts.Token)
                .ContinueWith(t => 
                {
                    if (t.IsCanceled || t.IsFaulted)
                    {
                        _isCacheValid = false;
                        throw t.Exception ?? new Exception();
                    }
                    return t.Result;
                }, TaskContinuationOptions.ExecuteSynchronously));
    }
}