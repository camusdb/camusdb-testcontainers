namespace CamusDB.Testcontainers.Xunit;

/// <summary>
/// The post-start step that <see cref="CamusDbFixture" /> and <see cref="CamusDbAdoNetFixture" />
/// share: prepare the database once the container runs.
/// </summary>
internal static class FixtureInitializer
{
    /// <summary>
    /// Runs <paramref name="initialize" /> with the started container. Does nothing when the container
    /// did not start: the base fixture keeps the start exception and throws it again when a test reads
    /// the container, so each test reports the real cause.
    /// </summary>
    public static async Task RunAsync(Func<CamusDbContainer> container, Func<CamusDbContainer, CancellationToken, Task> initialize)
    {
        CamusDbContainer started;

        try
        {
            started = container();
        }
        catch
        {
            return;
        }

#if XUNIT_V3
        await initialize(started, TestContext.Current.CancellationToken)
#else
        await initialize(started, CancellationToken.None)
#endif
            .ConfigureAwait(false);
    }
}
