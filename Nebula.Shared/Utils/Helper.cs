using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Nebula.Shared.Models;

namespace Nebula.Shared.Utils;

public static class Helper
{
    public static readonly JsonSerializerOptions JsonWebOptions = new(JsonSerializerDefaults.Web);
    public static void SafeOpenBrowser(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            Console.WriteLine("Unable to parse URI in server-provided link: {Link}", uri);
            return;
        }

        if (parsedUri.Scheme is not ("http" or "https"))
        {
            Console.WriteLine("Refusing to open server-provided link {Link}, only http/https are allowed", parsedUri);
            return;
        }

        OpenBrowser(parsedUri.ToString());
    }
    public static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static async Task<T> AsJson<T>(this HttpContent content, ILoadingHandler? loadingHandler) where T : notnull
    {
        var memoryStream = new MemoryStream();
        await using var stream = await content.ReadAsStreamAsync();
                
        if(loadingHandler != null)
            stream.CopyTo(memoryStream, loadingHandler);
        else
            await stream.CopyToAsync(memoryStream);
       
        memoryStream.Position = 0;
        
        return JsonSerializer.Deserialize<T>(memoryStream, JsonWebOptions) ??
               throw new JsonException("AsJson: did not expect null response");
    }
}