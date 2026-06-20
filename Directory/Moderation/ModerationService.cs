namespace Directory.Moderation;

using System.Data;
using System.Data.Common;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Entities;
using Enums;
using Microsoft.Extensions.Azure;

public sealed class ModerationService
{
    private const string SelectCorrection =
        "c.[Id], c.[ChurchId], c.[UserId], c.[Field], c.[OldValue], c.[NewValue], " +
        "c.[Status], c.[ReviewedBy], c.[ReviewedAt], c.[CreatedAt], ch.[CanonicalName]";

    private readonly DbConnection _dbConnection;
    private readonly ServiceBusClient _serviceBusClient;

    public ModerationService(DbConnection dbConnection, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
    {
        _dbConnection = dbConnection;
        _serviceBusClient = serviceBusClientFactory.CreateClient("crgolden");
    }

    public async Task<(IReadOnlyList<UserCorrection> Items, int TotalCount)> GetCorrectionsAsync(
        CorrectionStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText =
            "SELECT " + SelectCorrection + ", COUNT(*) OVER() AS [TotalCount] " +
            "FROM [dbo].[UserCorrections] c " +
            "LEFT JOIN [dbo].[Directory] ch ON c.[ChurchId] = ch.[Id] " +
            (status.HasValue ? "WHERE c.[Status] = @Status " : string.Empty) +
            "ORDER BY c.[CreatedAt] DESC " +
            "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        if (status.HasValue)
        {
            AddParam(cmd, "@Status", (int)status.Value);
        }

        AddParam(cmd, "@Offset", (page - 1) * pageSize);
        AddParam(cmd, "@PageSize", pageSize);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<UserCorrection>();
        var totalCount = 0;
        while (await reader.ReadAsync(ct))
        {
            if (items.Count == 0)
            {
                totalCount = (int)reader[11];
            }

            items.Add(MapCorrection(reader));
        }

        return (items, totalCount);
    }

    public async Task<UserCorrection?> GetCorrectionByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = $"SELECT {SelectCorrection} FROM [dbo].[UserCorrections] c LEFT JOIN [dbo].[Directory] ch ON c.[ChurchId] = ch.[Id] WHERE c.[Id] = @Id";
        AddParam(cmd, "@Id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapCorrection(reader);
    }

    public async Task<Guid> SubmitCorrectionAsync(
        Guid churchId,
        string userId,
        string field,
        string? oldValue,
        string newValue,
        CancellationToken ct = default)
    {
        var id = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        var payload = JsonSerializer.Serialize(new { ChurchId = churchId, UserId = userId, Field = field, OldValue = oldValue, NewValue = newValue });
        var serviceBusSender = _serviceBusClient.CreateSender("contributions");
        await serviceBusSender.SendMessageAsync(new ServiceBusMessage(payload) { MessageId = id.ToString() }, ct);
        return id;
    }

    public async Task<bool> ReviewCorrectionAsync(
        Guid id,
        CorrectionStatus status,
        string reviewedBy,
        CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[UserCorrections]
            SET [Status] = @Status, [ReviewedBy] = @ReviewedBy, [ReviewedAt] = @ReviewedAt
            WHERE [Id] = @Id AND [Status] = 0
            """;
        AddParam(cmd, "@Id", id);
        AddParam(cmd, "@Status", (int)status);
        AddParam(cmd, "@ReviewedBy", reviewedBy);
        AddParam(cmd, "@ReviewedAt", DateTimeOffset.UtcNow.UtcDateTime);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task MergeAsync(
        Guid survivingId,
        Guid absorbedId,
        string mergedBy,
        CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var tx = await _dbConnection.BeginTransactionAsync(ct);
        try
        {
            var repoint = new[]
            {
                "UPDATE [dbo].[CrawlSources] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
                "UPDATE [dbo].[ChurchAttributes] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
                "UPDATE [dbo].[ServiceSchedules] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
                "UPDATE [dbo].[Ministries] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
                "UPDATE [dbo].[Campuses] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
                "UPDATE [dbo].[UserCorrections] SET [ChurchId] = @Surviving WHERE [ChurchId] = @Absorbed",
            };
            foreach (var sql in repoint)
            {
                await using var cmd = _dbConnection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                AddParam(cmd, "@Surviving", survivingId);
                AddParam(cmd, "@Absorbed", absorbedId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using var softDelete = _dbConnection.CreateCommand();
            softDelete.Transaction = tx;
            softDelete.CommandText = """
                UPDATE [dbo].[Directory]
                SET [IsActive] = 0, [UpdatedAt] = @Now
                WHERE [Id] = @Absorbed
                """;
            AddParam(softDelete, "@Absorbed", absorbedId);
            AddParam(softDelete, "@Now", DateTimeOffset.UtcNow.UtcDateTime);
            await softDelete.ExecuteNonQueryAsync(ct);

            await using var auditCmd = _dbConnection.CreateCommand();
            auditCmd.Transaction = tx;
            auditCmd.CommandText = """
                INSERT INTO [dbo].[MergeAuditLog]
                    ([Id], [SurvivingId], [AbsorbedId], [MergedBy], [MergedAt])
                VALUES (@Id, @Surviving, @Absorbed, @MergedBy, @MergedAt)
                """;
            AddParam(auditCmd, "@Id", Guid.CreateVersion7(DateTimeOffset.UtcNow));
            AddParam(auditCmd, "@Surviving", survivingId);
            AddParam(auditCmd, "@Absorbed", absorbedId);
            AddParam(auditCmd, "@MergedBy", mergedBy);
            AddParam(auditCmd, "@MergedAt", DateTimeOffset.UtcNow.UtcDateTime);
            await auditCmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

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
        UserId = (string)r[2],
        Field = (string)r[3],
        OldValue = r[4] is DBNull ? null : (string)r[4],
        NewValue = (string)r[5],
        Status = (CorrectionStatus)(int)r[6],
        ReviewedBy = r[7] is DBNull ? null : (string)r[7],
        ReviewedAt = r[8] is DBNull ? null : ToUtc((DateTime)r[8]),
        CreatedAt = ToUtc((DateTime)r[9]),
        ChurchName = r[10] is DBNull ? null : (string)r[10],
    };

    private static DateTimeOffset ToUtc(DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

    private async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (_dbConnection.State == ConnectionState.Closed)
        {
            await _dbConnection.OpenAsync(ct);
        }
    }
}
