namespace Directory.Tests.E2E;

using System.Net;
using TestSupport;

public sealed class SearchEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SearchEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Search_ReturnsOk()
    {
        var response = await _client.GetAsync("/search?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Search_ClampsPagination_WhenOutOfRange()
    {
        var response = await _client.GetAsync("/search?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
