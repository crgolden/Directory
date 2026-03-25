namespace Experience.Server.Extensions;

using Azure.Core;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Security.KeyVault.Secrets;
using Duende.Bff;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp;
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
        public async Task<IHostApplicationBuilder> AddObservabilityAsync(SecretClient secretClient, CancellationToken cancellationToken = default)
        {
            var elasticsearchNode = builder.Configuration.GetValue<Uri>("ElasticsearchNode") ?? throw new InvalidOperationException("Invalid 'ElasticsearchNode'.");
            var tasks = new[]
            {
                secretClient.GetSecretAsync("ElasticsearchUsername", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("ElasticsearchPassword", cancellationToken: cancellationToken),
            };
            var result = await Task.WhenAll(tasks);
            builder.Logging.AddOpenTelemetry(openTelemetryLoggerOptions =>
            {
                openTelemetryLoggerOptions.IncludeFormattedMessage = true;
                openTelemetryLoggerOptions.IncludeScopes = true;
            });
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
                                var header = new BasicAuthentication(result[0].Value.Value, result[1].Value.Value);
                                transportConfiguration.Authentication(header);
                            });
                });
            return builder;
        }

        public async Task<IHostApplicationBuilder> AddAuthAsync(SecretClient secretClient, CancellationToken cancellationToken = default)
        {
            var tasks = new[]
            {
                secretClient.GetSecretAsync("OidcAuthority", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("ExperienceClientId", cancellationToken: cancellationToken),
                secretClient.GetSecretAsync("ExperienceClientSecret", cancellationToken: cancellationToken)
            };
            var result = await Task.WhenAll(tasks);
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
                    options.Authority = result[0].Value.Value;
                    options.ClientId = result[1].Value.Value;
                    options.ClientSecret = result[2].Value.Value;
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
            var blobUrl = builder.Configuration.GetValue<Uri>("BlobUri") ?? throw new InvalidOperationException("Invalid 'BlobUri'.");
            var dataProtectionKeyIdentifier = builder.Configuration.GetValue<Uri>("DataProtectionKeyIdentifier") ?? throw new InvalidOperationException("Invalid 'DataProtectionKeyIdentifier'.");
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
