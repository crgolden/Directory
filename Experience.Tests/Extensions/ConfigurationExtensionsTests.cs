namespace Experience.Tests.Extensions;

using Azure.Core;
using Experience.Server.Extensions;
using Microsoft.Extensions.Configuration;
using Moq;

[Trait("Category", "Unit")]
public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void ToSecretClient_WithValidKeyVaultUri_ReturnsSecretClient()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KeyVaultUri"] = "https://test.vault.azure.net/"
            })
            .Build();
        var credential = new Mock<TokenCredential>();

        // Act
        var client = config.ToSecretClient(credential.Object);

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://test.vault.azure.net/"), client.VaultUri);
    }

    [Fact]
    public void ToSecretClient_WithMissingKeyVaultUri_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var credential = new Mock<TokenCredential>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            config.ToSecretClient(credential.Object));

        Assert.Contains("KeyVaultUri", exception.Message, StringComparison.Ordinal);
    }
}
