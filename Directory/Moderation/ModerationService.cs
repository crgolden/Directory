namespace Directory.Moderation;

using System.Data;
using System.Data.Common;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Directory.Entities;
using Directory.Enums;
using Directory.Messaging;
using Microsoft.Extensions.Azure;

public sealed class ModerationService
{
    internal const string MergeField = "merge";

    private const string SelectCorrection =
        "c.[Id], c.[ChurchId], c.[UserId], c.[Field], c.[OldValue], c.[NewValue], " +
        "c.[Status], c.[ReviewedBy], c.[ReviewedAt], c.[CreatedAt], ch.[CanonicalName], target.[CanonicalName], " +
        "ch.[Slug], target.[Slug]";

    private const string FromCorrection =
        " FROM [dbo].[UserCorrections] c" +
        " LEFT JOIN [dbo].[Churches] ch ON c.[ChurchId] = ch.[Id]" +
        " LEFT JOIN [dbo].[Churches] target ON target.[Id] = TRY_CONVERT(UNIQUEIDENTIFIER, c.[NewValue])";

    private const int SoftDeleteAndAuditWrites = 2;

    private const string UpdateSuffix = " = @NewValue, [UpdatedAt] = @Now WHERE [Id] = @ChurchId AND [IsActive] = 1";

    private static readonly Dictionary<string, string> CorrectableUpdates = new(StringComparer.OrdinalIgnoreCase)
    {
        [ChurchJsonNames.CanonicalName] = "UPDATE [dbo].[Churches] SET [CanonicalName]" + UpdateSuffix,
        [ChurchJsonNames.Street] = "UPDATE [dbo].[Churches] SET [Street]" + UpdateSuffix,
        [ChurchJsonNames.City] = "UPDATE [dbo].[Churches] SET [City]" + UpdateSuffix,
        [ChurchJsonNames.State] = "UPDATE [dbo].[Churches] SET [State]" + UpdateSuffix,
        [ChurchJsonNames.Zip] = "UPDATE [dbo].[Churches] SET [Zip]" + UpdateSuffix,
        [ChurchJsonNames.PhoneNumber] = "UPDATE [dbo].[Churches] SET [PhoneNumber]" + UpdateSuffix,
        [ChurchJsonNames.Website] = "UPDATE [dbo].[Churches] SET [Website]" + UpdateSuffix,
        [ChurchJsonNames.EmailAddress] = "UPDATE [dbo].[Churches] SET [EmailAddress]" + UpdateSuffix,
        [ChurchJsonNames.PrimaryLanguage] = "UPDATE [dbo].[Churches] SET [PrimaryLanguage]" + UpdateSuffix,
        [ChurchJsonNames.WorshipStyle] = "UPDATE [dbo].[Churches] SET [WorshipStyle]" + UpdateSuffix,
        [ChurchJsonNames.DenominationId] = "UPDATE [dbo].[Churches] SET [DenominationId]" + UpdateSuffix,
        [ChurchJsonNames.WheelchairAccessible] = "UPDATE [dbo].[Churches] SET [WheelchairAccessible]" + UpdateSuffix,
        [ChurchJsonNames.HasNursery] = "UPDATE [dbo].[Churches] SET [HasNursery]" + UpdateSuffix,
        [ChurchJsonNames.HasYouthProgram] = "UPDATE [dbo].[Churches] SET [HasYouthProgram]" + UpdateSuffix,
        [ChurchJsonNames.AcceptsLGBTQ] = "UPDATE [dbo].[Churches] SET [AcceptsLGBTQ]" + UpdateSuffix,
    };

    private static readonly string[] MergeRepointStatements =
    [
        "UPDATE [dbo].[CrawlSources] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
        "UPDATE [dbo].[ChurchAttributes] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
        "UPDATE [dbo].[ServiceSchedules] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
        "UPDATE [dbo].[Ministries] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
        "UPDATE [dbo].[Campuses] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
        "UPDATE [dbo].[UserCorrections] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
    ];

    private readonly DbConnection _dbConnection;
    private readonly ServiceBusClient _serviceBusClient;

