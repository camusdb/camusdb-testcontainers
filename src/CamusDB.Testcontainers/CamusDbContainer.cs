namespace CamusDB.Testcontainers;

/// <inheritdoc cref="DockerContainer" />
[PublicAPI]
public sealed class CamusDbContainer : DockerContainer, IDatabaseContainer
{
    /// <summary>
    /// A token this close to its expiry is renewed before use.
    /// </summary>
    private static readonly TimeSpan TokenRenewalMargin = TimeSpan.FromSeconds(30);

    private readonly CamusDbConfiguration _configuration;

    private readonly SemaphoreSlim _loginLock = new(1, 1);

    private string? _token;

    private DateTimeOffset _tokenExpiresAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbContainer" /> class.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    public CamusDbContainer(CamusDbConfiguration configuration)
        : base(configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the base address of the JSON/HTTP API, e.g. <c>http://127.0.0.1:32768</c>.
    /// </summary>
    /// <returns>The JSON/HTTP API address.</returns>
    public string GetHttpAddress()
    {
        return GetAddress(CamusDbBuilder.HttpPort);
    }

    /// <summary>
    /// Gets the address of the gRPC API, e.g. <c>http://127.0.0.1:32769</c>. The port speaks plaintext
    /// HTTP/2.
    /// </summary>
    /// <returns>The gRPC API address.</returns>
    public string GetGrpcAddress()
    {
        return GetAddress(CamusDbBuilder.GrpcPort);
    }

    /// <summary>
    /// Gets a <c>CamusDB.Client</c> connection string that uses the JSON/HTTP API, without a database.
    /// </summary>
    /// <returns>The CamusDB connection string.</returns>
    public string GetConnectionString()
    {
        return GetConnectionString(null);
    }

    /// <summary>
    /// Gets a <c>CamusDB.Client</c> connection string that uses the JSON/HTTP API, e.g.
    /// <c>Endpoint=http://127.0.0.1:32768;Database=shop</c>.
    /// </summary>
    /// <remarks>
    /// With authentication on, the string also carries the bootstrap superuser credentials and
    /// <c>AllowInsecureCredentials=true</c>. The client refuses to send credentials in plaintext to an
    /// endpoint that is not loopback, and a remote Docker host is not loopback.
    /// </remarks>
    /// <param name="databaseName">The database, or <c>null</c> to leave it out.</param>
    /// <returns>The CamusDB connection string.</returns>
    public string GetConnectionString(string? databaseName)
    {
        StringBuilder connectionString = new();
        DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "Endpoint", GetHttpAddress());
        AppendCommonSettings(connectionString, databaseName);
        return connectionString.ToString();
    }

    /// <summary>
    /// Gets a <c>CamusDB.Client</c> connection string that uses the gRPC API, e.g.
    /// <c>Endpoint=http://127.0.0.1:32769;Protocol=grpc;BackupEndpoint=http://127.0.0.1:32768;Database=shop</c>.
    /// </summary>
    /// <remarks>
    /// <c>BackupEndpoint</c> names the JSON/HTTP API, because the backup admin API is REST only.
    /// With authentication on, the string carries the same credential settings as
    /// <see cref="GetConnectionString(string)" />.
    /// </remarks>
    /// <param name="databaseName">The database, or <c>null</c> to leave it out.</param>
    /// <returns>The CamusDB connection string.</returns>
    public string GetGrpcConnectionString(string? databaseName = null)
    {
        StringBuilder connectionString = new();
        DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "Endpoint", GetGrpcAddress());
        DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "Protocol", "grpc");
        DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "BackupEndpoint", GetHttpAddress());
        AppendCommonSettings(connectionString, databaseName);
        return connectionString.ToString();
    }

    /// <summary>
    /// Creates a database, unless a database with that name already exists.
    /// </summary>
    /// <param name="databaseName">The database name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task that completes when the database exists.</returns>
    public async Task CreateDatabaseAsync(string databaseName, CancellationToken ct = default)
    {
        _ = await PostAsync("/create-db", new { databaseName, ifNotExists = true }, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a DDL statement, such as <c>CREATE TABLE</c>, over the JSON/HTTP API.
    /// </summary>
    /// <param name="databaseName">The database the statement runs in.</param>
    /// <param name="sql">The statement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The JSON response body.</returns>
    public Task<string> ExecuteDdlAsync(string databaseName, string sql, CancellationToken ct = default)
    {
        return PostAsync("/execute-sql-ddl", new { databaseName, sql }, ct);
    }

    /// <summary>
    /// Runs an <c>INSERT</c>, <c>UPDATE</c> or <c>DELETE</c> statement over the JSON/HTTP API.
    /// </summary>
    /// <param name="databaseName">The database the statement runs in.</param>
    /// <param name="sql">The statement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The JSON response body, which carries the affected row count.</returns>
    public Task<string> ExecuteNonQueryAsync(string databaseName, string sql, CancellationToken ct = default)
    {
        return PostAsync("/execute-sql-non-query", new { databaseName, sql }, ct);
    }

    /// <summary>
    /// Runs a query over the JSON/HTTP API.
    /// </summary>
    /// <param name="databaseName">The database the query runs in.</param>
    /// <param name="sql">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The JSON response body, which carries the rows.</returns>
    public Task<string> ExecuteQueryAsync(string databaseName, string sql, CancellationToken ct = default)
    {
        return PostAsync("/execute-sql-query", new { databaseName, sql }, ct);
    }

    /// <summary>
    /// Gets a bearer token for the bootstrap superuser. The container caches the token and logs in
    /// again shortly before it expires.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bearer token.</returns>
    /// <exception cref="InvalidOperationException">Thrown when authentication is not enabled.</exception>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!_configuration.AuthenticationEnabled)
            throw new InvalidOperationException("Authentication is not enabled. Call WithAuthentication on the builder.");

        await _loginLock.WaitAsync(ct)
            .ConfigureAwait(false);

        try
        {
            if (_token == null || DateTimeOffset.UtcNow >= _tokenExpiresAt - TokenRenewalMargin)
            {
                (_token, _tokenExpiresAt) = await CamusDbHttpApi.LoginAsync(HttpBaseAddress, _configuration.Username!, _configuration.Password!, ct)
                    .ConfigureAwait(false);
            }

            return _token;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private Uri HttpBaseAddress => new(GetHttpAddress());

    private void AppendCommonSettings(StringBuilder connectionString, string? databaseName)
    {
        if (databaseName != null)
            DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "Database", databaseName);

        if (_configuration.AuthenticationEnabled)
        {
            DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "User", _configuration.Username);
            DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "Password", _configuration.Password);
            DbConnectionStringBuilder.AppendKeyValuePair(connectionString, "AllowInsecureCredentials", "true");
        }
    }

    private string GetAddress(ushort containerPort)
    {
        return new UriBuilder(Uri.UriSchemeHttp, Hostname, GetMappedPublicPort(containerPort)).Uri.GetLeftPart(UriPartial.Authority);
    }

    private async Task<string> PostAsync(string path, object payload, CancellationToken ct)
    {
        string? token = _configuration.AuthenticationEnabled
            ? await GetAccessTokenAsync(ct).ConfigureAwait(false)
            : null;

        return await CamusDbHttpApi.PostAsync(HttpBaseAddress, path, payload, token, ct)
            .ConfigureAwait(false);
    }
}
