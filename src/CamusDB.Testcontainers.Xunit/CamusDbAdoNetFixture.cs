namespace CamusDB.Testcontainers.Xunit;

/// <summary>
/// Fixture that shares one CamusDB container and gives the tests ADO.NET connections and commands
/// to it, through <see cref="DbContainerFixture{TBuilderEntity, TContainerEntity}" />: e.g.
/// <c>OpenConnectionAsync()</c> and <c>CreateCommand(sql)</c>.
/// </summary>
/// <remarks>
/// The package does not reference <c>CamusDB.Client</c>. A derived fixture supplies the provider
/// factory, so the test project chooses the client version. The fixture creates
/// <see cref="DatabaseName" /> when the container starts, and <see cref="ConnectionString" /> names
/// it, because a CamusDB connection needs a database.
/// </remarks>
/// <example>
///   <code>
///   public sealed class RobotsFixture(IMessageSink messageSink) : CamusDbAdoNetFixture(messageSink)
///   {
///     public override DbProviderFactory DbProviderFactory =&gt; CamusProviderFactory.Instance;
///   <br />
///     protected override async Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
///     {
///       await using DbCommand command = CreateCommand("CREATE TABLE robots (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)");
///       await command.ExecuteNonQueryAsync(ct);
///     }
///   }
///   </code>
/// </example>
/// <param name="messageSink">An optional <see cref="IMessageSink" /> where the logs are written to. Pass <c>null</c> to ignore logs.</param>
[PublicAPI]
public abstract class CamusDbAdoNetFixture(IMessageSink messageSink)
    : DbContainerFixture<CamusDbBuilder, CamusDbContainer>(messageSink)
{
    /// <summary>
    /// Gets the database that the fixture creates and that <see cref="ConnectionString" /> names.
    /// The default is <c>test</c>.
    /// </summary>
    public virtual string DatabaseName => "test";

    /// <summary>
    /// Gets the connection string for <see cref="DatabaseName" /> over the JSON/HTTP API. Override it
    /// with <c>Container.GetGrpcConnectionString(DatabaseName)</c> to use the gRPC API.
    /// </summary>
    public override string ConnectionString => Container.GetConnectionString(DatabaseName);

    /// <inheritdoc />
    protected override async LifetimeTask InitializeAsync()
    {
        await base.InitializeAsync()
            .ConfigureAwait(false);

        await FixtureInitializer.RunAsync(() => Container, CreateDatabaseAndInitializeAsync)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Prepares the database after the fixture creates <see cref="DatabaseName" /> and before the
    /// first test runs, e.g. to create tables or to load seed data. The connection and command methods
    /// of the fixture are available here. The default does nothing.
    /// </summary>
    /// <param name="container">The started container.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task that completes when the database is ready for the tests.</returns>
    protected virtual Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private async Task CreateDatabaseAndInitializeAsync(CamusDbContainer container, CancellationToken ct)
    {
        await container.CreateDatabaseAsync(DatabaseName, ct)
            .ConfigureAwait(false);

        await InitializeDatabaseAsync(container, ct)
            .ConfigureAwait(false);
    }
}
