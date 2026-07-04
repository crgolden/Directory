namespace Directory.Church;

using System.Data;
using System.Data.Common;
using System.Text;
using Entities;
using Enums;

public sealed class ChurchService
{
    private const string SelectColumns =
        "c.[Id], c.[CanonicalName], c.[Slug], c.[Latitude], c.[Longitude], c.[Street], " +
        "c.[City], c.[State], c.[Zip], c.[PhoneNumber], c.[Website], c.[EmailAddress], " +
        "c.[DenominationId], c.[WorshipStyle], c.[PrimaryLanguage], c.[AcceptsLGBTQ], " +
        "c.[WheelchairAccessible], c.[HasNursery], c.[HasYouthProgram], c.[ConfidenceScore], " +
        "c.[LastVerifiedAt], c.[CreatedAt], c.[UpdatedAt], c.[IsActive]";

    private readonly DbConnection _dbConnection;

    public ChurchService(DbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<(IReadOnlyList<Church> Items, int TotalCount)> GetPageAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = $"""
            SELECT {SelectColumns}, COUNT(*) OVER() AS [TotalCount]
            FROM [dbo].[Churches] c
            WHERE c.[IsActive] = 1
            ORDER BY c.[CanonicalName] ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;
        AddParam(cmd, "@Offset", (page - 1) * pageSize);
        AddParam(cmd, "@PageSize", pageSize);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<Church>();
        var totalCount = 0;
        while (await reader.ReadAsync(ct))
        {
            if (items.Count == 0)
            {
                totalCount = (int)reader[24];
            }

            items.Add(Map(reader));
        }

        return (items, totalCount);
    }

    public async Task<Church?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        Church church;
        await using (var cmd = _dbConnection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT {SelectColumns}
                FROM [dbo].[Churches] c
                WHERE c.[Slug] = @Slug AND c.[IsActive] = 1
                """;
            AddParam(cmd, "@Slug", slug);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            church = Map(reader);
        }

