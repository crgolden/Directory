namespace Experience.Tests.Infrastructure;

using System.Net;
using Experience.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Builds a single <see cref="WebApplication"/> that hosts the Experience BFF on a real
/// Kestrel loopback port for Playwright-driven E2E tests. Shares startup wiring with
/// <c>Program.cs</c> via <see cref="AppHost"/> so there is no double-build cost. Applies a
/// small set of test-only customizations (static web root, test-safe logger, early
/// <c>UseStaticFiles</c>).
/// </summary>
/// <remarks>
/// Requires Azure login (<c>az login</c> locally; <c>azure/login</c> in CI) because
/// startup calls Azure Key Vault.
/// </remarks>
public sealed class ExperienceWebApplicationFactory : IAsyncDisposable
{
    private WebApplication? _app;
    private string? _serverAddress;

    public string ServerAddress => _serverAddress
        ?? throw new InvalidOperationException("Server address is not available. Call StartAsync() first.");

    private static void Stage(string msg) =>
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Factory: {msg}");

    /// <summary>
    /// Builds and starts the test host. Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public async Task StartAsync()
    {
        if (_app is not null)
        {
            return;
        }

        Stage("StartAsync enter: creating builder");

        // WebApplicationFactory normally infers ContentRoot from the entry assembly's solution
        // layout; doing it ourselves since we no longer inherit from it.
        var contentRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Experience.Server"));

        // Point the web root at the Angular build output so UseStaticFiles() can serve
        // index.html and the JS/CSS bundles.
        var distPath = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "experience.client", "dist", "experience.client", "browser"));

        var options = new WebApplicationOptions
        {
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            ContentRootPath = contentRoot,
            ApplicationName = "Experience.Server",
            WebRootPath = Directory.Exists(distPath) ? distPath : null
        };
        var builder = WebApplication.CreateBuilder(options);

        // Real Kestrel socket on a random HTTPS loopback port for Playwright.
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0, lo => lo.UseHttps()));

        // Prevent background service exceptions from killing the host mid-test.
        builder.Services.Configure<HostOptions>(opts =>
            opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        // Replace Serilog (which connects to Elasticsearch) with a plain console logger in
        // non-Production environments.
        if (!builder.Environment.IsEnvironment("Production"))
        {
            builder.Services.RemoveAll<ILoggerFactory>();
            builder.Services.AddLogging(lb => lb.AddConsole());
        }

        // Inject UseStaticFiles() early in the pipeline (before MapStaticAssets) so the
        // Angular build output is served from the physical web root set above.
        builder.Services.AddSingleton<IStartupFilter, TestStaticFilesStartupFilter>();

        Stage("Calling AppHost.ConfigureServicesAsync");
        var (manualsApiAddress, productsApiAddress) = await AppHost.ConfigureServicesAsync(builder);
        Stage("Services configured; building app");

        _app = builder.Build();
        AppHost.ConfigurePipeline(_app, manualsApiAddress, productsApiAddress);

        Stage("App built; starting");
        await _app.StartAsync();

        var server = _app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.GetRequiredFeature<IServerAddressesFeature>();
        _serverAddress = addresses.Addresses.First().TrimEnd('/');
        Stage($"StartAsync exit: {_serverAddress}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
