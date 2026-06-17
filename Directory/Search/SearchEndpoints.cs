namespace Directory.Search;

using Entities;
using Enums;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", async (
            string? q,
            double? lat,
            double? lng,
            double? radiusMiles,
            string? state,
            Guid? denominationId,
            WorshipStyle? worshipStyle,
            bool? wheelchairAccessible,
            SearchService service,
            CancellationToken ct,
            int page = 1,
            int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var query = new SearchQuery(
                q,
                lat,
                lng,
                radiusMiles,
                state,
                denominationId,
                worshipStyle,
                wheelchairAccessible,
                page,
                pageSize);
            var (items, totalCount) = await service.SearchAsync(query, ct);
            return Results.Ok(new SearchPagedResult(items, totalCount, page, pageSize));
        }).WithTags("Search");

        return app;
    }
}

public record SearchQuery(
    string? Q,
    double? Lat,
    double? Lng,
    double? RadiusMiles,
    string? State,
    Guid? DenominationId,
    WorshipStyle? WorshipStyle,
    bool? WheelchairAccessible,
    int Page,
    int PageSize);

public record SearchResult(Church Church, double? DistanceMiles);

public record SearchPagedResult(
    IReadOnlyList<SearchResult> Items,
    int TotalCount,
    int Page,
    int PageSize);
