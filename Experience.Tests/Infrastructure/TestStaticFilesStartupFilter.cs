namespace Experience.Tests.Infrastructure;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

/// <summary>
/// Inserts <see cref="StaticFileExtensions.UseStaticFiles(IApplicationBuilder)"/> early in the
/// middleware pipeline so the Angular build output (registered as the Kestrel web root by
/// <see cref="ExperienceWebApplicationFactory"/>) is served before <c>MapStaticAssets()</c>
/// tries to look it up in the (empty) build-time static web assets manifest.
/// </summary>
internal sealed class TestStaticFilesStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseStaticFiles();
            next(app);
        };
    }
}
