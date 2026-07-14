using System.IO.Pipes;
using Nebula.Shared.Models;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;
using Robust.LoaderApi;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class RedialService : IRedialApi
{
    private CancellationTokenSource? _token;
    private bool _isServerRunning;
    private readonly ILogger _logger;
    
    public static readonly string RedialPipeName = "nebula-redial";
    
    public Action<RobustUrl, string?>? OnRedial;

    public RedialService(DebugService debugService)
    {
        _logger = debugService.GetLogger(this);
    }
    
    public void StartServer()
    {
        if(_isServerRunning)
            throw new Exception("Server is already running.");
        
        _token = new CancellationTokenSource();
        _isServerRunning = true;
        Task.Run(() => RunPipeServer(_token.Token));
    }

    public void StopServer()
    {
        _isServerRunning = false;
        _token?.Cancel();
    }
    
    private async Task RunPipeServer(CancellationToken cancellation)
    {
        _logger.Log("Init redial server");
        
        try
        {
            _logger.Log("Running redial server");
            while (!cancellation.IsCancellationRequested)
            {
                var serverStream = new NamedPipeServerStream(RedialPipeName, PipeDirection.In);
                await serverStream.WaitForConnectionAsync(cancellation);
                try
                {
                    using var streamReader = new StreamReader(serverStream);
                    await using var bitStream = new BitStream(serverStream);
                    
                    var url = bitStream.ReadString();
                    
                    string? message = null;
                    if (bitStream.ReadBit())
                        message = bitStream.ReadString();
                    
                    OnRedial?.Invoke(url.ToRobustUrl(), message);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e);
        }
        finally
        {
            _isServerRunning = false;
        }
    }

    public void Redial(Uri serverUrl, string? message)
    {
        using var client = new NamedPipeClientStream(".", RedialPipeName, PipeDirection.Out);
        client.Connect();
        using var bitStream = new BitStream(client);
        
        bitStream.WriteString(serverUrl.ToString());
        
        if(message is not null)
        {
            bitStream.WriteBit(true);
            bitStream.WriteString(message);
        }
        else
        {
            bitStream.WriteBit(false);
        }
    }
}