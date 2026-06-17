namespace Directory.Tests.Api;

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
    public async Task SearchAsync_IncludesFreetextFilter_WhenKeywordProvided()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(q: "grace"), TestContext.Current.CancellationToken);

        Assert.Contains("FREETEXT", cmd.CapturedCommandText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_OmitsFreetextFilter_WhenNoKeyword()
    {
        var conn = BuildConn(out var cmd);
        var service = new SearchService(conn);

        await service.SearchAsync(MinimalQuery(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("FREETEXT", cmd.CapturedCommandText, StringComparison.Ordinal);
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
        var query = new SearchQuery(null, null, null, null, null, Guid.NewGuid(), null, null, 1, 10);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("c.[DenominationId] = @DenominationId", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildQuery_WorshipStyleSet_AddsFilter()
    {
        var query = new SearchQuery(null, null, null, null, null, null, WorshipStyle.Contemporary, null, 1, 10);

        var sql = SearchService.BuildQuery(query, out _);

        Assert.Contains("c.[WorshipStyle] = @WorshipStyle", sql, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BindParams_AllFiltersSet_BindsProvidedRadiusAndOptionalParams()
    {
        var cmd = new FakeDbCommand();
        var query = new SearchQuery("grace", 33.4, -112.0, 50.0, "AZ", Guid.NewGuid(), WorshipStyle.Contemporary, true, 1, 10);

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
        var query = new SearchQuery(null, 33.4, -112.0, null, null, null, null, null, 1, 10);

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
        var query = new SearchQuery(null, 33.4, -112.0, null, null, null, null, null, 1, 10);

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
        var query = new SearchQuery(null, null, null, null, null, null, null, null, 1, 10);

        var (items, totalCount) = await service.SearchAsync(query, TestContext.Current.CancellationToken);

        Assert.Equal(3, totalCount);
        var result = Assert.Single(items);
        Assert.Null(result.DistanceMiles);
        Assert.Null(result.Church.Street);
        Assert.Null(result.Church.AcceptsLGBTQ);
    }

    private static SearchQuery MinimalQuery(
        string? q = null,
        double? lat = null,
        double? lng = null,
        string? state = null,
        bool? wheelchairAccessible = null) =>
        new SearchQuery(q, lat, lng, null, state, null, null, wheelchairAccessible, 1, 10);

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
