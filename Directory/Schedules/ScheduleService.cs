namespace Directory.Schedules;

using System.Data;
using System.Data.Common;
using Directory.Entities;

public sealed class ScheduleService
{
    internal const byte MaxDayOfWeek = 6;

    private readonly DbConnection _dbConnection;

    public ScheduleService(DbConnection dbConnection) => _dbConnection = dbConnection;

    public async Task<ServiceSchedule> CreateAsync(Guid churchId, byte dayOfWeek, TimeOnly startTime, string? description, CancellationToken ct = default)
    {
        var schedule = new ServiceSchedule
        {
            ChurchId = churchId,
            DayOfWeek = (DayOfWeek)dayOfWeek,
            StartTime = startTime,
            Description = description,
        };
        var now = DateTimeOffset.UtcNow;
        EnsureValid(schedule.Id, churchId, dayOfWeek, startTime, description, now, now);
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [dbo].[ServiceSchedules] ([Id], [ChurchId], [DayOfWeek], [StartTime], [Description], [CreatedAt], [UpdatedAt])
            VALUES (@Id, @ChurchId, @Day, @Start, @Desc, @Now, @Now)
            """;
        AddParam(cmd, SqlParameters.Id, schedule.Id);
        AddParam(cmd, SqlParameters.ChurchId, churchId);
        AddParam(cmd, SqlParameters.Day, dayOfWeek);
        AddParam(cmd, SqlParameters.Start, startTime.ToTimeSpan());
        AddParam(cmd, SqlParameters.Desc, (object?)description ?? DBNull.Value);
        AddParam(cmd, SqlParameters.Now, now);
        await cmd.ExecuteNonQueryAsync(ct);
        return schedule;
    }

    public async Task<bool> UpdateAsync(Guid id, byte dayOfWeek, TimeOnly startTime, string? description, CancellationToken ct = default)
    {
        if (dayOfWeek > MaxDayOfWeek)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, "DayOfWeek must be 0 (Sunday) through 6 (Saturday).");
        }

        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[ServiceSchedules]
            SET [DayOfWeek] = @Day, [StartTime] = @Start, [Description] = @Desc, [UpdatedAt] = @Now
            WHERE [Id] = @Id
            """;
        AddParam(cmd, SqlParameters.Id, id);
        AddParam(cmd, SqlParameters.Day, dayOfWeek);
        AddParam(cmd, SqlParameters.Start, startTime.ToTimeSpan());
        AddParam(cmd, SqlParameters.Desc, (object?)description ?? DBNull.Value);
        AddParam(cmd, SqlParameters.Now, DateTimeOffset.UtcNow);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "DELETE FROM [dbo].[ServiceSchedules] WHERE [Id] = @Id";
        AddParam(cmd, SqlParameters.Id, id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static void EnsureValid(Guid id, Guid churchId, byte dayOfWeek, TimeOnly startTime, string? description, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new Shared.Domain.ServiceScheduleBuilder()
            .WithId(id)
            .WithChurchId(churchId)
            .WithDayOfWeek(dayOfWeek)
            .WithStartTime(startTime)
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
