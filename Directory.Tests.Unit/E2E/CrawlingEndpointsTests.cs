namespace Directory.Tests.Unit.E2E;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TestSupport;

public sealed class CrawlingEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CrawlingEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetCrawlSources_ReturnsOk()
    {
        var response = await _client.GetAsync("/crawl-sources", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task CreateCrawlSource_ReturnsCreated()
    {
        var body = new { Url = $"https://test-{Guid.NewGuid():N}.example/sitemap.xml", ChurchId = (Guid?)null };

        var response = await _client.PostAsJsonAsync("/crawl-sources", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task DeleteCrawlSource_ReturnsNoContent_WhenFound()
    {
        var id = await CreateCrawlSourceAndGetIdAsync();

        var response = await _client.DeleteAsync($"/crawl-sources/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task DeleteCrawlSource_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.DeleteAsync($"/crawl-sources/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task TriggerScrape_ReturnsAccepted_WhenFound()
    {
        var id = await CreateCrawlSourceAndGetIdAsync();

        var response = await _client.PostAsync($"/crawl-sources/{id}/trigger", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task TriggerScrape_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.PostAsync($"/crawl-sources/{Guid.NewGuid()}/trigger", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateCrawlSourceAndGetIdAsync()
    {
        var body = new { Url = $"https://test-{Guid.NewGuid():N}.example/sitemap.xml", ChurchId = (Guid?)null };
        var response = await _client.PostAsJsonAsync("/crawl-sources", body, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return json.GetProperty("id").GetGuid();
    }
}
