namespace Directory.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Directory.Church;
using Directory.Entities;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class ChurchEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ChurchEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetChurches_ReturnsOk()
    {
        var response = await _client.GetAsync("/churches?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetChurches_ClampsPagination_WhenOutOfRange()
    {
        var response = await _client.GetAsync("/churches?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateChurch_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync(ChurchEndpoints.Route, TestRequests.NewChurch(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task GetChurchBySlug_ReturnsOk_WhenFound()
    {
        var slug = await CreateChurchAndGetSlugAsync();

        var response = await _client.GetAsync($"{ChurchEndpoints.Route}/{slug}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetChurchBySlug_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.GetAsync($"/churches/no-such-slug-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChurch_ReturnsOk_WhenFound()
    {
        var id = await CreateChurchAndGetIdAsync();

        var response = await _client.PutAsJsonAsync($"{ChurchEndpoints.Route}/{id}", TestRequests.NewChurch(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateChurch_ReturnsNotFound_WhenMissing()
    {
        var missingChurchId = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync($"{ChurchEndpoints.Route}/{missingChurchId}", TestRequests.NewChurch(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchChurch_ReturnsOk_WhenFound()
    {
        var id = await CreateChurchAndGetIdAsync();
        var patchedName = Generated.NewName();
        using var content = JsonContent.Create(new { CanonicalName = patchedName });

        var response = await _client.PatchAsync($"{ChurchEndpoints.Route}/{id}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(patchedName, body.GetProperty(ChurchJsonNames.CanonicalName).GetString());
    }

    [Fact]
    public async Task PatchChurch_ExplicitNull_ClearsTheColumn()
    {
        var id = await CreateChurchAndGetIdAsync();
        using var populate = JsonContent.Create(new { PhoneNumber = Generated.NewPhoneNumber() });
        var populated = await _client.PatchAsync($"{ChurchEndpoints.Route}/{id}", populate, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, populated.StatusCode);
        var beforeClearing = await populated.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.String, beforeClearing.GetProperty(ChurchJsonNames.PhoneNumber).ValueKind);

        using var clear = JsonContent.Create(new { PhoneNumber = (string?)null });

        var response = await _client.PatchAsync($"{ChurchEndpoints.Route}/{id}", clear, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, body.GetProperty(ChurchJsonNames.PhoneNumber).ValueKind);
    }

    [Fact]
    public async Task PatchChurch_AbsentMember_LeavesTheStoredValueAlone()
    {
        var id = await CreateChurchAndGetIdAsync();
        var phoneNumber = Generated.NewPhoneNumber();
        using var populate = JsonContent.Create(new { PhoneNumber = phoneNumber });
        await _client.PatchAsync($"{ChurchEndpoints.Route}/{id}", populate, TestContext.Current.CancellationToken);

        using var unrelated = JsonContent.Create(new { CanonicalName = Generated.NewName() });

        var response = await _client.PatchAsync($"{ChurchEndpoints.Route}/{id}", unrelated, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(phoneNumber, body.GetProperty(ChurchJsonNames.PhoneNumber).GetString());
    }

    [Fact]
    public async Task PatchChurch_ReturnsNotFound_WhenMissing()
    {
        var missingChurchId = Guid.NewGuid();
        using var content = JsonContent.Create(new { CanonicalName = Generated.NewName() });

        var response = await _client.PatchAsync($"{ChurchEndpoints.Route}/{missingChurchId}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteChurch_ReturnsNoContent_WhenFound()
    {
        var id = await CreateChurchAndGetIdAsync();

        var response = await _client.DeleteAsync($"{ChurchEndpoints.Route}/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteChurch_ReturnsNotFound_WhenMissing()
    {
        var missingChurchId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"{ChurchEndpoints.Route}/{missingChurchId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateChurchAndGetIdAsync()
    {
        var response = await _client.PostAsJsonAsync(ChurchEndpoints.Route, TestRequests.NewChurch(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<string> CreateChurchAndGetSlugAsync()
    {
        var response = await _client.PostAsJsonAsync(ChurchEndpoints.Route, TestRequests.NewChurch(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var slug = body.GetProperty("slug").GetString();
        Assert.NotNull(slug);
        return slug;
    }
}
