namespace Directory.Tests.Unit.Api;

using Schedules;
using TestSupport;

public sealed class ScheduleServiceTests
{
    private const byte DayOfWeekOutOfRangeOffset = 1;

    private const byte FirstDayOfWeekAboveRange = ScheduleService.MaxDayOfWeek + DayOfWeekOutOfRangeOffset;

    private const int OneRowAffected = 1;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_InsertsSchedule()
    {
        // Arrange
        var churchId = Guid.NewGuid();
        var scheduledDayOfWeek = (byte)TestValues.NewDayOfWeek();
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var scheduleDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        // Act
        var result = await service.CreateAsync(
            churchId, scheduledDayOfWeek, scheduledStartTime, scheduleDescription, TestContext.Current.CancellationToken);

        // Assert
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
        // Arrange
        var churchId = Guid.NewGuid();
        const byte dayOfWeek = FirstDayOfWeekAboveRange;
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var scheduleDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.CreateAsync(
                churchId,
                dayOfWeek,
                scheduledStartTime,
                scheduleDescription,
                TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(nameof(dayOfWeek), ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var scheduledDayOfWeek = (byte)TestValues.NewDayOfWeek();
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = new ScheduleService(conn);

        // Act
        var updated = await service.UpdateAsync(
            scheduleId, scheduledDayOfWeek, scheduledStartTime, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_DayOfWeekAboveSix_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        const byte dayOfWeek = FirstDayOfWeekAboveRange;
        var scheduledStartTime = TestValues.NewTimeOfDay();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.UpdateAsync(
                scheduleId,
                dayOfWeek,
                scheduledStartTime,
                null,
                TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(nameof(dayOfWeek), ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        var service = new ScheduleService(conn);

        // Act
        var deleted = await service.DeleteAsync(scheduleId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[ServiceSchedules]", StringComparison.Ordinal));
    }
}
