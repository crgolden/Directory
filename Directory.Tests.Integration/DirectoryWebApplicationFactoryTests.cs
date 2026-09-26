namespace Directory.Tests.Integration;

using Directory.Tests.Integration.TestSupport;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

[Trait("Category", "Integration")]
public sealed class DirectoryWebApplicationFactoryTests
{
    [Fact]
    public async Task StartingAgainstACatalogWithoutTheTestSuffix_IsRefusedBeforeAnythingIsWritten()
    {
        var productionLikeCatalog = Generated.NewDatabaseName();
        await using var configuredFactory = new DirectoryWebApplicationFactory();
        await using var factory = configuredFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{nameof(SqlConnectionStringBuilder)}:{nameof(SqlConnectionStringBuilder.InitialCatalog)}"] =
                        productionLikeCatalog,
                })));

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Equal(productionLikeCatalog, configuredFactory.RefusedCatalog);
    }
}
