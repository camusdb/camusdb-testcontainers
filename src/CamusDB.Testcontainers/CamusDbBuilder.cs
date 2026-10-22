namespace CamusDB.Testcontainers;

/// <inheritdoc cref="ContainerBuilder{TBuilderEntity, TContainerEntity, TConfigurationEntity}" />
/// <remarks>
/// Starts the standalone CamusDB server image (<c>camusdb/camusdb</c>). The JSON/HTTP API listens on
/// <see cref="HttpPort" /> and the gRPC API listens on <see cref="GrpcPort" />, as plaintext HTTP/2.
/// </remarks>
[PublicAPI]
public sealed class CamusDbBuilder : ContainerBuilder<CamusDbBuilder, CamusDbContainer, CamusDbConfiguration>
{
    /// <summary>
    /// The default Docker image, pinned to a published release.
    /// </summary>
    public const string CamusDbImage = "camusdb/camusdb:0.15.3";

    /// <summary>
    /// The container port of the JSON/HTTP API.
    /// </summary>
    public const ushort HttpPort = 5095;

    /// <summary>
    /// The container port of the gRPC API.
    /// </summary>
    public const ushort GrpcPort = 5096;

    /// <summary>
    /// The path in the container where the module writes the settings from <see cref="WithSetting" />.
    /// </summary>
    public const string ConfigurationFilePath = "/etc/camusdb/config.yml";

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbBuilder" /> class with the
    /// <see cref="CamusDbImage" /> image.
    /// </summary>
    public CamusDbBuilder()
        : this(CamusDbImage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbBuilder" /> class.
    /// </summary>
    /// <param name="image">
    /// The full Docker image name, including the image repository and tag
    /// (e.g., <c>camusdb/camusdb:0.15.3</c>).
    /// </param>
    /// <remarks>
    /// Docker image tags available at <see href="https://hub.docker.com/r/camusdb/camusdb/tags" />.
    /// </remarks>
    public CamusDbBuilder(string image)
        : this(new DockerImage(image))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbBuilder" /> class.
    /// </summary>
    /// <param name="image">
    /// An <see cref="IImage" /> instance that specifies the Docker image to be used
    /// for the container builder configuration.
    /// </param>
    public CamusDbBuilder(IImage image)
        : this(new CamusDbConfiguration())
    {
        DockerResourceConfiguration = Init().WithImage(image).DockerResourceConfiguration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CamusDbBuilder" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    private CamusDbBuilder(CamusDbConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    /// <inheritdoc />
    protected override CamusDbConfiguration DockerResourceConfiguration { get; }

    /// <summary>
    /// Enables authentication and seeds a bootstrap superuser with the given credentials.
    /// </summary>
    /// <remarks>
    /// The module generates a random access-token server key. It also turns off
    /// <c>require_tls_when_auth_enabled</c>, because the test host reaches the container over
    /// plaintext HTTP from a non-loopback address.
    /// </remarks>
    /// <param name="username">The bootstrap superuser name.</param>
    /// <param name="password">The bootstrap superuser password.</param>
    /// <returns>A configured instance of <see cref="CamusDbBuilder" />.</returns>
    public CamusDbBuilder WithAuthentication(string username, string password)
    {
        string tokenKey = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        return Merge(DockerResourceConfiguration, new CamusDbConfiguration(username: username, password: password))
            .WithEnvironment("CAMUSDB_AUTH_ENABLED", "true")
            .WithEnvironment("CAMUSDB_BOOTSTRAP_USER", username)
            .WithEnvironment("CAMUSDB_BOOTSTRAP_PASSWORD", password)
            .WithEnvironment("CAMUSDB_AUTH_TOKEN_KEY", tokenKey)
            .WithSetting("require_tls_when_auth_enabled", "false");
    }

    /// <summary>
    /// Sets a server setting in the <c>config.yml</c> that the module mounts into the container.
    /// </summary>
    /// <remarks>
    /// The value is written as a raw YAML scalar, so <c>true</c> and <c>4</c> stay a boolean and a number.
    /// A dotted key such as <c>kahuna.backup_dir</c> is written as a nested mapping. The image's command
    /// line sets the HTTP port, the data directory and the partition count, and the command line wins
    /// over <c>config.yml</c>, so these three cannot be changed here.
    /// </remarks>
    /// <param name="key">The setting name, as it appears in <c>config.yml</c>.</param>
    /// <param name="value">The setting value.</param>
    /// <returns>A configured instance of <see cref="CamusDbBuilder" />.</returns>
    public CamusDbBuilder WithSetting(string key, string value)
    {
        Dictionary<string, string> settings = new(StringComparer.Ordinal) { [key] = value };
        return Merge(DockerResourceConfiguration, new CamusDbConfiguration(settings: settings));
    }

    /// <summary>
    /// Sets the memory profile. Use <c>dev</c> to cap the server caches at about 96 MiB, which suits a
    /// test host that runs many containers. The server default is <c>prod</c>.
    /// </summary>
    /// <param name="profile">The memory profile: <c>dev</c> or <c>prod</c>.</param>
    /// <returns>A configured instance of <see cref="CamusDbBuilder" />.</returns>
    public CamusDbBuilder WithMemoryProfile(string profile)
    {
        return WithSetting("memory_profile", profile);
    }

    /// <summary>
    /// Sets the number of Raft partitions. One partition keeps every transaction on the one-phase
    /// commit path. The image default is 4.
    /// </summary>
    /// <param name="partitions">The number of partitions.</param>
    /// <returns>A configured instance of <see cref="CamusDbBuilder" />.</returns>
    public CamusDbBuilder WithPartitions(int partitions)
    {
        return WithEnvironment("CAMUS_INITIAL_PARTITIONS", partitions.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public override CamusDbContainer Build()
    {
        Validate();

        var camusDbBuilder = this;

        if (DockerResourceConfiguration.Settings is { Count: > 0 } settings)
        {
            camusDbBuilder = camusDbBuilder
                .WithResourceMapping(Encoding.UTF8.GetBytes(ToYaml(settings)), ConfigurationFilePath)
                .WithEnvironment("CAMUS_CONFIG_PATH", ConfigurationFilePath);
        }

        // The HTTP listener starts before the storage node, so /health answers before the server can
        // run a statement. If the user does not provide a custom waiting strategy, wait until a real
        // request succeeds.
        camusDbBuilder = DockerResourceConfiguration.WaitStrategies.Count() > 1
            ? camusDbBuilder
            : camusDbBuilder.WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new WaitUntil(DockerResourceConfiguration)));

        return new CamusDbContainer(camusDbBuilder.DockerResourceConfiguration);
    }

    /// <inheritdoc />
    protected override CamusDbBuilder Init()
    {
        return base.Init()
            .WithPortBinding(HttpPort, true)
            .WithPortBinding(GrpcPort, true)
            .WithConnectionStringProvider(new CamusDbConnectionStringProvider());
    }

    /// <inheritdoc />
    protected override void Validate()
    {
        base.Validate();

        if (DockerResourceConfiguration.AuthenticationEnabled)
        {
            _ = Guard.Argument(DockerResourceConfiguration.Username, nameof(DockerResourceConfiguration.Username))
                .NotNull()
                .NotEmpty();

            _ = Guard.Argument(DockerResourceConfiguration.Password, nameof(DockerResourceConfiguration.Password))
                .NotNull()
                .NotEmpty();
        }

        if (DockerResourceConfiguration.Settings is { } settings)
        {
            _ = ToYaml(settings);
        }
    }

    /// <inheritdoc />
    protected override CamusDbBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new CamusDbConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override CamusDbBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new CamusDbConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override CamusDbBuilder Merge(CamusDbConfiguration oldValue, CamusDbConfiguration newValue)
    {
        return new CamusDbBuilder(new CamusDbConfiguration(oldValue, newValue));
    }

    /// <summary>
    /// Writes the settings as a YAML document. Dotted keys become nested mappings, and the keys are
    /// sorted so the same settings always produce the same file.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a key is both a value and the parent of another key.</exception>
    internal static string ToYaml(IReadOnlyDictionary<string, string> settings)
    {
        SortedDictionary<string, object> root = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> setting in settings)
        {
            string[] path = setting.Key.Split('.');
            if (path.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException($"The setting key '{setting.Key}' is not valid.", nameof(settings));

            SortedDictionary<string, object> node = root;
            for (int i = 0; i < path.Length - 1; i++)
            {
                if (!node.TryGetValue(path[i], out object? child))
                {
                    child = new SortedDictionary<string, object>(StringComparer.Ordinal);
                    node[path[i]] = child;
                }

                node = child as SortedDictionary<string, object>
                    ?? throw new ArgumentException($"The setting key '{setting.Key}' conflicts with the value of '{string.Join(".", path.Take(i + 1))}'.", nameof(settings));
            }

            if (node.TryGetValue(path[^1], out object? existing) && existing is SortedDictionary<string, object>)
                throw new ArgumentException($"The setting key '{setting.Key}' conflicts with a nested setting under it.", nameof(settings));

            node[path[^1]] = setting.Value;
        }

        StringBuilder yaml = new();
        AppendYaml(yaml, root, 0);
        return yaml.ToString();
    }

    private static void AppendYaml(StringBuilder yaml, SortedDictionary<string, object> node, int depth)
    {
        string indent = new(' ', depth * 2);

        foreach (KeyValuePair<string, object> entry in node)
        {
            if (entry.Value is SortedDictionary<string, object> child)
            {
                yaml.Append(indent).Append(entry.Key).Append(':').Append('\n');
                AppendYaml(yaml, child, depth + 1);
            }
            else
            {
                yaml.Append(indent).Append(entry.Key).Append(": ").Append(entry.Value).Append('\n');
            }
        }
    }

    /// <inheritdoc cref="IWaitUntil" />
    private sealed class WaitUntil : IWaitUntil
    {
        private readonly CamusDbConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="WaitUntil" /> class.
        /// </summary>
        /// <param name="configuration">The container configuration.</param>
        public WaitUntil(CamusDbConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Checks whether the server finished its startup or not.
        /// </summary>
        /// <remarks>
        /// With authentication on, the check logs in as the bootstrap superuser. The server seeds that
        /// user as the last startup step, so a successful login means the startup is complete. With
        /// authentication off, the check runs <c>SHOW DATABASES</c>, which needs the storage node.
        /// </remarks>
        /// <param name="container">The starting container instance.</param>
        /// <returns>Task that completes and returns true when the server answers, otherwise false.</returns>
        public async Task<bool> UntilAsync(IContainer container)
        {
            Uri address = new UriBuilder(Uri.UriSchemeHttp, container.Hostname, container.GetMappedPublicPort(HttpPort)).Uri;

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

            try
            {
                if (_configuration.AuthenticationEnabled)
                {
                    _ = await CamusDbHttpApi.LoginAsync(address, _configuration.Username!, _configuration.Password!, timeout.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    _ = await CamusDbHttpApi.PostAsync(address, "/execute-sql-query", new { sql = "SHOW DATABASES" }, null, timeout.Token)
                        .ConfigureAwait(false);
                }

                return true;
            }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException or OperationCanceledException)
            {
                return false;
            }
        }
    }
}
