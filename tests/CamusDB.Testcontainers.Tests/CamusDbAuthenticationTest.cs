namespace CamusDB.Testcontainers.Tests;

public sealed class AuthenticatedCamusDbFixture(IMessageSink messageSink) : CamusDbFixture(messageSink)
{
    public const string Username = "admin";

    // The ';' and '=' check that the connection string quotes the password.
    public const string Password = "s3cret;test=password";

    protected override CamusDbBuilder Configure()
    {
        return new CamusDbBuilder().WithAuthentication(Username, Password);
    }
}

public sealed class CamusDbAuthenticationTest(AuthenticatedCamusDbFixture fixture) : IClassFixture<AuthenticatedCamusDbFixture>
{
    [Fact]
    public async Task HelpersAuthenticateAsTheBootstrapSuperuser()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await fixture.Container.CreateDatabaseAsync("secure", ct);
        await fixture.Container.ExecuteDdlAsync("secure", "CREATE TABLE notes (id int64 PRIMARY KEY NOT NULL, body STRING NOT NULL)", ct);
        await fixture.Container.ExecuteNonQueryAsync("secure", "INSERT INTO notes (id, body) VALUES (1, 'hello')", ct);

        string rows = await fixture.Container.ExecuteQueryAsync("secure", "SELECT body FROM notes", ct);

        Assert.Contains("hello", rows);
    }

    [Fact]
    public async Task RequestWithoutATokenIsRejected()
    {
        using HttpClient client = new();

        using HttpResponseMessage response = await client.PostAsync(
            new Uri(new Uri(fixture.Container.GetHttpAddress()), "/execute-sql-query"),
            new StringContent("{\"sql\":\"SHOW DATABASES\"}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccessTokenIsCached()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        string first = await fixture.Container.GetAccessTokenAsync(ct);
        string second = await fixture.Container.GetAccessTokenAsync(ct);

        Assert.False(string.IsNullOrEmpty(first));
        Assert.Equal(first, second);
    }
}
