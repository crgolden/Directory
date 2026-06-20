namespace Directory.Denomination;

using System.Data;
using System.Data.Common;
using Entities;

public sealed class DenominationService
{
    private readonly DbConnection _dbConnection;

    public DenominationService(DbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IReadOnlyList<Entities.Denomination>> GetAllAsync(CancellationToken ct = default)
    {
        if (_dbConnection.State == ConnectionState.Closed)
        {
            await _dbConnection.OpenAsync(ct);
        }

        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            SELECT [Id], [Name]
            FROM [dbo].[Denominations]
            ORDER BY [Name] ASC
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Entities.Denomination>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Entities.Denomination
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1)
            });
        }

        return results;
    }
}
