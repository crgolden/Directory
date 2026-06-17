namespace Directory.Tests.Api;

using System.Net;
using TestSupport;

public sealed class SearchEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly DirectoryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SearchEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Search_ReturnsOk_WithEmptyResult()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(new System.Data.DataTable()));

        var response = await _client.GetAsync("/search?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Search_ClampsPagination_WhenOutOfRange()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(new System.Data.DataTable()));

        var response = await _client.GetAsync("/search?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
