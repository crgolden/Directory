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
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campusName = TestValues.NewName();
        var campus = BuildCampus(campusName);

        // Act
        var result = await service.CreateAsync(campus.ChurchId, campus, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(campusName, result.Name);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Campuses]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var name = TestValues.NewBlank();
        var campus = BuildCampus(name);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.CreateAsync(campus.ChurchId, campus, TestContext.Current.CancellationToken));

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
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new CampusService(conn);
        var campus = BuildCampus(TestValues.NewName());
        var campusId = Guid.NewGuid();

        // Act
        var updated = await service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("UPDATE [dbo].[Campuses]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_NoRow_ReturnsFalse()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campusId = Guid.NewGuid();

        // Act
        var deleted = await service.DeleteAsync(campusId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(deleted);
        Assert.Contains(conn.ExecutedCommands, c =>
            c.CommandText.Contains("DELETE FROM [dbo].[Campuses]", StringComparison.Ordinal));
    }

    private static Campus BuildCampus(string name)
    {
        var churchId = Guid.NewGuid();
        return new Campus
        {
            ChurchId = churchId,
            Name = name,
            City = TestValues.NewCity(),
            State = TestValues.NewStateCode(),
            Zip = TestValues.NewZip(),
            Latitude = TestValues.NewLatitude(),
            Longitude = TestValues.NewLongitude(),
        };
    }
}
