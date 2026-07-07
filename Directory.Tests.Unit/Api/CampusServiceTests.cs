namespace Directory.Tests.Unit.Api;

using Campuses;
using Entities;
using TestSupport;

public sealed class CampusServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_InsertsCampus()
    {
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = new Campus { ChurchId = Guid.NewGuid(), Name = "North", City = "Denver", State = "CO", Zip = "80201", Latitude = 39.7, Longitude = -104.9 };

        var result = await service.CreateAsync(campus.ChurchId, campus, TestContext.Current.CancellationToken);

        Assert.Equal("North", result.Name);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Campuses]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = new Campus { ChurchId = Guid.NewGuid(), Name = string.Empty, City = "Denver", State = "CO", Zip = "80201", Latitude = 39.7, Longitude = -104.9 };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(campus.ChurchId, campus, TestContext.Current.CancellationToken));

        Assert.Equal("name", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new CampusService(conn);
        var campus = new Campus { ChurchId = Guid.NewGuid(), Name = "North", City = "Denver", State = "CO", Zip = "80201", Latitude = 39.7, Longitude = -104.9 };

        var updated = await service.UpdateAsync(Guid.NewGuid(), campus, TestContext.Current.CancellationToken);

        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[Campuses]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);

        var deleted = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[Campuses]", StringComparison.Ordinal));
    }
}
