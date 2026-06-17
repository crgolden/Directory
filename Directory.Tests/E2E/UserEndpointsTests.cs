namespace Directory.Tests.E2E;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TestSupport;

public sealed class UserEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetMe_ReturnsAuthenticatedUser_WithClaims()
    {
        var response = await _client.GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(IntegrationAuthHandler.TestSub, body.GetProperty("sub").GetString());
        Assert.True(body.GetProperty("hasModerationScope").GetBoolean());
    }
}
