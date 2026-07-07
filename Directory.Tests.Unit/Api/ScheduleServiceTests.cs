namespace Directory.Tests.Unit.Api;

using Schedules;
using TestSupport;

public sealed class ScheduleServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_InsertsSchedule()
    {
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var result = await service.CreateAsync(Guid.NewGuid(), 0, new TimeOnly(10, 30), "Worship", TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_DayOfWeekAboveSix_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(Guid.NewGuid(), 7, new TimeOnly(10, 30), "Worship", TestContext.Current.CancellationToken));

        Assert.Equal("dayOfWeek", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ScheduleService(conn);

        var updated = await service.UpdateAsync(Guid.NewGuid(), 1, new TimeOnly(9, 0), null, TestContext.Current.CancellationToken);

        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_DayOfWeekAboveSix_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.UpdateAsync(Guid.NewGuid(), 7, new TimeOnly(9, 0), null, TestContext.Current.CancellationToken));

        Assert.Equal("dayOfWeek", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var deleted = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }
}
