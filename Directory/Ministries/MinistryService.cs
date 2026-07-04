namespace Directory.Ministries;

using System.Data;
using System.Data.Common;
using Entities;

public sealed class MinistryService
{
    private readonly DbConnection _dbConnection;

    public MinistryService(DbConnection dbConnection) => _dbConnection = dbConnection;

    public async Task<Ministry> CreateAsync(Guid churchId, string name, string? description, CancellationToken ct = default)
    {
        var ministry = new Ministry { ChurchId = churchId, Name = name, Description = description };
        var now = DateTimeOffset.UtcNow.UtcDateTime;
        EnsureValid(ministry.Id, churchId, name, description, now, now);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [dbo].[Ministries] ([Id], [ChurchId], [Name], [Description], [CreatedAt], [UpdatedAt])
            VALUES (@Id, @ChurchId, @Name, @Desc, @Now, @Now)
            """;
        AddParam(cmd, "@Id", ministry.Id);
        AddParam(cmd, "@ChurchId", churchId);
        AddParam(cmd, "@Name", name);
        AddParam(cmd, "@Desc", (object?)description ?? DBNull.Value);
        AddParam(cmd, "@Now", now);
        await cmd.ExecuteNonQueryAsync(ct);
        return ministry;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string? description, CancellationToken ct = default)
    {
        // ChurchId isn't part of this UPDATE (it never changes), so the full Shared.Domain.Ministry
        // factory (which requires it) doesn't apply here — Name is the only NOT NULL field being
        // written, so it's the only thing that needs a guard on this path.
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[Ministries]
            SET [Name] = @Name, [Description] = @Desc, [UpdatedAt] = @Now
            WHERE [Id] = @Id
            """;
        AddParam(cmd, "@Id", id);
        AddParam(cmd, "@Name", name);
        AddParam(cmd, "@Desc", (object?)description ?? DBNull.Value);
        AddParam(cmd, "@Now", DateTimeOffset.UtcNow.UtcDateTime);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "DELETE FROM [dbo].[Ministries] WHERE [Id] = @Id";
        AddParam(cmd, "@Id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // Builds (and discards) a Shared.Domain.Ministry purely to run its With*/Build() invariant
    // checks before this Ministry ever reaches SQL.
    private static void EnsureValid(Guid id, Guid churchId, string name, string? description, DateTime createdAt, DateTime updatedAt) =>
        new Shared.Domain.MinistryBuilder()
            .WithId(id)
            .WithChurchId(churchId)
            .WithName(name)
            .WithDescription(description)
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
