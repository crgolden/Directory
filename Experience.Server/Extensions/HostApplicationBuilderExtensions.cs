namespace Experience.Server.Extensions;

using Azure.Core;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Duende.Bff;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp;
using Duende.IdentityModel;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
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

public static class HostApplicationBuilderExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddObservability(string elasticsearchUsername, string elasticsearchPassword)
        {
            var applicationName = builder.Configuration["WEBSITE_SITE_NAME"];
            var elasticsearchNode = builder.Configuration.GetValue<Uri?>("ElasticsearchNode") ?? throw new InvalidOperationException("Invalid 'ElasticsearchNode'.");
            builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
            {
                openTelemetryLoggerOptions.IncludeFormattedMessage = true;
                openTelemetryLoggerOptions.IncludeScopes = true;
            });
            var otelBuilder = builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resourceBuilder =>
                {
                    var serviceName = applicationName ?? builder.Environment.ApplicationName;
                    resourceBuilder.AddService(
                        serviceName: serviceName,
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0");
                    resourceBuilder.AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant()
                    });
                })
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
                        .SetSampler(new AlwaysOnSampler())
                        .AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation(aspNetCoreTraceInstrumentationOptions =>
                        {
                            aspNetCoreTraceInstrumentationOptions.Filter = context =>
                            {
                                return !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
                            };
                        })
                        .AddHttpClientInstrumentation();
                    if (builder.Environment.IsDevelopment())
                    {
                        tracerProviderBuilder.AddConsoleExporter();
                    }
                });

            if (builder.Environment.IsProduction())
            {
                otelBuilder.UseAzureMonitor();
            }

            builder.Services.AddSerilog((sp, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(sp)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithEnvironmentName();
                if (!IsNullOrWhiteSpace(applicationName))
                {
                    loggerConfiguration
                        .Enrich.WithProperty(nameof(IHostEnvironment.ApplicationName), applicationName);
                }

                if (builder.Environment.IsProduction())
                {
                    loggerConfiguration
                        .WriteTo.Elasticsearch(
                            [elasticsearchNode],
                            elasticsearchSinkOptions =>
                            {
                                elasticsearchSinkOptions.DataStream = new DataStreamName("logs", "dotnet", nameof(Experience));
                                elasticsearchSinkOptions.BootstrapMethod = BootstrapMethod.Failure;
                            },
                            transportConfiguration =>
                            {
                                var header = new BasicAuthentication(elasticsearchUsername, elasticsearchPassword);
                                transportConfiguration.Authentication(header);
                            });
                }
            });
            return builder;
        }

        public IHostApplicationBuilder AddAuth(string clientId, string clientSecret)
        {
            var authority = builder.Configuration.GetValue<Uri?>("OidcAuthority") ?? throw new InvalidOperationException("Invalid 'OidcAuthority'.");
            builder.Services
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
                    var openIdConnectOptions = builder.Configuration
                        .GetSection(nameof(OpenIdConnectOptions))
                        .Get<OpenIdConnectOptions>() ?? throw new InvalidOperationException($"Invalid '{nameof(OpenIdConnectOptions)}' section.");
                    options.Authority = authority.ToString();
                    options.ClientId = clientId;
                    options.ClientSecret = clientSecret;
                    foreach (var scope in openIdConnectOptions.Scope)
                    {
                        options.Scope.Add(scope);
                    }

                    options.ResponseType = OidcConstants.ResponseTypes.Code;
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
                            if (IsNullOrWhiteSpace(address))
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
                            if (IsNullOrWhiteSpace(address))
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
                .AddAuthorization();
            return builder;
        }

        public IHostApplicationBuilder AddDataProtection(TokenCredential tokenCredential)
        {
            var blobUrl = builder.Configuration.GetValue<Uri?>("BlobUri") ?? throw new InvalidOperationException("Invalid 'BlobUri'.");
            var dataProtectionKeyIdentifier = builder.Configuration.GetValue<Uri?>("DataProtectionKeyIdentifier") ?? throw new InvalidOperationException("Invalid 'DataProtectionKeyIdentifier'.");
            builder.Services
                .AddDataProtection()
                .SetApplicationName(builder.Environment.ApplicationName)
                .PersistKeysToAzureBlobStorage(blobUrl, tokenCredential)
                .ProtectKeysWithAzureKeyVault(dataProtectionKeyIdentifier, tokenCredential).Services
                .AddAzureClientsCore(true);
            return builder;
        }
    }
}
