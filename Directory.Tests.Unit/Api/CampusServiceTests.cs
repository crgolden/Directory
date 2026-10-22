namespace Directory.Tests.Unit.Api;

using Directory.Campuses;
using Directory.Entities;
using Directory.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class CampusServiceTests
{
    private const int OneRowAffected = 1;

    [Fact]
    public async Task CreateAsync_WithStreet_ExecutesAndBindsTheStreet()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        var street = Generated.NewStreet();
        campus.Street = street;

        // Act
        await service.CreateAsync(campus.ChurchId, campus, TestContext.Current.CancellationToken);

        // Assert
        var insert = Assert.Single(conn.ExecutedCommands);
        Assert.True(insert.Executed);
        Assert.Equal(street, insert.Parameters[SqlParameters.Street].Value);
    }

    [Fact]
    public async Task CreateAsync_NoStreet_BindsDbNull()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());

        // Act
        await service.CreateAsync(campus.ChurchId, campus, TestContext.Current.CancellationToken);

        // Assert
        var insert = Assert.Single(conn.ExecutedCommands);
        Assert.Equal(DBNull.Value, insert.Parameters[SqlParameters.Street].Value);
    }

    [Fact]
    public async Task UpdateAsync_WithStreet_BindsTheStreet()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        var street = Generated.NewStreet();
        campus.Street = street;
        var campusId = Guid.NewGuid();

        // Act
        await service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken);

        // Assert
        var update = Assert.Single(conn.ExecutedCommands);
        Assert.Equal(street, update.Parameters[SqlParameters.Street].Value);
    }

    [Fact]
    public async Task UpdateAsync_NoRowAffected_ReturnsFalse()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        var campusId = Guid.NewGuid();

        // Act
        var updated = await service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(updated);
    }

    [Fact]
    public async Task DeleteAsync_RowAffected_ReturnsTrue()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = new CampusService(conn);
        var campusId = Guid.NewGuid();

        // Act
        var deleted = await service.DeleteAsync(campusId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleted);
    }

    [Fact]
    public async Task UpdateAsync_BlankName_NamesTheRequiredColumns()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewBlank());
        var campusId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentException>(exception);
        Assert.StartsWith(CampusService.RequiredColumnsMissing, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_UndefinedState_ThrowsAndWritesNothing()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        campus.State = Generated.NewUndefinedValue<Shared.Domain.StateCode>();
        var campusId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.StartsWith(CampusService.StateUndefined, ex.Message, StringComparison.Ordinal);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task UpdateAsync_LatitudeAndLongitudeAtTheirLimits_AreAccepted()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(OneRowAffected));
        var service = new CampusService(conn);
        var southWest = BuildCampus(Generated.NewName());
        southWest.Latitude = Shared.Domain.CampusBuilder.MinLatitude;
        southWest.Longitude = Shared.Domain.CampusBuilder.MinLongitude;
        var northEast = BuildCampus(Generated.NewName());
        northEast.Latitude = Shared.Domain.CampusBuilder.MaxLatitude;
        northEast.Longitude = Shared.Domain.CampusBuilder.MaxLongitude;
        var southWestId = Guid.NewGuid();
        var northEastId = Guid.NewGuid();

        // Act
        var southWestUpdated = await service.UpdateAsync(southWestId, southWest, TestContext.Current.CancellationToken);
        var northEastUpdated = await service.UpdateAsync(northEastId, northEast, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(southWestUpdated);
        Assert.True(northEastUpdated);
    }

    [Fact]
    public async Task UpdateAsync_LatitudeBelowTheMinimum_IsRejected()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        campus.Latitude = Math.BitDecrement(Shared.Domain.CampusBuilder.MinLatitude);
        var campusId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Contains(nameof(Campus.Latitude), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_LatitudeAboveTheMaximum_IsRejected()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        campus.Latitude = Math.BitIncrement(Shared.Domain.CampusBuilder.MaxLatitude);
        var campusId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Contains(nameof(Campus.Latitude), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_LongitudeBelowTheMinimum_IsRejected()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        campus.Longitude = Math.BitDecrement(Shared.Domain.CampusBuilder.MinLongitude);
        var campusId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Contains(nameof(Campus.Longitude), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_LongitudeAboveTheMaximum_IsRejected()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        campus.Longitude = Math.BitIncrement(Shared.Domain.CampusBuilder.MaxLongitude);
        var campusId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Contains(nameof(Campus.Longitude), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_InsertsCampus()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campusName = Generated.NewName();
        var campus = BuildCampus(campusName);

        // Act
        var result = await service.CreateAsync(campus.ChurchId, campus, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(campusName, result.Name);
        Assert.Contains(conn.ExecutedCommands, c => c.Executed);
    }

    [Fact]
    public async Task CreateAsync_BlankName_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var name = Generated.NewBlank();
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
    public async Task UpdateAsync_RowAffected_ReturnsTrue()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewName());
        var campusId = Guid.NewGuid();

        // Act
        var updated = await service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(updated);
        Assert.Contains(conn.ExecutedCommands, c => c.Executed);
    }

    [Fact]
    public async Task UpdateAsync_WithNoChurchId_StillUpdates_BecauseTheStatementNeverWritesOne()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new CampusService(conn);
        var campus = BuildCampusTheRouteWouldSend(Generated.NewName());
        var campusId = Guid.NewGuid();

        // Act
        var updated = await service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(updated);
    }

    [Fact]
    public async Task UpdateAsync_BlankName_ThrowsAndWritesNothing()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new CampusService(conn);
        var campus = BuildCampus(Generated.NewBlank());
        var campusId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.UpdateAsync(campusId, campus, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<ArgumentException>(exception);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
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
        Assert.Contains(conn.ExecutedCommands, c => c.Executed);
    }

    private static Campus BuildCampusTheRouteWouldSend(string name) => new Campus
    {
        ChurchId = Guid.Empty,
        Name = name,
        City = Generated.NewCity(),
        State = Generated.NewStateCode(),
        Zip = Generated.NewZip(),
        Latitude = Generated.NewLatitude(),
        Longitude = Generated.NewLongitude(),
    };

    private static Campus BuildCampus(string name)
    {
        var churchId = Guid.NewGuid();
        return new Campus
        {
            ChurchId = churchId,
            Name = name,
            City = Generated.NewCity(),
            State = Generated.NewStateCode(),
            Zip = Generated.NewZip(),
            Latitude = Generated.NewLatitude(),
            Longitude = Generated.NewLongitude(),
        };
    }
}
