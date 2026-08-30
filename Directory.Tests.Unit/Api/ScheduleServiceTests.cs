namespace Directory.Tests.Unit.Api;

using Schedules;
using TestSupport;

public sealed class ScheduleServiceTests
{
    private const byte FirstDayOfWeekAboveRange = 7;

    private const int OneRowAffected = 1;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_InsertsSchedule()
    {
        var churchId = Guid.NewGuid();
        var scheduledDayOfWeek = (byte)TestValues.NewDayOfWeek();
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var scheduleDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var result = await service.CreateAsync(
            churchId, scheduledDayOfWeek, scheduledStartTime, scheduleDescription, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(churchId, result.ChurchId);
        Assert.Equal(scheduledStartTime, result.StartTime);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_DayOfWeekAboveSix_ThrowsWithoutTouchingDb()
    {
        var churchId = Guid.NewGuid();
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var scheduleDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(
                churchId,
                FirstDayOfWeekAboveRange,
                scheduledStartTime,
                scheduleDescription,
                TestContext.Current.CancellationToken));

        Assert.Equal("dayOfWeek", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        var scheduleId = Guid.NewGuid();
        var scheduledDayOfWeek = (byte)TestValues.NewDayOfWeek();
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = new ScheduleService(conn);

        var updated = await service.UpdateAsync(
            scheduleId, scheduledDayOfWeek, scheduledStartTime, null, TestContext.Current.CancellationToken);

        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_DayOfWeekAboveSix_ThrowsWithoutTouchingDb()
    {
        var scheduleId = Guid.NewGuid();
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.UpdateAsync(
                scheduleId,
                FirstDayOfWeekAboveRange,
                scheduledStartTime,
                null,
                TestContext.Current.CancellationToken));

        Assert.Equal("dayOfWeek", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        var scheduleId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        var deleted = await service.DeleteAsync(scheduleId, TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }
}
