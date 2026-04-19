namespace Experience.Server;

using System.Diagnostics;
using System.Security.Claims;
using Duende.Bff;
using Duende.Bff.Yarp;
using Experience.Server.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

/// <summary>
/// Shared startup for the Experience BFF host. Split from <c>Program.cs</c> so the same
/// wiring can be reused by the E2E test fixture without double-building a host.
/// </summary>
public static class AppHost
{
    /// <summary>
    /// Registers services on <paramref name="builder"/>. Returns the two downstream API
    /// addresses so the caller can pass them to <see cref="ConfigurePipeline"/>.
    /// </summary>
    public static async Task<(Uri ManualsApi, Uri ProductsApi)> ConfigureServicesAsync(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets("5480cab8-b41b-4dae-8c41-dbc2c01a15e0");
        }

        var manualsApiAddress = builder.Configuration.GetValue<Uri?>("ManualsApiAddress") ?? throw new InvalidOperationException("Invalid 'ManualsApiAddress'.");
        var productsApiAddress = builder.Configuration.GetValue<Uri?>("ProductsApiAddress") ?? throw new InvalidOperationException("Invalid 'ProductsApiAddress'.");
        var tokenCredential = await builder.Configuration.ToTokenCredentialAsync();
        var secretClient = builder.Configuration.ToSecretClient(tokenCredential);
        await builder.AddObservabilityAsync(secretClient);
        builder.Services.AddHealthChecks();
        builder.AddDataProtection(tokenCredential);
        await builder.AddAuthAsync(secretClient);
        builder.Services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
        });

        return (manualsApiAddress, productsApiAddress);
    }

    /// <summary>
    /// Wires the middleware pipeline and endpoints on <paramref name="app"/>.
    /// </summary>
    public static void ConfigurePipeline(WebApplication app, Uri manualsApiAddress, Uri productsApiAddress)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(manualsApiAddress);
        ArgumentNullException.ThrowIfNull(productsApiAddress);

        app.UseForwardedHeaders();
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, _) =>
            {
                var activity = Activity.Current;
                if (activity is null)
                {
                    return;
                }

                diagnosticContext.Set(nameof(Activity.TraceId), activity.TraceId.ToString());
                diagnosticContext.Set(nameof(Activity.SpanId), activity.SpanId.ToString());
            };
        });
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection().UseAuthorization();
        app.Use((ctx, next) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true)
            {
                return next(ctx);
            }

            using (Serilog.Context.LogContext.PushProperty("UserId", ctx.User.FindFirstValue("sub")))
            using (Serilog.Context.LogContext.PushProperty("UserEmail", ctx.User.FindFirstValue("email")))
            {
                return next(ctx);
            }
        });
        app.MapHealthChecks("/health").DisableHttpMetrics();
        app.MapGet("/config/telemetry", (IConfiguration configuration) =>
            {
                var value = new
                {
                    connectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                };
                return TypedResults.Ok(value);
            })
            .DisableHttpMetrics();
        app.UseAuthentication();
        app.UseBff();
        app.MapRemoteBffApiEndpoint("/manuals/api", manualsApiAddress).WithAccessToken();
        app.MapRemoteBffApiEndpoint("/products/api", productsApiAddress).WithAccessToken();
        app.UseDefaultFiles();
        app.MapStaticAssets();
        app.MapFallbackToFile("/index.html");
    }
}
