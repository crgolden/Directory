namespace Directory.Tests.Unit.Api;

using System.Data;
using Church;
using Entities;
using TestSupport;

public sealed class ChurchServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_ReturnsFalse_WhenNoRowsAffected()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = new ChurchService(conn);

        var result = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_ReturnsTrue_WhenRowDeleted()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);

        var result = await service.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_ReturnsFalse_WhenNoRowsAffected()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = new ChurchService(conn);

        var result = await service.UpdateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_ReturnsTrue_WhenRowUpdated()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);

        var result = await service.UpdateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_BlankCity_ThrowsBeforeInsert()
    {
        var conn = new FakeDbConnection();

        // Slug generation (a harmless read checking for collisions) runs before validation, so one
        // command is expected here — the point of this test is that the actual INSERT never happens.
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.City = string.Empty;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(church, TestContext.Current.CancellationToken));

        Assert.Equal("city", ex.ParamName);
        Assert.DoesNotContain(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Churches]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_BlankState_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.State = "Arizona";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(church, TestContext.Current.CancellationToken));

        Assert.Equal("state", ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_ReturnsNull_WhenNoRows()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync("some-slug", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_GeneratesKebabCaseSlug()
    {
        var conn = new FakeDbConnection();

        // slug existence check → 0 means slug is free
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));

        // INSERT
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.Slug = string.Empty;

        var result = await service.CreateAsync(church, TestContext.Current.CancellationToken);

        Assert.Equal("grace-church-phoenix-az", result.Slug);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_AppendsSuffix_WhenSlugCollides()
    {
        var conn = new FakeDbConnection();

        // first check: slug exists
        conn.Enqueue(FakeDbCommand.WithScalarResult(1));

        // second check: slug-2 is free
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));

        // INSERT
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.Slug = string.Empty;

        var result = await service.CreateAsync(church, TestContext.Current.CancellationToken);

        Assert.Equal("grace-church-phoenix-az-2", result.Slug);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPageAsync_NoRows_ReturnsEmptyAndOpensConnection()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(BuildChurchTable(includeTotalCount: true)));
        var service = new ChurchService(conn);

        var (items, totalCount) = await service.GetPageAsync(1, 20, TestContext.Current.CancellationToken);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPageAsync_WithRows_MapsItemsAndReadsTotalCount()
    {
        var table = BuildChurchTable(includeTotalCount: true);
        table.Rows.Add(PopulatedRow(totalCount: 5));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        var (items, totalCount) = await service.GetPageAsync(1, 20, TestContext.Current.CancellationToken);

        Assert.Single(items);
        Assert.Equal(5, totalCount);
        Assert.Equal("Grace Church", items[0].CanonicalName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_RowWithNullableNulls_MapsNullsForOptionalColumns()
    {
        var table = BuildChurchTable(includeTotalCount: false);
        table.Rows.Add(NullableNullRow());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync("grace-church", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Null(result.Street);
        Assert.Null(result.PhoneNumber);
        Assert.Null(result.DenominationId);
        Assert.Null(result.AcceptsLGBTQ);
        Assert.Null(result.LastVerifiedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_PopulatesServiceSchedules()
    {
        var churchTable = BuildChurchTable(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable)); // church query
        conn.Enqueue(FakeDbCommand.WithReader(SchedulesTable())); // schedules query
        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync("grace-church", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Schedules);
        Assert.Equal(2, result.Schedules.Count);
        Assert.Equal(DayOfWeek.Sunday, result.Schedules[0].DayOfWeek);
        Assert.Equal(new TimeOnly(10, 30), result.Schedules[0].StartTime);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_PopulatesMinistries()
    {
        var churchTable = BuildChurchTable(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));     // church query
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable())); // schedules query (none)
        conn.Enqueue(FakeDbCommand.WithReader(MinistriesTable())); // ministries query

        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync("grace-church", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Ministries);
        Assert.Equal(2, result.Ministries.Count);
        Assert.Equal("Food Bank", result.Ministries[0].Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_PopulatesCampuses()
    {
        var churchTable = BuildChurchTable(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));     // church query
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable())); // schedules query (none)
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable())); // ministries query (none)
        conn.Enqueue(FakeDbCommand.WithReader(CampusesTable())); // campuses query

        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync("grace-church", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Campuses);
        Assert.Single(result.Campuses);
        Assert.Equal("North Campus", result.Campuses[0].Name);
        Assert.Equal(39.7, result.Campuses[0].Latitude);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_NoRow_ReturnsNull()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(BuildChurchTable(includeTotalCount: false)));
        var service = new ChurchService(conn);

        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_RowPopulated_MapsAllOptionalColumns()
    {
        var table = BuildChurchTable(includeTotalCount: false);
        table.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("123 Main", result.Street);
        Assert.Equal("602-555-1212", result.PhoneNumber);
        Assert.NotNull(result.DenominationId);
        Assert.True(result.AcceptsLGBTQ is true);
        Assert.NotNull(result.LastVerifiedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExistsAsync_ScalarPositive_ReturnsTrue()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(1));
        var service = new ChurchService(conn);

        var result = await service.ExistsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExistsAsync_ScalarZero_ReturnsFalse()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));
        var service = new ChurchService(conn);

        var result = await service.ExistsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_FullyPopulatedChurch_BindsOptionalValues()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(0)); // slug free
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1)); // insert
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.Street = "123 Main";
        church.PhoneNumber = "602-555-1212";
        church.Website = "https://grace.example";
        church.EmailAddress = "hi@grace.example";
        church.DenominationId = Guid.NewGuid();
        church.AcceptsLGBTQ = true;
        church.WheelchairAccessible = true;
        church.HasNursery = true;
        church.HasYouthProgram = true;
        church.LastVerifiedAt = DateTimeOffset.UtcNow;

        await service.CreateAsync(church, TestContext.Current.CancellationToken);

        var insert = conn.ExecutedCommands[1];
        Assert.Equal("123 Main", insert.Parameters["@Street"].Value);
        Assert.True(insert.Parameters["@AcceptsLGBTQ"].Value is true);
        Assert.NotEqual(DBNull.Value, insert.Parameters["@DenominationId"].Value);
        Assert.NotEqual(DBNull.Value, insert.Parameters["@LastVerifiedAt"].Value);
    }

    private static Church BuildChurch() => new Church
    {
        CanonicalName = "Grace Church",
        Slug = "grace-church-phoenix-az",
        Latitude = 33.4,
        Longitude = -112.0,
        City = "Phoenix",
        State = "AZ",
        Zip = "85001",
        PrimaryLanguage = "English",
    };

    private static DataTable BuildChurchTable(bool includeTotalCount)
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("CanonicalName", typeof(string));
        t.Columns.Add("Slug", typeof(string));
        t.Columns.Add("Latitude", typeof(double));
        t.Columns.Add("Longitude", typeof(double));
        t.Columns.Add("Street", typeof(string));
        t.Columns.Add("City", typeof(string));
        t.Columns.Add("State", typeof(string));
        t.Columns.Add("Zip", typeof(string));
        t.Columns.Add("PhoneNumber", typeof(string));
        t.Columns.Add("Website", typeof(string));
        t.Columns.Add("EmailAddress", typeof(string));
        t.Columns.Add("DenominationId", typeof(Guid));
        t.Columns.Add("WorshipStyle", typeof(int));
        t.Columns.Add("PrimaryLanguage", typeof(string));
        t.Columns.Add("AcceptsLGBTQ", typeof(bool));
        t.Columns.Add("WheelchairAccessible", typeof(bool));
        t.Columns.Add("HasNursery", typeof(bool));
        t.Columns.Add("HasYouthProgram", typeof(bool));
        t.Columns.Add("ConfidenceScore", typeof(decimal));
        t.Columns.Add("LastVerifiedAt", typeof(DateTime));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        t.Columns.Add("IsActive", typeof(bool));
        if (includeTotalCount)
        {
            t.Columns.Add("TotalCount", typeof(int));
        }

        return t;
    }

    private static DataTable SchedulesTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("CampusId", typeof(Guid));
        t.Columns.Add("DayOfWeek", typeof(byte));
        t.Columns.Add("StartTime", typeof(TimeSpan));
        t.Columns.Add("Description", typeof(string));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), DBNull.Value, (byte)0, new TimeSpan(10, 30, 0), "Sunday Worship", DateTime.UtcNow, DateTime.UtcNow);
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), DBNull.Value, (byte)3, new TimeSpan(19, 0, 0), DBNull.Value, DateTime.UtcNow, DateTime.UtcNow);
        return t;
    }

    private static DataTable MinistriesTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("Name", typeof(string));
        t.Columns.Add("Description", typeof(string));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "Food Bank", "Weekly pantry", DateTime.UtcNow, DateTime.UtcNow);
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "Youth Group", DBNull.Value, DateTime.UtcNow, DateTime.UtcNow);
        return t;
    }

    private static DataTable CampusesTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("Name", typeof(string));
        t.Columns.Add("Street", typeof(string));
        t.Columns.Add("City", typeof(string));
        t.Columns.Add("State", typeof(string));
        t.Columns.Add("Zip", typeof(string));
        t.Columns.Add("Latitude", typeof(double));
        t.Columns.Add("Longitude", typeof(double));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), "North Campus", "1 N St", "Denver", "CO", "80201", 39.7, -104.9, DateTime.UtcNow, DateTime.UtcNow);
        return t;
    }

    private static object[] PopulatedRow(int? totalCount)
    {
        var values = new List<object>
        {
            Guid.NewGuid(), "Grace Church", "grace-church", 33.4, -112.0, "123 Main",
            "Phoenix", "AZ", "85001", "602-555-1212", "https://grace.example", "hi@grace.example",
            Guid.NewGuid(), 1, "English", true, true, true, true, 0.9m,
            DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true,
        };
        if (totalCount.HasValue)
        {
            values.Add(totalCount.Value);
        }

        return [.. values];
    }

    private static object[] NullableNullRow() =>
    [
        Guid.NewGuid(), "Grace Church", "grace-church", 33.4, -112.0, DBNull.Value,
        "Phoenix", "AZ", "85001", DBNull.Value, DBNull.Value, DBNull.Value,
        DBNull.Value, 1, "English", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 0.9m,
        DBNull.Value, DateTime.UtcNow, DateTime.UtcNow, true,
    ];
}
