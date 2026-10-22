namespace CamusDB.Testcontainers;

/// <summary>
/// Provides the CamusDB connection string.
/// </summary>
internal sealed class CamusDbConnectionStringProvider : ContainerConnectionStringProvider<CamusDbContainer, CamusDbConfiguration>
{
    /// <inheritdoc />
    protected override string GetHostConnectionString()
    {
        return Container.GetConnectionString();
    }
}
