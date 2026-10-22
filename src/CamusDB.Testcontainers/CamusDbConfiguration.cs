namespace CamusDB.Testcontainers;

/// <inheritdoc cref="ContainerConfiguration" />
[PublicAPI]
public sealed class CamusDbConfiguration : ContainerConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbConfiguration" /> class.
    /// </summary>
    /// <param name="username">The bootstrap superuser name, when authentication is enabled.</param>
    /// <param name="password">The bootstrap superuser password, when authentication is enabled.</param>
    /// <param name="settings">Settings written to the server's <c>config.yml</c>.</param>
    public CamusDbConfiguration(
        string? username = null,
        string? password = null,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        Username = username;
        Password = password;
        Settings = settings;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbConfiguration" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public CamusDbConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbConfiguration" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public CamusDbConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbConfiguration" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public CamusDbConfiguration(CamusDbConfiguration resourceConfiguration)
        : this(new CamusDbConfiguration(), resourceConfiguration)
    {
        // Passes the configuration upwards to the base implementations to create an updated immutable copy.
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbConfiguration" /> class.
    /// </summary>
    /// <param name="oldValue">The old Docker resource configuration.</param>
    /// <param name="newValue">The new Docker resource configuration.</param>
    public CamusDbConfiguration(CamusDbConfiguration oldValue, CamusDbConfiguration newValue)
        : base(oldValue, newValue)
    {
        Username = BuildConfiguration.Combine(oldValue.Username, newValue.Username);
        Password = BuildConfiguration.Combine(oldValue.Password, newValue.Password);
        Settings = BuildConfiguration.Combine(oldValue.Settings, newValue.Settings);
    }

    /// <summary>
    /// Gets the bootstrap superuser name, or <c>null</c> when authentication is disabled.
    /// </summary>
    public string? Username { get; }

    /// <summary>
    /// Gets the bootstrap superuser password, or <c>null</c> when authentication is disabled.
    /// </summary>
    public string? Password { get; }

    /// <summary>
    /// Gets the settings written to the server's <c>config.yml</c>. A dotted key such as
    /// <c>kahuna.backup_dir</c> is written as a nested YAML mapping.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Settings { get; }

    /// <summary>
    /// Gets a value indicating whether authentication is enabled or not.
    /// </summary>
    public bool AuthenticationEnabled => Username != null;
}
