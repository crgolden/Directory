namespace Directory.Tests.Integration;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Directory.Church;
using Directory.Enums;
using Directory.Schedules;
using Directory.Search;
using Directory.Tests.Integration.TestSupport;
using Shared.Domain;

[Trait("Category", "Integration")]
public sealed class SearchEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private const int Nearest = 0;

    private const int SecondNearest = 1;

    private const int OnePerPage = 1;

    private readonly HttpClient _client;

    public SearchEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_ReturnsOk()
    {
        var response = await _client.GetAsync("/search?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_ClampsPagination_WhenOutOfRange()
    {
        var response = await _client.GetAsync("/search?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(SearchService.SortByRelevance)]
    [InlineData(SearchService.SortByName)]
    [InlineData(SearchService.SortByDistance)]
    public async Task Search_AcceptsSortParam(string sort)
    {
        var response = await _client.GetAsync(
            $"/search?q={Generated.NewKeyword()}&sort={sort}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_UnrecognizedSort_StillReturnsOk()
    {
        var unrecognizedSort = Generated.NewTokenOtherThan(SearchService.SortByRelevance, SearchService.SortByName, SearchService.SortByDistance);

        var response = await _client.GetAsync(
            $"/search?sort={unrecognizedSort}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_StateFilter_ReturnsThatStatesChurchesOnly()
    {
        var inState = Generated.NewName();
        var outOfState = Generated.NewName();
        await CreateChurchAsync(inState, StateCode.AK, Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        await CreateChurchAsync(outOfState, StateCode.WY, Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);

        var items = await SearchAsync($"{nameof(SearchRequest.State)}={StateCode.AK}");

        Assert.All(items, item => Assert.Equal(StateCode.AK, item.Church.State));
        Assert.Contains(inState, CanonicalNames(items));
        Assert.DoesNotContain(outOfState, CanonicalNames(items));
    }

    [Fact]
    public async Task Search_WheelchairFilter_ReturnsAccessibleChurchesOnly()
    {
        var accessible = Generated.NewName();
        var inaccessible = Generated.NewName();
        await CreateChurchAsync(accessible, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: true);
        await CreateChurchAsync(inaccessible, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);

        var items = await SearchAsync($"{nameof(SearchRequest.WheelchairAccessible)}={true}");

        Assert.All(items, item => Assert.True(item.Church.WheelchairAccessible));
        Assert.Contains(accessible, CanonicalNames(items));
        Assert.DoesNotContain(inaccessible, CanonicalNames(items));
    }

    [Fact]
    public async Task Search_SearchPoint_ReturnsChurchesInsideTheRadiusNearestFirst()
    {
        var latitude = Generated.NewLatitude();
        var longitude = Generated.NewLongitude();
        var radiusFraction = Generated.NewFractionAboveHalf();
        var atThePoint = Generated.NewName();
        var nearby = Generated.NewName();
        var faraway = Generated.NewName();
        await CreateChurchAsync(nearby, Generated.NewStateCode(), TowardTheEquator(latitude, SearchService.DefaultRadiusMiles * radiusFraction), longitude, wheelchairAccessible: false);
        await CreateChurchAsync(faraway, Generated.NewStateCode(), TowardTheEquator(latitude, SearchService.DefaultRadiusMiles / radiusFraction), longitude, wheelchairAccessible: false);
        await CreateChurchAsync(atThePoint, Generated.NewStateCode(), latitude, longitude, wheelchairAccessible: false);

        var items = await SearchAsync(string.Create(CultureInfo.InvariantCulture, $"{nameof(SearchRequest.Lat)}={latitude}&{nameof(SearchRequest.Lng)}={longitude}"));

        Assert.Equal(atThePoint, items[Nearest].Church.CanonicalName);
        Assert.Equal(nearby, items[SecondNearest].Church.CanonicalName);
        Assert.True(items[Nearest].DistanceMiles < items[SecondNearest].DistanceMiles);
        Assert.DoesNotContain(faraway, CanonicalNames(items));
    }

    [Fact]
    public async Task Search_DayOfWeekFilter_ReturnsChurchesHoldingAServiceThatDay()
    {
        var scheduled = Generated.NewName();
        var unscheduled = Generated.NewName();
        var scheduledId = await CreateChurchAsync(scheduled, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        await CreateChurchAsync(unscheduled, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        var dayOfWeek = Generated.NewDayOfWeek();
        var schedule = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{scheduledId}{ScheduleEndpoints.Route}",
            new
            {
                DayOfWeek = dayOfWeek,
                StartTime = Generated.NewTimeOfDay().ToString("HH:mm", CultureInfo.InvariantCulture),
                Description = Generated.NewDescription(),
            },
            TestContext.Current.CancellationToken);
        schedule.EnsureSuccessStatusCode();

        var items = await SearchAsync(string.Create(CultureInfo.InvariantCulture, $"{nameof(SearchRequest.DayOfWeek)}={dayOfWeek}"));

        Assert.Contains(scheduled, CanonicalNames(items));
        Assert.DoesNotContain(unscheduled, CanonicalNames(items));
    }

    [Fact]
    public async Task Search_OnePerPage_ReturnsOneAndCountsEveryMatch()
    {
        await CreateChurchAsync(Generated.NewName(), StateCode.HI, Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        await CreateChurchAsync(Generated.NewName(), StateCode.HI, Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);

        var body = await SearchBodyAsync($"{nameof(SearchRequest.State)}={StateCode.HI}&{nameof(SearchRequest.PageSize)}={OnePerPage}");

        Assert.Single(body.Items);
        Assert.True(body.TotalCount > OnePerPage);
    }

    private static List<string> CanonicalNames(IEnumerable<SearchResult> items) =>
        [.. items.Select(item => item.Church.CanonicalName)];

    private static double TowardTheEquator(double latitude, double miles) =>
        latitude - Math.CopySign(double.RadiansToDegrees(miles / HaversineConstants.EarthRadiusMiles), latitude);

    private async Task<IReadOnlyList<SearchResult>> SearchAsync(string filter) =>
        (await SearchBodyAsync(filter)).Items;

    private async Task<SearchPagedResult> SearchBodyAsync(string filter) =>
        await _client.GetFromJsonAsync<SearchPagedResult>($"{SearchEndpoints.Route}?{filter}", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException($"{SearchEndpoints.Route}?{filter} answered with no body.");

    private async Task<Guid> CreateChurchAsync(string canonicalName, StateCode state, double latitude, double longitude, bool wheelchairAccessible)
    {
        var response = await _client.PostAsJsonAsync(
            ChurchEndpoints.Route,
            new
            {
                CanonicalName = canonicalName,
                Latitude = latitude,
                Longitude = longitude,
                City = Generated.NewCity(),
                State = state.ToString(),
                Zip = Generated.NewZip(),
                WorshipStyle = (int)Generated.NewDefinedValue<WorshipStyle>(),
                PrimaryLanguage = Generated.NewLanguage(),
                WheelchairAccessible = wheelchairAccessible,
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return created.GetProperty("id").GetGuid();
    }
}
