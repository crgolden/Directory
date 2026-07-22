#pragma warning disable SA1200
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using Azure.Identity;
using Directory.Admin;
using Directory.Campuses;
using Directory.Church;
using Directory.Crawling;
using Directory.Denomination;
using Directory.Extensions;
using Directory.Ministries;
using Directory.Moderation;
using Directory.Schedules;
using Directory.Search;
using Directory.User;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
#pragma warning restore SA1200

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var oidcAuthority = builder.Configuration.GetRequired<Uri>("OidcAuthority");
    var sqlConnectionStringBuilder = builder.Configuration.GetRequiredSection(nameof(SqlConnectionStringBuilder)).Get<SqlConnectionStringBuilder>() ?? throw new InvalidOperationException($"Invalid '{nameof(SqlConnectionStringBuilder)}' section.");
    if (builder.Environment.IsProduction())
    {
        var defaultAzureCredentialOptions = builder.Configuration.GetRequiredSection(nameof(DefaultAzureCredentialOptions)).Get<DefaultAzureCredentialOptions>() ?? throw new InvalidOperationException($"Invalid '{nameof(DefaultAzureCredentialOptions)}' section.");
        var tokenCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);
        Uri blobUri = builder.Configuration.GetRequired<Uri>("BlobUri"),
            dataProtectionKeyIdentifier = builder.Configuration.GetRequired<Uri>("DataProtectionKeyIdentifier"),
            elasticsearchNode = builder.Configuration.GetRequired<Uri>("ElasticsearchNode");
        string applicationName = builder.Configuration.GetRequired<string>("WEBSITE_SITE_NAME"),
            serviceBusNamespace = builder.Configuration.GetRequired<string>("ServiceBusNamespace");
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase));
        builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
        {
            openTelemetryLoggerOptions.IncludeFormattedMessage = true;
            openTelemetryLoggerOptions.IncludeScopes = true;
        });
        var elasticsearchUsername = builder.Configuration.GetRequired<string>("ElasticsearchUsername");
        var elasticsearchPassword = builder.Configuration.GetRequired<string>("ElasticsearchPassword");
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider)
                .Enrich.WithProperty(nameof(IHostEnvironment.ApplicationName), applicationName)
                .WriteTo.Elasticsearch(
                    [elasticsearchNode],
                    elasticsearchSinkOptions =>
                    {
                        elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "app", nameof(Directory));
                        elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                        elasticsearchSinkOptions.TextFormatting.MapCustom = (ecsDocument, _) =>
                        {
                            ecsDocument.Service ??= new Elastic.CommonSchema.Service();
                            ecsDocument.Service.Name = applicationName;
                            return ecsDocument;
                        };
                    },
                    transportConfiguration =>
                    {
                        var header = new BasicAuthentication(elasticsearchUsername, elasticsearchPassword);
                        transportConfiguration.Authentication(header);
                    }))
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder
                .AddService(applicationName, null, typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
                }))
            .WithMetrics(meterProviderBuilder => meterProviderBuilder
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration.GetRequired<string>("AlloyEndpoint"))))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(builder.Configuration.GetRequired<string>("AlloyEndpoint"))))
            .Services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToAzureBlobStorage(blobUri, tokenCredential)
            .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
            .AddAzureClients(azureClientFactoryBuilder =>
            {
                azureClientFactoryBuilder.UseCredential(tokenCredential);
                azureClientFactoryBuilder.AddServiceBusClientWithNamespace(serviceBusNamespace).WithName("crgolden");
            });
    }
    else
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets("61549613-3239-4c31-8300-39334a7c2657");
        }

        var serviceBusConnectionString = builder.Configuration.GetRequired<string>("ServiceBusConnectionString");
        builder.Services
            .AddSerilog((serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(serviceProvider))
            .AddDataProtection()
            .UseEphemeralDataProtectionProvider().Services
            .AddAzureClients(azureClientFactoryBuilder =>
            {
                azureClientFactoryBuilder.AddServiceBusClient(serviceBusConnectionString).WithName("crgolden");
            });
    }

    builder.Services.AddScoped<DbConnection>(sp =>
    {
        var conn = SqlClientFactory.Instance.CreateConnection() ?? throw new InvalidOperationException($"{nameof(SqlClientFactory)} failed to create a {nameof(DbConnection)}.");
        conn.ConnectionString = sqlConnectionStringBuilder.ConnectionString;
        return conn;
    });
    builder.Services
        .AddAuthentication()
        .AddJwtBearer(jwtBearerOptions =>
        {
            jwtBearerOptions.Authority = oidcAuthority.ToString();
            jwtBearerOptions.TokenValidationParameters.ValidateAudience = false;
            jwtBearerOptions.MapInboundClaims = false;
        }).Services
        .AddAuthorizationBuilder()
        .AddPolicy("Directory", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "directory");
        })
        .AddPolicy("ChurchesMod", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("churches.mod", "true");
        });
    builder.Services
        .AddScoped<AdminService>()
        .AddScoped<ChurchService>()
        .AddScoped<SearchService>()
        .AddScoped<CrawlingService>()
        .AddScoped<ModerationService>()
        .AddScoped<DenominationService>()
        .AddScoped<ScheduleService>()
        .AddScoped<MinistryService>()
        .AddScoped<CampusService>()
        .AddOpenApi()
        .AddHealthChecks().Services
        .Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();
        });

    var app = builder.Build();
    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnosticContext, _) =>
    {
        if (Activity.Current is null)
        {
            return;
        }

        diagnosticContext.Set(nameof(Activity.TraceId), Activity.Current.TraceId.ToString());
        diagnosticContext.Set(nameof(Activity.SpanId), Activity.Current.SpanId.ToString());
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
    app.MapOpenApi();
    app.MapHealthChecks("/health").DisableHttpMetrics();
    app.MapAdminEndpoints();
    app.MapChurchEndpoints();
    app.MapSearchEndpoints();
    app.MapDenominationEndpoints();
    app.MapCrawlingEndpoints();
    app.MapModerationEndpoints();
    app.MapUserEndpoints();
    app.MapScheduleEndpoints();
    app.MapMinistryEndpoints();
    app.MapCampusEndpoints();
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
