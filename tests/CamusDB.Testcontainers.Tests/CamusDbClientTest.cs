using CamusDB.Client;

namespace CamusDB.Testcontainers.Tests;

/// <summary>
/// Checks that the ADO.NET provider accepts the module's connection strings, over both transports.
/// </summary>
public sealed class CamusDbClientTest(CamusDbFixture fixture) : IClassFixture<CamusDbFixture>
{
    [Fact]
    public Task ClientRunsStatementsOverRest()
    {
        return ClientRoundTrip.RunAsync(fixture.Container, "client_rest", fixture.Container.GetConnectionString);
    }

    [Fact]
    public Task ClientRunsStatementsOverGrpc()
    {
        return ClientRoundTrip.RunAsync(fixture.Container, "client_grpc", fixture.Container.GetGrpcConnectionString);
    }
}

public sealed class CamusDbAuthenticatedClientTest(AuthenticatedCamusDbFixture fixture) : IClassFixture<AuthenticatedCamusDbFixture>
{
    [Fact]
    public void ConnectionStringCarriesTheCredentials()
    {
        CamusConnectionStringBuilder builder = new(fixture.Container.GetConnectionString("secure"));

        Assert.Equal(fixture.Container.GetHttpAddress(), builder.Config["Endpoint"]);
        Assert.Equal("secure", builder.Config["Database"]);
        Assert.Equal(AuthenticatedCamusDbFixture.Username, builder.Config["User"]);
        Assert.Equal(AuthenticatedCamusDbFixture.Password, builder.Config["Password"]);
        Assert.Equal("true", builder.Config["AllowInsecureCredentials"]);
    }

    [Fact]
    public Task ClientRunsStatementsOverRest()
    {
        return ClientRoundTrip.RunAsync(fixture.Container, "auth_client_rest", fixture.Container.GetConnectionString);
    }

    [Fact]
    public Task ClientRunsStatementsOverGrpc()
    {
        return ClientRoundTrip.RunAsync(fixture.Container, "auth_client_grpc", fixture.Container.GetGrpcConnectionString);
    }
}

internal static class ClientRoundTrip
{
    /// <summary>
    /// Creates a database and a table with the container helpers, then writes and reads a row with
    /// <c>CamusDB.Client</c> over the connection string that <paramref name="connectionString" /> returns.
    /// </summary>
    public static async Task RunAsync(CamusDbContainer container, string database, Func<string?, string> connectionString)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await container.CreateDatabaseAsync(database, ct);
        await container.ExecuteDdlAsync(database, "CREATE TABLE robots (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)", ct);

        await using CamusConnection connection = new(new CamusConnectionStringBuilder(connectionString(database)));
        await connection.OpenAsync(ct);

        await using (CamusCommand insert = connection.CreateCamusCommand("INSERT INTO robots (id, name) VALUES (1, 'R2-D2')"))
        {
            Assert.Equal(1, await insert.ExecuteNonQueryAsync(ct));
        }

        await using CamusCommand select = connection.CreateCamusCommand("SELECT name FROM robots WHERE id = 1");
        await using CamusDataReader reader = await select.ExecuteReaderAsync(ct);

        Assert.True(await reader.ReadAsync(ct));
        Assert.Equal("R2-D2", reader.GetString(0));
    }
}
