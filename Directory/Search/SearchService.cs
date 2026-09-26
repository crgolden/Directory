namespace Directory.Search;

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Directory.Entities;
using Directory.Enums;

public sealed class SearchService
{
    internal const double DefaultRadiusMiles = 25.0;

    internal const string SortByRelevance = "relevance";

    internal const string SortByName = "name";

    internal const string SortByDistance = "distance";

    private const string BaseColumns =
        "c.[Id], c.[CanonicalName], c.[Slug], c.[Latitude], c.[Longitude], c.[Street], " +
        "c.[City], c.[State], c.[Zip], c.[PhoneNumber], c.[Website], c.[EmailAddress], " +
        "c.[DenominationId], c.[WorshipStyle], c.[PrimaryLanguage], c.[AcceptsLGBTQ], " +
        "c.[WheelchairAccessible], c.[HasNursery], c.[HasYouthProgram], c.[ConfidenceScore], " +
        "c.[LastVerifiedAt], c.[CreatedAt], c.[UpdatedAt], c.[IsActive]";

    private readonly DbConnection _dbConnection;

    public SearchService(DbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    private enum SortMode
    {
        Name,
        Relevance,
        Distance,
    }

    public async Task<(IReadOnlyList<SearchResult> Items, int TotalCount)> SearchAsync(
        SearchQuery query, CancellationToken ct = default)
    {
        if (_dbConnection.State == ConnectionState.Closed)
        {
            await _dbConnection.OpenAsync(ct);
        }

        var sql = BuildQuery(query, out var hasDistance);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = sql;
        BindParams(cmd, query);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var totalCount = 0;
        if (await reader.ReadAsync(ct))
        {
            totalCount = (int)reader[0];
        }

        await reader.NextResultAsync(ct);
        var items = new List<SearchResult>();
        while (await reader.ReadAsync(ct))
        {
            if (Map(reader) is not { } church)
            {
                continue;
            }

            double? distance = hasDistance && reader[24] is not DBNull ? (double)reader[24] : null;
            items.Add(new SearchResult(church, distance));
        }

        return (items, totalCount);
    }

    internal static string BuildQuery(SearchQuery q, out bool hasDistance)
    {
        hasDistance = q is { Lat: not null, Lng: not null };
        var termCount = BuildTermConditions(q.Q).Count;
        var scope = BuildFromAndWhere(q, termCount, hasDistance);

        var sb = new StringBuilder();
        sb.Append("SELECT COUNT(*) AS [TotalCount]");
        sb.Append(scope);
        sb.Append(';');

        sb.Append(" SELECT ");
        sb.Append(BaseColumns);
        sb.Append(hasDistance
            ? ", [dbo].[fn_HaversineDistance](@Lat, @Lng, c.[Latitude], c.[Longitude]) AS [DistanceMiles]"
            : ", CAST(NULL AS FLOAT) AS [DistanceMiles]");
        sb.Append(scope);

        AppendOrderBy(sb, q, termCount, hasDistance);

        sb.Append(" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
        return sb.ToString();
    }

    internal static string? DescribeInvalidQuery(SearchQuery q) =>
        DescribeInvalidFilters(q) ?? DescribeInvalidLocation(q) ?? DescribeInvalidTimeWindow(q);

    internal static IReadOnlyList<string> BuildTermConditions(string? q)
    {
        var conditions = new List<string>();
        if (string.IsNullOrWhiteSpace(q))
        {
            return conditions;
        }

        var word = new StringBuilder();
        foreach (var ch in q)
        {
            if (char.IsLetterOrDigit(ch) || ch == '\'')
            {
                word.Append(ch);
                continue;
            }

            AppendTerm(conditions, word);
        }

        AppendTerm(conditions, word);
        return conditions;
    }

    internal static void BindParams(DbCommand cmd, SearchQuery q)
    {
        var termConditions = BuildTermConditions(q.Q);
        var hasFullText = termConditions.Count > 0;
        for (var term = 0; term < termConditions.Count; term++)
        {
            AddParam(cmd, $"@Q{term}", termConditions[term]);
        }

        if (!string.IsNullOrWhiteSpace(q.State))
        {
            AddParam(cmd, SqlParameters.State, q.State);
        }

        if (q.DenominationId.HasValue)
        {
            AddParam(cmd, SqlParameters.DenominationId, q.DenominationId.Value);
        }

        if (q.WorshipStyle.HasValue)
        {
            AddParam(cmd, SqlParameters.WorshipStyle, (int)q.WorshipStyle.Value);
        }

        if (q.WheelchairAccessible.HasValue)
        {
            AddParam(cmd, SqlParameters.WheelchairAccessible, q.WheelchairAccessible.Value);
        }

        var hasDistance = q is { Lat: not null, Lng: not null };
        if (q is { Lat: { } latitude, Lng: { } longitude })
        {
            AddParam(cmd, SqlParameters.Lat, latitude);
            AddParam(cmd, SqlParameters.Lng, longitude);
            AddParam(cmd, SqlParameters.RadiusMiles, q.RadiusMiles ?? DefaultRadiusMiles);
        }

        if (q.DayOfWeek.HasValue)
        {
            AddParam(cmd, SqlParameters.DayOfWeek, q.DayOfWeek.Value);
        }

        if (q.StartTimeAfter.HasValue)
        {
            AddParam(cmd, SqlParameters.StartTimeAfter, q.StartTimeAfter.Value.ToTimeSpan());
        }

        if (q.StartTimeBefore.HasValue)
        {
            AddParam(cmd, SqlParameters.StartTimeBefore, q.StartTimeBefore.Value.ToTimeSpan());
        }

        if (ResolveSortMode(q, hasFullText, hasDistance) == SortMode.Relevance && q.Q is { } relevanceQuery)
        {
            AddParam(cmd, SqlParameters.ExactQ, relevanceQuery);
            AddParam(cmd, SqlParameters.PrefixQ, EscapeLikePrefix(relevanceQuery) + "%");
        }

        AddParam(cmd, SqlParameters.Offset, (q.Page - 1) * q.PageSize);
        AddParam(cmd, SqlParameters.PageSize, q.PageSize);
    }

    private static string? DescribeInvalidFilters(SearchQuery q)
    {
        if (!string.IsNullOrWhiteSpace(q.Q) && BuildTermConditions(q.Q).Count == 0)
        {
            return "q must contain at least one letter or number.";
        }

        if (q.WorshipStyle.HasValue && !Enum.IsDefined(q.WorshipStyle.Value))
        {
            return "worshipStyle must be one of 0-5.";
        }

        if (q.DayOfWeek is < 0 or > 6)
        {
            return "dayOfWeek must be 0 (Sunday) through 6 (Saturday).";
        }

        if (!string.IsNullOrWhiteSpace(q.State) && !Shared.Domain.StateCodes.TryParse(q.State, out _))
        {
            return $"Unknown state code '{q.State}'.";
        }

        return null;
    }

    private static string? DescribeInvalidLocation(SearchQuery q)
    {
        if (q.Lat.HasValue != q.Lng.HasValue)
        {
            return "lat and lng must be supplied together.";
        }

        if (q.Lat is < -90 or > 90 || q.Lng is < -180 or > 180)
        {
            return "lat must be between -90 and 90, and lng between -180 and 180.";
        }

        if (q.RadiusMiles.HasValue && !q.Lat.HasValue)
        {
            return "radiusMiles needs lat and lng.";
        }

        if (q.RadiusMiles is <= 0)
        {
            return "radiusMiles must be greater than zero.";
        }

        return null;
    }

    private static string? DescribeInvalidTimeWindow(SearchQuery q)
    {
        if (q.StartTimeAfter.HasValue && q.StartTimeBefore.HasValue && q.StartTimeAfter > q.StartTimeBefore)
        {
            return "startTimeAfter must be earlier than startTimeBefore.";
        }

        return null;
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

    private static SortMode ResolveSortMode(SearchQuery q, bool hasFullText, bool hasDistance)
    {
        var requested = q.Sort?.Trim().ToLowerInvariant();
        switch (requested)
        {
            case SortByRelevance:
                return hasFullText ? SortMode.Relevance : SortMode.Name;
            case SortByName:
                return SortMode.Name;
            case SortByDistance:
                return hasDistance ? SortMode.Distance : SortMode.Name;
            default:
                if (hasFullText)
                {
                    return SortMode.Relevance;
                }

                return hasDistance ? SortMode.Distance : SortMode.Name;
        }
    }

    private static void AppendOrderBy(StringBuilder sb, SearchQuery q, int termCount, bool hasDistance)
    {
        switch (ResolveSortMode(q, termCount > 0, hasDistance))
        {
            case SortMode.Relevance:
                var rank = string.Join(" + ", Enumerable.Range(0, termCount).Select(term => $"ft{term}.[RANK]"));
                sb.Append(" ORDER BY CASE WHEN c.[CanonicalName] = @ExactQ THEN 0 " +
                          "WHEN c.[CanonicalName] LIKE @PrefixQ ESCAPE '\\' THEN 1 " +
                          "ELSE 2 END ASC, ");
                sb.Append(rank);
                sb.Append(" DESC, c.[CanonicalName] ASC");
                break;
            case SortMode.Distance:
                sb.Append(" ORDER BY [dbo].[fn_HaversineDistance](@Lat, @Lng, c.[Latitude], c.[Longitude]) ASC, c.[CanonicalName] ASC");
                break;
            default:
                sb.Append(" ORDER BY c.[CanonicalName] ASC");
                break;
        }
    }

    private static void AppendTerm(List<string> conditions, StringBuilder word)
    {
        if (word.Length > 0)
        {
            conditions.Add($"\"{word}*\"");
            word.Clear();
        }
    }

    private static string EscapeLikePrefix(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in value)
        {
            if (ch is '\\' or '%' or '_' or '[')
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
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

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static Church? Map(DbDataReader r)
    {
        if (!Shared.Domain.StateCodes.TryParse((string)r[7], out var state))
        {
            return null;
        }

        return new Church
        {
            Id = (Guid)r[0],
            CanonicalName = (string)r[1],
            Slug = (string)r[2],
            Latitude = (double)r[3],
            Longitude = (double)r[4],
            Street = r[5] is DBNull ? null : (string)r[5],
            City = (string)r[6],
            State = state,
            Zip = (string)r[8],
            PhoneNumber = r[9] is DBNull ? null : (string)r[9],
            Website = r[10] is DBNull ? null : (string)r[10],
            EmailAddress = r[11] is DBNull ? null : (string)r[11],
            DenominationId = r[12] is DBNull ? null : (Guid)r[12],
            WorshipStyle = (WorshipStyle)(int)r[13],
            PrimaryLanguage = (string)r[14],
            AcceptsLGBTQ = r[15] is DBNull ? null : (bool)r[15],
            WheelchairAccessible = r[16] is DBNull ? null : (bool)r[16],
            HasNursery = r[17] is DBNull ? null : (bool)r[17],
            HasYouthProgram = r[18] is DBNull ? null : (bool)r[18],
            ConfidenceScore = (decimal)r[19],
            LastVerifiedAt = r.IsDBNull(20) ? null : r.GetFieldValue<DateTimeOffset>(20),
            CreatedAt = r.GetFieldValue<DateTimeOffset>(21),
            UpdatedAt = r.GetFieldValue<DateTimeOffset>(22),
            IsActive = (bool)r[23],
        };
    }
}
