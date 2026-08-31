namespace Directory.Tests.Unit.Api;

using System.Data;
using Church;
using Entities;
using TestSupport;

public sealed class ChurchServiceTests
{
    private const string BlankFieldValue = " ";

    private static readonly string SlugSourceCanonicalName = TestValues.NewName();
    private static readonly string SlugSourceCity = TestValues.NewCity();
    private static readonly string SlugSourceState = TestValues.NewStateCode();
    private static readonly string StateThatIsNotATwoLetterCode = TestValues.LowercaseToken(7);

    private static readonly string StoredCanonicalName = TestValues.NewName();
    private static readonly string StoredStreet = TestValues.NewStreet();
    private static readonly string StoredPhoneNumber = TestValues.NewPhoneNumber();
    private static readonly string CampusName = TestValues.NewName();
    private static readonly double CampusLatitude = TestValues.NewLatitude();
    private static readonly string MinistryName = TestValues.NewName();
    private static readonly TimeOnly SundayServiceStartTime = TestValues.NewTimeOfDay();

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

        conn.Enqueue(SlugFree());
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.City = BlankFieldValue;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(church, TestContext.Current.CancellationToken));

        Assert.Equal("city", ex.ParamName);
        Assert.DoesNotContain(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Churches]", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateAsync_StateIsNotATwoLetterCode_ThrowsWithoutTouchingDb()
    {
        var conn = new FakeDbConnection();
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.State = StateThatIsNotATwoLetterCode;

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

        var result = await service.GetBySlugAsync(TestValues.NewSlug(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_GeneratesKebabCaseSlug()
    {
        var conn = new FakeDbConnection();

        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());

        var service = new ChurchService(conn);
        var callerSuppliedSlug = TestValues.NewSlug();
        var church = BuildChurch();
        church.Slug = callerSuppliedSlug;

        var result = await service.CreateAsync(church, TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedSlug(), result.Slug);
        Assert.NotEqual(callerSuppliedSlug, result.Slug);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_AppendsSuffix_WhenSlugCollides()
    {
        var conn = new FakeDbConnection();

        conn.Enqueue(SlugExists());
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());

        var service = new ChurchService(conn);
        var callerSuppliedSlug = TestValues.NewSlug();
        var church = BuildChurch();
        church.Slug = callerSuppliedSlug;

        var result = await service.CreateAsync(church, TestContext.Current.CancellationToken);

        Assert.Equal($"{ExpectedSlug()}-2", result.Slug);
        Assert.NotEqual(callerSuppliedSlug, result.Slug);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPageAsync_NoRows_ReturnsEmptyAndOpensConnection()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(BuildChurchTable(includeTotalCount: true)));
        var service = new ChurchService(conn);

        var (items, totalCount) = await service.GetPageAsync(
            TestValues.NewPage(), TestValues.NewPageSize(), TestContext.Current.CancellationToken);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPageAsync_WithRows_MapsItemsAndReadsTotalCount()
    {
        var expectedTotalCount = TestValues.NewRowCount();
        var table = BuildChurchTable(includeTotalCount: true);
        table.Rows.Add(PopulatedRow(expectedTotalCount));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        var (items, totalCount) = await service.GetPageAsync(
            TestValues.NewPage(), TestValues.NewPageSize(), TestContext.Current.CancellationToken);

        Assert.Single(items);
        Assert.Equal(expectedTotalCount, totalCount);
        Assert.Equal(StoredCanonicalName, items[0].CanonicalName);
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

        var result = await service.GetBySlugAsync(TestValues.NewSlug(), TestContext.Current.CancellationToken);

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
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));
        conn.Enqueue(FakeDbCommand.WithReader(SchedulesTable()));
        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync(TestValues.NewSlug(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Schedules);
        Assert.Equal(2, result.Schedules.Count);
        Assert.Equal(DayOfWeek.Sunday, result.Schedules[0].DayOfWeek);
        Assert.Equal(SundayServiceStartTime, result.Schedules[0].StartTime);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_PopulatesMinistries()
    {
        var churchTable = BuildChurchTable(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        conn.Enqueue(FakeDbCommand.WithReader(MinistriesTable()));

        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync(TestValues.NewSlug(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Ministries);
        Assert.Equal(2, result.Ministries.Count);
        Assert.Equal(MinistryName, result.Ministries[0].Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetBySlugAsync_PopulatesCampuses()
    {
        var churchTable = BuildChurchTable(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        conn.Enqueue(FakeDbCommand.WithReader(CampusesTable()));

        var service = new ChurchService(conn);

        var result = await service.GetBySlugAsync(TestValues.NewSlug(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(result.Campuses);
        Assert.Single(result.Campuses);
        Assert.Equal(CampusName, result.Campuses[0].Name);
        Assert.Equal(CampusLatitude, result.Campuses[0].Latitude);
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
        Assert.Equal(StoredStreet, result.Street);
        Assert.Equal(StoredPhoneNumber, result.PhoneNumber);
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
        var street = TestValues.NewStreet();
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.Street = street;
        church.PhoneNumber = TestValues.NewPhoneNumber();
        church.Website = TestValues.NewWebsite();
        church.EmailAddress = TestValues.NewEmailAddress();
        church.DenominationId = Guid.NewGuid();
        church.AcceptsLGBTQ = true;
        church.WheelchairAccessible = true;
        church.HasNursery = true;
        church.HasYouthProgram = true;
        church.LastVerifiedAt = TestValues.NewUtcTimestamp();

        await service.CreateAsync(church, TestContext.Current.CancellationToken);

        var insert = conn.ExecutedCommands[1];
        Assert.Equal(street, insert.Parameters["@Street"].Value);
        Assert.True(insert.Parameters["@AcceptsLGBTQ"].Value is true);
        Assert.NotEqual(DBNull.Value, insert.Parameters["@DenominationId"].Value);
        Assert.NotEqual(DBNull.Value, insert.Parameters["@LastVerifiedAt"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_NewChurch_BindsCreatedAtAsDateTimeOffset()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);

        await service.CreateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        var insert = conn.ExecutedCommands[1];
        Assert.IsType<DateTimeOffset>(insert.Parameters["@CreatedAt"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_NewChurch_BindsCreatedAtInUtc()
    {
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);

        await service.CreateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        var insert = conn.ExecutedCommands[1];
        Assert.Equal(TimeSpan.Zero, Assert.IsType<DateTimeOffset>(insert.Parameters["@CreatedAt"].Value).Offset);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateAsync_LastVerifiedAtCarriesNonZeroOffset_BindsTheSameInstant()
    {
        var lastVerifiedAtInSourceOffset = TestValues.NewTimestampWithNonZeroOffset();
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.LastVerifiedAt = lastVerifiedAtInSourceOffset;

        await service.CreateAsync(church, TestContext.Current.CancellationToken);

        var insert = conn.ExecutedCommands[1];
        Assert.Equal(
            lastVerifiedAtInSourceOffset.UtcDateTime,
            Assert.IsType<DateTimeOffset>(insert.Parameters["@LastVerifiedAt"].Value).UtcDateTime);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_StoredOffsetIsNotZero_PreservesTheInstant()
    {
        var storedCreatedAt = TestValues.NewTimestampWithNonZeroOffset();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(TableWithCreatedAt(storedCreatedAt)));
        var service = new ChurchService(conn);

        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(storedCreatedAt.UtcDateTime, result.CreatedAt.UtcDateTime);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByIdAsync_StoredOffsetIsNotZero_PreservesTheOffset()
    {
        var storedCreatedAt = TestValues.NewTimestampWithNonZeroOffset();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(TableWithCreatedAt(storedCreatedAt)));
        var service = new ChurchService(conn);

        var result = await service.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(storedCreatedAt.Offset, result.CreatedAt.Offset);
    }

    private static DataTable TableWithCreatedAt(DateTimeOffset createdAt)
    {
        var table = BuildChurchTable(includeTotalCount: false);
        var onlyRow = table.Rows.Add(PopulatedRow(totalCount: null));
        onlyRow[nameof(Church.CreatedAt)] = createdAt;
        return table;
    }

    private static FakeDbCommand SlugFree() => FakeDbCommand.WithScalarResult(0);

    private static FakeDbCommand SlugExists() => FakeDbCommand.WithScalarResult(1);

    private static FakeDbCommand InsertSucceeds() => FakeDbCommand.WithNonQueryResult(1);

    private static string ExpectedSlug() =>
        $"{SlugSourceCanonicalName.Replace(' ', '-')}-{SlugSourceCity}-{SlugSourceState.ToLowerInvariant()}";

    private static Church BuildChurch() => new Church
    {
        CanonicalName = SlugSourceCanonicalName.ToUpperInvariant(),
        Slug = ExpectedSlug(),
        Latitude = TestValues.NewLatitude(),
        Longitude = TestValues.NewLongitude(),
        City = SlugSourceCity,
        State = SlugSourceState,
        Zip = TestValues.NewZip(),
        PrimaryLanguage = TestValues.NewLanguage(),
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
        t.Columns.Add("LastVerifiedAt", typeof(DateTimeOffset));
        t.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        t.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
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
        t.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        t.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), DBNull.Value, (byte)0, SundayServiceStartTime.ToTimeSpan(), TestValues.NewDescription(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), DBNull.Value, (byte)3, TestValues.NewTimeOfDay().ToTimeSpan(), DBNull.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static DataTable MinistriesTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("Name", typeof(string));
        t.Columns.Add("Description", typeof(string));
        t.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        t.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), MinistryName, TestValues.NewDescription(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), TestValues.NewName(), DBNull.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
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
        t.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        t.Columns.Add("UpdatedAt", typeof(DateTimeOffset));
        t.Rows.Add(Guid.NewGuid(), Guid.NewGuid(), CampusName, TestValues.NewStreet(), TestValues.NewCity(), TestValues.NewStateCode(), TestValues.NewZip(), CampusLatitude, TestValues.NewLongitude(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static object[] PopulatedRow(int? totalCount)
    {
        var values = new List<object>
        {
            Guid.NewGuid(), StoredCanonicalName, TestValues.NewSlug(), TestValues.NewLatitude(), TestValues.NewLongitude(), StoredStreet,
            TestValues.NewCity(), TestValues.NewStateCode(), TestValues.NewZip(), StoredPhoneNumber, TestValues.NewWebsite(), TestValues.NewEmailAddress(),
            Guid.NewGuid(), (int)TestValues.NewWorshipStyle(), TestValues.NewLanguage(), true, true, true, true, TestValues.NewConfidenceScore(),
            TestValues.NewUtcTimestamp(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true,
        };
        if (totalCount.HasValue)
        {
            values.Add(totalCount.Value);
        }

        return [.. values];
    }

    private static object[] NullableNullRow() =>
    [
        Guid.NewGuid(), TestValues.NewName(), TestValues.NewSlug(), TestValues.NewLatitude(), TestValues.NewLongitude(), DBNull.Value,
        TestValues.NewCity(), TestValues.NewStateCode(), TestValues.NewZip(), DBNull.Value, DBNull.Value, DBNull.Value,
        DBNull.Value, (int)TestValues.NewWorshipStyle(), TestValues.NewLanguage(), DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, TestValues.NewConfidenceScore(),
        DBNull.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true,
    ];
}
