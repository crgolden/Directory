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
        // Arrange
        var churchId = Guid.NewGuid();
        var ministryName = TestValues.NewName();
        var ministryDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        // Act
        var result = await service.CreateAsync(
            churchId, ministryName, ministryDescription, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ministryName, result.Name);
        Assert.Equal(ministryDescription, result.Description);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Ministries]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var churchId = Guid.NewGuid();
        var name = TestValues.NewBlank();
        var ministryDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.CreateAsync(churchId, name, ministryDescription, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentException>(exception);
        Assert.Equal(nameof(name), ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        // Arrange
        var ministryId = Guid.NewGuid();
        var ministryName = TestValues.NewName();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = new MinistryService(conn);

        // Act
        var updated = await service.UpdateAsync(
            ministryId, ministryName, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[Ministries]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var ministryId = Guid.NewGuid();
        var name = TestValues.NewBlank();
        var ministryDescription = TestValues.NewName();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.UpdateAsync(ministryId, name, ministryDescription, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentException>(exception);
        Assert.Equal(nameof(name), ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        // Arrange
        var ministryId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        var service = new MinistryService(conn);

        // Act
        var deleted = await service.DeleteAsync(ministryId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[Ministries]", StringComparison.Ordinal));
    }
}
