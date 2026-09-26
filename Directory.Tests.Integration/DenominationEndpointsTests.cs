namespace Directory.Tests.Integration;

using System.Net.Http.Json;
using System.Text.Json;
using Directory.Denomination;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class DenominationEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DenominationEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDenominations_ReturnsTheSeededTimestamps_NotTheTimeOfTheRequest()
    {
        var requestedAt = DateTimeOffset.UtcNow;

        var denominations = await _client.GetFromJsonAsync<JsonElement[]>(DenominationEndpoints.Route, TestContext.Current.CancellationToken);

        Assert.NotNull(denominations);
        Assert.NotEmpty(denominations);
        Assert.All(denominations, denomination => Assert.True(
            denomination.GetProperty("createdAt").GetDateTimeOffset() < requestedAt));
    }
}
