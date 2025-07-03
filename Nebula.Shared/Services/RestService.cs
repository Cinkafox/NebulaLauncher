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
        var response = await _client.GetAsync(uri, cancellationToken);
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
    public async Task<K> PostAsync<K, T>(T information, Uri uri, CancellationToken cancellationToken) where K : notnull
    {
        var json = JsonSerializer.Serialize(information, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync(uri, content, cancellationToken);
        return await ReadResult<K>(response, cancellationToken, uri);
    }

    [Pure]
    public async Task<T> PostAsync<T>(Stream stream, Uri uri, CancellationToken cancellationToken) where T : notnull
    {
        using var multipartFormContent =
            new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
        multipartFormContent.Add(new StreamContent(stream), "formFile", "image.png");
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
    private async Task<T> ReadResult<T>(HttpResponseMessage response, CancellationToken cancellationToken, Uri uri) where T : notnull
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (typeof(T) == typeof(string) && content is T t)
            return t;
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.AsJson<T>();
        }
        
        throw new RestRequestException(response.Content, response.StatusCode, $"Error while processing {uri.ToString()}: {response.ReasonPhrase}");
    }
}

public sealed class RestRequestException(HttpContent content, HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public HttpContent Content { get; } = content;
}