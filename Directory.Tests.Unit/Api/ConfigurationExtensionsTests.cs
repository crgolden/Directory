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

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDirectorySecrets_AllKeysPresent_ReturnsValues()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("SqlConnectionStringBuilder:UserID", "sa"),
                new("SqlConnectionStringBuilder:Password", "pass"),
                new("ServiceBusConnectionString", "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc="),
            ])
            .Build();

        var (userId, password, sbConn) = config.GetDirectorySecrets();

        Assert.Equal("sa", userId);
        Assert.Equal("pass", password);
        Assert.Equal("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc=", sbConn);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDirectorySecrets_MissingKey_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("SqlConnectionStringBuilder:UserID", "sa"),
                new("SqlConnectionStringBuilder:Password", "pass"),
            ])
            .Build();

        Assert.Throws<InvalidOperationException>(() => config.GetDirectorySecrets());
    }
}
