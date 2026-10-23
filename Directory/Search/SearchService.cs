namespace Directory.Search;

using System.Data;
using System.Data.Common;
using System.Text;
using Directory.Entities;
using Directory.Enums;

public sealed class SearchService
{
    internal const double DefaultRadiusMiles = 25.0;

    internal const string SortByRelevance = "relevance";

    internal const string SortByName = "name";

    internal const string SortByDistance = "distance";

    internal const char LikeEscape = '\\';

    internal const char LikeAnyRun = '%';

    internal const char LikeAnyCharacter = '_';

    internal const char LikeCharacterSet = '[';

    internal const string QueryHasNoTerms = "q must contain at least one letter or number.";

    internal const string WorshipStyleUndefined = "worshipStyle must be one of 0-5.";

    internal const string DayOfWeekOutOfRange = "dayOfWeek must be 0 (Sunday) through 6 (Saturday).";

    internal const string CoordinatesUnpaired = "lat and lng must be supplied together.";

    internal const string CoordinatesOutOfRange = "lat must be between -90 and 90, and lng between -180 and 180.";

    internal const string RadiusWithoutCoordinates = "radiusMiles needs lat and lng.";

    internal const string RadiusNotPositive = "radiusMiles must be greater than zero.";

    internal const string StartTimeWindowInverted = "startTimeAfter must be earlier than startTimeBefore.";

    private readonly DbConnection _dbConnection;

    public SearchService(DbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    internal enum SortMode
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

        var sql = SearchSql.BuildQuery(query, out var hasDistance);
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

        if (SortsByRelevance(q, hasFullText) && q.Q is { } relevanceQuery)
        {
            AddParam(cmd, SqlParameters.ExactQ, relevanceQuery);
            AddParam(cmd, SqlParameters.PrefixQ, EscapeLikePrefix(relevanceQuery) + LikeAnyRun);
        }

        AddParam(cmd, SqlParameters.Offset, (q.Page - 1) * q.PageSize);
        AddParam(cmd, SqlParameters.PageSize, q.PageSize);
    }

    internal static SortMode ResolveSortMode(SearchQuery q, bool hasFullText, bool hasDistance)
    {
        if (SortsByRelevance(q, hasFullText))
        {
            return SortMode.Relevance;
        }

        return hasDistance && RequestedSort(q) is not (SortByName or SortByRelevance) ? SortMode.Distance : SortMode.Name;
    }

    private static string? DescribeInvalidFilters(SearchQuery q)
    {
        if (!string.IsNullOrWhiteSpace(q.Q) && BuildTermConditions(q.Q).Count == 0)
        {
            return QueryHasNoTerms;
        }

        if (q.WorshipStyle.HasValue && !Enum.IsDefined(q.WorshipStyle.Value))
        {
            return WorshipStyleUndefined;
        }

        if (q.DayOfWeek is < 0 or > 6)
        {
            return DayOfWeekOutOfRange;
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
            return CoordinatesUnpaired;
        }

        if (q.Lat is < -90 or > 90 || q.Lng is < -180 or > 180)
        {
            return CoordinatesOutOfRange;
        }

        if (q.RadiusMiles.HasValue && !q.Lat.HasValue)
        {
            return RadiusWithoutCoordinates;
        }

        if (q.RadiusMiles is <= 0)
        {
            return RadiusNotPositive;
        }

        return null;
    }

    private static string? DescribeInvalidTimeWindow(SearchQuery q)
    {
        if (q.StartTimeAfter > q.StartTimeBefore)
        {
            return StartTimeWindowInverted;
        }

        return null;
    }

    private static string? RequestedSort(SearchQuery q) => q.Sort?.Trim().ToLowerInvariant();

    private static bool SortsByRelevance(SearchQuery q, bool hasFullText) =>
        hasFullText && RequestedSort(q) is not (SortByName or SortByDistance);

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
            if (ch is LikeEscape or LikeAnyRun or LikeAnyCharacter or LikeCharacterSet)
            {
                sb.Append(LikeEscape);
            }

            sb.Append(ch);
        }

        return sb.ToString();
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
