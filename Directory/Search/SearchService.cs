namespace Directory.Search;

using System.Data;
using System.Data.Common;
using System.Text;
using Entities;
using Enums;

public sealed class SearchService
{
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
        var items = new List<SearchResult>();
        var totalCount = 0;
        while (await reader.ReadAsync(ct))
        {
            if (items.Count == 0)
            {
                totalCount = (int)reader[25];
            }

            var church = Map(reader);
            double? distance = hasDistance && reader[24] is not DBNull ? (double)reader[24] : null;
            items.Add(new SearchResult(church, distance));
        }

        return (items, totalCount);
    }

    internal static string BuildQuery(SearchQuery q, out bool hasDistance)
    {
        var sb = new StringBuilder();
        sb.Append("SELECT ");
        sb.Append(BaseColumns);

        hasDistance = q is { Lat: not null, Lng: not null };
        if (hasDistance)
        {
            sb.Append(", [dbo].[fn_HaversineDistance](@Lat, @Lng, c.[Latitude], c.[Longitude]) AS [DistanceMiles]");
        }
        else
        {
            sb.Append(", CAST(NULL AS FLOAT) AS [DistanceMiles]");
        }

        sb.Append(", COUNT(*) OVER() AS [TotalCount]");
        sb.Append(" FROM [dbo].[Directory] c WHERE c.[IsActive] = 1");

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            sb.Append(" AND FREETEXT((c.[CanonicalName], c.[City]), @Q)");
        }

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

        if (hasDistance)
        {
            sb.Append(" ORDER BY [dbo].[fn_HaversineDistance](@Lat, @Lng, c.[Latitude], c.[Longitude]) ASC, c.[CanonicalName] ASC");
        }
        else
        {
            sb.Append(" ORDER BY c.[CanonicalName] ASC");
        }

        sb.Append(" OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
        return sb.ToString();
    }

    internal static void BindParams(DbCommand cmd, SearchQuery q)
    {
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            AddParam(cmd, "@Q", q.Q);
        }

        if (!string.IsNullOrWhiteSpace(q.State))
        {
            AddParam(cmd, "@State", q.State);
        }

        if (q.DenominationId.HasValue)
        {
            AddParam(cmd, "@DenominationId", q.DenominationId.Value);
        }

        if (q.WorshipStyle.HasValue)
        {
            AddParam(cmd, "@WorshipStyle", (int)q.WorshipStyle.Value);
        }

        if (q.WheelchairAccessible.HasValue)
        {
            AddParam(cmd, "@WheelchairAccessible", q.WheelchairAccessible.Value);
        }

        if (q is { Lat: not null, Lng: not null })
        {
            AddParam(cmd, "@Lat", q.Lat.Value);
            AddParam(cmd, "@Lng", q.Lng.Value);
            AddParam(cmd, "@RadiusMiles", q.RadiusMiles ?? 25.0);
        }

        AddParam(cmd, "@Offset", (q.Page - 1) * q.PageSize);
        AddParam(cmd, "@PageSize", q.PageSize);
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static Church Map(DbDataReader r) => new Church
    {
        Id = (Guid)r[0],
        CanonicalName = (string)r[1],
        Slug = (string)r[2],
        Latitude = (double)r[3],
        Longitude = (double)r[4],
        Street = r[5] is DBNull ? null : (string)r[5],
        City = (string)r[6],
        State = (string)r[7],
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
        LastVerifiedAt = r[20] is DBNull ? null : ToUtc((DateTime)r[20]),
        CreatedAt = ToUtc((DateTime)r[21]),
        UpdatedAt = ToUtc((DateTime)r[22]),
        IsActive = (bool)r[23],
    };

    private static DateTimeOffset ToUtc(DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
}
