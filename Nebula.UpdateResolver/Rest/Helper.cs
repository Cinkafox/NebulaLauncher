using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nebula.UpdateResolver.Rest;

public static class Helper
{
    public static readonly JsonSerializerOptions JsonWebOptions = new(JsonSerializerDefaults.Web);
    public static async Task<T> AsJson<T>(this HttpContent content) where T : notnull
    {
        var str = await content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(str, JsonWebOptions) ??
               throw new JsonException("AsJson: did not expect null response");
    }
    
    public static void CopyTo(this Stream input, Stream output, string fileName, long totalLength)
    {
        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];

        int skipStep = 0;
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, bytesRead);
            totalRead += bytesRead;

            skipStep++;
            
            if(skipStep < 50) continue;
            
            skipStep = 0;
            
            LogStandalone.Log($"Saving {fileName}", (int)((totalRead / (float)totalLength) * 100));
        }
    }
}