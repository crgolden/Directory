namespace Directory.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Directory.Crawling;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class CrawlingEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CrawlingEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCrawlSources_ReturnsOk()
    {
        var response = await _client.GetAsync(CrawlingEndpoints.Route, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateCrawlSource_ReturnsCreated()
    {
        var body = new { Url = $"https://test-{Guid.NewGuid():N}.example/sitemap.xml", ChurchId = (Guid?)null };

        var response = await _client.PostAsJsonAsync(CrawlingEndpoints.Route, body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task DeleteCrawlSource_ReturnsNoContent_WhenFound()
    {
        var id = await CreateCrawlSourceAndGetIdAsync();

        var response = await _client.DeleteAsync($"{CrawlingEndpoints.Route}/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCrawlSource_ReturnsNotFound_WhenMissing()
    {
        var missingCrawlSourceId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"{CrawlingEndpoints.Route}/{missingCrawlSourceId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TriggerScrape_ReturnsAccepted_WhenFound()
    {
        var id = await CreateCrawlSourceAndGetIdAsync();

        var response = await _client.PostAsync($"{CrawlingEndpoints.Route}/{id}{CrawlingEndpoints.TriggerSegment}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task TriggerScrape_ReturnsNotFound_WhenMissing()
    {
        var missingCrawlSourceId = Guid.NewGuid();

        var response = await _client.PostAsync($"{CrawlingEndpoints.Route}/{missingCrawlSourceId}{CrawlingEndpoints.TriggerSegment}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateCrawlSourceAndGetIdAsync()
    {
        var body = new { Url = $"https://test-{Guid.NewGuid():N}.example/sitemap.xml", ChurchId = (Guid?)null };
        var response = await _client.PostAsJsonAsync(CrawlingEndpoints.Route, body, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return json.GetProperty("id").GetGuid();
    }
}
