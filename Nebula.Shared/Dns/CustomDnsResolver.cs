using System.Net;
using System.Net.Sockets;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;

namespace Nebula.Shared.Dns;

public class CustomDnsResolver(CustomDnsConfiguration configuration, ILogger logger)
{
    public ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;
        
        if (configuration.TryResolve(host, out var customIp))
        {
            logger.Debug($"Custom resolving DNS for host: {host} {customIp}");
            return ConnectToIpAsync(customIp, port, cancellationToken);
        }
        
        return ResolveAndConnectAsync(host, port, cancellationToken);
    }
    
    public HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync
        };
        
        return new HttpClient(handler, disposeHandler: true);
    }

    private async ValueTask<Stream> ResolveAndConnectAsync(
        string host, int port, CancellationToken ct)
    {
        var addresses = await System.Net.Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0)
            throw new InvalidOperationException($"Failed to resolve DNS for host: {host}");

        return await ConnectToIpAsync(addresses, port, ct);
    }

    private static async ValueTask<Stream> ConnectToIpAsync(
        IPAddress[] addresses, int port, CancellationToken ct)
    {
        var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(addresses, port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async ValueTask<Stream> ConnectToIpAsync(
        IPAddress ip, int port, CancellationToken ct)
    {
        var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(ip, port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}