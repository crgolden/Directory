namespace Directory.Tests.Integration.TestSupport;

using System.Data.Common;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

public sealed class DirectoryWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SweepSql = """
        DELETE FROM [dbo].[UserCorrections];
        DELETE FROM [dbo].[MergeAuditLog];
        DELETE FROM [dbo].[ChurchAttributes];
        DELETE FROM [dbo].[ServiceSchedules];
        DELETE FROM [dbo].[Ministries];
        DELETE FROM [dbo].[Campuses];
        DELETE FROM [dbo].[CrawlSources];
        DELETE FROM [dbo].[Churches];
        """;

    private bool _hostCreated;

    public string? RefusedCatalog { get; private set; }

    public async Task<SqlConnection> OpenTestConnectionAsync(CancellationToken ct = default)
    {
        string connectionString;
        using (var scope = Services.CreateScope())
        {
            var dbConn = scope.ServiceProvider.GetRequiredService<DbConnection>();
            connectionString = dbConn.ConnectionString;
        }

        var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async ValueTask InitializeAsync()
    {
        await DeleteEveryRowInTheTestCatalogAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_hostCreated)
        {
            await DeleteEveryRowInTheTestCatalogAsync();
        }

        await base.DisposeAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        _hostCreated = true;
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var catalog = context.Configuration[$"{nameof(SqlConnectionStringBuilder)}:{nameof(SqlConnectionStringBuilder.InitialCatalog)}"];
            if (catalog is null || !catalog.EndsWith(TestDatabaseContractConstants.TestCatalogSuffix, StringComparison.Ordinal))
            {
                RefusedCatalog = catalog;
                throw new InvalidOperationException(
                    $"The integration tier writes to the catalog it is given, so it refuses '{catalog}': the catalog must end in '{TestDatabaseContractConstants.TestCatalogSuffix}'.");
            }

            services.RemoveAll<ILoggerFactory>();
            services.AddLogging(lb => lb.AddConsole());

            var senderMock = new Mock<ServiceBusSender>(MockBehavior.Loose);
            senderMock
                .Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var clientMock = new Mock<ServiceBusClient>(MockBehavior.Loose);
            clientMock.Setup(c => c.CreateSender(It.IsAny<string>())).Returns(senderMock.Object);
            var factoryMock = new Mock<IAzureClientFactory<ServiceBusClient>>(MockBehavior.Loose);
            factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(clientMock.Object);
            services.RemoveAll<IAzureClientFactory<ServiceBusClient>>();
            services.AddSingleton(factoryMock.Object);

            services.AddAuthentication(IntegrationAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, IntegrationAuthHandler>(IntegrationAuthHandler.SchemeName, _ => { });
        });
    }

    private async Task DeleteEveryRowInTheTestCatalogAsync()
    {
        await using var connection = await OpenTestConnectionAsync();
        var catalog = new SqlConnectionStringBuilder(connection.ConnectionString).InitialCatalog;
        if (!catalog.EndsWith(TestDatabaseContractConstants.TestCatalogSuffix, StringComparison.Ordinal))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = SweepSql;
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
