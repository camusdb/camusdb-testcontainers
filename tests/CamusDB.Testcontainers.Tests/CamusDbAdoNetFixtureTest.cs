using System.Data;
using System.Data.Common;
using CamusDB.Client;

namespace CamusDB.Testcontainers.Tests;

/// <summary>
/// Stands in for <c>CamusProviderFactory</c>, which the next <c>CamusDB.Client</c> release adds.
/// <see cref="DbContainerFixture{TBuilderEntity, TContainerEntity}" /> only calls
/// <see cref="DbProviderFactory.CreateDataSource" />, so that is all this double provides.
/// </summary>
internal sealed class TestCamusProviderFactory : DbProviderFactory
{
    public static readonly TestCamusProviderFactory Instance = new();

    public override DbDataSource CreateDataSource(string connectionString) => new DataSource(connectionString);

    private sealed class DataSource(string connectionString) : DbDataSource
    {
        public override string ConnectionString => connectionString;

        protected override DbConnection CreateDbConnection() => new CamusConnection(new CamusConnectionStringBuilder(connectionString));
    }
}

public sealed class RobotsAdoNetFixture(IMessageSink messageSink) : CamusDbAdoNetFixture(messageSink)
{
    public override DbProviderFactory DbProviderFactory => TestCamusProviderFactory.Instance;

    public override string DatabaseName => "robots_db";

    protected override async Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
    {
        await using DbCommand create = CreateCommand("CREATE TABLE robots (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)");
        await create.ExecuteNonQueryAsync(ct);

        await using DbCommand insert = CreateCommand("INSERT INTO robots (id, name) VALUES (1, 'R2-D2')");
        await insert.ExecuteNonQueryAsync(ct);
    }
}

public sealed class CamusDbAdoNetFixtureTest(RobotsAdoNetFixture fixture) : IClassFixture<RobotsAdoNetFixture>
{
    [Fact]
    public void ConnectionStringNamesTheFixtureDatabase()
    {
        Assert.Equal(fixture.Container.GetConnectionString("robots_db"), fixture.ConnectionString);
    }

    [Fact]
    public async Task SeedDataIsVisibleThroughAnOpenConnection()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using DbConnection connection = await fixture.OpenConnectionAsync(ct);
        Assert.Equal(ConnectionState.Open, connection.State);

        await using DbCommand select = connection.CreateCommand();
        select.CommandText = "SELECT name FROM robots WHERE id = 1";

        Assert.Equal("R2-D2", await select.ExecuteScalarAsync(ct));
    }

    [Fact]
    public async Task CommandsFromTheFixtureWriteAndRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using (DbCommand insert = fixture.CreateCommand("INSERT INTO robots (id, name) VALUES (2, 'C-3PO')"))
        {
            Assert.Equal(1, await insert.ExecuteNonQueryAsync(ct));
        }

        await using DbCommand select = fixture.CreateCommand("SELECT name FROM robots WHERE id = 2");

        Assert.Equal("C-3PO", await select.ExecuteScalarAsync(ct));
    }
}
