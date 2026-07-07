namespace Directory.Tests.Unit.Api;

using Ministries;
using TestSupport;

public sealed class MinistryServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_InsertsMinistry()
    {
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var result = await service.CreateAsync(Guid.NewGuid(), "Food Bank", "Weekly pantry", TestContext.Current.CancellationToken);

        Assert.Equal("Food Bank", result.Name);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Ministries]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Guid.NewGuid(), string.Empty, "Weekly pantry", TestContext.Current.CancellationToken));

        Assert.Equal("name", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new MinistryService(conn);

        var updated = await service.UpdateAsync(Guid.NewGuid(), "Youth", null, TestContext.Current.CancellationToken);

        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[Ministries]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(Guid.NewGuid(), string.Empty, "Weekly pantry", TestContext.Current.CancellationToken));

        Assert.Equal("name", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var deleted = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[Ministries]", StringComparison.Ordinal));
    }
}
