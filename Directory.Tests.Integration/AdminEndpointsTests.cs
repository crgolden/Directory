namespace Directory.Tests.Integration;

using System.Net.Http.Json;
using Directory.Admin;
using Directory.Church;
using Directory.Enums;
using Directory.Tests.Integration.TestSupport;
using Shared.Domain;

[Trait("Category", "Integration")]
public sealed class AdminEndpointsTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private const int CsvHeaderLineCount = 1;

    private readonly HttpClient _client;

    public AdminEndpointsTests(DirectoryWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExportCsv_OrdersRowsByStateBeforeName()
    {
        var laterStateChurch = Generated.NewName();
        var earlierStateChurch = Generated.NewName();
        await CreateChurchAsync(laterStateChurch, StateCode.WY);
        await CreateChurchAsync(earlierStateChurch, StateCode.AK);

        var csv = await _client.GetStringAsync($"{AdminEndpoints.Route}{AdminEndpoints.ExportSegment}", TestContext.Current.CancellationToken);

        var rows = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Skip(CsvHeaderLineCount);
        Assert.Collection(
            rows,
            first => Assert.Contains(earlierStateChurch, first, StringComparison.Ordinal),
            second => Assert.Contains(laterStateChurch, second, StringComparison.Ordinal));
    }

    private async Task CreateChurchAsync(string canonicalName, StateCode state)
    {
        var response = await _client.PostAsJsonAsync(
            ChurchEndpoints.Route,
            new
            {
                CanonicalName = canonicalName,
                Latitude = Generated.NewLatitude(),
                Longitude = Generated.NewLongitude(),
                City = Generated.NewCity(),
                State = state.ToString(),
                Zip = Generated.NewZip(),
                WorshipStyle = (int)Generated.NewDefinedValue<WorshipStyle>(),
                PrimaryLanguage = Generated.NewLanguage(),
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
