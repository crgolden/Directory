namespace Experience.Tests.E2E;

using Experience.Tests.Infrastructure;
using Microsoft.Playwright;

/// <summary>
/// E2E tests for the public Catalog feature (/catalog/**).
/// All Catalog OData API calls are intercepted by Playwright route mocks backed by
/// <see cref="InMemoryCatalogStore"/> — no real Products service is contacted.
/// The catalog route is anonymous; no auth guard or /bff/user mock is set up.
/// </summary>
[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class CatalogTests
{
    private readonly PlaywrightFixture _fixture;

    public CatalogTests(PlaywrightFixture fixture) => _fixture = fixture;

    // -------------------------------------------------------------------------
    // List
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Catalog_shows_all_seeded_products()
    {
        _fixture.CatalogStore.Clear();
        _fixture.CatalogStore.Create("LG OLED TV", price: 1299.99m, brand: "LG", category: "Electronics");
        _fixture.CatalogStore.Create("Dyson V15", price: 499.99m, brand: "Dyson", category: "Home");

        var (ctx, page) = await _fixture.NewCatalogPageAsync();
        await using (ctx)
        {
            var rows = page.Locator("tbody tr");
            await Assertions.Expect(rows).ToHaveCountAsync(2);
            await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "LG OLED TV" })).ToHaveCountAsync(1);
        }
    }

    [Fact]
    public async Task Catalog_shows_empty_state_when_no_products()
    {
        _fixture.CatalogStore.Clear();

        var (ctx, page) = await _fixture.NewCatalogPageAsync();
        await using (ctx)
        {
            await Assertions.Expect(page.Locator(".empty-state")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator("tbody tr")).ToHaveCountAsync(0);
        }
    }

    // -------------------------------------------------------------------------
    // Search
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Catalog_search_filters_by_name()
    {
        _fixture.CatalogStore.Clear();
        _fixture.CatalogStore.Create("LG OLED TV");
        _fixture.CatalogStore.Create("Dyson Vacuum");

        var (ctx, page) = await _fixture.NewCatalogPageAsync();
        await using (ctx)
        {
            var searchInput = page.Locator("input[type='search']");
            await searchInput.FillAsync("dyson");

            await Task.Delay(400, TestContext.Current.CancellationToken);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var rows = page.Locator("tbody tr");
            await Assertions.Expect(rows).ToHaveCountAsync(1);
            await Assertions.Expect(rows.First).ToContainTextAsync("Dyson Vacuum");
        }
    }

    [Fact]
    public async Task Catalog_search_shows_no_match_message_when_no_results()
    {
        _fixture.CatalogStore.Clear();
        _fixture.CatalogStore.Create("LG OLED TV");

        var (ctx, page) = await _fixture.NewCatalogPageAsync();
        await using (ctx)
        {
            var searchInput = page.Locator("input[type='search']");
            await searchInput.FillAsync("zzznomatch");

            await Task.Delay(400, TestContext.Current.CancellationToken);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Assertions.Expect(page.Locator(".empty-state")).ToContainTextAsync("zzznomatch");
        }
    }

    // -------------------------------------------------------------------------
    // Sorting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Catalog_sorts_by_name_descending_when_Name_header_clicked()
    {
        _fixture.CatalogStore.Clear();
        _fixture.CatalogStore.Create("Zebra Printer", brand: "Zebra");
        _fixture.CatalogStore.Create("Apple TV", brand: "Apple");

        var (ctx, page) = await _fixture.NewCatalogPageAsync();
        await using (ctx)
        {
            var rows = page.Locator("tbody tr");

            // Default: Name asc — Apple TV first
            await Assertions.Expect(rows.First).ToContainTextAsync("Apple TV");

            // Click Name header → toggles to desc
            await page.ClickAsync("thead button:has-text('Name')");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Assertions.Expect(rows.First).ToContainTextAsync("Zebra Printer");
        }
    }

    // -------------------------------------------------------------------------
    // Detail navigation
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Critical")]
    public async Task Catalog_navigates_to_detail_page_when_View_clicked()
    {
        _fixture.CatalogStore.Clear();
        var product = _fixture.CatalogStore.Create("Sony OLED TV", brand: "Sony");

        var (ctx, page) = await _fixture.NewCatalogPageAsync();
        await using (ctx)
        {
            await page.ClickAsync("tbody tr:first-child a:has-text('View')");

            // Use a DOM-presence assertion rather than WaitForURLAsync.
            //
            // WaitForURLAsync listens for a future navigation event. In CI, the resolver
            // mock returns in ~4ms, so history.pushState can fire before WaitForURLAsync
            // registers its listener — causing a 60-second timeout waiting for a navigation
            // that already happened. ToBeVisibleAsync polls the DOM on every retry tick,
            // resolving as soon as the product heading appears regardless of when pushState
            // fired. The URL is then verified after content is confirmed, by which point the
            // navigation has unambiguously committed.
            await Assertions.Expect(
                page.Locator($"h2:has-text('{product.Name}')")
            ).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });

            Assert.Contains($"/catalog/{product.Id}", page.Url);
        }
    }

    [Fact]
    public async Task Catalog_detail_shows_not_found_for_unknown_product_id()
    {
        _fixture.CatalogStore.Clear();

        var (ctx, page) = await _fixture.NewCatalogPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/catalog/00000000-0000-0000-0000-000000000000");

            await page.WaitForURLAsync("**/catalog/not-found");
            await Assertions.Expect(page.Locator("h2")).ToContainTextAsync("Product Not Found");
        }
    }
}
