namespace Directory.Schedules;

using System.Data;
using System.Data.Common;
using Entities;

public sealed class ScheduleService
{
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
        var now = DateTimeOffset.UtcNow.UtcDateTime;
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO [dbo].[ServiceSchedules] ([Id], [ChurchId], [DayOfWeek], [StartTime], [Description], [CreatedAt], [UpdatedAt])
            VALUES (@Id, @ChurchId, @Day, @Start, @Desc, @Now, @Now)
            """;
        AddParam(cmd, "@Id", schedule.Id);
        AddParam(cmd, "@ChurchId", churchId);
        AddParam(cmd, "@Day", dayOfWeek);
        AddParam(cmd, "@Start", startTime.ToTimeSpan());
        AddParam(cmd, "@Desc", (object?)description ?? DBNull.Value);
        AddParam(cmd, "@Now", now);
        await cmd.ExecuteNonQueryAsync(ct);
        return schedule;
    }

    public async Task<bool> UpdateAsync(Guid id, byte dayOfWeek, TimeOnly startTime, string? description, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = """
            UPDATE [dbo].[ServiceSchedules]
            SET [DayOfWeek] = @Day, [StartTime] = @Start, [Description] = @Desc, [UpdatedAt] = @Now
            WHERE [Id] = @Id
            """;
        AddParam(cmd, "@Id", id);
        AddParam(cmd, "@Day", dayOfWeek);
        AddParam(cmd, "@Start", startTime.ToTimeSpan());
        AddParam(cmd, "@Desc", (object?)description ?? DBNull.Value);
        AddParam(cmd, "@Now", DateTimeOffset.UtcNow.UtcDateTime);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = "DELETE FROM [dbo].[ServiceSchedules] WHERE [Id] = @Id";
        AddParam(cmd, "@Id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

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
