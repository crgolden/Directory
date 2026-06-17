namespace Directory.Tests.TestSupport;

using System.Data.Common;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

public sealed class DirectoryWebApplicationFactory : WebApplicationFactory<Program>
{
    internal FakeDbConnection FakeDb { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection([
                new("OidcAuthority", "https://localhost/"),
                new("SqlConnectionStringBuilder:DataSource", "test"),
                new("SqlConnectionStringBuilder:InitialCatalog", "test"),
                new("ServiceBusConnectionString", "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="),
            ]));

        builder.ConfigureServices((ctx, services) =>
        {
            if (!ctx.HostingEnvironment.IsProduction())
            {
                services.RemoveAll<ILoggerFactory>();
                services.AddLogging(lb => lb.AddConsole());
            }

            services.RemoveAll<DbConnection>();
            services.AddSingleton<DbConnection>(FakeDb);

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
