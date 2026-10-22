namespace CamusDB.Testcontainers;

/// <summary>
/// Minimal client for the CamusDB JSON/HTTP API. The module uses it for the readiness check and for
/// the convenience methods on <see cref="CamusDbContainer" />, so the package needs no dependency on a
/// CamusDB client library.
/// </summary>
internal static class CamusDbHttpApi
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        // Every container gets a fresh host port, so pooled connections must not outlive it for long.
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Logs in and returns the bearer token with the instant after which the server rejects it.
    /// </summary>
    public static async Task<(string Token, DateTimeOffset ExpiresAt)> LoginAsync(
        Uri address,
        string username,
        string password,
        CancellationToken ct)
    {
        string body = await PostAsync(address, "/login", new { user = username, password }, null, ct)
            .ConfigureAwait(false);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        string token = root.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("CamusDB returned a login reply without a token");

        DateTimeOffset expiresAt = root.TryGetProperty("expiresAtUnixMs", out JsonElement expires) && expires.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeMilliseconds(expires.GetInt64())
            : DateTimeOffset.MaxValue;

        return (token, expiresAt);
    }

    /// <summary>
    /// Posts <paramref name="payload" /> as JSON and returns the response body. Throws when the server
    /// answers with a status code outside 2xx; CamusDB answers every failed command that way.
    /// </summary>
    public static async Task<string> PostAsync(Uri address, string path, object payload, string? token, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(address, path));
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        if (token != null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await HttpClient.SendAsync(request, ct)
            .ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"CamusDB request '{path}' failed with HTTP {(int)response.StatusCode}: {body}");

        return body;
    }
}