    public ModerationService(DbConnection dbConnection, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
    {
        _dbConnection = dbConnection;
        _serviceBusClient = serviceBusClientFactory.CreateClient(ServiceBusNames.Client);
    }

    internal static int MergeWriteCount => MergeRepointStatements.Length + SoftDeleteAndAuditWrites;

    public async Task<(IReadOnlyList<UserCorrection> Items, int TotalCount)> GetCorrectionsAsync(
        CorrectionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText =
            "SELECT " + SelectCorrection + ", COUNT(*) OVER() AS [TotalCount]" +
            FromCorrection +
            " WHERE (@Status IS NULL OR c.[Status] = @Status)" +
            " ORDER BY c.[CreatedAt] DESC" +
            " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        AddParam(cmd, SqlParameters.Status, status.HasValue ? (int)status.Value : DBNull.Value);
        AddParam(cmd, SqlParameters.Offset, (page - 1) * pageSize);
        AddParam(cmd, SqlParameters.PageSize, pageSize);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<UserCorrection>();
        var totalCount = 0;
        while (await reader.ReadAsync(ct))
        {
            if (items.Count == 0)
            {
                totalCount = (int)reader[14];
            }

            items.Add(MapCorrection(reader));
        }

        return (items, totalCount);
    }

    public async Task<UserCorrection?> GetCorrectionByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = $"SELECT {SelectCorrection}{FromCorrection} WHERE c.[Id] = @Id";
        AddParam(cmd, SqlParameters.Id, id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapCorrection(reader);
    }

    public async Task<Guid> SubmitCorrectionAsync(
        Guid churchId,
        Guid userId,
        string field,
        string? oldValue,
        string newValue,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        var payload = JsonSerializer.Serialize(new { ChurchId = churchId, UserId = userId, Field = field, OldValue = oldValue, NewValue = newValue });
        var serviceBusSender = _serviceBusClient.CreateSender(ServiceBusNames.Contributions);
        await serviceBusSender.SendMessageAsync(new ServiceBusMessage(payload) { MessageId = id.ToString() }, ct);
        return id;
    }

    public async Task<bool> ReviewCorrectionAsync(
        Guid id,
        CorrectionStatus status,
        Guid reviewedBy,
        CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[UserCorrections]
            SET [Status] = @Status, [ReviewedBy] = @ReviewedBy, [ReviewedAt] = @ReviewedAt
            WHERE [Id] = @Id AND [Status] = 0
            """;
        AddParam(cmd, SqlParameters.Id, id);
        AddParam(cmd, SqlParameters.Status, (int)status);
        AddParam(cmd, SqlParameters.ReviewedBy, reviewedBy);
        AddParam(cmd, SqlParameters.ReviewedAt, DateTimeOffset.UtcNow);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<string?> ApplyCorrectionAsync(
        UserCorrection correction,
        Guid reviewedBy,
        Guid? survivingId,
        CancellationToken ct = default)
    {
        if (string.Equals(correction.Field, MergeField, StringComparison.OrdinalIgnoreCase))
        {
            return await ApplyMergeAsync(correction, reviewedBy, survivingId, ct);
        }

        if (!CorrectableUpdates.TryGetValue(correction.Field, out var updateSql))
        {
            return $"'{correction.Field}' is not a field this app can apply.";
        }

        var value = ReadCorrectionValue(correction.Field, correction.NewValue);
        if (value is null)
        {
            return $"'{correction.NewValue}' is not a value {correction.Field} accepts.";
        }

        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = updateSql;
        AddParam(cmd, SqlParameters.NewValue, value);
        AddParam(cmd, SqlParameters.Now, DateTimeOffset.UtcNow);
        AddParam(cmd, SqlParameters.ChurchId, correction.ChurchId);
        var updated = await cmd.ExecuteNonQueryAsync(ct);
        return updated > 0 ? null : "That church is no longer active.";
    }

    public async Task MergeAsync(
        Guid survivingId,
        Guid absorbedId,
        Guid mergedBy,
        CancellationToken ct = default)
    {
        if (survivingId == absorbedId)
        {
            throw new ArgumentException("A church cannot be merged into itself.", nameof(absorbedId));
        }

        await EnsureOpenAsync(ct);

        if (!await ChurchIsActiveAsync(survivingId, ct))
        {
            throw new InvalidOperationException($"Surviving church '{survivingId}' does not exist or is not active.");
        }

        if (!await ChurchIsActiveAsync(absorbedId, ct))
        {
            throw new InvalidOperationException($"Absorbed church '{absorbedId}' does not exist or is not active.");
        }

        await using var tx = await _dbConnection.BeginTransactionAsync(ct);
        try
        {
            foreach (var sql in MergeRepointStatements)
            {
                await using var cmd = _dbConnection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                AddParam(cmd, SqlParameters.Surviving, survivingId);
                AddParam(cmd, SqlParameters.Absorbed, absorbedId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using var softDelete = _dbConnection.CreateCommand();
            softDelete.Transaction = tx;
            softDelete.CommandText = """
                UPDATE [dbo].[Churches]
                SET [IsActive] = 0, [UpdatedAt] = @Now
                WHERE [Id] = @Absorbed
                """;
            AddParam(softDelete, SqlParameters.Absorbed, absorbedId);
            AddParam(softDelete, SqlParameters.Now, DateTimeOffset.UtcNow);
            await softDelete.ExecuteNonQueryAsync(ct);

            await using var auditCmd = _dbConnection.CreateCommand();
            auditCmd.Transaction = tx;
            auditCmd.CommandText = """
                INSERT INTO [dbo].[MergeAuditLog]
                    ([Id], [SurvivingId], [AbsorbedId], [MergedBy], [MergedAt])
                VALUES (@Id, @Surviving, @Absorbed, @MergedBy, @MergedAt)
                """;
            AddParam(auditCmd, SqlParameters.Id, Guid.CreateVersion7(DateTimeOffset.UtcNow));
            AddParam(auditCmd, SqlParameters.Surviving, survivingId);
            AddParam(auditCmd, SqlParameters.Absorbed, absorbedId);
            AddParam(auditCmd, SqlParameters.MergedBy, mergedBy);
            AddParam(auditCmd, SqlParameters.MergedAt, DateTimeOffset.UtcNow);
            await auditCmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static object? ReadCorrectionValue(string field, string newValue) =>
        field.ToUpperInvariant() switch
        {
            "STATE" => Shared.Domain.StateCodes.TryParse(newValue, out var state) ? state.ToString() : null,
            "WORSHIPSTYLE" => Enum.TryParse<WorshipStyle>(newValue, out var style) && Enum.IsDefined(style) ? (int)style : null,
            "DENOMINATIONID" => Guid.TryParse(newValue, out var denominationId) ? denominationId : null,
            "WHEELCHAIRACCESSIBLE" or "HASNURSERY" or "HASYOUTHPROGRAM" or "ACCEPTSLGBTQ" =>
                bool.TryParse(newValue, out var flag) ? flag : null,
            _ => newValue,
        };

    private static void AddParam(DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static UserCorrection MapCorrection(DbDataReader r) => new UserCorrection
    {
        Id = (Guid)r[0],
        ChurchId = (Guid)r[1],
        UserId = r.IsDBNull(2) ? null : r.GetGuid(2),
        Field = (string)r[3],
        OldValue = r[4] is DBNull ? null : (string)r[4],
        NewValue = (string)r[5],
        Status = (CorrectionStatus)(int)r[6],
        ReviewedBy = r.IsDBNull(7) ? null : r.GetGuid(7),
        ReviewedAt = r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8),
        CreatedAt = r.GetFieldValue<DateTimeOffset>(9),
        ChurchName = r[10] is DBNull ? null : (string)r[10],
        TargetChurchName = r[11] is DBNull ? null : (string)r[11],
        ChurchSlug = r[12] is DBNull ? null : (string)r[12],
        TargetChurchSlug = r[13] is DBNull ? null : (string)r[13],
    };

    private async Task<string?> ApplyMergeAsync(
        UserCorrection correction,
        Guid reviewedBy,
        Guid? survivingId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(correction.NewValue, out var suggestedId))
        {
            return "This merge suggestion does not name a church to merge with.";
        }

        if (survivingId is null)
        {
            return "Choose which church survives the merge.";
        }

        var surviving = survivingId.Value;
        if (surviving != correction.ChurchId && surviving != suggestedId)
        {
            return "The surviving church must be one of the two in the suggestion.";
        }

        var absorbed = surviving == correction.ChurchId ? suggestedId : correction.ChurchId;
        await MergeAsync(surviving, absorbed, reviewedBy, ct);
        await CloseSuggestionsForAsync(absorbed, reviewedBy, ct);
        return null;
    }

    private async Task CloseSuggestionsForAsync(Guid absorbedId, Guid reviewedBy, CancellationToken ct)
    {
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[UserCorrections]
            SET [Status] = @Rejected, [ReviewedBy] = @ReviewedBy, [ReviewedAt] = @ReviewedAt
            WHERE [Status] = 0 AND [Field] = 'merge'
              AND ([ChurchId] = @Absorbed OR TRY_CONVERT(UNIQUEIDENTIFIER, [NewValue]) = @Absorbed)
            """;
        AddParam(cmd, SqlParameters.Rejected, (int)CorrectionStatus.Rejected);
        AddParam(cmd, SqlParameters.ReviewedBy, reviewedBy);
        AddParam(cmd, SqlParameters.ReviewedAt, DateTimeOffset.UtcNow);
        AddParam(cmd, SqlParameters.Absorbed, absorbedId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<bool> ChurchIsActiveAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM [dbo].[Churches] WHERE [Id] = @Id AND [IsActive] = 1";
        AddParam(cmd, SqlParameters.Id, id);
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
