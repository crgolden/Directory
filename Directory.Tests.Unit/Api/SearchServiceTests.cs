namespace Directory.Tests.Unit.Api;

using System.Data;
using Directory.Entities;
using Directory.Enums;
using Directory.Search;
using Directory.Tests.Unit.TestSupport;

[Trait("Category", "Unit")]
public sealed class SearchServiceTests
{
    [Fact]
    public async Task SearchAsync_IncludesDistanceColumn_WhenGeoFilterProvided()
    {
        // Arrange
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);
        var query = UnfilteredQuery() with { Lat = searchLatitude, Lng = searchLongitude };

        // Act
        await service.SearchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_ExcludesDistanceColumn_WhenNoGeoFilter()
    {
        // Arrange
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        // Act
        await service.SearchAsync(UnfilteredQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("CAST(NULL AS FLOAT)", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_IncludesContainsTableJoin_WhenKeywordProvided()
    {
        // Arrange
        var searchKeyword = Generated.NewKeyword();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        // Act
        await service.SearchAsync(UnfilteredQuery() with { Q = searchKeyword }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Contains("ft0.[KEY] = c.[Id]", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_OmitsContainsTableJoin_WhenNoKeyword()
    {
        // Arrange
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        // Act
        await service.SearchAsync(UnfilteredQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_OmitsContainsTableJoin_WhenKeywordIsJunkOnly()
    {
        // Arrange
        var punctuationOnlyQuery = Generated.NewPunctuationOnlyQuery();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);
        var query = UnfilteredQuery() with { Q = punctuationOnlyQuery };

        // Act
        await service.SearchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("CONTAINSTABLE", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_IncludesStateFilter_WhenStateProvided()
    {
        // Arrange
        var stateFilter = Generated.NewStateCodeText();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);
        var query = UnfilteredQuery() with { State = stateFilter };

        // Act
        await service.SearchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(SqlParameters.State, cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_IncludesWheelchairFilter_WhenFilterProvided()
    {
        // Arrange
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        // Act
        await service.SearchAsync(
            UnfilteredQuery() with { WheelchairAccessible = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(SqlParameters.WheelchairAccessible, cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_OrdersByDistance_WhenGeoFilterProvided()
    {
        // Arrange
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);
        var query = UnfilteredQuery() with { Lat = searchLatitude, Lng = searchLongitude };

        // Act
        await service.SearchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("ORDER BY", cmd.CapturedCommandText, StringComparison.Ordinal);
        Assert.Contains("fn_HaversineDistance", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyResult_WhenNoRows()
    {
        // Arrange
        var conn = BuildConn(out _);
        var service = new SearchService(conn);

        // Act
        var (items, totalCount) = await service.SearchAsync(
            UnfilteredQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    [Fact]
    public void BuildQuery_DenominationIdSet_AddsFilter()
    {
        var filteredDenominationId = Guid.NewGuid();
        var query = UnfilteredQuery() with { DenominationId = filteredDenominationId };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("c.[DenominationId] = @DenominationId", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_WorshipStyleSet_AddsFilter()
    {
        var filteredWorshipStyle = Generated.NewDefinedValue<WorshipStyle>();
        var query = UnfilteredQuery() with { WorshipStyle = filteredWorshipStyle };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("c.[WorshipStyle] = @WorshipStyle", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_DayOfWeekSet_AddsScheduleJoin()
    {
        // Arrange
        var filteredDayOfWeek = Generated.NewDayOfWeek();
        var query = UnfilteredQuery() with { DayOfWeek = filteredDayOfWeek };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("[ServiceSchedules]", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[DayOfWeek] = @DayOfWeek", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(SqlParameters.StartTimeAfter, sql, StringComparison.Ordinal);
        Assert.DoesNotContain(SqlParameters.StartTimeBefore, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_StartTimeAfterSet_AddsScheduleJoinWithTimeFilter()
    {
        // Arrange
        var earliestStartTime = Generated.NewTimeOfDay();
        var query = UnfilteredQuery() with { StartTimeAfter = earliestStartTime };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("[ServiceSchedules]", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] >= @StartTimeAfter", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(SqlParameters.DayOfWeek, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_StartTimeBeforeSet_AddsScheduleJoinWithTimeFilter()
    {
        // Arrange
        var latestStartTime = Generated.NewTimeOfDay();
        var query = UnfilteredQuery() with { StartTimeBefore = latestStartTime };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("[ServiceSchedules]", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] <= @StartTimeBefore", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(SqlParameters.StartTimeAfter, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_AllScheduleFiltersSet_AddsAllConditions()
    {
        // Arrange
        var filteredDayOfWeek = Generated.NewDayOfWeek();
        var latestStartTime = Generated.NewTimeOfDay();
        var earliestStartTime = Generated.NewTimeOfDay();
        var query = UnfilteredQuery() with
        {
            DayOfWeek = filteredDayOfWeek,
            StartTimeBefore = latestStartTime,
            StartTimeAfter = earliestStartTime,
        };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ss.[DayOfWeek] = @DayOfWeek", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] >= @StartTimeAfter", sql, StringComparison.Ordinal);
        Assert.Contains("ss.[StartTime] <= @StartTimeBefore", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_NoScheduleFilters_OmitsScheduleJoin()
    {
        // Arrange
        var stateFilter = Generated.NewStateCodeText();
        var query = UnfilteredQuery() with { State = stateFilter };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.DoesNotContain("[ServiceSchedules]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BindParams_ScheduleFiltersSet_BindsAllThree()
    {
        // Arrange
        var cmd = new FakeDbCommand();
        var filteredDayOfWeek = (int)Generated.NewDayOfWeek();
        var earliestStartTime = Generated.NewTimeOfDay();
        var latestStartTime = Generated.NewTimeOfDay();
        var query = UnfilteredQuery() with
        {
            DayOfWeek = filteredDayOfWeek,
            StartTimeBefore = latestStartTime,
            StartTimeAfter = earliestStartTime,
        };

        // Act
        SearchService.BindParams(cmd, query);

        // Assert
        Assert.True(cmd.Parameters.Contains(SqlParameters.DayOfWeek));
        Assert.True(cmd.Parameters.Contains(SqlParameters.StartTimeAfter));
        Assert.True(cmd.Parameters.Contains(SqlParameters.StartTimeBefore));
        Assert.Equal(filteredDayOfWeek, cmd.Parameters[SqlParameters.DayOfWeek].Value);
        Assert.Equal(earliestStartTime.ToTimeSpan(), cmd.Parameters[SqlParameters.StartTimeAfter].Value);
        Assert.Equal(latestStartTime.ToTimeSpan(), cmd.Parameters[SqlParameters.StartTimeBefore].Value);
    }

    [Fact]
    public void BindParams_AllFiltersSet_BindsProvidedRadiusAndOptionalParams()
    {
        var cmd = new FakeDbCommand();
        var searchKeyword = Generated.NewKeyword();
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var searchRadiusMiles = Generated.NewRadiusMiles();
        var stateFilter = Generated.NewStateCodeText();
        var filteredDenominationId = Guid.NewGuid();
        var filteredWorshipStyle = Generated.NewDefinedValue<WorshipStyle>();
        var query = UnfilteredQuery() with
        {
            Q = searchKeyword,
            Lat = searchLatitude,
            Lng = searchLongitude,
            RadiusMiles = searchRadiusMiles,
            State = stateFilter,
            DenominationId = filteredDenominationId,
            WorshipStyle = filteredWorshipStyle,
            WheelchairAccessible = true,
        };

        // Act
        SearchService.BindParams(cmd, query);

        // Assert
        Assert.Equal(searchRadiusMiles, cmd.Parameters[SqlParameters.RadiusMiles].Value);
        Assert.Equal(filteredDenominationId, cmd.Parameters[SqlParameters.DenominationId].Value);
        Assert.Equal((int)filteredWorshipStyle, cmd.Parameters[SqlParameters.WorshipStyle].Value);
        Assert.True(cmd.Parameters.Contains(SqlParameters.WheelchairAccessible));
        Assert.Equal(stateFilter, cmd.Parameters[SqlParameters.State].Value);
    }

    [Fact]
    public async Task SearchAsync_GeoQueryRowWithDistance_MapsDistanceAndTotalCount()
    {
        var expectedStreet = Generated.NewStreet();
        var expectedDistanceMiles = Generated.NewRadiusMiles();
        var expectedTotalCount = Generated.NewRowCount();
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowPopulated(expectedStreet, expectedDistanceMiles));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReaders(BuildCountTable(expectedTotalCount), table));
        var service = new SearchService(conn);
        var query = UnfilteredQuery() with { Lat = searchLatitude, Lng = searchLongitude };

        // Act
        var (items, totalCount) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedTotalCount, totalCount);
        var result = Assert.Single(items);
        Assert.Equal(expectedDistanceMiles, result.DistanceMiles);
        Assert.Equal(expectedStreet, result.Church.Street);
    }

    [Fact]
    public async Task SearchAsync_GeoQueryRowWithNullDistance_LeavesDistanceNull()
    {
        var rowStreet = Generated.NewStreet();
        var rowTotalCount = Generated.NewRowCount();
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowPopulated(rowStreet, DBNull.Value));
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReaders(BuildCountTable(rowTotalCount), table));
        var service = new SearchService(conn);
        var query = UnfilteredQuery() with { Lat = searchLatitude, Lng = searchLongitude };

        // Act
        var (items, _) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(Assert.Single(items).DistanceMiles);
    }

    [Fact]
    public async Task SearchAsync_NoGeoQueryRowWithNullableNulls_MapsNullsAndNoDistance()
    {
        var expectedTotalCount = Generated.NewRowCount();
        var table = BuildSearchTable();
        table.Rows.Add(SearchRowNullable());
        var conn = new FakeDbConnection();
        conn.Enqueue(FakeDbCommand.WithReaders(BuildCountTable(expectedTotalCount), table));
        var service = new SearchService(conn);
        var query = UnfilteredQuery();

        // Act
        var (items, totalCount) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedTotalCount, totalCount);
        var result = Assert.Single(items);
        Assert.Null(result.DistanceMiles);
        Assert.Null(result.Church.Street);
        Assert.Null(result.Church.AcceptsLGBTQ);
    }

    [Fact]
    public void BuildTermConditions_MultipleWords_BuildsOnePrefixTermEach()
    {
        var firstWord = Generated.NewKeyword();
        var secondWord = Generated.NewKeyword();

        // Act
        var conditions = SearchService.BuildTermConditions($"{firstWord} {secondWord}");

        // Assert
        Assert.Equal([$"\"{firstWord}*\"", $"\"{secondWord}*\""], conditions);
    }

    [Fact]
    public void BuildQuery_TwoWords_JoinsOneFullTextTablePerTermSoEitherColumnCanMatch()
    {
        // Arrange
        var nameWord = Generated.NewKeyword();
        var cityWord = Generated.NewKeyword();

        // Act
        var sql = SearchService.BuildQuery(UnfilteredQuery() with { Q = $"{nameWord} {cityWord}" }, out _);

        // Assert
        Assert.Contains("CONTAINSTABLE([dbo].[Churches], ([CanonicalName], [City]), @Q0) AS ft0", sql, StringComparison.Ordinal);
        Assert.Contains("CONTAINSTABLE([dbo].[Churches], ([CanonicalName], [City]), @Q1) AS ft1", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(" AND \"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTermConditions_HyphenatedWord_SplitsIntoTwoTerms()
    {
        // Arrange
        var beforeHyphen = Generated.NewKeyword();
        var afterHyphen = Generated.NewKeyword();

        // Act
        var conditions = SearchService.BuildTermConditions($"{beforeHyphen}-{afterHyphen}");

        // Assert
        Assert.Equal([$"\"{beforeHyphen}*\"", $"\"{afterHyphen}*\""], conditions);
    }

    [Fact]
    public void BuildTermConditions_JunkOnlyInput_ReturnsNoTerms()
    {
        // Arrange
        var punctuationOnlyQuery = Generated.NewPunctuationOnlyQuery();

        // Act
        var conditions = SearchService.BuildTermConditions(punctuationOnlyQuery);

        // Assert
        Assert.Empty(conditions);
    }

    [Fact]
    public void BuildTermConditions_Null_ReturnsNoTerms()
    {
        // Act
        var conditions = SearchService.BuildTermConditions(null);

        // Assert
        Assert.Empty(conditions);
    }

    [Fact]
    public void BuildTermConditions_Blank_ReturnsNoTerms()
    {
        // Arrange
        var blankQuery = Generated.NewBlank();

        // Act
        var conditions = SearchService.BuildTermConditions(blankQuery);

        // Assert
        Assert.Empty(conditions);
    }

    [Fact]
    public void BuildTermConditions_StripsPunctuation_KeepsApostrophe()
    {
        var beforeApostrophe = Generated.NewKeyword();
        var afterApostrophe = Generated.NewKeyword();
        var apostrophedName = $"{beforeApostrophe}'{afterApostrophe}";

        // Act
        var conditions = SearchService.BuildTermConditions($"{apostrophedName}!");

        // Assert
        Assert.Equal([$"\"{apostrophedName}*\""], conditions);
    }

    [Fact]
    public void BuildQuery_RelevanceSortWithKeyword_UsesRankOrdering()
    {
        // Arrange
        var searchKeyword = Generated.NewKeyword();
        var query = UnfilteredQuery() with { Q = searchKeyword, Sort = SearchService.SortByRelevance };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("CASE WHEN c.[CanonicalName] = @ExactQ THEN 0", sql, StringComparison.Ordinal);
        Assert.Contains("ft0.[RANK] DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_RelevanceSortWithoutUsableKeyword_FallsBackToName()
    {
        var punctuationOnlyQuery = Generated.NewPunctuationToken();
        var query = UnfilteredQuery() with { Q = punctuationOnlyQuery, Sort = SearchService.SortByRelevance };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.DoesNotContain("[RANK]", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_NameSort_AlwaysAlphabetical_EvenWithKeywordAndGeo()
    {
        // Arrange
        var searchKeyword = Generated.NewKeyword();
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var query = UnfilteredQuery() with
        {
            Q = searchKeyword,
            Lat = searchLatitude,
            Lng = searchLongitude,
            Sort = SearchService.SortByName,
        };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("fn_HaversineDistance) ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ft.[RANK]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_DistanceSort_UsesHaversineWhenGeoPresent()
    {
        // Arrange
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var query = UnfilteredQuery() with
        {
            Lat = searchLatitude,
            Lng = searchLongitude,
            Sort = SearchService.SortByDistance,
        };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ORDER BY [dbo].[fn_HaversineDistance]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_DistanceSortWithoutGeo_FallsBackToName()
    {
        var query = UnfilteredQuery() with { Sort = SearchService.SortByDistance };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_DefaultSort_NoSortParam_PrefersRelevanceWhenKeywordPresent()
    {
        // Arrange
        var searchKeyword = Generated.NewKeyword();
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var query = UnfilteredQuery() with { Q = searchKeyword, Lat = searchLatitude, Lng = searchLongitude };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ft0.[RANK] DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_DefaultSort_NoKeywordButGeo_UsesDistance()
    {
        // Arrange
        var searchLatitude = Generated.NewLatitude();
        var searchLongitude = Generated.NewLongitude();
        var query = UnfilteredQuery() with { Lat = searchLatitude, Lng = searchLongitude };

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ORDER BY [dbo].[fn_HaversineDistance]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuery_DefaultSort_NoKeywordNoGeo_UsesName()
    {
        var query = UnfilteredQuery();

        // Act
        var sql = SearchService.BuildQuery(query, out _);

        // Assert
        Assert.Contains("ORDER BY c.[CanonicalName] ASC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BindParams_RelevanceSort_BindsExactAndPrefixParams()
    {
        var cmd = new FakeDbCommand();
        var searchKeyword = Generated.NewKeyword();
        var query = UnfilteredQuery() with { Q = searchKeyword, Sort = SearchService.SortByRelevance };

        // Act
        SearchService.BindParams(cmd, query);

        // Assert
        Assert.True(cmd.Parameters.Contains(SqlParameters.ExactQ));
        Assert.True(cmd.Parameters.Contains(SqlParameters.PrefixQ));
        Assert.Equal(searchKeyword, cmd.Parameters[SqlParameters.ExactQ].Value);
        Assert.Equal(searchKeyword + "%", cmd.Parameters[SqlParameters.PrefixQ].Value);
        Assert.Equal($"\"{searchKeyword}*\"", cmd.Parameters["@Q0"].Value);
    }

    [Fact]
    public void BindParams_NonRelevanceSort_DoesNotBindExactOrPrefixParams()
    {
        var cmd = new FakeDbCommand();
        var searchKeyword = Generated.NewKeyword();
        var query = UnfilteredQuery() with { Q = searchKeyword, Sort = SearchService.SortByName };

        // Act
        SearchService.BindParams(cmd, query);

        // Assert
        Assert.False(cmd.Parameters.Contains(SqlParameters.ExactQ));
        Assert.False(cmd.Parameters.Contains(SqlParameters.PrefixQ));
    }

    [Fact]
    public void DescribeInvalidQuery_PunctuationOnlyQuery_IsRejectedRatherThanMatchingEverything()
    {
        // Arrange
        var query = UnfilteredQuery() with { Q = Generated.NewPunctuationOnlyQuery() };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.NotNull(invalid);
    }

    [Fact]
    public void DescribeInvalidQuery_BlankQuery_IsAcceptedBecauseBrowsingIsNotSearching()
    {
        // Arrange
        var query = UnfilteredQuery() with { Q = Generated.NewBlank() };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.Null(invalid);
    }

    [Fact]
    public void DescribeInvalidQuery_WorshipStyleOutsideTheEnum_IsRejected()
    {
        // Arrange
        var query = UnfilteredQuery() with { WorshipStyle = Generated.NewUndefinedValue<WorshipStyle>() };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.NotNull(invalid);
    }

    [Fact]
    public void DescribeInvalidQuery_UnknownStateCode_IsRejected()
    {
        // Arrange
        var unknownState = Generated.NewUnparseableStateCode();
        var query = UnfilteredQuery() with { State = unknownState };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.NotNull(invalid);
        Assert.Contains(unknownState, invalid, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeInvalidQuery_RadiusWithoutCoordinates_IsRejected()
    {
        // Arrange
        var query = UnfilteredQuery() with { RadiusMiles = Generated.NewRadiusMiles() + 1 };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.NotNull(invalid);
    }

    [Fact]
    public void DescribeInvalidQuery_NegativeRadius_IsRejected()
    {
        // Arrange
        var query = UnfilteredQuery() with
        {
            Lat = Generated.NewLatitude(),
            Lng = Generated.NewLongitude(),
            RadiusMiles = -Generated.NewRadiusMiles() - 1,
        };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.NotNull(invalid);
    }

    [Fact]
    public void DescribeInvalidQuery_LatitudeOffThePlanet_IsRejected()
    {
        // Arrange
        var query = UnfilteredQuery() with
        {
            Lat = Generated.NewLatitudeBeyondThePole(),
            Lng = Generated.NewLongitude(),
        };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.NotNull(invalid);
    }

    [Fact]
    public void DescribeInvalidQuery_StartTimeWindowInverted_IsRejected()
    {
        // Arrange
        var earlier = Generated.NewTimeOfDayBeforeTheLastHour();
        var later = earlier.AddHours(1);
        var query = UnfilteredQuery() with { StartTimeAfter = later, StartTimeBefore = earlier };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.NotNull(invalid);
    }

    [Fact]
    public void DescribeInvalidQuery_EveryFilterInRange_IsAccepted()
    {
        // Arrange
        var query = UnfilteredQuery() with
        {
            Q = Generated.NewKeyword(),
            Lat = Generated.NewLatitude(),
            Lng = Generated.NewLongitude(),
            RadiusMiles = Generated.NewRadiusMiles() + 1,
            State = Generated.NewStateCodeText(),
            WorshipStyle = Generated.NewDefinedValue<WorshipStyle>(),
            DayOfWeek = Generated.NewDayOfWeek(),
        };

        // Act
        var invalid = SearchService.DescribeInvalidQuery(query);

        // Assert
        Assert.Null(invalid);
    }

    private static SearchQuery UnfilteredQuery()
    {
        var requestedPage = Generated.NewPage();
        var requestedPageSize = Generated.NewPageSize();
        return new SearchQuery(
            Q: null,
            Lat: null,
            Lng: null,
            RadiusMiles: null,
            State: null,
            DenominationId: null,
            WorshipStyle: null,
            WheelchairAccessible: null,
            DayOfWeek: null,
            StartTimeBefore: null,
            StartTimeAfter: null,
            Page: requestedPage,
            PageSize: requestedPageSize);
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
        t.Columns.Add(nameof(Church.Id), typeof(Guid));
        t.Columns.Add(nameof(Church.CanonicalName), typeof(string));
        t.Columns.Add(nameof(Church.Slug), typeof(string));
        t.Columns.Add(nameof(Church.Latitude), typeof(double));
        t.Columns.Add(nameof(Church.Longitude), typeof(double));
        t.Columns.Add(nameof(Church.Street), typeof(string));
        t.Columns.Add(nameof(Church.City), typeof(string));
        t.Columns.Add(nameof(Church.State), typeof(string));
        t.Columns.Add(nameof(Church.Zip), typeof(string));
        t.Columns.Add(nameof(Church.PhoneNumber), typeof(string));
        t.Columns.Add(nameof(Church.Website), typeof(string));
        t.Columns.Add(nameof(Church.EmailAddress), typeof(string));
        t.Columns.Add(nameof(Church.DenominationId), typeof(Guid));
        t.Columns.Add(nameof(Church.WorshipStyle), typeof(int));
        t.Columns.Add(nameof(Church.PrimaryLanguage), typeof(string));
        t.Columns.Add(nameof(Church.AcceptsLGBTQ), typeof(bool));
        t.Columns.Add(nameof(Church.WheelchairAccessible), typeof(bool));
        t.Columns.Add(nameof(Church.HasNursery), typeof(bool));
        t.Columns.Add(nameof(Church.HasYouthProgram), typeof(bool));
        t.Columns.Add(nameof(Church.ConfidenceScore), typeof(decimal));
        t.Columns.Add(nameof(Church.LastVerifiedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Church.CreatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Church.UpdatedAt), typeof(DateTimeOffset));
        t.Columns.Add(nameof(Church.IsActive), typeof(bool));
        t.Columns.Add(nameof(SearchResult.DistanceMiles), typeof(double));
        return t;
    }

    private static DataTable BuildCountTable(int totalCount)
    {
        var t = new DataTable();
        t.Columns.Add(nameof(SearchPagedResult.TotalCount), typeof(int));
        t.Rows.Add(totalCount);
        return t;
    }

    private static object[] SearchRowPopulated(string street, object distanceMiles)
    {
        var churchId = Guid.NewGuid();
        var canonicalName = Generated.NewName();
        var slug = Generated.NewSlug();
        var latitude = Generated.NewLatitude();
        var longitude = Generated.NewLongitude();
        var city = Generated.NewCity();
        var state = Generated.NewStateCodeText();
        var zip = Generated.NewZip();
        var phoneNumber = Generated.NewPhoneNumber();
        var website = Generated.NewWebsite();
        var emailAddress = Generated.NewEmailAddress();
        var denominationId = Guid.NewGuid();
        var worshipStyle = Generated.NewDefinedValue<WorshipStyle>();
        var primaryLanguage = Generated.NewLanguage();
        var confidenceScore = Generated.NewConfidenceScore();
        var lastVerifiedAt = Generated.NewUtcTimestamp();
        var createdAt = Generated.NewUtcTimestamp();
        var updatedAt = Generated.NewUtcTimestamp();
        return
        [
            churchId, canonicalName, slug, latitude, longitude, street,
            city, state, zip, phoneNumber, website, emailAddress,
            denominationId, (int)worshipStyle, primaryLanguage, true, true, true, true, confidenceScore,
            lastVerifiedAt, createdAt, updatedAt, true, distanceMiles,
        ];
    }

    private static object[] SearchRowNullable()
    {
        var churchId = Guid.NewGuid();
        var canonicalName = Generated.NewName();
        var slug = Generated.NewSlug();
        var latitude = Generated.NewLatitude();
        var longitude = Generated.NewLongitude();
        var city = Generated.NewCity();
        var state = Generated.NewStateCodeText();
        var zip = Generated.NewZip();
        var worshipStyle = Generated.NewDefinedValue<WorshipStyle>();
        var primaryLanguage = Generated.NewLanguage();
        var confidenceScore = Generated.NewConfidenceScore();
        var createdAt = Generated.NewUtcTimestamp();
        var updatedAt = Generated.NewUtcTimestamp();
        return
        [
            churchId, canonicalName, slug, latitude, longitude, DBNull.Value,
            city, state, zip, DBNull.Value, DBNull.Value, DBNull.Value,
            DBNull.Value, (int)worshipStyle, primaryLanguage, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, confidenceScore,
            DBNull.Value, createdAt, updatedAt, true, DBNull.Value,
        ];
    }
}
