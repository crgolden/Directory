namespace Directory.Crawling;

using System.Data;
using System.Data.Common;
using Azure.Messaging.ServiceBus;
using Entities;
using Enums;
using Microsoft.Extensions.Azure;

public sealed class CrawlingService
{
    private const string SelectColumns =
        "c.[Id], c.[ChurchId], c.[Url], c.[LastCrawledAt], c.[LastStatus], c.[CreatedAt], c.[UpdatedAt]";

    private readonly DbConnection _dbConnection;
    private readonly ServiceBusClient _serviceBusClient;

    public CrawlingService(DbConnection dbConnection, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
    {
        _dbConnection = dbConnection;
        _serviceBusClient = serviceBusClientFactory.CreateClient("crgolden");
    }

    public async Task<IReadOnlyList<CrawlSource>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumns} FROM [dbo].[CrawlSources] c ORDER BY c.[CreatedAt] DESC";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var items = new List<CrawlSource>();
        while (await reader.ReadAsync(ct))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<CrawlSource> CreateAsync(string url, Guid? churchId, CancellationToken ct = default)
    {
        var source = new CrawlSource
        {
            Url = url,
            ChurchId = churchId,
            LastStatus = CrawlStatus.Pending,
        };
        var now = DateTimeOffset.UtcNow.UtcDateTime;
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [dbo].[CrawlSources] ([Id], [ChurchId], [Url], [LastStatus], [CreatedAt], [UpdatedAt])
            VALUES (@Id, @ChurchId, @Url, @LastStatus, @CreatedAt, @UpdatedAt)
            """;
        AddParam(cmd, "@Id", source.Id);
        AddParam(cmd, "@ChurchId", churchId.HasValue ? churchId.Value : DBNull.Value);
        AddParam(cmd, "@Url", url);
        AddParam(cmd, "@LastStatus", (int)CrawlStatus.Pending);
        AddParam(cmd, "@CreatedAt", now);
        AddParam(cmd, "@UpdatedAt", now);
        await cmd.ExecuteNonQueryAsync(ct);
        return source;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "DELETE FROM [dbo].[CrawlSources] WHERE [Id] = @Id";
        AddParam(cmd, "@Id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> TriggerScrapeAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var lookupCmd = _dbConnection.CreateCommand();
        lookupCmd.CommandText = "SELECT [Url] FROM [dbo].[CrawlSources] WHERE [Id] = @Id";
        AddParam(lookupCmd, "@Id", id);
        var url = await lookupCmd.ExecuteScalarAsync(ct) as string;
        if (url is null)
        {
            return false;
        }

        var payload = BinaryData.FromObjectAsJson(new { CrawlSourceId = id, Url = url });
        var serviceBusSender = _serviceBusClient.CreateSender("scrape-requests");
        await serviceBusSender.SendMessageAsync(new ServiceBusMessage(payload), ct);

        await using var updateCmd = _dbConnection.CreateCommand();
        updateCmd.CommandText = """
            UPDATE [dbo].[CrawlSources]
            SET [LastStatus] = @Status, [UpdatedAt] = @Now
            WHERE [Id] = @Id
            """;
        AddParam(updateCmd, "@Id", id);
        AddParam(updateCmd, "@Status", (int)CrawlStatus.Pending);
        AddParam(updateCmd, "@Now", DateTimeOffset.UtcNow.UtcDateTime);
        await updateCmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static void AddParam(DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static CrawlSource Map(DbDataReader r) => new CrawlSource
    {
        Id = (Guid)r[0],
        ChurchId = r[1] is DBNull ? null : (Guid)r[1],
        Url = (string)r[2],
        LastCrawledAt = r[3] is DBNull ? null : ToUtc((DateTime)r[3]),
        LastStatus = (CrawlStatus)(int)r[4],
        CreatedAt = ToUtc((DateTime)r[5]),
        UpdatedAt = ToUtc((DateTime)r[6]),
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