        church.Schedules = await LoadSchedulesAsync(church.Id, ct);
        church.Ministries = await LoadMinistriesAsync(church.Id, ct);
        church.Campuses = await LoadCampusesAsync(church.Id, ct);
        return church;
    }

    public async Task<Church?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = $"""
            SELECT {SelectColumns}
            FROM [dbo].[Churches] c
            WHERE c.[Id] = @Id
            """;
        AddParam(cmd, "@Id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<Church> CreateAsync(Church church, CancellationToken ct = default)
    {
        church.Slug = await GenerateUniqueSlugAsync(church.CanonicalName, church.City, church.State, ct);
        var now = DateTimeOffset.UtcNow.UtcDateTime;
        EnsureValid(church, now, now);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [dbo].[Churches]
                ([Id], [CanonicalName], [Slug], [Latitude], [Longitude], [Street], [City], [State],
                 [Zip], [PhoneNumber], [Website], [EmailAddress], [DenominationId], [WorshipStyle],
                 [PrimaryLanguage], [AcceptsLGBTQ], [WheelchairAccessible], [HasNursery], [HasYouthProgram],
                 [ConfidenceScore], [LastVerifiedAt], [CreatedAt], [UpdatedAt], [IsActive])
            VALUES
                (@Id, @CanonicalName, @Slug, @Latitude, @Longitude, @Street, @City, @State,
                 @Zip, @PhoneNumber, @Website, @EmailAddress, @DenominationId, @WorshipStyle,
                 @PrimaryLanguage, @AcceptsLGBTQ, @WheelchairAccessible, @HasNursery, @HasYouthProgram,
                 @ConfidenceScore, @LastVerifiedAt, @CreatedAt, @UpdatedAt, @IsActive)
            """;
        BindChurch(cmd, church);
        AddParam(cmd, "@CreatedAt", now);
        AddParam(cmd, "@UpdatedAt", now);
        AddParam(cmd, "@IsActive", true);
        await cmd.ExecuteNonQueryAsync(ct);
        return church;
    }

    public async Task<bool> UpdateAsync(Church church, CancellationToken ct = default)
    {
        EnsureValid(church, church.CreatedAt.UtcDateTime, DateTimeOffset.UtcNow.UtcDateTime);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[Churches]
            SET [CanonicalName] = @CanonicalName, [Slug] = @Slug,
                [Latitude] = @Latitude, [Longitude] = @Longitude,
                [Street] = @Street, [City] = @City, [State] = @State, [Zip] = @Zip,
                [PhoneNumber] = @PhoneNumber, [Website] = @Website, [EmailAddress] = @EmailAddress,
                [DenominationId] = @DenominationId, [WorshipStyle] = @WorshipStyle,
                [PrimaryLanguage] = @PrimaryLanguage, [AcceptsLGBTQ] = @AcceptsLGBTQ,
                [WheelchairAccessible] = @WheelchairAccessible, [HasNursery] = @HasNursery,
                [HasYouthProgram] = @HasYouthProgram, [ConfidenceScore] = @ConfidenceScore,
                [LastVerifiedAt] = @LastVerifiedAt, [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id
            """;
        BindChurch(cmd, church);
        AddParam(cmd, "@UpdatedAt", DateTimeOffset.UtcNow.UtcDateTime);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM [dbo].[Churches] WHERE [Id] = @Id AND [IsActive] = 1";
        AddParam(cmd, "@Id", id);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[Churches]
            SET [IsActive] = 0, [UpdatedAt] = @UpdatedAt
            WHERE [Id] = @Id
            """;
        AddParam(cmd, "@Id", id);
        AddParam(cmd, "@UpdatedAt", DateTimeOffset.UtcNow.UtcDateTime);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // Builds (and discards) a Shared.Domain.Church purely to run its With*/Build() invariant checks
    // before this Church ever reaches SQL — a bad value (e.g. blank City) fails fast here with a
    // specific ArgumentException/ArgumentOutOfRangeException instead of a raw SQL constraint violation.
    private static void EnsureValid(Church church, DateTime createdAt, DateTime updatedAt) =>
        new Shared.Domain.ChurchBuilder()
            .WithId(church.Id)
            .WithCanonicalName(church.CanonicalName)
            .WithSlug(church.Slug)
            .WithLatitude(church.Latitude)
            .WithLongitude(church.Longitude)
            .WithStreet(church.Street)
            .WithCity(church.City)
            .WithState(church.State)
            .WithZip(church.Zip)
            .WithPhoneNumber(church.PhoneNumber)
            .WithWebsite(church.Website)
            .WithEmailAddress(church.EmailAddress)
            .WithDenominationId(church.DenominationId)
            .WithWorshipStyle((int)church.WorshipStyle)
            .WithPrimaryLanguage(church.PrimaryLanguage)
            .WithAcceptsLGBTQ(church.AcceptsLGBTQ)
            .WithWheelchairAccessible(church.WheelchairAccessible)
            .WithHasNursery(church.HasNursery)
            .WithHasYouthProgram(church.HasYouthProgram)
            .WithConfidenceScore(church.ConfidenceScore)
            .WithLastVerifiedAt(church.LastVerifiedAt?.UtcDateTime)
            .WithCreatedAt(createdAt)
            .WithUpdatedAt(updatedAt)
            .WithIsActive(church.IsActive)
            .Build();

    private static void BindChurch(DbCommand cmd, Church church)
    {
        AddParam(cmd, "@Id", church.Id);
        AddParam(cmd, "@CanonicalName", church.CanonicalName);
        AddParam(cmd, "@Slug", church.Slug);
        AddParam(cmd, "@Latitude", church.Latitude);
        AddParam(cmd, "@Longitude", church.Longitude);
        AddParam(cmd, "@Street", (object?)church.Street ?? DBNull.Value);
        AddParam(cmd, "@City", church.City);
        AddParam(cmd, "@State", church.State);
        AddParam(cmd, "@Zip", church.Zip);
        AddParam(cmd, "@PhoneNumber", (object?)church.PhoneNumber ?? DBNull.Value);
        AddParam(cmd, "@Website", (object?)church.Website ?? DBNull.Value);
        AddParam(cmd, "@EmailAddress", (object?)church.EmailAddress ?? DBNull.Value);
        AddParam(cmd, "@DenominationId", church.DenominationId.HasValue ? church.DenominationId.Value : DBNull.Value);
        AddParam(cmd, "@WorshipStyle", (int)church.WorshipStyle);
        AddParam(cmd, "@PrimaryLanguage", church.PrimaryLanguage);
        AddParam(cmd, "@AcceptsLGBTQ", church.AcceptsLGBTQ.HasValue ? church.AcceptsLGBTQ.Value : DBNull.Value);
        AddParam(cmd, "@WheelchairAccessible", church.WheelchairAccessible.HasValue ? church.WheelchairAccessible.Value : DBNull.Value);
        AddParam(cmd, "@HasNursery", church.HasNursery.HasValue ? church.HasNursery.Value : DBNull.Value);
        AddParam(cmd, "@HasYouthProgram", church.HasYouthProgram.HasValue ? church.HasYouthProgram.Value : DBNull.Value);
        AddParam(cmd, "@ConfidenceScore", church.ConfidenceScore);
        AddParam(cmd, "@LastVerifiedAt", church.LastVerifiedAt.HasValue ? church.LastVerifiedAt.Value.UtcDateTime : DBNull.Value);
    }

    private static void AddParam(DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static string ToSlug(string value)
    {
        var sb = new StringBuilder();
        var prevDash = false;
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                prevDash = false;
            }
            else if (!prevDash && sb.Length > 0)
            {
                sb.Append('-');
                prevDash = true;
            }
        }

        return sb.ToString().TrimEnd('-');
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

    private async Task<IReadOnlyList<ServiceSchedule>> LoadSchedulesAsync(Guid churchId, CancellationToken ct)
    {
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [ChurchId], [CampusId], [DayOfWeek], [StartTime], [Description], [CreatedAt], [UpdatedAt]
            FROM [dbo].[ServiceSchedules]
            WHERE [ChurchId] = @Id
            ORDER BY [DayOfWeek] ASC, [StartTime] ASC
            """;
        AddParam(cmd, "@Id", churchId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var schedules = new List<ServiceSchedule>();
        while (await reader.ReadAsync(ct))
        {
            schedules.Add(new ServiceSchedule
            {
                Id = (Guid)reader[0],
                ChurchId = (Guid)reader[1],
                CampusId = reader[2] is DBNull ? null : (Guid)reader[2],
                DayOfWeek = (DayOfWeek)Convert.ToInt32(reader[3], System.Globalization.CultureInfo.InvariantCulture),
                StartTime = TimeOnly.FromTimeSpan((TimeSpan)reader[4]),
                Description = reader[5] is DBNull ? null : (string)reader[5],
                CreatedAt = ToUtc((DateTime)reader[6]),
                UpdatedAt = ToUtc((DateTime)reader[7]),
            });
        }

        return schedules;
    }

    private async Task<IReadOnlyList<Ministry>> LoadMinistriesAsync(Guid churchId, CancellationToken ct)
    {
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [ChurchId], [Name], [Description], [CreatedAt], [UpdatedAt]
            FROM [dbo].[Ministries]
            WHERE [ChurchId] = @Id
            ORDER BY [Name] ASC
            """;
        AddParam(cmd, "@Id", churchId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var ministries = new List<Ministry>();
        while (await reader.ReadAsync(ct))
        {
            ministries.Add(new Ministry
            {
                Id = (Guid)reader[0],
                ChurchId = (Guid)reader[1],
                Name = (string)reader[2],
                Description = reader[3] is DBNull ? null : (string)reader[3],
                CreatedAt = ToUtc((DateTime)reader[4]),
                UpdatedAt = ToUtc((DateTime)reader[5]),
            });
        }

        return ministries;
    }

    private async Task<IReadOnlyList<Campus>> LoadCampusesAsync(Guid churchId, CancellationToken ct)
    {
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [ChurchId], [Name], [Street], [City], [State], [Zip], [Latitude], [Longitude], [CreatedAt], [UpdatedAt]
            FROM [dbo].[Campuses]
            WHERE [ChurchId] = @Id
            ORDER BY [Name] ASC
            """;
        AddParam(cmd, "@Id", churchId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var campuses = new List<Campus>();
        while (await reader.ReadAsync(ct))
        {
            campuses.Add(new Campus
            {
                Id = (Guid)reader[0],
                ChurchId = (Guid)reader[1],
                Name = (string)reader[2],
                Street = reader[3] is DBNull ? null : (string)reader[3],
                City = (string)reader[4],
                State = (string)reader[5],
                Zip = (string)reader[6],
                Latitude = (double)reader[7],
                Longitude = (double)reader[8],
                CreatedAt = ToUtc((DateTime)reader[9]),
                UpdatedAt = ToUtc((DateTime)reader[10]),
            });
        }

        return campuses;
    }

    private async Task<string> GenerateUniqueSlugAsync(
        string canonicalName, string city, string state, CancellationToken ct)
    {
        var baseSlug = $"{ToSlug(canonicalName)}-{ToSlug(city)}-{state.ToLowerInvariant().Trim()}";
        var candidate = baseSlug;
        var suffix = 2;
        while (await SlugExistsAsync(candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM [dbo].[Churches] WHERE [Slug] = @Slug";
        AddParam(cmd, "@Slug", slug);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is > 0;
    }

    private async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (_dbConnection.State == ConnectionState.Closed)
        {
            await _dbConnection.OpenAsync(ct);
        }
    }
}
