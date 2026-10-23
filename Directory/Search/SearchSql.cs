namespace Directory.Search;

using System.Globalization;
using System.Text;

internal static class SearchSql
{
    private const string BaseColumns =
        "c.[Id], c.[CanonicalName], c.[Slug], c.[Latitude], c.[Longitude], c.[Street], " +
        "c.[City], c.[State], c.[Zip], c.[PhoneNumber], c.[Website], c.[EmailAddress], " +
        "c.[DenominationId], c.[WorshipStyle], c.[PrimaryLanguage], c.[AcceptsLGBTQ], " +
        "c.[WheelchairAccessible], c.[HasNursery], c.[HasYouthProgram], c.[ConfidenceScore], " +
        "c.[LastVerifiedAt], c.[CreatedAt], c.[UpdatedAt], c.[IsActive]";

    internal static string BuildQuery(SearchQuery q, out bool hasDistance)
    {
        hasDistance = q is { Lat: not null, Lng: not null };
        var termCount = SearchService.BuildTermConditions(q.Q).Count;
        var scope = BuildFromAndWhere(q, termCount, hasDistance);

        var sb = new StringBuilder();
        sb.Append("SELECT COUNT(*) AS [TotalCount]");
        sb.Append(scope);

        // Stryker disable once Statement : T-SQL runs the two SELECTs as one batch with or without the terminator
        sb.Append(';');

        sb.Append(" SELECT ");
        sb.Append(BaseColumns);
        if (hasDistance)
        {
            sb.Append(", [dbo].[fn_HaversineDistance](@Lat, @Lng, c.[Latitude], c.[Longitude]) AS [DistanceMiles]");
        }

        sb.Append(scope);

        AppendOrderBy(sb, q, termCount, hasDistance);

        sb.Append(" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
        return sb.ToString();
    }

    private static string BuildFromAndWhere(SearchQuery q, int termCount, bool hasDistance)
    {
        var sb = new StringBuilder();
        sb.Append(" FROM [dbo].[Churches] c");

        for (var term = 0; term < termCount; term++)
        {
            sb.Append(CultureInfo.InvariantCulture, $" INNER JOIN CONTAINSTABLE([dbo].[Churches], ([CanonicalName], [City]), @Q{term}) AS ft{term} ON ft{term}.[KEY] = c.[Id]");
        }

        sb.Append(" WHERE c.[IsActive] = 1");

        if (!string.IsNullOrWhiteSpace(q.State))
        {
            sb.Append(" AND c.[State] = @State");
        }

        if (q.DenominationId.HasValue)
        {
            sb.Append(" AND c.[DenominationId] = @DenominationId");
        }

        if (q.WorshipStyle.HasValue)
        {
            sb.Append(" AND c.[WorshipStyle] = @WorshipStyle");
        }

        if (q.WheelchairAccessible.HasValue)
        {
            sb.Append(" AND c.[WheelchairAccessible] = @WheelchairAccessible");
        }

        if (hasDistance)
        {
            sb.Append(" AND [dbo].[fn_HaversineDistance](@Lat, @Lng, c.[Latitude], c.[Longitude]) <= @RadiusMiles");
        }

        AppendScheduleFilter(sb, q);

        return sb.ToString();
    }

    private static void AppendOrderBy(StringBuilder sb, SearchQuery q, int termCount, bool hasDistance)
    {
        switch (SearchService.ResolveSortMode(q, termCount > 0, hasDistance))
        {
            case SearchService.SortMode.Relevance:
                RelevanceOrder.Append(sb, termCount);
                break;
            case SearchService.SortMode.Distance:
                sb.Append(" ORDER BY [dbo].[fn_HaversineDistance](@Lat, @Lng, c.[Latitude], c.[Longitude]) ASC, c.[CanonicalName] ASC");
                break;
            default:
                sb.Append(" ORDER BY c.[CanonicalName] ASC");
                break;
        }
    }

    private static void AppendScheduleFilter(StringBuilder sb, SearchQuery q)
    {
        if (!q.DayOfWeek.HasValue && !q.StartTimeAfter.HasValue && !q.StartTimeBefore.HasValue)
        {
            return;
        }

        sb.Append(" AND EXISTS (SELECT 1 FROM [dbo].[ServiceSchedules] ss WHERE ss.[ChurchId] = c.[Id]");
        if (q.DayOfWeek.HasValue)
        {
            sb.Append(" AND ss.[DayOfWeek] = @DayOfWeek");
        }

        if (q.StartTimeAfter.HasValue)
        {
            sb.Append(" AND ss.[StartTime] >= @StartTimeAfter");
        }

        if (q.StartTimeBefore.HasValue)
        {
            sb.Append(" AND ss.[StartTime] <= @StartTimeBefore");
        }

        sb.Append(')');
    }
}
