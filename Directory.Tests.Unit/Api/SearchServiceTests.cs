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
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(lat: 33.4, lng: -112.0), TestContext.Current.CancellationToken);

        Assert.Contains("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_ExcludesDistanceColumn_WhenNoGeoFilter()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(), TestContext.Current.CancellationToken);

        Assert.Contains("CAST(NULL AS FLOAT)", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_IncludesContainsTableJoin_WhenKeywordProvided()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(q: "grace"), TestContext.Current.CancellationToken);

        Assert.Contains("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Contains("ft.[KEY] = c.[Id]", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_OmitsContainsTableJoin_WhenNoKeyword()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_OmitsContainsTableJoin_WhenKeywordIsJunkOnly()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(q: "!!! ---"), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_IncludesStateFilter_WhenStateProvided()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(state: "AZ"), TestContext.Current.CancellationToken);

        Assert.Contains("@State", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_IncludesWheelchairFilter_WhenFilterProvided()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(wheelchairAccessible: true), TestContext.Current.CancellationToken);

        Assert.Contains("@WheelchairAccessible", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_OrdersByDistance_WhenGeoFilterProvided()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(lat: 33.4, lng: -112.0), TestContext.Current.CancellationToken);

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
            MinimalQuery(), TestContext.Current.CancellationToken);

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DenominationIdSet_AddsFilter()
    {
        var query = new SearchQuery(null, null, null, null, null, Guid.NewGuid(), null, null, null, null, null, 1, 10);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("c.[DenominationId] = @DenominationId", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_WorshipStyleSet_AddsFilter()
    {
        var query = new SearchQuery(null, null, null, null, null, null, WorshipStyle.Contemporary, null, null, null, null, 1, 10);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("c.[WorshipStyle] = @WorshipStyle", sql, StringComparison.Ordinal);
    }

    // --- Schedule filter tests (Gap 5) ---
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DayOfWeekSet_AddsScheduleJoin()
    {
        // Arrange — only dayOfWeek provided (no time range)
        var query = new SearchQuery(null, null, null, null, null, null, null, null, 0, null, null, 1, 10);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert — EXISTS subquery with DayOfWeek filter
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
        var query = new SearchQuery(null, null, null, null, null, null, null, null, null, null, new TimeOnly(9, 0), 1, 10);

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
        var query = new SearchQuery(null, null, null, null, null, null, null, null, null, new TimeOnly(12, 0), null, 1, 10);

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
        // Arrange — Sunday (0) with time window 9:00–12:00
        var query = new SearchQuery(null, null, null, null, null, null, null, null, 0, new TimeOnly(12, 0), new TimeOnly(9, 0), 1, 10);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert — all three conditions present within the EXISTS clause
        Assert.Contains("ss.[DayOfWeek] = @DayOfWeek", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] >= @StartTimeAfter", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] <= @StartTimeBefore", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_NoScheduleFilters_OmitsScheduleJoin()
    {
        // Arrange
        var query = new SearchQuery(null, null, null, null, "AZ", null, null, null, null, null, null, 1, 10);

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert — no EXISTS subquery
        Assert.DoesNotContain("[ServiceSchedules]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_ScheduleFiltersSet_BindsAllThree()
    {
        // Arrange
        var cmd = new FakeDbCommand();
        var query = new SearchQuery(null, null, null, null, null, null, null, null, 0, new TimeOnly(12, 0), new TimeOnly(9, 0), 1, 10);

        // Act
        SearchService.BindParams(cmd, query);

        // Assert — all three schedule params bound; @StartTimeAfter > @StartTimeBefore as TimeSpan
        Assert.True(cmd.Parameters.Contains("@DayOfWeek"));
        Assert.True(cmd.Parameters.Contains("@StartTimeAfter"));
        Assert.True(cmd.Parameters.Contains("@StartTimeBefore"));
        Assert.Equal(new TimeSpan(9, 0, 0), cmd.Parameters["@StartTimeAfter"].Value);
        Assert.Equal(new TimeSpan(12, 0, 0), cmd.Parameters["@StartTimeBefore"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_AllFiltersSet_BindsProvidedRadiusAndOptionalParams()
    {
        var cmd = new FakeDbCommand();
        var query = new SearchQuery("grace", 33.4, -112.0, 50.0, "AZ", Guid.NewGuid(), WorshipStyle.Contemporary, true, null, null, null, 1, 10);

        SearchService.BindParams(cmd, query);

        Assert.Equal(50.0, cmd.Parameters["@RadiusMiles"].Value);
        Assert.True(cmd.Parameters.Contains("@DenominationId"));
        Assert.True(cmd.Parameters.Contains("@WorshipStyle"));
        Assert.True(cmd.Parameters.Contains("@WheelchairAccessible"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_GeoQueryRowWithDistance_MapsDistanceAndTotalCount()
    {
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowPopulated(12.5, totalCount: 7));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new SearchService(conn);
        var query = new SearchQuery(null, 33.4, -112.0, null, null, null, null, null, null, null, null, 1, 10);

        var (items, totalCount) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(7, totalCount);
        var result = Assert.Single(items);
        Assert.Equal(12.5, result.DistanceMiles);
        Assert.Equal("123 Main", result.Church.Street);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_GeoQueryRowWithNullDistance_LeavesDistanceNull()
    {
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowPopulated(DBNull.Value, totalCount: 1));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new SearchService(conn);
        var query = new SearchQuery(null, 33.4, -112.0, null, null, null, null, null, null, null, null, 1, 10);

        var (items, _) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(items).DistanceMiles);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_NoGeoQueryRowWithNullableNulls_MapsNullsAndNoDistance()
    {
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowNullable(totalCount: 3));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReader(table));
        var service = new SearchService(conn);
        var query = new SearchQuery(null, null, null, null, null, null, null, null, null, null, null, 1, 10);

        var (items, totalCount) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(3, totalCount);
        var result = Assert.Single(items);
        Assert.Null(result.DistanceMiles);
        Assert.Null(result.Church.Street);
        Assert.Null(result.Church.AcceptsLGBTQ);
    }

    // --- BuildContainsCondition tests ---
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_MultipleWords_BuildsAndOfPrefixTerms()
    {
        var condition = SearchService.BuildContainsCondition("University Lutheran", out var terms);

        Assert.Equal("\"University*\" AND \"Lutheran*\"", condition);
        Assert.Equal(["University", "Lutheran"], terms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_JunkOnlyInput_ReturnsNullAndNoTerms()
    {
        var condition = SearchService.BuildContainsCondition("!!! ---", out var terms);

        Assert.Null(condition);
        Assert.Empty(terms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_NullOrWhitespace_ReturnsNullAndNoTerms()
    {
        Assert.Null(SearchService.BuildContainsCondition(null, out var terms1));
        Assert.Empty(terms1);

        Assert.Null(SearchService.BuildContainsCondition("   ", out var terms2));
        Assert.Empty(terms2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainsCondition_StripsPunctuation_KeepsApostrophe()
    {
        var condition = SearchService.BuildContainsCondition("O'Brien!", out var terms);

        Assert.Equal("\"O'Brien*\"", condition);
        Assert.Equal(["O'Brien"], terms);
    }

    // --- ORDER BY / sort mode tests ---
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_RelevanceSortWithKeyword_UsesRankOrdering()
    {
        var query = new SearchQuery("grace", null, null, null, null, null, null, null, null, null, null, 1, 10, "relevance");

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("CASE WHEN c.[CanonicalName] = @ExactQ THEN 0", sql, StringComparison.Ordinal);
        Assert.Contains("ft.[RANK] DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_RelevanceSortWithoutUsableKeyword_FallsBackToName()
    {
        var query = new SearchQuery("!!!", null, null, null, null, null, null, null, null, null, null, 1, 10, "relevance");

        var sql = SearchService.BuildQuery(query, out _);

        Assert.DoesNotContain("ft.[RANK]", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_NameSort_AlwaysAlphabetical_EvenWithKeywordAndGeo()
    {
        var query = new SearchQuery("grace", 33.4, -112.0, null, null, null, null, null, null, null, null, 1, 10, "name");

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("fn_HaversineDistance) ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ft.[RANK]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DistanceSort_UsesHaversineWhenGeoPresent()
    {
        var query = new SearchQuery(null, 33.4, -112.0, null, null, null, null, null, null, null, null, 1, 10, "distance");

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY [dbo].[fn_HaversineDistance]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DistanceSortWithoutGeo_FallsBackToName()
    {
        var query = new SearchQuery(null, null, null, null, null, null, null, null, null, null, null, 1, 10, "distance");

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DefaultSort_NoSortParam_PrefersRelevanceWhenKeywordPresent()
    {
        var query = new SearchQuery("grace", 33.4, -112.0, null, null, null, null, null, null, null, null, 1, 10);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ft.[RANK] DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DefaultSort_NoKeywordButGeo_UsesDistance()
    {
        var query = new SearchQuery(null, 33.4, -112.0, null, null, null, null, null, null, null, null, 1, 10);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY [dbo].[fn_HaversineDistance]", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_DefaultSort_NoKeywordNoGeo_UsesName()
    {
        var query = new SearchQuery(null, null, null, null, null, null, null, null, null, null, null, 1, 10);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_RelevanceSort_BindsExactAndPrefixParams()
    {
        var cmd = new FakeDbCommand();
        var query = new SearchQuery("grace", null, null, null, null, null, null, null, null, null, null, 1, 10, "relevance");

        SearchService.BindParams(cmd, query);

        Assert.True(cmd.Parameters.Contains("@ExactQ"));
        Assert.True(cmd.Parameters.Contains("@PrefixQ"));
        Assert.Equal("grace", cmd.Parameters["@ExactQ"].Value);
        Assert.Equal("grace%", cmd.Parameters["@PrefixQ"].Value);
        Assert.Equal("\"grace*\"", cmd.Parameters["@Q"].Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_NonRelevanceSort_DoesNotBindExactOrPrefixParams()
    {
        var cmd = new FakeDbCommand();
        var query = new SearchQuery("grace", null, null, null, null, null, null, null, null, null, null, 1, 10, "name");

        SearchService.BindParams(cmd, query);

        Assert.False(cmd.Parameters.Contains("@ExactQ"));
        Assert.False(cmd.Parameters.Contains("@PrefixQ"));
    }

    private static SearchQuery MinimalQuery(
        string? q = null,
        double? lat = null,
        double? lng = null,
        string? state = null,
        bool? wheelchairAccessible = null) =>
        new SearchQuery(q, lat, lng, null, state, null, null, wheelchairAccessible, null, null, null, 1, 10);

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
        t.Columns.Add("LastVerifiedAt", typeof(DateTime));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        t.Columns.Add("IsActive", typeof(bool));
        t.Columns.Add("DistanceMiles", typeof(double));
        t.Columns.Add("TotalCount", typeof(int));
        return t;
    }

    private static object[] SearchRowPopulated(object distance, int totalCount) =>
    [
        Guid.NewGuid(), "Grace Church", "grace-church", 33.4, -112.0, "123 Main",
        "Phoenix", "AZ", "85001", "602-555-1212", "https://grace.example", "hi@grace.example",
        Guid.NewGuid(), 1, "English", true, true, true, true, 0.9m,
        DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true, distance, totalCount,
    ];

    private static object[] SearchRowNullable(int totalCount) =>
    [
        Guid.NewGuid(), "Grace Church", "grace-church", 33.4, -112.0, DBNull.Value,
        "Phoenix", "AZ", "85001", DBNull.Value, DBNull.Value, DBNull.Value,
        DBNull.Value, 1, "English", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 0.9m,
        DBNull.Value, DateTime.UtcNow, DateTime.UtcNow, true, DBNull.Value, totalCount,
    ];
}
