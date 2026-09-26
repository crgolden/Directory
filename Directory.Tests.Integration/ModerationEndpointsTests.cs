namespace Directory.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Directory.Church;
using Directory.Entities;
using Directory.Moderation;
using Directory.Tests.Integration.TestSupport;
using Microsoft.Data.SqlClient;

[Trait("Category", "Integration")]
public sealed class ModerationEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private const string SeedCorrectionSql = """
        INSERT INTO [dbo].[UserCorrections]
            ([Id], [ChurchId], [UserId], [Field], [OldValue], [NewValue], [Status], [CreatedAt])
        VALUES
            (@Id, @ChurchId, @UserId, @Field, NULL, @NewValue, 0, @CreatedAt)
        """;

    private readonly DirectoryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ModerationEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCorrections_ReturnsOk()
    {
        var response = await _client.GetAsync("/corrections?page=1&pageSize=10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCorrections_ClampsPagination_WhenOutOfRange()
    {
        var response = await _client.GetAsync("/corrections?page=0&pageSize=200", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCorrectionById_ReturnsOk_WhenFound()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var correctionId = await SeedCorrectionAsync(churchId);

        var response = await _client.GetAsync($"{ModerationEndpoints.Route}/{correctionId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCorrectionById_ReturnsNotFound_WhenMissing()
    {
        var missingCorrectionId = Guid.NewGuid();

        var response = await _client.GetAsync($"{ModerationEndpoints.Route}/{missingCorrectionId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmitCorrection_ReturnsAccepted_WhenChurchExists()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var body = new { ChurchId = churchId, Field = nameof(Church.PhoneNumber), NewValue = Generated.NewPhoneNumber() };

        var response = await _client.PostAsJsonAsync(ModerationEndpoints.Route, body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task SubmitCorrection_ReturnsNotFound_WhenChurchMissing()
    {
        var missingChurchId = Guid.NewGuid();
        var body = new { ChurchId = missingChurchId, Field = nameof(Church.PhoneNumber), NewValue = Generated.NewPhoneNumber() };

        var response = await _client.PostAsJsonAsync(ModerationEndpoints.Route, body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApproveCorrection_ReturnsNoContent_WhenFound()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var correctionId = await SeedCorrectionAsync(churchId);

        var response = await _client.PatchAsync($"{ModerationEndpoints.Route}/{correctionId}{ModerationEndpoints.ApproveSegment}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ApproveCorrection_ReturnsNotFound_WhenMissing()
    {
        var missingCorrectionId = Guid.NewGuid();

        var response = await _client.PatchAsync($"{ModerationEndpoints.Route}/{missingCorrectionId}{ModerationEndpoints.ApproveSegment}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RejectCorrection_ReturnsNoContent_WhenFound()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var correctionId = await SeedCorrectionAsync(churchId);

        var response = await _client.PatchAsync($"{ModerationEndpoints.Route}/{correctionId}{ModerationEndpoints.RejectSegment}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RejectCorrection_ReturnsNotFound_WhenMissing()
    {
        var missingCorrectionId = Guid.NewGuid();

        var response = await _client.PatchAsync($"{ModerationEndpoints.Route}/{missingCorrectionId}{ModerationEndpoints.RejectSegment}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MergeChurches_ReturnsNoContent()
    {
        var survivingId = await CreateChurchAndGetIdAsync();
        var absorbedId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsync(
            $"{ChurchEndpoints.Route}/{survivingId}{ModerationEndpoints.MergeSegment}/{absorbedId}", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<Guid> CreateChurchAndGetIdAsync()
    {
        var response = await _client.PostAsJsonAsync(ChurchEndpoints.Route, TestRequests.NewChurch(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> SeedCorrectionAsync(Guid churchId)
    {
        var id = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        await using var conn = await _factory.OpenTestConnectionAsync(TestContext.Current.CancellationToken);
        await using var cmd = new SqlCommand(SeedCorrectionSql, conn);
        cmd.Parameters.AddWithValue(SqlParameters.Id, id);
        cmd.Parameters.AddWithValue(SqlParameters.ChurchId, churchId);
        cmd.Parameters.AddWithValue("@UserId", IntegrationAuthHandler.TestSub);
        cmd.Parameters.AddWithValue("@Field", nameof(Church.PhoneNumber));
        cmd.Parameters.AddWithValue(SqlParameters.NewValue, Generated.NewPhoneNumber());
        cmd.Parameters.AddWithValue(SqlParameters.CreatedAt, now);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return id;
    }
}
