namespace CamusDB.Testcontainers.Tests;

public sealed class DevProfileCamusDbFixture(IMessageSink messageSink) : CamusDbFixture(messageSink)
{
    protected override CamusDbBuilder Configure()
    {
        return new CamusDbBuilder().WithMemoryProfile("dev").WithPartitions(1);
    }
}

public sealed class CamusDbSettingsTest(DevProfileCamusDbFixture fixture) : IClassFixture<DevProfileCamusDbFixture>
{
    [Fact]
    public async Task ServerReadsTheMountedConfigurationFile()
    {
        (string stdout, string stderr) = await fixture.Container.GetLogsAsync(ct: TestContext.Current.CancellationToken);

        Assert.Contains("Memory profile: dev", stdout + stderr);
    }

    [Fact]
    public async Task ServerRunsStatementsWithOnePartition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await fixture.Container.CreateDatabaseAsync("small", ct);
        await fixture.Container.ExecuteDdlAsync("small", "CREATE TABLE t (id int64 PRIMARY KEY NOT NULL)", ct);
        await fixture.Container.ExecuteNonQueryAsync("small", "INSERT INTO t (id) VALUES (7)", ct);

        string rows = await fixture.Container.ExecuteQueryAsync("small", "SELECT id FROM t", ct);

        Assert.Contains("7", rows);
    }
}
