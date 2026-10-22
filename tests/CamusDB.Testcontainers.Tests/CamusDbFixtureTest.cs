namespace CamusDB.Testcontainers.Tests;

public sealed class SeededCamusDbFixture(IMessageSink messageSink) : CamusDbFixture(messageSink)
{
    public const string Database = "inventory";

    protected override CamusDbBuilder Configure()
    {
        return new CamusDbBuilder().WithMemoryProfile("dev");
    }

    protected override async Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
    {
        await container.CreateDatabaseAsync(Database, ct);
        await container.ExecuteDdlAsync(Database, "CREATE TABLE items (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)", ct);
        await container.ExecuteNonQueryAsync(Database, "INSERT INTO items (id, name) VALUES (1, 'bolt')", ct);
    }
}

public sealed class CamusDbFixtureTest(SeededCamusDbFixture fixture) : IClassFixture<SeededCamusDbFixture>
{
    [Fact]
    public async Task FixtureSeedsTheDatabaseBeforeTheTests()
    {
        string rows = await fixture.Container.ExecuteQueryAsync(
            SeededCamusDbFixture.Database,
            "SELECT name FROM items WHERE id = 1",
            TestContext.Current.CancellationToken);

        Assert.Contains("bolt", rows);
    }

    [Fact]
    public void FixtureExposesTheContainerAddresses()
    {
        Assert.Equal(fixture.Container.GetHttpAddress(), fixture.HttpAddress);
        Assert.Equal(fixture.Container.GetGrpcAddress(), fixture.GrpcAddress);
    }
}
