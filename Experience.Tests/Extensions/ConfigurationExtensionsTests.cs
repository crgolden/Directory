namespace Experience.Tests.Extensions;

using System.Collections.Generic;
using Experience.Server.Extensions;
using Microsoft.Extensions.Configuration;

[Trait("Category", "Unit")]
public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void GetRequired_ReturnsValue_WhenKeyExists()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MyKey"] = "expected" })
            .Build();

        Assert.Equal("expected", config.GetRequired<string>("MyKey"));
    }

    [Fact]
    public void GetRequired_ThrowsWithKeyNameInMessage_WhenKeyIsMissing()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => config.GetRequired<string>("MissingKey"));
        Assert.Equal("Invalid 'MissingKey'.", ex.Message);
    }

    [Fact]
    public void GetExperienceSecrets_ReadsCorrectConfigurationKeys()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExperienceClientId"] = "client-id",
                ["ExperienceClientSecret"] = "client-secret",
            })
            .Build();

        var (id, secret) = config.GetExperienceSecrets();

        Assert.Equal("client-id", id);
        Assert.Equal("client-secret", secret);
    }
}
