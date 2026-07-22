namespace Directory.Tests.Unit.Api;

using Extensions;
using Microsoft.Extensions.Configuration;

public sealed class ConfigurationExtensionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void GetRequired_KeyPresent_ReturnsValue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ApiKey", "secret")])
            .Build();

        var result = config.GetRequired<string>("ApiKey");

        Assert.Equal((string?)"secret", (string?)result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetRequired_KeyMissing_Throws()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => config.GetRequired<string>("ApiKey"));
    }
}
