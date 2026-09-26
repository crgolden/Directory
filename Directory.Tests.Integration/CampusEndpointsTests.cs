namespace Directory.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Directory.Campuses;
using Directory.Church;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class CampusEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CampusEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateCampus_ReturnsCreated()
    {
        var churchId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{CampusEndpoints.Route}", TestRequests.NewCampus(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateCampus_ReturnsBadRequest_WhenTheStateIsNotAUspsCode()
    {
        var churchId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{CampusEndpoints.Route}",
            new
            {
                Name = Generated.NewName(),
                City = Generated.NewCity(),
                State = Generated.NewUnrecognizedStateCode(),
                Zip = Generated.NewZip(),
                Latitude = Generated.NewLatitude(),
                Longitude = Generated.NewLongitude(),
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCampus_ReturnsNoContent_WhenFound()
    {
        var campusId = await CreateCampusAndGetIdAsync();

        var response = await _client.PutAsJsonAsync(
            $"{CampusEndpoints.Route}/{campusId}", TestRequests.NewCampus(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCampus_ReturnsNotFound_WhenMissing()
    {
        var missingCampusId = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync(
            $"{CampusEndpoints.Route}/{missingCampusId}", TestRequests.NewCampus(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCampus_ReturnsNoContent_WhenFound()
    {
        var campusId = await CreateCampusAndGetIdAsync();

        var response = await _client.DeleteAsync($"{CampusEndpoints.Route}/{campusId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCampus_ReturnsNotFound_WhenMissing()
    {
        var missingCampusId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"{CampusEndpoints.Route}/{missingCampusId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateChurchAndGetIdAsync()
    {
        var response = await _client.PostAsJsonAsync(ChurchEndpoints.Route, TestRequests.NewChurch(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateCampusAndGetIdAsync()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{CampusEndpoints.Route}", TestRequests.NewCampus(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
