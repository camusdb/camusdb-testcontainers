# CamusDB Testcontainers

[Testcontainers](https://dotnet.testcontainers.org/) modules for [CamusDB](https://github.com/camusdb/camusdb).
A module starts a temporary CamusDB server in Docker for an integration test. When the test ends,
the module removes the container.

| Package | Use |
| --- | --- |
| `CamusDB.Testcontainers` | The module: `CamusDbBuilder` and `CamusDbContainer`. It works with any test framework. |
| `CamusDB.Testcontainers.Xunit` | `CamusDbFixture` and `CamusDbAdoNetFixture` for xUnit.net v2. They are built on `Testcontainers.Xunit`. |
| `CamusDB.Testcontainers.XunitV3` | `CamusDbFixture` and `CamusDbAdoNetFixture` for xUnit.net v3. They are built on `Testcontainers.XunitV3`. |

All packages target .NET 8, .NET 9 and .NET 10. They need a Docker host that Testcontainers can find.

## Quick start

```shell
dotnet add package CamusDB.Testcontainers
```

```csharp
using CamusDB.Testcontainers;

await using CamusDbContainer camusDb = new CamusDbBuilder().Build();
await camusDb.StartAsync();

await camusDb.CreateDatabaseAsync("shop");
await camusDb.ExecuteDdlAsync("shop", "CREATE TABLE products (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)");
await camusDb.ExecuteNonQueryAsync("shop", "INSERT INTO products (id, name) VALUES (1, 'kettle')");

string json = await camusDb.ExecuteQueryAsync("shop", "SELECT name FROM products");
```

`StartAsync` returns when the server can run a statement. The `/health` endpoint is not a
sufficient check, because the HTTP listener starts before the storage node. Thus the module sends a
real request (`SHOW DATABASES`, or a login when authentication is on) until the request succeeds.

## Connection strings

The container returns connection strings for [`CamusDB.Client`](https://www.nuget.org/packages/CamusDB.Client),
the CamusDB ADO.NET provider. The packages do not reference `CamusDB.Client`. Add it to your test
project yourself:

```shell
dotnet add package CamusDB.Client
```

```csharp
using CamusDB.Client;

await using CamusConnection connection = new(new CamusConnectionStringBuilder(camusDb.GetConnectionString("shop")));
await connection.OpenAsync();

await using CamusCommand select = connection.CreateCamusCommand("SELECT name FROM products");
await using CamusDataReader reader = await select.ExecuteReaderAsync();
```

| Method | Returns |
| --- | --- |
| `GetConnectionString()` | A connection string for the JSON/HTTP API, without a database: `Endpoint=http://127.0.0.1:32768`. |
| `GetConnectionString(database)` | The same string with the database: `Endpoint=http://127.0.0.1:32768;Database=shop`. |
| `GetGrpcConnectionString(database)` | A connection string for the gRPC API: `Endpoint=http://127.0.0.1:32769;Protocol=grpc;BackupEndpoint=http://127.0.0.1:32768;Database=shop`. The database is optional. |
| `GetHttpAddress()` | The JSON/HTTP API address only, e.g. `http://127.0.0.1:32768`. |
| `GetGrpcAddress()` | The gRPC API address only, e.g. `http://127.0.0.1:32769`. The port uses plaintext HTTP/2. |

The container maps each API port to a random host port. Thus, get the strings after `StartAsync`.

The gRPC string sets `BackupEndpoint` to the JSON/HTTP API, because the backup admin API is REST only.

When authentication is on, each connection string also contains these settings:

- `User` and `Password`: the bootstrap superuser credentials.
- `AllowInsecureCredentials=true`. The client refuses to send credentials in plaintext to an
  endpoint that is not loopback. On a remote Docker host, the container endpoint is not loopback.

The tests of this repository use `CamusDB.Client` 0.16.0 with both strings, with and without
authentication. `CamusDB.Dapper` and `CamusDB.EntityFrameworkCore` use `CamusDB.Client`, so they
take the same strings. These two packages are not tested here.

## Helper methods

The helper methods use the JSON/HTTP API. Each method returns the JSON response body as a string.
If the server returns a status code that is not 2xx, the method throws `InvalidOperationException`.

| Method | HTTP endpoint |
| --- | --- |
| `CreateDatabaseAsync(name)` | `/create-db`. If the database exists, the method does nothing. |
| `ExecuteDdlAsync(database, sql)` | `/execute-sql-ddl` |
| `ExecuteNonQueryAsync(database, sql)` | `/execute-sql-non-query` |
| `ExecuteQueryAsync(database, sql)` | `/execute-sql-query` |
| `GetAccessTokenAsync()` | `/login`. It returns a bearer token. Use it only when authentication is on. |

## Configuration

| Builder method | Effect |
| --- | --- |
| `new CamusDbBuilder(image)` | Sets the image. The default is `camusdb/camusdb:0.15.3`. |
| `WithAuthentication(username, password)` | Turns on authentication and creates a bootstrap superuser. |
| `WithMemoryProfile("dev")` | Limits the server caches to approximately 96 MiB. Use it when one host runs many containers. |
| `WithPartitions(n)` | Sets the number of Raft partitions. The image default is 4. |
| `WithSetting(key, value)` | Writes one setting to the server `config.yml`. |

All the usual Testcontainers builder methods are also available, for example `WithEnvironment`
and `WithReuse`.

### Authentication

```csharp
CamusDbContainer camusDb = new CamusDbBuilder()
    .WithAuthentication("admin", "s3cret-test-password")
    .Build();
```

The module sets the CamusDB authentication environment variables. It also generates a random
access-token server key. The helper methods log in as the bootstrap superuser and keep the token
until 30 seconds before it expires.

The module also sets `require_tls_when_auth_enabled: false`. The server refuses credentials over
plaintext HTTP from a non-loopback address. A request from the test host comes from such an address,
because Docker forwards it.

### Server settings

`WithSetting` writes each setting to `/etc/camusdb/config.yml` in the container. The module then
points `CAMUS_CONFIG_PATH` at that file. A dotted key becomes a nested YAML mapping:

```csharp
new CamusDbBuilder()
    .WithSetting("kahuna.backup_dir", "/data/backups")
    .WithSetting("query_result_cache_enabled", "false");
```

The module writes each value as a raw YAML scalar. Thus `true` stays a boolean and `4` stays a number.

The image command line sets the HTTP port, the data directory and the partition count. The command
line overrides `config.yml`, so `WithSetting` cannot change these three values. Use `WithPartitions`
for the partition count.

## xUnit.net fixture

```shell
dotnet add package CamusDB.Testcontainers.XunitV3   # or CamusDB.Testcontainers.Xunit for xUnit.net v2
```

`CamusDbFixture` starts one container for all the tests of a class. To prepare the database before
the first test, override `InitializeDatabaseAsync`:

```csharp
using CamusDB.Testcontainers;
using CamusDB.Testcontainers.Xunit;

public sealed class InventoryFixture(IMessageSink messageSink) : CamusDbFixture(messageSink)
{
    protected override CamusDbBuilder Configure()
    {
        return new CamusDbBuilder().WithMemoryProfile("dev");
    }

    protected override async Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
    {
        await container.CreateDatabaseAsync("inventory", ct);
        await container.ExecuteDdlAsync("inventory", "CREATE TABLE items (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)", ct);
    }
}

public sealed class InventoryTests(InventoryFixture fixture) : IClassFixture<InventoryFixture>
{
    [Fact]
    public async Task ReadsItems()
    {
        string json = await fixture.Container.ExecuteQueryAsync("inventory", "SELECT * FROM items");
        // ...
    }
}
```

To share one container across many test classes, use an xUnit collection fixture. xUnit requires
the `[CollectionDefinition]` class to be in the test assembly, so declare it in your test project:

```csharp
[CollectionDefinition(nameof(CamusDbCollection))]
public sealed class CamusDbCollection : ICollectionFixture<InventoryFixture>;

[Collection(nameof(CamusDbCollection))]
public sealed class OrderTests(InventoryFixture fixture) { /* ... */ }
```

If the container does not start, each test that reads `fixture.Container` fails with the start
exception.

### ADO.NET fixture

`CamusDbAdoNetFixture` extends the Testcontainers `DbContainerFixture`. It gives the tests ADO.NET
connections and commands: `OpenConnectionAsync()`, `CreateConnection()` and `CreateCommand(sql)`.
It needs `CamusProviderFactory`, the provider factory of `CamusDB.Client`.

> `CamusProviderFactory` is not in `CamusDB.Client` 0.16.0. It comes in the next release of
> `CamusDB.Client`.

The fixture package does not reference `CamusDB.Client`. Your fixture supplies the factory, so your
test project selects the client version:

```csharp
using System.Data.Common;
using CamusDB.Client;
using CamusDB.Testcontainers;
using CamusDB.Testcontainers.Xunit;

public sealed class RobotsFixture(IMessageSink messageSink) : CamusDbAdoNetFixture(messageSink)
{
    public override DbProviderFactory DbProviderFactory => CamusProviderFactory.Instance;

    protected override async Task InitializeDatabaseAsync(CamusDbContainer container, CancellationToken ct)
    {
        await using DbCommand create = CreateCommand("CREATE TABLE robots (id int64 PRIMARY KEY NOT NULL, name STRING NOT NULL)");
        await create.ExecuteNonQueryAsync(ct);
    }
}

public sealed class RobotTests(RobotsFixture fixture) : IClassFixture<RobotsFixture>
{
    [Fact]
    public async Task InsertsARobot()
    {
        await using DbCommand insert = fixture.CreateCommand("INSERT INTO robots (id, name) VALUES (1, 'R2-D2')");
        Assert.Equal(1, await insert.ExecuteNonQueryAsync());
    }
}
```

When the container starts, the fixture creates the database `DatabaseName`. The default name is
`test`. Then the fixture calls `InitializeDatabaseAsync`. The fixture `ConnectionString` names
that database, because a CamusDB connection needs a database.

The fixture uses the JSON/HTTP API. To use the gRPC API, override `ConnectionString`:

```csharp
public override string ConnectionString => Container.GetGrpcConnectionString(DatabaseName);
```

## Build and test

```shell
dotnet build
dotnet test      # needs a running Docker host
dotnet pack -c Release   # writes the packages to artifacts/packages
```

## Release

The `release` workflow publishes the packages to nuget.org when you push a tag such as `v0.1.0`.
The tag sets the package version. The workflow needs a `NUGET_API_KEY` repository secret.
