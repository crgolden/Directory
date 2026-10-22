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
    private const double InsideTheDefaultRadiusDegrees = 0.01;

    private const double OutsideTheDefaultRadiusDegrees = 2.0;

    private const int HalvedToStayOffThePoles = 2;

    private const int Nearest = 0;

    private const int SecondNearest = 1;

    private const int OnePerPage = 1;

    private const int Exact = 0;

    private const int Prefixed = 1;

    private const int Contained = 2;

    private readonly DirectoryWebApplicationFactory _factory;

    private readonly HttpClient _client;

    public SearchEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
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

        var items = await SearchAsync($"state={StateCode.AK}");

        Assert.All(items, item => Assert.Equal(nameof(StateCode.AK), item.GetProperty("church").GetProperty("state").GetString()));
        Assert.Contains(items, item => item.GetProperty("church").GetProperty("canonicalName").GetString() == inState);
        Assert.DoesNotContain(items, item => item.GetProperty("church").GetProperty("canonicalName").GetString() == outOfState);
    }

    [Fact]
    public async Task Search_WheelchairFilter_ReturnsAccessibleChurchesOnly()
    {
        var accessible = Generated.NewName();
        var inaccessible = Generated.NewName();
        await CreateChurchAsync(accessible, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: true);
        await CreateChurchAsync(inaccessible, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);

        var items = await SearchAsync("wheelchairAccessible=true");

        Assert.All(items, item => Assert.True(item.GetProperty("church").GetProperty("wheelchairAccessible").GetBoolean()));
        Assert.Contains(items, item => item.GetProperty("church").GetProperty("canonicalName").GetString() == accessible);
        Assert.DoesNotContain(items, item => item.GetProperty("church").GetProperty("canonicalName").GetString() == inaccessible);
    }

    [Fact]
    public async Task Search_SearchPoint_ReturnsChurchesInsideTheRadiusNearestFirst()
    {
        var latitude = Generated.NewLatitude() / HalvedToStayOffThePoles;
        var longitude = Generated.NewLongitude();
        var atThePoint = Generated.NewName();
        var nearby = Generated.NewName();
        var faraway = Generated.NewName();
        await CreateChurchAsync(nearby, Generated.NewStateCode(), latitude + InsideTheDefaultRadiusDegrees, longitude, wheelchairAccessible: false);
        await CreateChurchAsync(faraway, Generated.NewStateCode(), latitude + OutsideTheDefaultRadiusDegrees, longitude, wheelchairAccessible: false);
        await CreateChurchAsync(atThePoint, Generated.NewStateCode(), latitude, longitude, wheelchairAccessible: false);

        var items = await SearchAsync(string.Create(CultureInfo.InvariantCulture, $"lat={latitude}&lng={longitude}"));

        Assert.Equal(atThePoint, items[Nearest].GetProperty("church").GetProperty("canonicalName").GetString());
        Assert.Equal(nearby, items[SecondNearest].GetProperty("church").GetProperty("canonicalName").GetString());
        Assert.True(items[Nearest].GetProperty("distanceMiles").GetDouble() < items[SecondNearest].GetProperty("distanceMiles").GetDouble());
        Assert.DoesNotContain(items, item => item.GetProperty("church").GetProperty("canonicalName").GetString() == faraway);
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

        var items = await SearchAsync(string.Create(CultureInfo.InvariantCulture, $"dayOfWeek={dayOfWeek}"));

        Assert.Contains(items, item => item.GetProperty("church").GetProperty("canonicalName").GetString() == scheduled);
        Assert.DoesNotContain(items, item => item.GetProperty("church").GetProperty("canonicalName").GetString() == unscheduled);
    }

    [Fact]
    public async Task Search_TwoKeywords_RankExactThenPrefixThenContainedMatches()
    {
        var phrase = $"{Generated.NewKeyword()} {Generated.NewKeyword()}";
        var prefixed = $"{phrase} {Generated.NewName()}";
        var contained = $"{Generated.NewName()} {phrase} {phrase}";
        await CreateChurchAsync(contained, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        await CreateChurchAsync(prefixed, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        await CreateChurchAsync(phrase, Generated.NewStateCode(), Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        await _factory.WaitForFullTextIndexAsync(TestContext.Current.CancellationToken);

        var items = await SearchAsync($"q={Uri.EscapeDataString(phrase)}");

        Assert.Equal(phrase, items[Exact].GetProperty("church").GetProperty("canonicalName").GetString());
        Assert.Equal(prefixed, items[Prefixed].GetProperty("church").GetProperty("canonicalName").GetString());
        Assert.Equal(contained, items[Contained].GetProperty("church").GetProperty("canonicalName").GetString());
    }

    [Fact]
    public async Task Search_OnePerPage_ReturnsOneAndCountsEveryMatch()
    {
        await CreateChurchAsync(Generated.NewName(), StateCode.HI, Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);
        await CreateChurchAsync(Generated.NewName(), StateCode.HI, Generated.NewLatitude(), Generated.NewLongitude(), wheelchairAccessible: false);

        var body = await SearchBodyAsync($"state={StateCode.HI}&pageSize={OnePerPage}");

        Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.True(body.GetProperty("totalCount").GetInt32() > OnePerPage);
    }

    private async Task<JsonElement[]> SearchAsync(string filter) =>
        [.. (await SearchBodyAsync(filter)).GetProperty("items").EnumerateArray()];

    private Task<JsonElement> SearchBodyAsync(string filter) =>
        _client.GetFromJsonAsync<JsonElement>($"{SearchEndpoints.Route}?{filter}", TestContext.Current.CancellationToken);

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
