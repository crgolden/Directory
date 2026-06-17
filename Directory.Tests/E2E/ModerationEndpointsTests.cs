namespace Directory.Tests.E2E;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Enums;
using Microsoft.Data.SqlClient;
using TestSupport;

public sealed class ModerationEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private readonly DirectoryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ModerationEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetCorrections_ReturnsOk()
    {
        var response = await _client.GetAsync("/corrections?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetCorrections_ClampsPagination_WhenOutOfRange()
    {
        var response = await _client.GetAsync("/corrections?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetCorrectionById_ReturnsOk_WhenFound()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var correctionId = await SeedCorrectionAsync(churchId);

        var response = await _client.GetAsync($"/corrections/{correctionId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task GetCorrectionById_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.GetAsync($"/corrections/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task SubmitCorrection_ReturnsAccepted_WhenChurchExists()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var body = new { ChurchId = churchId, Field = "PhoneNumber", OldValue = (string?)null, NewValue = "602-555-1212" };

        var response = await _client.PostAsJsonAsync("/corrections", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task SubmitCorrection_ReturnsNotFound_WhenChurchMissing()
    {
        var body = new { ChurchId = Guid.NewGuid(), Field = "PhoneNumber", OldValue = (string?)null, NewValue = "602-555-1212" };

        var response = await _client.PostAsJsonAsync("/corrections", body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task ApproveCorrection_ReturnsNoContent_WhenFound()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var correctionId = await SeedCorrectionAsync(churchId);

        var response = await _client.PatchAsync($"/corrections/{correctionId}/approve", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task ApproveCorrection_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.PatchAsync($"/corrections/{Guid.NewGuid()}/approve", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task RejectCorrection_ReturnsNoContent_WhenFound()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var correctionId = await SeedCorrectionAsync(churchId);

        var response = await _client.PatchAsync($"/corrections/{correctionId}/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task RejectCorrection_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.PatchAsync($"/corrections/{Guid.NewGuid()}/reject", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task MergeChurches_ReturnsNoContent()
    {
        var survivingId = await CreateChurchAndGetIdAsync();
        var absorbedId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsync(
            $"/churches/{survivingId}/merge/{absorbedId}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<Guid> CreateChurchAndGetIdAsync()
    {
        var req = new
        {
            CanonicalName = $"Test Church {Guid.NewGuid():N}",
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
        var response = await _client.PostAsJsonAsync("/churches", req, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> SeedCorrectionAsync(Guid churchId)
    {
        var id = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        var now = DateTime.UtcNow;
        await using var conn = await _factory.OpenTestConnectionAsync(TestContext.Current.CancellationToken);
        await using var cmd = new SqlCommand(
            """
            INSERT INTO [dbo].[UserCorrections]
                ([Id], [ChurchId], [UserId], [Field], [OldValue], [NewValue], [Status], [CreatedAt])
            VALUES
                (@Id, @ChurchId, @UserId, @Field, NULL, @NewValue, 0, @CreatedAt)
            """,
            conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@ChurchId", churchId);
        cmd.Parameters.AddWithValue("@UserId", IntegrationAuthHandler.TestSub);
        cmd.Parameters.AddWithValue("@Field", "PhoneNumber");
        cmd.Parameters.AddWithValue("@NewValue", "602-555-1212");
        cmd.Parameters.AddWithValue("@CreatedAt", now);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return id;
    }
}
