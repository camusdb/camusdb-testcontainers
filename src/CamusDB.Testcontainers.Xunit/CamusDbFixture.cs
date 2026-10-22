namespace CamusDB.Testcontainers.Xunit;

/// <summary>
/// Fixture that shares one CamusDB container across the tests of a class or of a collection.
/// See <a href="https://xunit.net/docs/shared-context">Shared Context between Tests</a> from xUnit.net
/// documentation for more information about fixtures. A logger writes the container diagnostics to
/// xUnit's <see cref="IMessageSink" />.
/// </summary>
/// <example>
///   <code>
///   public sealed class OrdersFixture(IMessageSink messageSink) : CamusDbFixture(messageSink)
///   {
///     protected override CamusDbBuilder Configure()
///     {
///       return new CamusDbBuilder().WithMemoryProfile("dev");
///     }
///   <br />
///     protected override async Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
///     {
///       await container.CreateDatabaseAsync("orders", ct);
///       await container.ExecuteDdlAsync("orders", "CREATE TABLE items (id INT64 PRIMARY KEY NOT NULL, name STRING NOT NULL)", ct);
///     }
///   }
///   </code>
/// </example>
/// <param name="messageSink">An optional <see cref="IMessageSink" /> where the logs are written to. Pass <c>null</c> to ignore logs.</param>
[PublicAPI]
public class CamusDbFixture(IMessageSink messageSink)
    : ContainerFixture<CamusDbBuilder, CamusDbContainer>(messageSink)
{
    /// <summary>
    /// Gets the base address of the JSON/HTTP API.
    /// </summary>
    public string HttpAddress => Container.GetHttpAddress();

    /// <summary>
    /// Gets the address of the gRPC API.
    /// </summary>
    public string GrpcAddress => Container.GetGrpcAddress();

    /// <inheritdoc />
    protected override async LifetimeTask InitializeAsync()
    {
        await base.InitializeAsync()
            .ConfigureAwait(false);

        await FixtureInitializer.RunAsync(() => Container, InitializeDatabaseAsync)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Prepares the database after the container starts and before the first test runs, e.g. to
    /// create databases and tables or to load seed data. The default does nothing.
    /// </summary>
    /// <param name="container">The started container.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task that completes when the database is ready for the tests.</returns>
    protected virtual Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
