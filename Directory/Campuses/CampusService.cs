namespace Directory.Campuses;

using System.Data;
using System.Data.Common;
using Entities;

public sealed class CampusService
{
    private readonly DbConnection _dbConnection;

    public CampusService(DbConnection dbConnection) => _dbConnection = dbConnection;

    public async Task<Campus> CreateAsync(Guid churchId, Campus campus, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.UtcDateTime;
        EnsureValid(campus.Id, churchId, campus, now, now);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [dbo].[Campuses] ([Id], [ChurchId], [Name], [Street], [City], [State], [Zip], [Latitude], [Longitude], [CreatedAt], [UpdatedAt])
            VALUES (@Id, @ChurchId, @Name, @Street, @City, @State, @Zip, @Lat, @Lng, @Now, @Now)
            """;
        AddParam(cmd, "@Id", campus.Id);
        AddParam(cmd, "@ChurchId", churchId);
        AddParam(cmd, "@Name", campus.Name);
        AddParam(cmd, "@Street", (object?)campus.Street ?? DBNull.Value);
        AddParam(cmd, "@City", campus.City);
        AddParam(cmd, "@State", campus.State);
        AddParam(cmd, "@Zip", campus.Zip);
        AddParam(cmd, "@Lat", campus.Latitude);
        AddParam(cmd, "@Lng", campus.Longitude);
        AddParam(cmd, "@Now", now);
        await cmd.ExecuteNonQueryAsync(ct);
        return campus;
    }

    public async Task<bool> UpdateAsync(Guid id, Campus campus, CancellationToken ct = default)
    {
        EnsureValid(id, campus.ChurchId, campus, campus.CreatedAt.UtcDateTime, DateTimeOffset.UtcNow.UtcDateTime);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[Campuses]
            SET [Name] = @Name, [Street] = @Street, [City] = @City, [State] = @State, [Zip] = @Zip,
                [Latitude] = @Lat, [Longitude] = @Lng, [UpdatedAt] = @Now
            WHERE [Id] = @Id
            """;
        AddParam(cmd, "@Id", id);
        AddParam(cmd, "@Name", campus.Name);
        AddParam(cmd, "@Street", (object?)campus.Street ?? DBNull.Value);
        AddParam(cmd, "@City", campus.City);
        AddParam(cmd, "@State", campus.State);
        AddParam(cmd, "@Zip", campus.Zip);
        AddParam(cmd, "@Lat", campus.Latitude);
        AddParam(cmd, "@Lng", campus.Longitude);
        AddParam(cmd, "@Now", DateTimeOffset.UtcNow.UtcDateTime);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "DELETE FROM [dbo].[Campuses] WHERE [Id] = @Id";
        AddParam(cmd, "@Id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // Builds (and discards) a Shared.Domain.Campus purely to run its With*/Build() invariant checks
    // before this Campus ever reaches SQL.
    private static void EnsureValid(Guid id, Guid churchId, Campus campus, DateTime createdAt, DateTime updatedAt) =>
        new Shared.Domain.CampusBuilder()
            .WithId(id)
            .WithChurchId(churchId)
            .WithName(campus.Name)
            .WithStreet(campus.Street)
            .WithCity(campus.City)
            .WithState(campus.State)
            .WithZip(campus.Zip)
            .WithLatitude(campus.Latitude)
            .WithLongitude(campus.Longitude)
            .WithCreatedAt(createdAt)
            .WithUpdatedAt(updatedAt)
            .Build();

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (_dbConnection.State == ConnectionState.Closed)
        {
            await _dbConnection.OpenAsync(ct);
        }
    }
}
