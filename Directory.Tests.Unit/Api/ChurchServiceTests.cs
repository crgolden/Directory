namespace Directory.Tests.Unit.Api;

using System.Data;
using Directory.Church;
using Directory.Entities;
using Directory.Enums;
using Directory.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class ChurchServiceTests
{
    private static readonly string SlugSourceCanonicalName = Generated.NewName();
    private static readonly string SlugSourceCity = Generated.NewCity();
    private static readonly Shared.Domain.StateCode SlugSourceState = Generated.NewStateCode();
    private static readonly string SlugSourceStateText = SlugSourceState.ToString();

    private static readonly string StoredCanonicalName = Generated.NewName();
    private static readonly string StoredStreet = Generated.NewStreet();
    private static readonly string StoredPhoneNumber = Generated.NewPhoneNumber();
    private static readonly string StoredWebsite = Generated.NewWebsite();
    private static readonly string StoredEmailAddress = Generated.NewEmailAddress();
    private static readonly string CampusName = Generated.NewName();
    private static readonly double CampusLatitude = Generated.NewLatitude();
    private static readonly string MinistryName = Generated.NewName();
    private static readonly TimeOnly SundayServiceStartTime = Generated.NewTimeOfDay();

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNoRowsAffected()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.DeleteAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenRowDeleted()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.DeleteAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFalse_WhenNoRowsAffected()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(0));
        var service = new ChurchService(conn);

        // Act
        var result = await service.UpdateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsTrue_WhenRowUpdated()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);

        // Act
        var result = await service.UpdateAsync(BuildChurch(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CreateAsync_BlankCity_ThrowsBeforeInsert()
    {
        // Arrange
        var conn = new FakeDbConnection();

        conn.Enqueue(SlugFree());
        var service = new ChurchService(conn);
        var city = Generated.NewBlank();
        var request = BuildRequest() with { City = city };

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.CreateAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentException>(exception);
        Assert.Equal(nameof(city), ex.ParamName);
        Assert.DoesNotContain(conn.ExecutedCommands, c =>
            c.CommandText.Contains("INSERT INTO [dbo].[Churches]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateAsync_StateIsNotAUspsCode_ThrowsWithoutTouchingDb()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new ChurchService(conn);
        var state = Generated.NewUndefinedValue<Shared.Domain.StateCode>();
        var church = BuildChurch();
        church.State = state;

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.UpdateAsync(church, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(nameof(state), ex.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenNoRows()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_GeneratesKebabCaseSlug()
    {
        // Arrange
        var conn = new FakeDbConnection();

        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());

        var service = new ChurchService(conn);

        // Act
        var result = await service.CreateAsync(BuildRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExpectedSlug(), result.Slug);
    }

    [Fact]
    public async Task CreateAsync_AppendsSuffix_WhenSlugCollides()
    {
        // Arrange
        var conn = new FakeDbConnection();

        conn.Enqueue(SlugExists());
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());

        var service = new ChurchService(conn);

        // Act
        var result = await service.CreateAsync(BuildRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExpectedSlugWithFirstCollisionSuffix(), result.Slug);
    }

    [Fact]
    public async Task GetPageAsync_NoRows_ReturnsEmptyAndOpensConnection()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.Table(includeTotalCount: true)));
        var service = new ChurchService(conn);

        // Act
        var (items, totalCount) = await service.GetPageAsync(
            Generated.NewPage(), Generated.NewPageSize(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(items);
        Assert.Equal(0, totalCount);
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public async Task GetPageAsync_WithRows_MapsItemsAndReadsTotalCount()
    {
        // Arrange
        var expectedTotalCount = Generated.NewRowCount();
        var table = ChurchRows.Table(includeTotalCount: true);
        table.Rows.Add(PopulatedRow(expectedTotalCount));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        // Act
        var (items, totalCount) = await service.GetPageAsync(
            Generated.NewPage(), Generated.NewPageSize(), TestContext.Current.CancellationToken);

        // Assert
        var item = Assert.Single(items);
        Assert.Equal(expectedTotalCount, totalCount);
        Assert.Equal(StoredCanonicalName, item.CanonicalName);
    }

    [Fact]
    public async Task GetBySlugAsync_RowWithNullableNulls_MapsNullsForOptionalColumns()
    {
        var table = ChurchRows.Table(includeTotalCount: false);
        table.Rows.Add(NullableNullRow());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Street);
        Assert.Null(result.PhoneNumber);
        Assert.Null(result.DenominationId);
        Assert.Null(result.AcceptsLGBTQ);
        Assert.Null(result.LastVerifiedAt);
    }

    [Fact]
    public async Task GetBySlugAsync_PopulatesServiceSchedules()
    {
        var churchTable = ChurchRows.Table(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var schedulesTable = SchedulesTable();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));
        conn.Enqueue(FakeDbCommand.WithReader(schedulesTable));
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Schedules);
        Assert.Equal(schedulesTable.Rows.Count, result.Schedules.Count);
        Assert.Equal(DayOfWeek.Sunday, result.Schedules[0].DayOfWeek);
        Assert.Equal(SundayServiceStartTime, result.Schedules[0].StartTime);
    }

    [Fact]
    public async Task GetBySlugAsync_PopulatesMinistries()
    {
        var churchTable = ChurchRows.Table(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        var ministriesTable = MinistriesTable();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        conn.Enqueue(FakeDbCommand.WithReader(ministriesTable));

        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Ministries);
        Assert.Equal(ministriesTable.Rows.Count, result.Ministries.Count);
        Assert.Equal(MinistryName, result.Ministries[0].Name);
    }

    [Fact]
    public async Task GetBySlugAsync_PopulatesCampuses()
    {
        var churchTable = ChurchRows.Table(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        conn.Enqueue(FakeDbCommand.WithReader(new DataTable()));
        conn.Enqueue(FakeDbCommand.WithReader(CampusesTable()));

        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Campuses);
        var campus = Assert.Single(result.Campuses);
        Assert.Equal(CampusName, campus.Name);
        Assert.Equal(CampusLatitude, campus.Latitude);
    }

    [Fact]
    public async Task GetByIdAsync_NoRow_ReturnsNull()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(ChurchRows.Table(includeTotalCount: false)));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.GetByIdAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_RowPopulated_MapsAllOptionalColumns()
    {
        var table = ChurchRows.Table(includeTotalCount: false);
        table.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.GetByIdAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(StoredStreet, result.Street);
        Assert.Equal(StoredPhoneNumber, result.PhoneNumber);
        Assert.NotNull(result.DenominationId);
        Assert.True(result.AcceptsLGBTQ is true);
        Assert.NotNull(result.LastVerifiedAt);
    }

    [Fact]
    public async Task ExistsAsync_ScalarPositive_ReturnsTrue()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(1));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.ExistsAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ScalarZero_ReturnsFalse()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithScalarResult(0));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.ExistsAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateAsync_FullyPopulatedChurch_BindsOptionalValues()
    {
        // Arrange
        var street = Generated.NewStreet();
        var denominationId = Guid.NewGuid();
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);
        var request = BuildRequest() with
        {
            Street = street,
            PhoneNumber = Generated.NewPhoneNumber(),
            Website = Generated.NewWebsite(),
            EmailAddress = Generated.NewEmailAddress(),
            DenominationId = denominationId,
            AcceptsLGBTQ = true,
            WheelchairAccessible = true,
            HasNursery = true,
            HasYouthProgram = true,
        };

        // Act
        await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var insert = conn.ExecutedCommands[1];
        Assert.Equal(street, insert.Parameters[SqlParameters.Street].Value);
        Assert.True(insert.Parameters[SqlParameters.AcceptsLGBTQ].Value is true);
        Assert.Equal(denominationId, insert.Parameters[SqlParameters.DenominationId].Value);
    }

    [Fact]
    public async Task CreateAsync_NewChurch_BindsCreatedAtAsDateTimeOffset()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);

        // Act
        await service.CreateAsync(BuildRequest(), TestContext.Current.CancellationToken);

        // Assert
        var insert = conn.ExecutedCommands[1];
        Assert.IsType<DateTimeOffset>(insert.Parameters[SqlParameters.CreatedAt].Value);
    }

    [Fact]
    public async Task CreateAsync_NewChurch_BindsCreatedAtInUtc()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);

        // Act
        await service.CreateAsync(BuildRequest(), TestContext.Current.CancellationToken);

        // Assert
        var insert = conn.ExecutedCommands[1];
        Assert.Equal(TimeSpan.Zero, Assert.IsType<DateTimeOffset>(insert.Parameters[SqlParameters.CreatedAt].Value).Offset);
    }

    [Fact]
    public async Task UpdateAsync_LastVerifiedAtCarriesNonZeroOffset_BindsTheSameInstant()
    {
        // Arrange
        var lastVerifiedAtInSourceOffset = Generated.NewTimestampWithNonZeroOffset();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithNonQueryResult(1));
        var service = new ChurchService(conn);
        var church = BuildChurch();
        church.LastVerifiedAt = lastVerifiedAtInSourceOffset;

        // Act
        await service.UpdateAsync(church, TestContext.Current.CancellationToken);

        // Assert
        var update = conn.ExecutedCommands[0];
        Assert.Equal(
            lastVerifiedAtInSourceOffset.UtcDateTime,
            Assert.IsType<DateTimeOffset>(update.Parameters[SqlParameters.LastVerifiedAt].Value).UtcDateTime);
    }

    [Fact]
    public async Task GetByIdAsync_StoredOffsetIsNotZero_PreservesTheInstant()
    {
        var storedCreatedAt = Generated.NewTimestampWithNonZeroOffset();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(TableWithCreatedAt(storedCreatedAt)));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.GetByIdAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(storedCreatedAt.UtcDateTime, result.CreatedAt.UtcDateTime);
    }

    [Fact]
    public async Task GetByIdAsync_StoredOffsetIsNotZero_PreservesTheOffset()
    {
        var storedCreatedAt = Generated.NewTimestampWithNonZeroOffset();
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(TableWithCreatedAt(storedCreatedAt)));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.GetByIdAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(storedCreatedAt.Offset, result.CreatedAt.Offset);
    }

    [Fact]
    public async Task GetPageAsync_PageAfterTheFirst_BindsTheRowsBeforeItAsTheOffset()
    {
        // Arrange
        var pageAfterTheFirst = Generated.NewPage() + 1;
        var pageSizeAboveAnyPage = Generated.NewCountCeiling();
        var expectedOffset = (pageAfterTheFirst - 1) * pageSizeAboveAnyPage;
        var conn = new FakeDbConnection();
        var pageQuery = FakeDbCommand.WithReader(ChurchRows.Table(includeTotalCount: true));
        conn.Enqueue(pageQuery);
        var service = new ChurchService(conn);

        // Act
        await service.GetPageAsync(pageAfterTheFirst, pageSizeAboveAnyPage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedOffset, pageQuery.Parameters[SqlParameters.Offset].Value);
        Assert.Equal(pageSizeAboveAnyPage, pageQuery.Parameters[SqlParameters.PageSize].Value);
    }

    [Fact]
    public async Task GetBySlugAsync_StoredStateIsNotAUspsCode_ReturnsNullWithoutLoadingChildren()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(TableWithState(Generated.NewUnrecognizedStateCode())));
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        Assert.Single(conn.ExecutedCommands);
    }

    [Fact]
    public async Task CreateAsync_NullRequest_ThrowsNamingTheRequest()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var service = new ChurchService(conn);
        var createAsync = typeof(ChurchService).GetMethod(nameof(ChurchService.CreateAsync));
        Assert.NotNull(createAsync);
        var requestParameter = createAsync.GetParameters()[0].Name;

        // Act
        var pending = Assert.IsType<Task>(createAsync.Invoke(service, [null, TestContext.Current.CancellationToken]), exactMatch: false);
        var exception = await Record.ExceptionAsync(() => pending);

        // Assert
        var argumentNull = Assert.IsType<ArgumentNullException>(exception);
        Assert.Equal(requestParameter, argumentNull.ParamName);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task CreateAsync_StateIsNotAUspsCode_ThrowsNamingTheCodeWithoutTouchingDb()
    {
        // Arrange
        var state = Generated.NewUnrecognizedStateCode();
        var conn = new FakeDbConnection();
        var service = new ChurchService(conn);
        var request = BuildRequest() with { State = state };

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.CreateAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var ex = Assert.IsType<ArgumentException>(exception);
        Assert.Equal(nameof(request), ex.ParamName);
        Assert.Contains(state, ex.Message, StringComparison.Ordinal);
        Assert.Empty(conn.ExecutedCommands);
    }

    [Fact]
    public async Task CreateAsync_NameWithLeadingTrailingAndRepeatedSeparators_CollapsesThemToSingleDashes()
    {
        // Arrange
        var firstWord = Generated.NewLettersOnlyToken();
        var secondWord = Generated.NewLettersOnlyToken();
        var canonicalName = $"{Generated.NewBlank()}{firstWord}{Generated.NewBlank()}{Generated.NewPunctuationToken()}{secondWord}{Generated.NewBlank()}{Generated.NewPunctuationToken()}";
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);
        var request = BuildRequest() with { CanonicalName = canonicalName };

        // Act
        var result = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExpectedSlugForWords(firstWord, secondWord), result.Slug);
    }

    [Fact]
    public async Task CreateAsync_SlugCollidesTwice_AppendsTheNextSuffix()
    {
        // Arrange
        var conn = new FakeDbConnection();
        conn.Enqueue(SlugExists());
        conn.Enqueue(SlugExists());
        conn.Enqueue(SlugFree());
        conn.Enqueue(InsertSucceeds());
        var service = new ChurchService(conn);

        // Act
        var result = await service.CreateAsync(BuildRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ExpectedSlugWithSecondCollisionSuffix(), result.Slug);
    }

    [Fact]
    public async Task CreateAsync_NewChurch_BindsItActiveAndExecutesTheInsert()
    {
        // Arrange
        var conn = new FakeDbConnection();
        var insert = InsertSucceeds();
        conn.Enqueue(SlugFree());
        conn.Enqueue(insert);
        var service = new ChurchService(conn);

        // Act
        await service.CreateAsync(BuildRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(insert.Parameters[SqlParameters.IsActive].Value is true);
        Assert.True(insert.Executed);
    }

    [Fact]
    public async Task CreateAsync_ContactAndProgramValuesSet_BindsEachValue()
    {
        // Arrange
        var website = Generated.NewWebsite();
        var emailAddress = Generated.NewEmailAddress();
        var conn = new FakeDbConnection();
        var insert = InsertSucceeds();
        conn.Enqueue(SlugFree());
        conn.Enqueue(insert);
        var service = new ChurchService(conn);
        var request = BuildRequest() with
        {
            Website = website,
            EmailAddress = emailAddress,
            HasNursery = true,
            HasYouthProgram = true,
        };

        // Act
        await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(website, insert.Parameters[SqlParameters.Website].Value);
        Assert.Equal(emailAddress, insert.Parameters[SqlParameters.EmailAddress].Value);
        Assert.True(insert.Parameters[SqlParameters.HasNursery].Value is true);
        Assert.True(insert.Parameters[SqlParameters.HasYouthProgram].Value is true);
    }

    [Fact]
    public async Task GetByIdAsync_RowPopulated_MapsContactAndProgramColumns()
    {
        // Arrange
        var table = ChurchRows.Table(includeTotalCount: false);
        table.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new ChurchService(conn);
        var churchId = Guid.NewGuid();

        // Act
        var result = await service.GetByIdAsync(churchId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(StoredWebsite, result.Website);
        Assert.Equal(StoredEmailAddress, result.EmailAddress);
        Assert.True(result.WheelchairAccessible is true);
        Assert.True(result.HasNursery is true);
        Assert.True(result.HasYouthProgram is true);
    }

    [Fact]
    public async Task GetBySlugAsync_ScheduleAtACampusWithADescription_MapsBoth()
    {
        // Arrange
        var campusId = Generated.NewCampusId();
        var description = Generated.NewDescription();
        var conn = ConnectionServingChildren(
            ScheduleTable(campusId, description),
            new DataTable(),
            new DataTable());
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var schedule = Assert.Single(result.Schedules);
        Assert.Equal(campusId, schedule.CampusId);
        Assert.Equal(description, schedule.Description);
    }

    [Fact]
    public async Task GetBySlugAsync_MinistryWithADescription_MapsTheDescription()
    {
        // Arrange
        var description = Generated.NewDescription();
        var conn = ConnectionServingChildren(
            new DataTable(),
            MinistryTable(description),
            new DataTable());
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(description, Assert.Single(result.Ministries).Description);
    }

    [Fact]
    public async Task GetBySlugAsync_CampusWithAStreet_MapsTheStreet()
    {
        // Arrange
        var street = Generated.NewStreet();
        var conn = ConnectionServingChildren(
            new DataTable(),
            new DataTable(),
            CampusTable(street, Generated.NewStateCodeText()));
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(street, Assert.Single(result.Campuses).Street);
    }

    [Fact]
    public async Task GetBySlugAsync_CampusWithoutAStreet_MapsANullStreet()
    {
        // Arrange
        var conn = ConnectionServingChildren(
            new DataTable(),
            new DataTable(),
            CampusTable(DBNull.Value, Generated.NewStateCodeText()));
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(Assert.Single(result.Campuses).Street);
    }

    [Fact]
    public async Task GetBySlugAsync_CampusStateIsNotAUspsCode_SkipsTheCampus()
    {
        // Arrange
        var conn = ConnectionServingChildren(
            new DataTable(),
            new DataTable(),
            CampusTable(Generated.NewStreet(), Generated.NewUnrecognizedStateCode()));
        var service = new ChurchService(conn);

        // Act
        var result = await service.GetBySlugAsync(Generated.NewSlug(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Campuses);
    }

    private static DataTable TableWithState(string state)
    {
        var table = ChurchRows.Table(includeTotalCount: false);
        var onlyRow = table.Rows.Add(PopulatedRow(totalCount: null));
        onlyRow[nameof(Church.State)] = state;
        return table;
    }

    private static FakeDbConnection ConnectionServingChildren(DataTable schedules, DataTable ministries, DataTable campuses)
    {
        var churchTable = ChurchRows.Table(includeTotalCount: false);
        churchTable.Rows.Add(PopulatedRow(totalCount: null));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(churchTable));
        conn.Enqueue(FakeDbCommand.WithReader(schedules));
        conn.Enqueue(FakeDbCommand.WithReader(ministries));
        conn.Enqueue(FakeDbCommand.WithReader(campuses));
        return conn;
    }

    private static DataTable ScheduleTable(Guid campusId, string description)
    {
        var t = new DataTable();
        t.Columns.Add(nameof(ServiceSchedule.Id), typeof(Guid));
        t.Columns.Add(nameof(ServiceSchedule.ChurchId), typeof(Guid));
        t.Columns.Add(nameof(ServiceSchedule.CampusId), typeof(Guid));
        t.Columns.Add(nameof(ServiceSchedule.DayOfWeek), typeof(byte));
        t.Columns.Add(nameof(ServiceSchedule.StartTime), typeof(TimeSpan));
        t.Columns.Add(nameof(ServiceSchedule.Description), typeof(string));
        t.Columns.Add(nameof(ServiceSchedule.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(ServiceSchedule.UpdatedAt), typeof(DateTimeOffset));
        var scheduleId = Guid.NewGuid();
        var scheduleChurchId = Guid.NewGuid();
        t.Rows.Add(scheduleId, scheduleChurchId, campusId, Generated.NewDayOfWeek(), Generated.NewTimeOfDay().ToTimeSpan(), description, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static DataTable MinistryTable(string description)
    {
        var t = new DataTable();
        t.Columns.Add(nameof(Ministry.Id), typeof(Guid));
        t.Columns.Add(nameof(Ministry.ChurchId), typeof(Guid));
        t.Columns.Add(nameof(Ministry.Name), typeof(string));
        t.Columns.Add(nameof(Ministry.Description), typeof(string));
        t.Columns.Add(nameof(Ministry.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Ministry.UpdatedAt), typeof(DateTimeOffset));
        var ministryId = Guid.NewGuid();
        var ministryChurchId = Guid.NewGuid();
        t.Rows.Add(ministryId, ministryChurchId, Generated.NewName(), description, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static DataTable CampusTable(object street, string state)
    {
        var t = new DataTable();
        t.Columns.Add(nameof(Campus.Id), typeof(Guid));
        t.Columns.Add(nameof(Campus.ChurchId), typeof(Guid));
        t.Columns.Add(nameof(Campus.Name), typeof(string));
        t.Columns.Add(nameof(Campus.Street), typeof(string));
        t.Columns.Add(nameof(Campus.City), typeof(string));
        t.Columns.Add(nameof(Campus.State), typeof(string));
        t.Columns.Add(nameof(Campus.Zip), typeof(string));
        t.Columns.Add(nameof(Campus.Latitude), typeof(double));
        t.Columns.Add(nameof(Campus.Longitude), typeof(double));
        t.Columns.Add(nameof(Campus.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Campus.UpdatedAt), typeof(DateTimeOffset));
        var campusId = Guid.NewGuid();
        var campusChurchId = Guid.NewGuid();
        t.Rows.Add(campusId, campusChurchId, Generated.NewName(), street, Generated.NewCity(), state, Generated.NewZip(), Generated.NewLatitude(), Generated.NewLongitude(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static DataTable TableWithCreatedAt(DateTimeOffset createdAt)
    {
        var table = ChurchRows.Table(includeTotalCount: false);
        var onlyRow = table.Rows.Add(PopulatedRow(totalCount: null));
        onlyRow[nameof(Church.CreatedAt)] = createdAt;
        return table;
    }

    private static FakeDbCommand SlugFree() => FakeDbCommand.WithScalarResult(0);

    private static FakeDbCommand SlugExists() => FakeDbCommand.WithScalarResult(1);

    private static FakeDbCommand InsertSucceeds() => FakeDbCommand.WithNonQueryResult(1);

    private static string ExpectedSlug() =>
        $"{SlugSourceCanonicalName.Replace(' ', '-')}-{SlugSourceCity}-{SlugSourceStateText.ToLowerInvariant()}";

    private static string ExpectedSlugWithFirstCollisionSuffix() =>
        $"{ExpectedSlug()}-{ChurchService.FirstSlugCollisionSuffix}";

    private static string ExpectedSlugWithSecondCollisionSuffix() =>
        $"{ExpectedSlug()}-{ChurchService.FirstSlugCollisionSuffix + 1}";

    private static string ExpectedSlugForWords(string firstWord, string secondWord) =>
        $"{firstWord}-{secondWord}-{SlugSourceCity}-{SlugSourceStateText.ToLowerInvariant()}";

    private static ChurchRequest BuildRequest() => new ChurchRequest(
        CanonicalName: SlugSourceCanonicalName.ToUpperInvariant(),
        Latitude: Generated.NewLatitude(),
        Longitude: Generated.NewLongitude(),
        Street: null,
        City: SlugSourceCity,
        State: SlugSourceStateText,
        Zip: Generated.NewZip(),
        PhoneNumber: null,
        Website: null,
        EmailAddress: null,
        DenominationId: null,
        WorshipStyle: Generated.NewDefinedValue<WorshipStyle>(),
        PrimaryLanguage: Generated.NewLanguage(),
        AcceptsLGBTQ: null,
        WheelchairAccessible: null,
        HasNursery: null,
        HasYouthProgram: null);

    private static Church BuildChurch() => new Church
    {
        CanonicalName = SlugSourceCanonicalName.ToUpperInvariant(),
        Slug = ExpectedSlug(),
        Latitude = Generated.NewLatitude(),
        Longitude = Generated.NewLongitude(),
        City = SlugSourceCity,
        State = SlugSourceState,
        Zip = Generated.NewZip(),
        PrimaryLanguage = Generated.NewLanguage(),
    };

    private static DataTable SchedulesTable()
    {
        var t = new DataTable();
        t.Columns.Add(nameof(ServiceSchedule.Id), typeof(Guid));
        t.Columns.Add(nameof(ServiceSchedule.ChurchId), typeof(Guid));
        t.Columns.Add(nameof(ServiceSchedule.CampusId), typeof(Guid));
        t.Columns.Add(nameof(ServiceSchedule.DayOfWeek), typeof(byte));
        t.Columns.Add(nameof(ServiceSchedule.StartTime), typeof(TimeSpan));
        t.Columns.Add(nameof(ServiceSchedule.Description), typeof(string));
        t.Columns.Add(nameof(ServiceSchedule.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(ServiceSchedule.UpdatedAt), typeof(DateTimeOffset));
        var sundayScheduleId = Guid.NewGuid();
        var sundayScheduleChurchId = Guid.NewGuid();
        var midweekScheduleId = Guid.NewGuid();
        var midweekScheduleChurchId = Guid.NewGuid();
        t.Rows.Add(sundayScheduleId, sundayScheduleChurchId, DBNull.Value, (byte)DayOfWeek.Sunday, SundayServiceStartTime.ToTimeSpan(), Generated.NewDescription(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        t.Rows.Add(midweekScheduleId, midweekScheduleChurchId, DBNull.Value, (byte)DayOfWeek.Wednesday, Generated.NewTimeOfDay().ToTimeSpan(), DBNull.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static DataTable MinistriesTable()
    {
        var t = new DataTable();
        t.Columns.Add(nameof(Ministry.Id), typeof(Guid));
        t.Columns.Add(nameof(Ministry.ChurchId), typeof(Guid));
        t.Columns.Add(nameof(Ministry.Name), typeof(string));
        t.Columns.Add(nameof(Ministry.Description), typeof(string));
        t.Columns.Add(nameof(Ministry.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Ministry.UpdatedAt), typeof(DateTimeOffset));
        var describedMinistryId = Guid.NewGuid();
        var describedMinistryChurchId = Guid.NewGuid();
        var undescribedMinistryId = Guid.NewGuid();
        var undescribedMinistryChurchId = Guid.NewGuid();
        t.Rows.Add(describedMinistryId, describedMinistryChurchId, MinistryName, Generated.NewDescription(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        t.Rows.Add(undescribedMinistryId, undescribedMinistryChurchId, Generated.NewName(), DBNull.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static DataTable CampusesTable()
    {
        var t = new DataTable();
        t.Columns.Add(nameof(Campus.Id), typeof(Guid));
        t.Columns.Add(nameof(Campus.ChurchId), typeof(Guid));
        t.Columns.Add(nameof(Campus.Name), typeof(string));
        t.Columns.Add(nameof(Campus.Street), typeof(string));
        t.Columns.Add(nameof(Campus.City), typeof(string));
        t.Columns.Add(nameof(Campus.State), typeof(string));
        t.Columns.Add(nameof(Campus.Zip), typeof(string));
        t.Columns.Add(nameof(Campus.Latitude), typeof(double));
        t.Columns.Add(nameof(Campus.Longitude), typeof(double));
        t.Columns.Add(nameof(Campus.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Campus.UpdatedAt), typeof(DateTimeOffset));
        var campusId = Guid.NewGuid();
        var campusChurchId = Guid.NewGuid();
        t.Rows.Add(campusId, campusChurchId, CampusName, Generated.NewStreet(), Generated.NewCity(), Generated.NewStateCodeText(), Generated.NewZip(), CampusLatitude, Generated.NewLongitude(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        return t;
    }

    private static object[] PopulatedRow(int? totalCount)
    {
        var churchId = Guid.NewGuid();
        var denominationId = Guid.NewGuid();
        var values = new List<object>
        {
            churchId, StoredCanonicalName, Generated.NewSlug(), Generated.NewLatitude(), Generated.NewLongitude(), StoredStreet,
            Generated.NewCity(), Generated.NewStateCodeText(), Generated.NewZip(), StoredPhoneNumber, StoredWebsite, StoredEmailAddress,
            denominationId, (int)Generated.NewDefinedValue<WorshipStyle>(), Generated.NewLanguage(), true, true, true, true, Generated.NewConfidenceScore(),
            Generated.NewUtcTimestamp(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true,
        };
        if (totalCount.HasValue)
        {
            values.Add(totalCount.Value);
        }

        return [.. values];
    }

    private static object[] NullableNullRow()
    {
        var churchId = Guid.NewGuid();
        return
        [
            churchId, Generated.NewName(), Generated.NewSlug(), Generated.NewLatitude(), Generated.NewLongitude(), DBNull.Value,
            Generated.NewCity(), Generated.NewStateCodeText(), Generated.NewZip(), DBNull.Value, DBNull.Value, DBNull.Value,
            DBNull.Value, (int)Generated.NewDefinedValue<WorshipStyle>(), Generated.NewLanguage(), DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, Generated.NewConfidenceScore(),
            DBNull.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true,
        ];
    }
}
