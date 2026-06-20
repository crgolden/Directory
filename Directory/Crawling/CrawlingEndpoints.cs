namespace Directory.Crawling;
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class CrawlingEndpoints
{
    public static IEndpointRouteBuilder MapCrawlingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/crawl-sources")
            .WithTags("Crawling")
            .RequireAuthorization("ChurchesMod");

        group.MapGet("/", async (CrawlingService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapPost("/", async (
            CreateCrawlSourceRequest req,
            CrawlingService service,
            CancellationToken ct) =>
        {
            var source = await service.CreateAsync(req.Url, req.ChurchId, ct);
            return Results.Created($"/crawl-sources/{source.Id}", source);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            CrawlingService service,
            CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/trigger", async (
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

public record CreateCrawlSourceRequest(string Url, Guid? ChurchId);
