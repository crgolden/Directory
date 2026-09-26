namespace Directory.Tests.Integration;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Directory.Church;
using Directory.Schedules;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class ScheduleEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private const byte FirstDayNumberPastSaturday = (byte)(DayOfWeek.Saturday + 1);

    private readonly HttpClient _client;

    public ScheduleEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateSchedule_ReturnsCreated()
    {
        var churchId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{ScheduleEndpoints.Route}", TestRequests.NewSchedule(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateSchedule_ReturnsBadRequest_WhenTheDayIsOutsideTheWeek()
    {
        var churchId = await CreateChurchAndGetIdAsync();

        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{ScheduleEndpoints.Route}",
            new
            {
                DayOfWeek = FirstDayNumberPastSaturday,
                StartTime = Generated.NewTimeOfDay().ToString("HH:mm", CultureInfo.InvariantCulture),
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSchedule_ReturnsNoContent_WhenFound()
    {
        var scheduleId = await CreateScheduleAndGetIdAsync();

        var response = await _client.PutAsJsonAsync(
            $"{ScheduleEndpoints.Route}/{scheduleId}", TestRequests.NewSchedule(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSchedule_ReturnsNotFound_WhenMissing()
    {
        var missingScheduleId = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync(
            $"{ScheduleEndpoints.Route}/{missingScheduleId}", TestRequests.NewSchedule(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSchedule_ReturnsNoContent_WhenFound()
    {
        var scheduleId = await CreateScheduleAndGetIdAsync();

        var response = await _client.DeleteAsync($"{ScheduleEndpoints.Route}/{scheduleId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSchedule_ReturnsNotFound_WhenMissing()
    {
        var missingScheduleId = Guid.NewGuid();

        var response = await _client.DeleteAsync(
            $"{ScheduleEndpoints.Route}/{missingScheduleId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateChurchAndGetIdAsync()
    {
        var response = await _client.PostAsJsonAsync(ChurchEndpoints.Route, TestRequests.NewChurch(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateScheduleAndGetIdAsync()
    {
        var churchId = await CreateChurchAndGetIdAsync();
        var response = await _client.PostAsJsonAsync(
            $"{ChurchEndpoints.Route}/{churchId}{ScheduleEndpoints.Route}", TestRequests.NewSchedule(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
