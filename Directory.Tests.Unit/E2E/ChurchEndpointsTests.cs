namespace Directory.Tests.Unit.E2E;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Enums;
using TestSupport;

public sealed class ChurchEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ChurchEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetChurches_ReturnsOk()
    {
        var response = await _client.GetAsync("/churches?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetChurches_ClampsPagination_WhenOutOfRange()
    {
        var response = await _client.GetAsync("/churches?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task CreateChurch_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/churches", NewChurchRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetChurchBySlug_ReturnsOk_WhenFound()
    {
        var slug = await CreateChurchAndGetSlugAsync();

        var response = await _client.GetAsync($"/churches/{slug}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetChurchBySlug_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.GetAsync($"/churches/no-such-slug-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task UpdateChurch_ReturnsOk_WhenFound()
    {
        var id = await CreateChurchAndGetIdAsync();

        var response = await _client.PutAsJsonAsync($"/churches/{id}", NewChurchRequest("Updated"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task UpdateChurch_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.PutAsJsonAsync($"/churches/{Guid.NewGuid()}", NewChurchRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task PatchChurch_ReturnsOk_WhenFound()
    {
        var id = await CreateChurchAndGetIdAsync();
        var patch = new { CanonicalName = $"Patched Church {Guid.NewGuid():N}" };

        using var content = JsonContent.Create(patch);
        var response = await _client.PatchAsync($"/churches/{id}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task PatchChurch_ReturnsNotFound_WhenMissing()
    {
        var patch = new { CanonicalName = "Patched Name" };

        using var content = JsonContent.Create(patch);
        var response = await _client.PatchAsync($"/churches/{Guid.NewGuid()}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task DeleteChurch_ReturnsNoContent_WhenFound()
    {
        var id = await CreateChurchAndGetIdAsync();

        var response = await _client.DeleteAsync($"/churches/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task DeleteChurch_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.DeleteAsync($"/churches/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object NewChurchRequest(string nameSuffix = "")
    {
        var suffix = $"{Guid.NewGuid():N}";
        return new
        {
            CanonicalName = $"Test Church {suffix}{nameSuffix}",
            Latitude = 33.4484,
            Longitude = -112.0740,
            Street = (string?)null,
            City = "Phoenix",
            State = "AZ",
            Zip = "85001",
            PhoneNumber = (string?)null,
            Website = (string?)null,
            EmailAddress = (string?)null,
            DenominationId = (Guid?)null,
            WorshipStyle = (int)WorshipStyle.Traditional,
            PrimaryLanguage = "English",
            AcceptsLGBTQ = (bool?)null,
            WheelchairAccessible = (bool?)null,
            HasNursery = (bool?)null,
            HasYouthProgram = (bool?)null,
        };
    }

    private async Task<Guid> CreateChurchAndGetIdAsync()
    {
        var response = await _client.PostAsJsonAsync("/churches", NewChurchRequest(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<string> CreateChurchAndGetSlugAsync()
    {
        var response = await _client.PostAsJsonAsync("/churches", NewChurchRequest(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var slug = body.GetProperty("slug").GetString();
        Assert.NotNull(slug);
        return slug;
    }
}
