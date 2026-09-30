namespace Directory.Moderation;

using System.Data;
using System.Data.Common;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Directory.Church;
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

    private const string ChurchNotActive = "That church is no longer active.";

    private static readonly Dictionary<string, Func<Church, string, bool>> CorrectionAppliers = new(StringComparer.OrdinalIgnoreCase)
    {
        [ChurchJsonNames.CanonicalName] = Text(static (church, value) => church.CanonicalName = value),
        [ChurchJsonNames.Street] = Text(static (church, value) => church.Street = value),
        [ChurchJsonNames.City] = Text(static (church, value) => church.City = value),
        [ChurchJsonNames.State] = static (church, value) =>
        {
            if (!Shared.Domain.StateCodes.TryParse(value, out var state))
            {
                return false;
            }

            church.State = state;
            return true;
        },
        [ChurchJsonNames.Zip] = Text(static (church, value) => church.Zip = value),
        [ChurchJsonNames.PhoneNumber] = Text(static (church, value) => church.PhoneNumber = value),
        [ChurchJsonNames.Website] = Text(static (church, value) => church.Website = value),
        [ChurchJsonNames.EmailAddress] = Text(static (church, value) => church.EmailAddress = value),
        [ChurchJsonNames.PrimaryLanguage] = Text(static (church, value) => church.PrimaryLanguage = value),
        [ChurchJsonNames.WorshipStyle] = static (church, value) =>
        {
            if (!Enum.TryParse<WorshipStyle>(value, out var style) || !Enum.IsDefined(style))
            {
                return false;
            }

            church.WorshipStyle = style;
            return true;
        },
        [ChurchJsonNames.DenominationId] = static (church, value) =>
        {
            if (!Guid.TryParse(value, out var denominationId))
            {
                return false;
            }

            church.DenominationId = denominationId;
            return true;
        },
        [ChurchJsonNames.WheelchairAccessible] = Flag(static (church, flag) => church.WheelchairAccessible = flag),
        [ChurchJsonNames.HasNursery] = Flag(static (church, flag) => church.HasNursery = flag),
        [ChurchJsonNames.HasYouthProgram] = Flag(static (church, flag) => church.HasYouthProgram = flag),
        [ChurchJsonNames.AcceptsLGBTQ] = Flag(static (church, flag) => church.AcceptsLGBTQ = flag),
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
    private readonly ChurchService _churches;

    public ModerationService(
        DbConnection dbConnection,
        IAzureClientFactory<ServiceBusClient> serviceBusClientFactory,
        ChurchService churches)
    {
        _dbConnection = dbConnection;
        _serviceBusClient = serviceBusClientFactory.CreateClient(ServiceBusNames.Client);
        _churches = churches;
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

        if (!CorrectionAppliers.TryGetValue(correction.Field, out var apply))
        {
            return $"'{correction.Field}' is not a field this app can apply.";
        }

        var church = await _churches.GetByIdAsync(correction.ChurchId, ct);
        if (church is not { IsActive: true })
        {
            return ChurchNotActive;
        }

        if (!apply(church, correction.NewValue))
        {
            return $"'{correction.NewValue}' is not a value {correction.Field} accepts.";
        }

        try
        {
            return await _churches.UpdateAsync(church, ct) ? null : ChurchNotActive;
        }
        catch (ArgumentException exception)
        {
            return $"The church cannot be saved with this correction: {exception.Message}";
        }
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

    private static Func<Church, string, bool> Text(Action<Church, string> assign) =>
        (church, value) =>
        {
            assign(church, value);
            return true;
        };

    private static Func<Church, string, bool> Flag(Action<Church, bool> assign) =>
        (church, value) =>
        {
            if (!bool.TryParse(value, out var flag))
            {
                return false;
            }

            assign(church, flag);
            return true;
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
