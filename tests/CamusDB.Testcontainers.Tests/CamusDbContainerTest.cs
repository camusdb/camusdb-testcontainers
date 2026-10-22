namespace CamusDB.Testcontainers.Tests;

public sealed class CamusDbContainerTest(CamusDbFixture fixture) : IClassFixture<CamusDbFixture>
{
    [Fact]
    public async Task RunsStatementsOverTheHttpApi()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await fixture.Container.CreateDatabaseAsync("shop", ct);
        await fixture.Container.ExecuteDdlAsync("shop", "CREATE TABLE products (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)", ct);
        await fixture.Container.ExecuteNonQueryAsync("shop", "INSERT INTO products (id, name) VALUES (1, 'kettle')", ct);

        string rows = await fixture.Container.ExecuteQueryAsync("shop", "SELECT name FROM products WHERE id = 1", ct);

        Assert.Contains("kettle", rows);
    }

    [Fact]
    public async Task CreateDatabaseAcceptsAnExistingDatabase()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await fixture.Container.CreateDatabaseAsync("twice", ct);
        await fixture.Container.CreateDatabaseAsync("twice", ct);
    }

    [Fact]
    public async Task FailedStatementThrows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await fixture.Container.CreateDatabaseAsync("broken", ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Container.ExecuteQueryAsync("broken", "SELECT * FROM no_such_table", ct));
    }

    [Fact]
    public async Task HealthEndpointAnswersOnTheHttpAddress()
    {
        using HttpClient client = new();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri(new Uri(fixture.Container.GetHttpAddress()), "/health"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GrpcAddressSpeaksPlaintextHttp2()
    {
        using HttpClient client = new();

        using HttpRequestMessage request = new(HttpMethod.Get, fixture.Container.GetGrpcAddress())
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpVersion.Version20, response.Version);
    }

    [Fact]
    public void ConnectionStringNamesTheHttpEndpoint()
    {
        string http = fixture.Container.GetHttpAddress();

        Assert.Equal($"Endpoint={http}", fixture.Container.GetConnectionString());
        Assert.Equal($"Endpoint={http};Database=shop", fixture.Container.GetConnectionString("shop"));
    }

    [Fact]
    public void GrpcConnectionStringNamesTheGrpcEndpointAndTheBackupEndpoint()
    {
        string http = fixture.Container.GetHttpAddress();
        string grpc = fixture.Container.GetGrpcAddress();

        Assert.NotEqual(http, grpc);
        Assert.Equal(
            $"Endpoint={grpc};Protocol=grpc;BackupEndpoint={http};Database=shop",
            fixture.Container.GetGrpcConnectionString("shop"));
    }

    [Fact]
    public async Task GetAccessTokenThrowsWithoutAuthentication()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Container.GetAccessTokenAsync(TestContext.Current.CancellationToken));
    }
}
