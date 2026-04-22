namespace Experience.Tests.Extensions;

using Azure.Core;
using Experience.Server.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

[Trait("Category", "Unit")]
public sealed class HostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddObservability_WithValidConfig_ReturnsBuilder()
    {
        // Arrange
        var builder = new Mock<IHostApplicationBuilder>();
        var configuration = new Mock<IConfigurationManager>();
        var configurationSection = new Mock<IConfigurationSection>();
        var serviceCollection = new ServiceCollection();
        var loggingBuilder = new Mock<ILoggingBuilder>();
        loggingBuilder.SetupGet(x => x.Services).Returns(serviceCollection);
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.ApplicationName).Returns(Guid.NewGuid().ToString);
        const string path = "ElasticsearchNode";
        configurationSection.SetupGet(x => x.Path).Returns(path);
        configurationSection.SetupGet(x => x.Value).Returns("http://localhost:9200");
        configuration.Setup(x => x.GetSection(path)).Returns(configurationSection.Object);
        builder.SetupGet(x => x.Configuration).Returns(configuration.Object);
        builder.SetupGet(x => x.Logging).Returns(loggingBuilder.Object);
        builder.SetupGet(x => x.Environment).Returns(environment.Object);
        builder.SetupGet(x => x.Services).Returns(serviceCollection);

        // Act
        var result = builder.Object.AddObservability(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        // Assert
        Assert.Same(builder.Object, result);
        builder.VerifyGet(x => x.Configuration, Times.Exactly(2));
        builder.VerifyGet(x => x.Services, Times.Exactly(2));
        builder.VerifyGet(x => x.Environment, Times.Exactly(3));
        loggingBuilder.VerifyGet(x => x.Services, Times.Exactly(3));
        configuration.Verify(x => x.GetSection(path), Times.Once);
        configurationSection.VerifyGet(x => x.Value, Times.Once);
        configurationSection.VerifyGet(x => x.Path, Times.Once);
        environment.Verify(x => x.ApplicationName, Times.Once);
        environment.Verify(x => x.EnvironmentName, Times.Exactly(2));
    }

    [Fact]
    public void AddObservability_WithMissingElasticsearchNode_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new Mock<IHostApplicationBuilder>();
        var configuration = new Mock<IConfigurationManager>();
        var configurationSection = new Mock<IConfigurationSection>();
        const string path = "ElasticsearchNode";
        configurationSection.SetupGet(x => x.Path).Returns(path);
        configurationSection.SetupGet(x => x.Value).Returns((string?)null);
        configuration.Setup(x => x.GetSection(path)).Returns(configurationSection.Object);
        builder.SetupGet(x => x.Configuration).Returns(configuration.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            builder.Object.AddObservability(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
    }

    [Fact]
    public void AddAuth_WithValidSecrets_ReturnsBuilder()
    {
        // Arrange
        var builder = new Mock<IHostApplicationBuilder>();
        var configuration = new Mock<IConfigurationManager>();
        var configurationSection = new Mock<IConfigurationSection>();
        var serviceCollection = new ServiceCollection();
        const string oidcAuthorityKey = "OidcAuthority";
        configurationSection.SetupGet(x => x.Path).Returns(oidcAuthorityKey);
        configurationSection.SetupGet(x => x.Value).Returns("https://login.microsoftonline.com/tenant");
        configuration.Setup(x => x.GetSection(oidcAuthorityKey)).Returns(configurationSection.Object);
        builder.SetupGet(x => x.Configuration).Returns(configuration.Object);
        builder.SetupGet(x => x.Services).Returns(serviceCollection);

        // Act
        var result = builder.Object.AddAuth(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        // Assert
        Assert.Same(builder.Object, result);
        builder.VerifyGet(x => x.Configuration, Times.Once);
        builder.VerifyGet(x => x.Services, Times.Once);
        configuration.Verify(x => x.GetSection(oidcAuthorityKey), Times.Once);
        configurationSection.VerifyGet(x => x.Value, Times.Once);
        configurationSection.VerifyGet(x => x.Path, Times.Once);
    }

    [Fact]
    public void AddDataProtection_WithValidConfig_ReturnsBuilder()
    {
        // Arrange
        var builder = new Mock<IHostApplicationBuilder>();
        var configuration = new Mock<IConfigurationManager>();
        var serviceCollection = new ServiceCollection();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.ApplicationName).Returns(Guid.NewGuid().ToString);
        const string blobUriKey = "BlobUri";
        const string dpKeyKey = "DataProtectionKeyIdentifier";
        var blobSection = new Mock<IConfigurationSection>();
        blobSection.SetupGet(x => x.Path).Returns(blobUriKey);
        blobSection.SetupGet(x => x.Value).Returns("https://account.blob.core.windows.net/container/keys.xml");
        var dpSection = new Mock<IConfigurationSection>();
        dpSection.SetupGet(x => x.Path).Returns(dpKeyKey);
        dpSection.SetupGet(x => x.Value).Returns("https://test.vault.azure.net/keys/dpkey/version");
        configuration.Setup(x => x.GetSection(blobUriKey)).Returns(blobSection.Object);
        configuration.Setup(x => x.GetSection(dpKeyKey)).Returns(dpSection.Object);
        builder.SetupGet(x => x.Configuration).Returns(configuration.Object);
        builder.SetupGet(x => x.Services).Returns(serviceCollection);
        builder.SetupGet(x => x.Environment).Returns(environment.Object);
        var credential = new Mock<TokenCredential>();

        // Act
        var result = builder.Object.AddDataProtection(credential.Object);

        // Assert
        Assert.Same(builder.Object, result);
        builder.VerifyGet(x => x.Configuration, Times.Exactly(2));
        builder.VerifyGet(x => x.Services, Times.Once);
        builder.VerifyGet(x => x.Environment, Times.Once);
        configuration.Verify(x => x.GetSection(blobUriKey), Times.Once);
        configuration.Verify(x => x.GetSection(dpKeyKey), Times.Once);
        environment.VerifyGet(x => x.ApplicationName, Times.Once);
    }

    [Fact]
    public void AddDataProtection_WithMissingBlobUri_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new Mock<IHostApplicationBuilder>();
        var configuration = new Mock<IConfigurationManager>();
        const string blobUriKey = "BlobUri";
        var blobSection = new Mock<IConfigurationSection>();
        blobSection.SetupGet(x => x.Path).Returns(blobUriKey);
        blobSection.SetupGet(x => x.Value).Returns((string?)null);
        configuration.Setup(x => x.GetSection(blobUriKey)).Returns(blobSection.Object);
        builder.SetupGet(x => x.Configuration).Returns(configuration.Object);
        var credential = new Mock<TokenCredential>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Object.AddDataProtection(credential.Object));

        Assert.Contains("BlobUri", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddDataProtection_WithMissingDataProtectionKeyIdentifier_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new Mock<IHostApplicationBuilder>();
        var configuration = new Mock<IConfigurationManager>();
        const string blobUriKey = "BlobUri";
        const string dpKeyKey = "DataProtectionKeyIdentifier";
        var blobSection = new Mock<IConfigurationSection>();
        blobSection.SetupGet(x => x.Path).Returns(blobUriKey);
        blobSection.SetupGet(x => x.Value).Returns("https://account.blob.core.windows.net/container/keys.xml");
        var dpSection = new Mock<IConfigurationSection>();
        dpSection.SetupGet(x => x.Path).Returns(dpKeyKey);
        dpSection.SetupGet(x => x.Value).Returns((string?)null);
        configuration.Setup(x => x.GetSection(blobUriKey)).Returns(blobSection.Object);
        configuration.Setup(x => x.GetSection(dpKeyKey)).Returns(dpSection.Object);
        builder.SetupGet(x => x.Configuration).Returns(configuration.Object);
        var credential = new Mock<TokenCredential>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Object.AddDataProtection(credential.Object));

        Assert.Contains("DataProtectionKeyIdentifier", exception.Message, StringComparison.Ordinal);
    }
}
