namespace Experience.Tests.Extensions;

using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Experience.Server.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

public sealed class HostApplicationBuilderExtensionsTests
{
    [Fact]
    public async Task AddObservabilityAsync_WithValidConfig_ReturnsBuilder()
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
        var secret1 = new KeyVaultSecret("ElasticsearchUsername", Guid.NewGuid().ToString());
        var secret2 = new KeyVaultSecret("ElasticsearchPassword", Guid.NewGuid().ToString());
        var response1 = Response.FromValue(secret1, Mock.Of<Response>());
        var response2 = Response.FromValue(secret2, Mock.Of<Response>());
        var secretClient = new Mock<SecretClient>();
        secretClient.Setup(x => x.GetSecretAsync(secret1.Name, null, null, TestContext.Current.CancellationToken)).ReturnsAsync(response1);
        secretClient.Setup(x => x.GetSecretAsync(secret2.Name, null, null, TestContext.Current.CancellationToken)).ReturnsAsync(response2);

        // Act
        var result = await builder.Object.AddObservabilityAsync(secretClient.Object, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(builder.Object, result);
        builder.VerifyGet(x => x.Configuration, Times.Once);
        builder.VerifyGet(x => x.Services, Times.Once);
        builder.VerifyGet(x => x.Configuration, Times.Once);
        builder.VerifyGet(x => x.Environment, Times.Exactly(2));
        loggingBuilder.VerifyGet(x => x.Services, Times.Exactly(3));
        configuration.Verify(x => x.GetSection(path), Times.Once);
        configurationSection.VerifyGet(x => x.Value, Times.Once);
        configurationSection.VerifyGet(x => x.Path, Times.Once);
        secretClient.Verify(x => x.GetSecretAsync(secret1.Name, null, null, TestContext.Current.CancellationToken), Times.Once);
        secretClient.Verify(x => x.GetSecretAsync(secret2.Name, null, null, TestContext.Current.CancellationToken), Times.Once);
        environment.Verify(x => x.ApplicationName, Times.Once);
        environment.Verify(x => x.EnvironmentName, Times.Once);
    }

    [Fact]
    public async Task AddObservabilityAsync_WithMissingElasticsearchNode_ThrowsInvalidOperationException()
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
        var secretClient = new Mock<SecretClient>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            builder.Object.AddObservabilityAsync(secretClient.Object, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAuthAsync_WithValidSecrets_ReturnsBuilder()
    {
        // Arrange
        var builder = new Mock<IHostApplicationBuilder>();
        var serviceCollection = new ServiceCollection();
        builder.SetupGet(x => x.Services).Returns(serviceCollection);
        var secret1 = new KeyVaultSecret("OidcAuthority", "https://login.microsoftonline.com/tenant");
        var secret2 = new KeyVaultSecret("ExperienceClientId", Guid.NewGuid().ToString());
        var secret3 = new KeyVaultSecret("ExperienceClientSecret", Guid.NewGuid().ToString());
        var response1 = Response.FromValue(secret1, Mock.Of<Response>());
        var response2 = Response.FromValue(secret2, Mock.Of<Response>());
        var response3 = Response.FromValue(secret3, Mock.Of<Response>());
        var secretClient = new Mock<SecretClient>();
        secretClient.Setup(x => x.GetSecretAsync(secret1.Name, null, null, TestContext.Current.CancellationToken)).ReturnsAsync(response1);
        secretClient.Setup(x => x.GetSecretAsync(secret2.Name, null, null, TestContext.Current.CancellationToken)).ReturnsAsync(response2);
        secretClient.Setup(x => x.GetSecretAsync(secret3.Name, null, null, TestContext.Current.CancellationToken)).ReturnsAsync(response3);

        // Act
        var result = await builder.Object.AddAuthAsync(secretClient.Object, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(builder.Object, result);
        builder.VerifyGet(x => x.Services, Times.Once);
        secretClient.Verify(x => x.GetSecretAsync(secret1.Name, null, null, TestContext.Current.CancellationToken), Times.Once);
        secretClient.Verify(x => x.GetSecretAsync(secret2.Name, null, null, TestContext.Current.CancellationToken), Times.Once);
        secretClient.Verify(x => x.GetSecretAsync(secret3.Name, null, null, TestContext.Current.CancellationToken), Times.Once);
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
