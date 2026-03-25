#pragma warning disable SA1200
using Duende.Bff;
using Experience.Server.Extensions;
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

    var tokenCredential = await builder.Configuration.ToTokenCredentialAsync();
    var secretClient = builder.Configuration.ToSecretClient(tokenCredential);
    await builder.AddObservabilityAsync(secretClient);
    builder.Services.AddHealthChecks();
    builder.AddDataProtection(tokenCredential);
    await builder.AddAuthAsync(secretClient);

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
