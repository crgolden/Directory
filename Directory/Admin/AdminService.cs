namespace Directory.Admin;

using System.Data;
using System.Data.Common;
using System.Text;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;

public sealed class AdminService
{
    private readonly DbConnection _dbConnection;
    private readonly ServiceBusClient _serviceBusClient;

    public AdminService(DbConnection dbConnection, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
    {
        _dbConnection = dbConnection;
        _serviceBusClient = serviceBusClientFactory.CreateClient("crgolden");
    }

    public async Task<int> ImportCsvAsync(string csv, CancellationToken ct = default)
    {
        var rows = ParseCsv(csv);
        await using var sender = _serviceBusClient.CreateSender("geocoding-requests");
        var published = 0;
        foreach (var row in rows)
        {
            await sender.SendMessageAsync(
                new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(row)),
                ct);
            published++;
        }

        return published;
    }

    public async Task<string> ExportCsvAsync(CancellationToken ct = default)
    {
        if (_dbConnection.State == ConnectionState.Closed)
        {
            await _dbConnection.OpenAsync(ct);
        }

        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [CanonicalName], [Slug], [Street], [City], [State], [Zip],
                   [PhoneNumber], [Website], [EmailAddress], [WorshipStyle], [PrimaryLanguage],
                   [AcceptsLGBTQ], [WheelchairAccessible], [HasNursery], [HasYouthProgram],
                   [ConfidenceScore], [CreatedAt], [UpdatedAt]
            FROM [dbo].[Churches]
            WHERE [IsActive] = 1
            ORDER BY [State] ASC, [CanonicalName] ASC
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Id,CanonicalName,Slug,Street,City,State,Zip,PhoneNumber,Website,EmailAddress,WorshipStyle,PrimaryLanguage,AcceptsLGBTQ,WheelchairAccessible,HasNursery,HasYouthProgram,ConfidenceScore,CreatedAt,UpdatedAt");
        while (await reader.ReadAsync(ct))
        {
            sb.AppendLine(FormatRow(reader));
        }

        return sb.ToString();
    }

    internal static IEnumerable<ImportRow> ParseCsv(string csv)
    {
        using var reader = new System.IO.StringReader(csv);
        var header = reader.ReadLine();
        if (header is null)
        {
            yield break;
        }

        var columns = header.Split(',');
        var nameIdx = IndexOf(columns, "CanonicalName");
        var streetIdx = IndexOf(columns, "Street");
        var cityIdx = IndexOf(columns, "City");
        var stateIdx = IndexOf(columns, "State");
        var zipIdx = IndexOf(columns, "Zip");
        var phoneIdx = IndexOf(columns, "PhoneNumber");
        var websiteIdx = IndexOf(columns, "Website");
        var emailIdx = IndexOf(columns, "EmailAddress");

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = line.Split(',');
            var name = SafeGet(fields, nameIdx);
            var state = SafeGet(fields, stateIdx);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(state))
            {
                continue;
            }

            yield return new ImportRow(
                SafeGet(fields, nameIdx),
                SafeGet(fields, streetIdx),
                SafeGet(fields, cityIdx),
                SafeGet(fields, stateIdx),
                SafeGet(fields, zipIdx),
                SafeGet(fields, phoneIdx),
                SafeGet(fields, websiteIdx),
                SafeGet(fields, emailIdx));
        }
    }

    private static string FormatCsvField(object? value)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        var s = value.ToString() ?? string.Empty;
        return s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }

    private static string FormatRow(DbDataReader r)
    {
        var fields = new object[19];
        r.GetValues(fields);
        return string.Join(",", fields.Select(FormatCsvField));
    }

    private static int IndexOf(string[] columns, string name)
    {
        for (var i = 0; i < columns.Length; i++)
        {
            if (string.Equals(columns[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string? SafeGet(string[] fields, int index) =>
        index >= 0 && index < fields.Length && !string.IsNullOrWhiteSpace(fields[index])
            ? fields[index].Trim()
            : null;
}

internal sealed record ImportRow(
    string? CanonicalName,
    string? Street,
    string? City,
    string? State,
    string? Zip,
    string? PhoneNumber,
    string? Website,
    string? EmailAddress);
