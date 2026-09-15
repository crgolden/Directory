namespace Directory.Admin;

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using Azure.Messaging.ServiceBus;
using Messaging;
using Microsoft.Extensions.Azure;

public sealed class AdminService
{
    internal const string ExportHeader =
        "Id,CanonicalName,Slug,Street,City,State,Zip,PhoneNumber,Website,EmailAddress,WorshipStyle," +
        "PrimaryLanguage,AcceptsLGBTQ,WheelchairAccessible,HasNursery,HasYouthProgram,ConfidenceScore,CreatedAt,UpdatedAt";

    private const int ExportColumnCount = 19;

    private readonly DbConnection _dbConnection;
    private readonly ServiceBusClient _serviceBusClient;

    public AdminService(DbConnection dbConnection, IAzureClientFactory<ServiceBusClient> serviceBusClientFactory)
    {
        _dbConnection = dbConnection;
        _serviceBusClient = serviceBusClientFactory.CreateClient(ServiceBusNames.Client);
    }

    public async Task<int> ImportCsvAsync(string csv, CancellationToken ct = default)
    {
        var rows = ParseCsv(csv);
        await using var sender = _serviceBusClient.CreateSender(ServiceBusNames.GeocodingRequests);
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
        sb.AppendLine(ExportHeader);
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
        var nameIdx = IndexOf(columns, nameof(ImportRow.CanonicalName));
        var streetIdx = IndexOf(columns, nameof(ImportRow.Street));
        var cityIdx = IndexOf(columns, nameof(ImportRow.City));
        var stateIdx = IndexOf(columns, nameof(ImportRow.State));
        var zipIdx = IndexOf(columns, nameof(ImportRow.Zip));
        var phoneIdx = IndexOf(columns, nameof(ImportRow.PhoneNumber));
        var websiteIdx = IndexOf(columns, nameof(ImportRow.Website));
        var emailIdx = IndexOf(columns, nameof(ImportRow.EmailAddress));

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var fields = line.Split(',');
            var name = SafeGet(fields, nameIdx);
            if (string.IsNullOrWhiteSpace(name) || !IsImportableStateCode(SafeGet(fields, stateIdx)))
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

    private static string? FormatCsvField(object? value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        var formatted = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        if (formatted is null)
        {
            return null;
        }

        return formatted.Contains(',', StringComparison.Ordinal) || formatted.Contains('"', StringComparison.Ordinal)
            ? $"\"{formatted.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : formatted;
    }

    private static string FormatRow(DbDataReader r)
    {
        var fields = new object[ExportColumnCount];
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

    private static bool IsImportableStateCode(string? state) =>
        Shared.Domain.StateCodes.TryParse(state, out _);

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