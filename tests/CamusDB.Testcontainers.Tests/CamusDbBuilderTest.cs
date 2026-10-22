namespace CamusDB.Testcontainers.Tests;

public sealed class CamusDbBuilderTest
{
    [Fact]
    public void ToYamlWritesDottedKeysAsNestedMappingsInKeyOrder()
    {
        Dictionary<string, string> settings = new()
        {
            ["require_tls_when_auth_enabled"] = "false",
            ["kahuna.backup_dir"] = "/data/backups",
            ["memory_profile"] = "dev",
            ["kahuna.block_cache_mb"] = "64",
        };

        string yaml = CamusDbBuilder.ToYaml(settings);

        Assert.Equal(
            "kahuna:\n" +
            "  backup_dir: /data/backups\n" +
            "  block_cache_mb: 64\n" +
            "memory_profile: dev\n" +
            "require_tls_when_auth_enabled: false\n",
            yaml);
    }

    [Theory]
    [InlineData("kahuna", "kahuna.backup_dir")]
    [InlineData("kahuna.backup_dir", "kahuna")]
    public void ToYamlRejectsAKeyThatIsBothAValueAndAParent(string first, string second)
    {
        Dictionary<string, string> settings = new() { [first] = "1", [second] = "2" };

        Assert.Throws<ArgumentException>(() => CamusDbBuilder.ToYaml(settings));
    }

    [Theory]
    [InlineData("")]
    [InlineData("kahuna.")]
    [InlineData(".memory_profile")]
    public void ToYamlRejectsAnEmptyKeySegment(string key)
    {
        Dictionary<string, string> settings = new() { [key] = "1" };

        Assert.Throws<ArgumentException>(() => CamusDbBuilder.ToYaml(settings));
    }

    [Fact]
    public void BuildRejectsAnEmptyPassword()
    {
        CamusDbBuilder builder = new CamusDbBuilder().WithAuthentication("admin", "");

        Assert.ThrowsAny<ArgumentException>(() => builder.Build());
    }
}
