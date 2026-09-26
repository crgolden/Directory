namespace Directory.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Directory.Church;
using Directory.Ministries;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class MinistryEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MinistryEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateMinistry_ReturnsCreated()
    {
        var churchId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{MinistryEndpoints.Route}", TestRequests.NewMinistry(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateMinistry_ReturnsBadRequest_WhenTheNameIsBlank()
    {
        var churchId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{MinistryEndpoints.Route}",
            new { Name = Generated.NewBlank() },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMinistry_ReturnsNoContent_WhenFound()
    {
        var ministryId = await CreateMinistryAndGetIdAsync();

        var response = await _client.PutAsJsonAsync(
            $"{MinistryEndpoints.Route}/{ministryId}", TestRequests.NewMinistry(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMinistry_ReturnsNotFound_WhenMissing()
    {
        var missingMinistryId = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync(
            $"{MinistryEndpoints.Route}/{missingMinistryId}", TestRequests.NewMinistry(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMinistry_ReturnsNoContent_WhenFound()
    {
        var ministryId = await CreateMinistryAndGetIdAsync();

        var response = await _client.DeleteAsync($"{MinistryEndpoints.Route}/{ministryId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMinistry_ReturnsNotFound_WhenMissing()
    {
        var missingMinistryId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"{MinistryEndpoints.Route}/{missingMinistryId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateChurchAndGetIdAsync()
    {
        var response = await _client.PostAsJsonAsync(ChurchEndpoints.Route, TestRequests.NewChurch(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateMinistryAndGetIdAsync()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{MinistryEndpoints.Route}", TestRequests.NewMinistry(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
