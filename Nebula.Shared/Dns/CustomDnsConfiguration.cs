using System.Net;

namespace Nebula.Shared.Dns;

public class CustomDnsConfiguration()
{
    private readonly Dictionary<string, IPAddress> _map = new(StringComparer.OrdinalIgnoreCase);
    
    public int MapCount => _map.Count;
    
    public CustomDnsConfiguration Add(string host, string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            throw new ArgumentException($"Invalid IP address: {ipAddress}");

        _map[host] = ip;
        return this;
    }

    public CustomDnsConfiguration Add(string host, IPAddress ipAddress)
    {
        _map[host] = ipAddress;
        return this;
    }

    public bool TryResolve(string host, out IPAddress ipAddress)
        => _map.TryGetValue(host, out ipAddress);
}