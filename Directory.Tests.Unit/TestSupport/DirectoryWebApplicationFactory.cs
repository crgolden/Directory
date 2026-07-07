namespace Directory.Tests.Unit.TestSupport;

using System.Data.Common;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

public sealed class DirectoryWebApplicationFactory : WebApplicationFactory<Program>
{
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices((_, services) =>
        {
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

            services.AddAuthorizationBuilder()
                .AddPolicy("Directory", p => p.RequireAuthenticatedUser().RequireClaim("scope", "directory"))
                .AddPolicy("ChurchesMod", p => p.RequireAuthenticatedUser().RequireClaim("churches.mod", "true"));
        });
    }
}
