namespace Directory.Tests.Api;

using System.Data;
using System.Net;
using System.Net.Http.Json;
using TestSupport;

public sealed class CrawlingEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly DirectoryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CrawlingEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCrawlSources_ReturnsOk()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithReader(CrawlTable()));

        var response = await _client.GetAsync("/crawl-sources", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateCrawlSource_ReturnsCreated()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var body = new { Url = "https://grace.example/sitemap.xml", ChurchId = (Guid?)null };
        var response = await _client.PostAsJsonAsync("/crawl-sources", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteCrawlSource_ReturnsNoContent_WhenDeleted()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1));

        var response = await _client.DeleteAsync($"/crawl-sources/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteCrawlSource_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(0));

        var response = await _client.DeleteAsync($"/crawl-sources/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TriggerScrape_ReturnsAccepted_WhenFound()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithScalarResult("https://grace.example/sitemap.xml")); // URL lookup
        _factory.FakeDb.Enqueue(FakeDbCommand.WithNonQueryResult(1)); // status update

        var response = await _client.PostAsync($"/crawl-sources/{Guid.NewGuid()}/trigger", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TriggerScrape_ReturnsNotFound_WhenMissing()
    {
        _factory.FakeDb.Reset();
        _factory.FakeDb.Enqueue(FakeDbCommand.WithScalarResult(null)); // URL not found

        var response = await _client.PostAsync($"/crawl-sources/{Guid.NewGuid()}/trigger", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static DataTable CrawlTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("ChurchId", typeof(Guid));
        t.Columns.Add("Url", typeof(string));
        t.Columns.Add("LastCrawledAt", typeof(DateTime));
        t.Columns.Add("LastStatus", typeof(int));
        t.Columns.Add("CreatedAt", typeof(DateTime));
        t.Columns.Add("UpdatedAt", typeof(DateTime));
        return t;
    }
}
