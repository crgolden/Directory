namespace Directory.Tests.Unit.Api;

using Ministries;
using TestSupport;

public sealed class MinistryServiceTests
{
    private const int OneRowAffected = 1;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_InsertsMinistry()
    {
        var churchId = Guid.NewGuid();
        var ministryName = TestValues.NewName();
        var ministryDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var result = await service.CreateAsync(
            churchId, ministryName, ministryDescription, TestContext.Current.CancellationToken);

        Assert.Equal(ministryName, result.Name);
        Assert.Equal(ministryDescription, result.Description);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Ministries]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        var churchId = Guid.NewGuid();
        var ministryDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(churchId, string.Empty, ministryDescription, TestContext.Current.CancellationToken));

        Assert.Equal("name", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        var ministryId = Guid.NewGuid();
        var ministryName = TestValues.NewName();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = new MinistryService(conn);

        var updated = await service.UpdateAsync(
            ministryId, ministryName, null, TestContext.Current.CancellationToken);

        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[Ministries]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        var ministryId = Guid.NewGuid();
        var ministryDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(ministryId, string.Empty, ministryDescription, TestContext.Current.CancellationToken));

        Assert.Equal("name", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        var ministryId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        var deleted = await service.DeleteAsync(ministryId, TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[Ministries]", StringComparison.Ordinal));
    }
}
