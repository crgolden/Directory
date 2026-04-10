namespace Experience.Tests.Infrastructure;

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for E2E tests.
/// Starts a real Kestrel server on a random HTTPS port (for Playwright) alongside the
/// in-process TestServer. Replaces Serilog with a test-safe console logger, sets the
/// Kestrel web root to the Angular build output, and registers
/// the <see cref="TestStaticFilesStartupFilter"/>.
/// </summary>
/// <remarks>
/// Requires Azure login (<c>az login</c> locally; <c>azure/login</c> in CI) because
/// <c>Experience.Server/Program.cs</c> calls Azure Key Vault at startup.
/// </remarks>
public sealed class ExperienceWebApplicationFactory : WebApplicationFactory<Program>
{
    private IHost? _kestrelHost;
    private string? _serverAddress;

    public string ServerAddress => _serverAddress
        ?? throw new InvalidOperationException("Server address is not available. Call CreateClient() first.");

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the in-memory TestHost used by CreateClient().
        var testHost = builder.Build();

        // Build a second host with a real Kestrel socket for Playwright.
        builder.ConfigureWebHost(b => b.UseKestrel(o => o.Listen(IPAddress.Loopback, 0, lo => lo.UseHttps())));
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var server = _kestrelHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.GetRequiredFeature<IServerAddressesFeature>();
        _serverAddress = addresses.Addresses.First().TrimEnd('/');

        return testHost;
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Point the web root at the Angular build output so UseStaticFiles() can serve
        // index.html and the JS/CSS bundles. The path is relative to the test assembly
        // output directory (bin/Release/net10.0/), going up four levels to the solution
        // root and then into the Angular dist directory.
        var distPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "experience.client", "dist", "experience.client", "browser"));

        if (Directory.Exists(distPath))
        {
            builder.UseWebRoot(distPath);
        }

        builder.ConfigureServices((ctx, services) =>
        {
            // Prevent background service exceptions from killing the Kestrel host mid-test.
            services.Configure<HostOptions>(opts =>
                opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

            if (!ctx.HostingEnvironment.IsEnvironment("Production"))
            {
                // Replace Serilog (which connects to Elasticsearch) with a plain console logger.
                services.RemoveAll<ILoggerFactory>();
                services.AddLogging(lb => lb.AddConsole());
            }

            // Inject UseStaticFiles() early in the pipeline (before MapStaticAssets) so the
            // Angular build output is served from the physical web root set above.
            services.AddSingleton<IStartupFilter, TestStaticFilesStartupFilter>();
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kestrelHost?.Dispose();
        }

        base.Dispose(disposing);
    }
}
