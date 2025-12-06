using System.Buffers;
using Nebula.Shared.Models;

namespace Nebula.Shared.Utils;

public static class StreamHelper
{
    public static void CopyTo(this Stream input, Stream output, ILoadingHandler loadingHandler)
    {
        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];

        int bytesRead;
        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, bytesRead);
            loadingHandler.AppendResolvedJob(bytesRead);
        }
    }
    
    public static async ValueTask<byte[]> ReadExactAsync(this Stream stream, int amount, CancellationToken cancel = default)
    {
        var data = new byte[amount];
        await ReadExactAsync(stream, data, cancel);
        return data;
    }

    public static async ValueTask ReadExactAsync(this Stream stream, Memory<byte> into, CancellationToken cancel  = default, ILoadingHandler? loadingHandler = null)
    {
        while (into.Length > 0)
        {
            var read = await stream.ReadAsync(into, cancel);
            
            loadingHandler?.AppendResolvedJob(read);

            // Check EOF.
            if (read == 0)
                throw new EndOfStreamException();

            into = into[read..];
        }
    }
}