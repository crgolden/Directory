namespace Directory.Tests.Unit.Api;

using System.Data;
using Enums;
using Search;
using TestSupport;

public sealed class SearchServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_IncludesDistanceColumn_WhenGeoFilterProvided()
    {
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(
            QueryWith(lat: searchLatitude, lng: searchLongitude), TestContext.Current.CancellationToken);

        Assert.Contains("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_ExcludesDistanceColumn_WhenNoGeoFilter()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(QueryWith(), TestContext.Current.CancellationToken);

        Assert.Contains("CAST(NULL AS FLOAT)", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_IncludesContainsTableJoin_WhenKeywordProvided()
    {
        var searchKeyword = TestValues.NewKeyword();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(QueryWith(q: searchKeyword), TestContext.Current.CancellationToken);

        Assert.Contains("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Contains("ft.[KEY] = c.[Id]", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_OmitsContainsTableJoin_WhenNoKeyword()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(QueryWith(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_OmitsContainsTableJoin_WhenKeywordIsJunkOnly()
    {
        var punctuationOnlyQuery = "!!! ---";
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(QueryWith(q: punctuationOnlyQuery), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_IncludesStateFilter_WhenStateProvided()
    {
        var stateFilter = TestValues.NewStateCode();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(QueryWith(state: stateFilter), TestContext.Current.CancellationToken);

        Assert.Contains("@State", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_IncludesWheelchairFilter_WhenFilterProvided()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(
            QueryWith(wheelchairAccessible: true), TestContext.Current.CancellationToken);

        Assert.Contains("@WheelchairAccessible", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_OrdersByDistance_WhenGeoFilterProvided()
    {
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(
            QueryWith(lat: searchLatitude, lng: searchLongitude), TestContext.Current.CancellationToken);

        Assert.Contains("ORDER BY", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Contains("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_ReturnsEmptyResult_WhenNoRows()
    {
        var conn = BuildConn(out _);
        var service = new SearchService(conn);

        var (items, totalCount) = await service.SearchAsync(
            QueryWith(), TestContext.Current.CancellationToken);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DenominationIdSet_AddsFilter()
    {
        var filteredDenominationId = Guid.NewGuid();
        var query = QueryWith(denominationId: filteredDenominationId);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("c.[DenominationId] = @DenominationId", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_WorshipStyleSet_AddsFilter()
    {
        var filteredWorshipStyle = TestValues.NewWorshipStyle();
        var query = QueryWith(worshipStyle: filteredWorshipStyle);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("c.[WorshipStyle] = @WorshipStyle", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DayOfWeekSet_AddsScheduleJoin()
    {
        // Arrange
        var filteredDayOfWeek = TestValues.NewDayOfWeek();
        var query = QueryWith(dayOfWeek: filteredDayOfWeek);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("[ServiceSchedules]", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[DayOfWeek] = @DayOfWeek", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@StartTimeAfter", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@StartTimeBefore", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_StartTimeAfterSet_AddsScheduleJoinWithTimeFilter()
    {
        // Arrange
        var earliestStartTime = TestValues.NewTimeOfDay();
        var query = QueryWith(startTimeAfter: earliestStartTime);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("[ServiceSchedules]", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] >= @StartTimeAfter", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@DayOfWeek", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_StartTimeBeforeSet_AddsScheduleJoinWithTimeFilter()
    {
        // Arrange
        var latestStartTime = TestValues.NewTimeOfDay();
        var query = QueryWith(startTimeBefore: latestStartTime);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("[ServiceSchedules]", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] <= @StartTimeBefore", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@StartTimeAfter", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_AllScheduleFiltersSet_AddsAllConditions()
    {
        // Arrange
        var filteredDayOfWeek = TestValues.NewDayOfWeek();
        var latestStartTime = TestValues.NewTimeOfDay();
        var earliestStartTime = TestValues.NewTimeOfDay();
        var query = QueryWith(
            dayOfWeek: filteredDayOfWeek,
            startTimeBefore: latestStartTime,
            startTimeAfter: earliestStartTime);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ss.[DayOfWeek] = @DayOfWeek", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] >= @StartTimeAfter", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] <= @StartTimeBefore", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_NoScheduleFilters_OmitsScheduleJoin()
    {
        // Arrange
        var stateFilter = TestValues.NewStateCode();
        var query = QueryWith(state: stateFilter);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.DoesNotContain("[ServiceSchedules]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_ScheduleFiltersSet_BindsAllThree()
    {
        // Arrange
        var cmd = new FakeDbCommand();
        var filteredDayOfWeek = TestValues.NewDayOfWeek();
        var earliestStartHour = Random.Shared.Next(0, 24);
        var earliestStartMinute = Random.Shared.Next(0, 60);
        var latestStartHour = Random.Shared.Next(0, 24);
        var latestStartMinute = Random.Shared.Next(0, 60);
        var earliestStartTime = new TimeOnly(earliestStartHour, earliestStartMinute);
        var latestStartTime = new TimeOnly(latestStartHour, latestStartMinute);
        var query = QueryWith(
            dayOfWeek: filteredDayOfWeek,
            startTimeBefore: latestStartTime,
            startTimeAfter: earliestStartTime);

        // Act
        SearchService.BindParams(cmd, query);

        // Assert
        Assert.True(cmd.Parameters.Contains("@DayOfWeek"));
        Assert.True(cmd.Parameters.Contains("@StartTimeAfter"));
        Assert.True(cmd.Parameters.Contains("@StartTimeBefore"));
        Assert.Equal(filteredDayOfWeek, cmd.Parameters["@DayOfWeek"].Value);
        Assert.Equal(new TimeSpan(earliestStartHour, earliestStartMinute, 0), cmd.Parameters["@StartTimeAfter"].Value);
        Assert.Equal(new TimeSpan(latestStartHour, latestStartMinute, 0), cmd.Parameters["@StartTimeBefore"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_AllFiltersSet_BindsProvidedRadiusAndOptionalParams()
    {
        var cmd = new FakeDbCommand();
        var searchKeyword = TestValues.NewKeyword();
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var searchRadiusMiles = TestValues.NewRadiusMiles();
        var stateFilter = TestValues.NewStateCode();
        var filteredDenominationId = Guid.NewGuid();
        var filteredWorshipStyle = TestValues.NewWorshipStyle();
        var query = QueryWith(
            q: searchKeyword,
            lat: searchLatitude,
            lng: searchLongitude,
            radiusMiles: searchRadiusMiles,
            state: stateFilter,
            denominationId: filteredDenominationId,
            worshipStyle: filteredWorshipStyle,
            wheelchairAccessible: true);

        SearchService.BindParams(cmd, query);

        Assert.Equal(searchRadiusMiles, cmd.Parameters["@RadiusMiles"].Value);
        Assert.Equal(filteredDenominationId, cmd.Parameters["@DenominationId"].Value);
        Assert.Equal((int)filteredWorshipStyle, cmd.Parameters["@WorshipStyle"].Value);
        Assert.True(cmd.Parameters.Contains("@WheelchairAccessible"));
        Assert.Equal(stateFilter, cmd.Parameters["@State"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_GeoQueryRowWithDistance_MapsDistanceAndTotalCount()
    {
        var expectedStreet = TestValues.NewStreet();
        var expectedDistanceMiles = TestValues.NewRadiusMiles();
        var expectedTotalCount = TestValues.NewRowCount();
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowPopulated(expectedStreet, expectedDistanceMiles, expectedTotalCount));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new SearchService(conn);
        var query = QueryWith(lat: searchLatitude, lng: searchLongitude);

        var (items, totalCount) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(expectedTotalCount, totalCount);
        var result = Assert.Single(items);
        Assert.Equal(expectedDistanceMiles, result.DistanceMiles);
        Assert.Equal(expectedStreet, result.Church.Street);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_GeoQueryRowWithNullDistance_LeavesDistanceNull()
    {
        var rowStreet = TestValues.NewStreet();
        var rowTotalCount = TestValues.NewRowCount();
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowPopulated(rowStreet, DBNull.Value, rowTotalCount));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new SearchService(conn);
        var query = QueryWith(lat: searchLatitude, lng: searchLongitude);

        var (items, _) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(items).DistanceMiles);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_NoGeoQueryRowWithNullableNulls_MapsNullsAndNoDistance()
    {
        var expectedTotalCount = TestValues.NewRowCount();
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowNullable(expectedTotalCount));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new SearchService(conn);
        var query = QueryWith();

        var (items, totalCount) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(expectedTotalCount, totalCount);
        var result = Assert.Single(items);
        Assert.Null(result.DistanceMiles);
        Assert.Null(result.Church.Street);
        Assert.Null(result.Church.AcceptsLGBTQ);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_MultipleWords_BuildsAndOfPrefixTerms()
    {
        var firstWord = TestValues.NewKeyword();
        var secondWord = TestValues.NewKeyword();

        var condition = SearchService.BuildContainsCondition($"{firstWord} {secondWord}", out var terms);

        Assert.Equal($"\"{firstWord}*\" AND \"{secondWord}*\"", condition);
        Assert.Equal([firstWord, secondWord], terms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_JunkOnlyInput_ReturnsNullAndNoTerms()
    {
        var punctuationOnlyQuery = "!!! ---";

        var condition = SearchService.BuildContainsCondition(punctuationOnlyQuery, out var terms);

        Assert.Null(condition);
        Assert.Empty(terms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_NullOrWhitespace_ReturnsNullAndNoTerms()
    {
        var whitespaceOnlyQuery = new string(' ', Random.Shared.Next(1, 4));

        Assert.Null(SearchService.BuildContainsCondition(null, out var terms1));
        Assert.Empty(terms1);

        Assert.Null(SearchService.BuildContainsCondition(whitespaceOnlyQuery, out var terms2));
        Assert.Empty(terms2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_StripsPunctuation_KeepsApostrophe()
    {
        var beforeApostrophe = TestValues.NewKeyword();
        var afterApostrophe = TestValues.NewKeyword();
        var apostrophedName = $"{beforeApostrophe}'{afterApostrophe}";

        var condition = SearchService.BuildContainsCondition($"{apostrophedName}!", out var terms);

        Assert.Equal($"\"{apostrophedName}*\"", condition);
        Assert.Equal([apostrophedName], terms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_RelevanceSortWithKeyword_UsesRankOrdering()
    {
        var searchKeyword = TestValues.NewKeyword();
        var query = QueryWith(q: searchKeyword, sort: SearchService.SortByRelevance);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("CASE WHEN c.[CanonicalName] = @ExactQ THEN 0", sql, StringComparison.Ordinal);
        Assert.Contains("ft.[RANK] DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_RelevanceSortWithoutUsableKeyword_FallsBackToName()
    {
        var punctuationOnlyQuery = "!!!";
        var query = QueryWith(q: punctuationOnlyQuery, sort: SearchService.SortByRelevance);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.DoesNotContain("ft.[RANK]", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_NameSort_AlwaysAlphabetical_EvenWithKeywordAndGeo()
    {
        var searchKeyword = TestValues.NewKeyword();
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var query = QueryWith(
            q: searchKeyword, lat: searchLatitude, lng: searchLongitude, sort: SearchService.SortByName);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("fn_HaversineDistance) ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ft.[RANK]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DistanceSort_UsesHaversineWhenGeoPresent()
    {
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var query = QueryWith(
            lat: searchLatitude, lng: searchLongitude, sort: SearchService.SortByDistance);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY [dbo].[fn_HaversineDistance]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DistanceSortWithoutGeo_FallsBackToName()
    {
        var query = QueryWith(sort: SearchService.SortByDistance);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DefaultSort_NoSortParam_PrefersRelevanceWhenKeywordPresent()
    {
        var searchKeyword = TestValues.NewKeyword();
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var query = QueryWith(q: searchKeyword, lat: searchLatitude, lng: searchLongitude);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ft.[RANK] DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DefaultSort_NoKeywordButGeo_UsesDistance()
    {
        var searchLatitude = TestValues.NewLatitude();
        var searchLongitude = TestValues.NewLongitude();
        var query = QueryWith(lat: searchLatitude, lng: searchLongitude);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY [dbo].[fn_HaversineDistance]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DefaultSort_NoKeywordNoGeo_UsesName()
    {
        var query = QueryWith();

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_RelevanceSort_BindsExactAndPrefixParams()
    {
        var cmd = new FakeDbCommand();
        var searchKeyword = TestValues.NewKeyword();
        var query = QueryWith(q: searchKeyword, sort: SearchService.SortByRelevance);

        SearchService.BindParams(cmd, query);

        Assert.True(cmd.Parameters.Contains("@ExactQ"));
        Assert.True(cmd.Parameters.Contains("@PrefixQ"));
        Assert.Equal(searchKeyword, cmd.Parameters["@ExactQ"].Value);
        Assert.Equal(searchKeyword + "%", cmd.Parameters["@PrefixQ"].Value);
        Assert.Equal($"\"{searchKeyword}*\"", cmd.Parameters["@Q"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_NonRelevanceSort_DoesNotBindExactOrPrefixParams()
    {
        var cmd = new FakeDbCommand();
        var searchKeyword = TestValues.NewKeyword();
        var query = QueryWith(q: searchKeyword, sort: SearchService.SortByName);

        SearchService.BindParams(cmd, query);

        Assert.False(cmd.Parameters.Contains("@ExactQ"));
        Assert.False(cmd.Parameters.Contains("@PrefixQ"));
    }

    private static SearchQuery QueryWith(
        string? q = null,
        double? lat = null,
        double? lng = null,
        double? radiusMiles = null,
        string? state = null,
        Guid? denominationId = null,
        WorshipStyle? worshipStyle = null,
        bool? wheelchairAccessible = null,
        int? dayOfWeek = null,
        TimeOnly? startTimeBefore = null,
        TimeOnly? startTimeAfter = null,
        string? sort = null)
    {
        var requestedPage = TestValues.NewPage();
        var requestedPageSize = TestValues.NewPageSize();
        return new SearchQuery(
            q,
            lat,
            lng,
            radiusMiles,
            state,
            denominationId,
            worshipStyle,
            wheelchairAccessible,
            dayOfWeek,
            startTimeBefore,
            startTimeAfter,
            requestedPage,
            requestedPageSize,
            sort);
    }

    private static FakeDbConnection BuildConn(out FakeDbCommand cmd)
    {
        var conn = new FakeDbConnection();
        cmd = FakeDbCommand.WithReader(new DataTable());
        conn.Enqueue(cmd);
        return conn;
    }

    private static DataTable BuildSearchTable()
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
        t.Columns.Add("DistanceMiles", typeof(double));
        t.Columns.Add("TotalCount", typeof(int));
        return t;
    }

    private static object[] SearchRowPopulated(string street, object distanceMiles, int totalCount)
    {
        var churchId = Guid.NewGuid();
        var canonicalName = TestValues.NewName();
        var slug = TestValues.NewSlug();
        var latitude = TestValues.NewLatitude();
        var longitude = TestValues.NewLongitude();
        var city = TestValues.NewCity();
        var state = TestValues.NewStateCode();
        var zip = TestValues.NewZip();
        var phoneNumber = TestValues.NewPhoneNumber();
        var website = TestValues.NewWebsite();
        var emailAddress = TestValues.NewEmailAddress();
        var denominationId = Guid.NewGuid();
        var worshipStyle = TestValues.NewWorshipStyle();
        var primaryLanguage = TestValues.NewLanguage();
        var confidenceScore = TestValues.NewConfidenceScore();
        var lastVerifiedAt = TestValues.NewUtcTimestamp();
        var createdAt = TestValues.NewUtcTimestamp();
        var updatedAt = TestValues.NewUtcTimestamp();
        return
        [
            churchId, canonicalName, slug, latitude, longitude, street,
            city, state, zip, phoneNumber, website, emailAddress,
            denominationId, (int)worshipStyle, primaryLanguage, true, true, true, true, confidenceScore,
            lastVerifiedAt, createdAt, updatedAt, true, distanceMiles, totalCount,
        ];
    }

    private static object[] SearchRowNullable(int totalCount)
    {
        var churchId = Guid.NewGuid();
        var canonicalName = TestValues.NewName();
        var slug = TestValues.NewSlug();
        var latitude = TestValues.NewLatitude();
        var longitude = TestValues.NewLongitude();
        var city = TestValues.NewCity();
        var state = TestValues.NewStateCode();
        var zip = TestValues.NewZip();
        var worshipStyle = TestValues.NewWorshipStyle();
        var primaryLanguage = TestValues.NewLanguage();
        var confidenceScore = TestValues.NewConfidenceScore();
        var createdAt = TestValues.NewUtcTimestamp();
        var updatedAt = TestValues.NewUtcTimestamp();
        return
        [
            churchId, canonicalName, slug, latitude, longitude, DBNull.Value,
            city, state, zip, DBNull.Value, DBNull.Value, DBNull.Value,
            DBNull.Value, (int)worshipStyle, primaryLanguage, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, confidenceScore,
            DBNull.Value, createdAt, updatedAt, true, DBNull.Value, totalCount,
        ];
    }
}
