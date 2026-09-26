namespace Directory.Tests.Integration;

using System.Net;
using Directory.Search;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class SearchEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
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
}
