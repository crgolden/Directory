namespace Directory.Crawling;

using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class CrawlingEndpoints
{
    internal const string Route = "/crawl-sources";

    internal const string TriggerSegment = "/trigger";

    public static IEndpointRouteBuilder MapCrawlingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Route)
            .WithTags("Crawling")
            .RequireAuthorization(AuthorizationPolicies.ChurchesModPolicy);

        group.MapGet("/", async (CrawlingService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapPost("/", async (
            CreateCrawlSourceRequest req,
            CrawlingService service,
            CancellationToken ct) =>
        {
            var source = await service.CreateAsync(req.Url, req.ChurchId, ct);
            return Results.Created($"{Route}/{source.Id}", source);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            CrawlingService service,
            CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost($"/{{id:guid}}{TriggerSegment}", async (
            Guid id,
            CrawlingService service,
            CancellationToken ct) =>
        {
            var found = await service.TriggerScrapeAsync(id, ct);
            return found ? Results.Accepted() : Results.NotFound();
        });

        return app;
    }
}
