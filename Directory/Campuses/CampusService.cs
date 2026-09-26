namespace Directory.Campuses;

using System.Data;
using System.Data.Common;
using Directory.Entities;

public sealed class CampusService
{
    private readonly DbConnection _dbConnection;

    public CampusService(DbConnection dbConnection) => _dbConnection = dbConnection;

    public async Task<Campus> CreateAsync(Guid churchId, Campus campus, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        EnsureValid(campus.Id, churchId, campus, now, now);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [dbo].[Campuses] ([Id], [ChurchId], [Name], [Street], [City], [State], [Zip], [Latitude], [Longitude], [CreatedAt], [UpdatedAt])
            VALUES (@Id, @ChurchId, @Name, @Street, @City, @State, @Zip, @Lat, @Lng, @Now, @Now)
            """;
        AddParam(cmd, SqlParameters.Id, campus.Id);
        AddParam(cmd, SqlParameters.ChurchId, churchId);
        AddParam(cmd, SqlParameters.Name, campus.Name);
        AddParam(cmd, SqlParameters.Street, (object?)campus.Street ?? DBNull.Value);
        AddParam(cmd, SqlParameters.City, campus.City);
        AddParam(cmd, SqlParameters.State, campus.State.ToString());
        AddParam(cmd, SqlParameters.Zip, campus.Zip);
        AddParam(cmd, SqlParameters.Lat, campus.Latitude);
        AddParam(cmd, SqlParameters.Lng, campus.Longitude);
        AddParam(cmd, SqlParameters.Now, now);
        await cmd.ExecuteNonQueryAsync(ct);
        return campus;
    }

    public async Task<bool> UpdateAsync(Guid id, Campus campus, CancellationToken ct = default)
    {
        EnsureTheUpdatedColumnsAreValid(campus);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[Campuses]
            SET [Name] = @Name, [Street] = @Street, [City] = @City, [State] = @State, [Zip] = @Zip,
                [Latitude] = @Lat, [Longitude] = @Lng, [UpdatedAt] = @Now
            WHERE [Id] = @Id
            """;
        AddParam(cmd, SqlParameters.Id, id);
        AddParam(cmd, SqlParameters.Name, campus.Name);
        AddParam(cmd, SqlParameters.Street, (object?)campus.Street ?? DBNull.Value);
        AddParam(cmd, SqlParameters.City, campus.City);
        AddParam(cmd, SqlParameters.State, campus.State.ToString());
        AddParam(cmd, SqlParameters.Zip, campus.Zip);
        AddParam(cmd, SqlParameters.Lat, campus.Latitude);
        AddParam(cmd, SqlParameters.Lng, campus.Longitude);
        AddParam(cmd, SqlParameters.Now, DateTimeOffset.UtcNow);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "DELETE FROM [dbo].[Campuses] WHERE [Id] = @Id";
        AddParam(cmd, SqlParameters.Id, id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static void EnsureTheUpdatedColumnsAreValid(Campus campus)
    {
        if (string.IsNullOrWhiteSpace(campus.Name) || string.IsNullOrWhiteSpace(campus.City)
            || string.IsNullOrWhiteSpace(campus.Zip))
        {
            throw new ArgumentException("Name, City and Zip are required.", nameof(campus));
        }

        if (!Enum.IsDefined(campus.State))
        {
            throw new ArgumentOutOfRangeException(
                nameof(campus), campus.State, "State must be a defined StateCode.");
        }

        if (campus.Latitude is < Shared.Domain.CampusBuilder.MinLatitude
            or > Shared.Domain.CampusBuilder.MaxLatitude)
        {
            throw new ArgumentOutOfRangeException(
                nameof(campus),
                campus.Latitude,
                $"Latitude must be between {Shared.Domain.CampusBuilder.MinLatitude} and {Shared.Domain.CampusBuilder.MaxLatitude}.");
        }

        if (campus.Longitude is < Shared.Domain.CampusBuilder.MinLongitude
            or > Shared.Domain.CampusBuilder.MaxLongitude)
        {
            throw new ArgumentOutOfRangeException(
                nameof(campus),
                campus.Longitude,
                $"Longitude must be between {Shared.Domain.CampusBuilder.MinLongitude} and {Shared.Domain.CampusBuilder.MaxLongitude}.");
        }
    }

    private static void EnsureValid(Guid id, Guid churchId, Campus campus, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
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
