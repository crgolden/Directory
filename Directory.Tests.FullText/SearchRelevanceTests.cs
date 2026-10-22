namespace Directory.Tests.FullText;

using System.Net.Http.Json;
using System.Text.Json;
using Directory.Search;
using Directory.Tests.Integration.TestSupport;

[Trait("Category", "Integration")]
public sealed class SearchRelevanceTests : IClassFixture<DirectoryWebApplicationFactory>
{
    private const int Exact = 0;

    private const int Prefixed = 1;

    private const int Contained = 2;

    private readonly DirectoryWebApplicationFactory _factory;

    private readonly HttpClient _client;

    public SearchRelevanceTests(DirectoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_TwoKeywords_RankExactThenPrefixThenContainedMatches()
    {
        var firstKeyword = Generated.NewKeyword();
        var phrase = $"{firstKeyword} {Generated.NewKeyword()}";
        var prefixed = $"{phrase} {Generated.NewName()}";
        var contained = $"{Generated.NewName()} {phrase} {phrase}";
        Guid[] seededIds =
        [
            await FullTextSeeding.SeedChurchAsync(_factory, contained, TestContext.Current.CancellationToken),
            await FullTextSeeding.SeedChurchAsync(_factory, prefixed, TestContext.Current.CancellationToken),
            await FullTextSeeding.SeedChurchAsync(_factory, phrase, TestContext.Current.CancellationToken),
        ];
        await FullTextSeeding.WaitForKeywordIndexedAsync(_factory, firstKeyword, seededIds, TestContext.Current.CancellationToken);

        var body = await _client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Route}?q={Uri.EscapeDataString(phrase)}", TestContext.Current.CancellationToken);
        JsonElement[] items = [.. body.GetProperty("items").EnumerateArray()];

        Assert.Equal(phrase, items[Exact].GetProperty("church").GetProperty("canonicalName").GetString());
        Assert.Equal(prefixed, items[Prefixed].GetProperty("church").GetProperty("canonicalName").GetString());
        Assert.Equal(contained, items[Contained].GetProperty("church").GetProperty("canonicalName").GetString());
    }
}
