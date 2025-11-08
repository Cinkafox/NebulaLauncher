using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;

namespace Nebula.Shared.Services;

[ServiceRegister]
public class RestService
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public RestService(DebugService debug, HttpClient? client = null)
    {
        _client = client ?? new HttpClient();
        _logger = debug.GetLogger(this);
    }

    [Pure]
    public async Task<T> GetAsync<T>(Uri uri, CancellationToken cancellationToken) where T : notnull
    {
        var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Version = HttpVersion.Version10,
        };
        
        var response = await _client.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);
        return await ReadResult<T>(response, cancellationToken, uri);
    }
    
    public async Task<K> PostAsync<K, T>(T information, Uri uri, CancellationToken cancellationToken) where K : notnull
    {
        var json = JsonSerializer.Serialize(information, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(uri, content, cancellationToken);
        return await ReadResult<K>(response, cancellationToken, uri);
    }

    [Pure]
    public async Task<T> PostAsync<T>(Stream stream, string fileName, Uri uri, CancellationToken cancellationToken) where T : notnull
    {
        using var multipartFormContent =
            new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
        multipartFormContent.Add(new StreamContent(stream), "formFile", fileName);
        var response = await _client.PostAsync(uri, multipartFormContent, cancellationToken);
        return await ReadResult<T>(response, cancellationToken, uri);
    }

    [Pure]
    public async Task<T> DeleteAsync<T>(Uri uri, CancellationToken cancellationToken) where T : notnull
    {
        var response = await _client.DeleteAsync(uri, cancellationToken);
        return await ReadResult<T>(response, cancellationToken, uri);
    }
    
    [Pure]
    public async Task<T> GetAsyncDefault<T>(Uri uri, T defaultValue, CancellationToken cancellationToken) where T : notnull
    {
        try
        {
            return await GetAsync<T>(uri, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.Error(e);
            return defaultValue;
        }
    }

    [Pure]
    private async Task<T> ReadResult<T>(HttpResponseMessage response, CancellationToken cancellationToken, Uri uri) where T : notnull
    {
        if (typeof(T) == typeof(NullResponse) && new NullResponse() is T nullResponse)
        {
            return nullResponse;
        }
        
        if (typeof(T) == typeof(string) && await response.Content.ReadAsStringAsync(cancellationToken) is T t)
            return t;
        
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.AsJson<T>();
            response.Dispose();
            return data;
        }

        var ex = new RestRequestException(response.Content, response.StatusCode,
            $"Error while processing {uri.ToString()}: {response.ReasonPhrase}");
        
        throw ex;
    }
}

public sealed class NullResponse
{
}

public sealed class RestRequestException(HttpContent content, HttpStatusCode statusCode, string message) : Exception(message), IDisposable
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public HttpContent Content { get; } = content;

    public void Dispose()
    {
        Content.Dispose();
    }
}