namespace Directory.Search;
using System.Diagnostics.CodeAnalysis;

using Entities;
using Enums;

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
            var (items, totalCount) = await service.SearchAsync(query, ct);
            return Results.Ok(new SearchPagedResult(items, totalCount, page, pageSize));
        }).WithTags("Search");

        return app;
    }
}

// Bundles the query-string-bound search filters via [AsParameters] so the route handler's own
// parameter list stays under the lambda-parameter-count limit; DI services (SearchService,
// CancellationToken) remain direct lambda parameters.
public readonly record struct SearchRequest(
    string? Q,
    double? Lat,
    double? Lng,
    double? RadiusMiles,
    string? State,
    Guid? DenominationId,
    WorshipStyle? WorshipStyle,
    bool? WheelchairAccessible,
    int? DayOfWeek,
    TimeOnly? StartTimeBefore,
    TimeOnly? StartTimeAfter,
    string? Sort,
    int Page = 1,
    int PageSize = 20);

public record SearchQuery(
    string? Q,
    double? Lat,
    double? Lng,
    double? RadiusMiles,
    string? State,
    Guid? DenominationId,
    WorshipStyle? WorshipStyle,
    bool? WheelchairAccessible,
    int? DayOfWeek,
    TimeOnly? StartTimeBefore,
    TimeOnly? StartTimeAfter,
    int Page,
    int PageSize,
    string? Sort = null);

public record SearchResult(Church Church, double? DistanceMiles);

public record SearchPagedResult(
    IReadOnlyList<SearchResult> Items,
    int TotalCount,
    int Page,
    int PageSize);
