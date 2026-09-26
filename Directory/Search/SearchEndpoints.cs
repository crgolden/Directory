namespace Directory.Search;

using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", async (
            [AsParameters] SearchRequest request,
            SearchService service,
            CancellationToken ct) =>
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 50);
            var query = new SearchQuery(
                request.Q,
                request.Lat,
                request.Lng,
                request.RadiusMiles,
                request.State,
                request.DenominationId,
                request.WorshipStyle,
                request.WheelchairAccessible,
                request.DayOfWeek,
                request.StartTimeBefore,
                request.StartTimeAfter,
                page,
                pageSize,
                request.Sort);
            if (SearchService.DescribeInvalidQuery(query) is { } invalid)
            {
                return Results.BadRequest(invalid);
            }

            var (items, totalCount) = await service.SearchAsync(query, ct);
            return Results.Ok(new SearchPagedResult(items, totalCount, page, pageSize));
        }).WithTags("Search");

        return app;
    }
}
