#pragma warning disable SA1200
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Duende.Bff;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Experience.Server.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Azure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
#pragma warning restore SA1200

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets("5480cab8-b41b-4dae-8c41-dbc2c01a15e0");
    }

    var (defaultAzureCredentialOptionsSection, openIdConnectOptionsSection) = builder.Configuration.GetSections();
    var defaultAzureCredentialOptions = defaultAzureCredentialOptionsSection.Get<DefaultAzureCredentialOptions>() ?? throw new InvalidOperationException($"Invalid '{nameof(DefaultAzureCredentialOptions)}' section.");
    var openIdConnectOptions = openIdConnectOptionsSection.Get<OpenIdConnectOptions>() ?? throw new InvalidOperationException($"Invalid '{nameof(OpenIdConnectOptions)}' section.");
    TokenCredential tokenCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);
    var (elasticsearchNode, keyVaultUrl, blobUrl, dataProtectionKeyIdentifier) = builder.Configuration.GetUris();
    var secretClient = new SecretClient(keyVaultUrl, tokenCredential);
    var (elasticsearchUsername, elasticsearchPassword, clientId, clientSecret, oidcAuthority) = await secretClient.GetSecrets();
    builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(x => x.AddService(builder.Environment.ApplicationName))
        .UseAzureMonitor()
        .WithMetrics(meterProviderBuilder =>
        {
            meterProviderBuilder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
        })
        .WithTracing(tracerProviderBuilder =>
        {
            tracerProviderBuilder
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(aspNetCoreTraceInstrumentationOptions =>
                {
                    aspNetCoreTraceInstrumentationOptions.Filter = context =>
                    {
                        return !context.Request.Path.StartsWithSegments("/Health");
                    };
                })
                .AddHttpClientInstrumentation();
            if (builder.Environment.IsDevelopment())
            {
                tracerProviderBuilder.AddConsoleExporter();
            }
        }).Services
        .AddSerilog((sp, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(sp)
                .Enrich.FromLogContext();
            if (!builder.Environment.IsProduction())
            {
                return;
            }

            loggerConfiguration
                .WriteTo.OpenTelemetry()
                .WriteTo.Elasticsearch(
                    [elasticsearchNode],
                    elasticsearchSinkOptions =>
                    {
                        elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "dotnet", nameof(Experience));
                        elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                    },
                    transportConfiguration =>
                    {
                        var header = new BasicAuthentication(elasticsearchUsername.Value, elasticsearchPassword.Value);
                        transportConfiguration.Authentication(header);
                    });
        })
        .AddAuthentication(options =>
        {
            options.DefaultScheme = BffAuthenticationSchemes.BffCookie;
            options.DefaultChallengeScheme = BffAuthenticationSchemes.BffOpenIdConnect;
            options.DefaultSignOutScheme = BffAuthenticationSchemes.BffOpenIdConnect;
        }).Services
        .AddBff()
        .AddRemoteApis()
        .ConfigureOpenIdConnect(options =>
        {
            options.Authority = oidcAuthority.Value;
            options.ClientId = clientId.Value;
            options.ClientSecret = clientSecret.Value;
            options.ResponseType = openIdConnectOptions.ResponseType;
            options.ResponseMode = openIdConnectOptions.ResponseMode;
            foreach (var scope in openIdConnectOptions.Scope)
            {
                options.Scope.Add(scope);
            }

            options.SaveTokens = openIdConnectOptions.SaveTokens;
            options.GetClaimsFromUserInfoEndpoint = openIdConnectOptions.GetClaimsFromUserInfoEndpoint;
            options.MapInboundClaims = openIdConnectOptions.MapInboundClaims;
            options.TokenValidationParameters = openIdConnectOptions.TokenValidationParameters;
            if (builder.Environment.IsProduction())
            {
                return;
            }

            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    var server = context.HttpContext.RequestServices.GetRequiredService<IServer>();
                    var serverAddresses = server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;
                    var address = serverAddresses.FirstOrDefault(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) ?? serverAddresses.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        return Task.CompletedTask;
                    }

                    context.ProtocolMessage.RedirectUri = address.TrimEnd('/') + options.CallbackPath;
                    return Task.CompletedTask;
                },
                OnRedirectToIdentityProviderForSignOut = context =>
                {
                    var server = context.HttpContext.RequestServices.GetRequiredService<IServer>();
                    var serverAddresses = server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;
                    var address = serverAddresses.FirstOrDefault(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) ?? serverAddresses.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        return Task.CompletedTask;
                    }

                    context.ProtocolMessage.PostLogoutRedirectUri = address.TrimEnd('/') + options.SignedOutCallbackPath;
                    return Task.CompletedTask;
                }
            };
        })
        .ConfigureCookies(cookieAuthenticationOptions =>
        {
            cookieAuthenticationOptions.Cookie.SameSite = SameSiteMode.Strict;
        }).Services
        .AddAuthorization()
        .AddHealthChecks().Services
        .AddDataProtection()
        .SetApplicationName(builder.Environment.ApplicationName)
        .PersistKeysToAzureBlobStorage(blobUrl, tokenCredential)
        .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
        .AddAzureClientsCore(true);
    builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
    {
        openTelemetryLoggerOptions.IncludeFormattedMessage = true;
        openTelemetryLoggerOptions.IncludeScopes = true;
    });

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection().UseAuthorization();
    app.MapHealthChecks("Health").DisableHttpMetrics();
    app.UseDefaultFiles();
    app.MapStaticAssets();
    app.UseAuthentication();
    app.UseBff();
    app.MapBffManagementEndpoints();
    app.MapFallbackToFile("/index.html");
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
