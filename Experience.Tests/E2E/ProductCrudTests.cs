namespace Experience.Tests.E2E;

using Experience.Tests.Infrastructure;
using Microsoft.Playwright;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class ProductCrudTests
{
    private readonly PlaywrightFixture _fixture;

    public ProductCrudTests(PlaywrightFixture fixture) => _fixture = fixture;

    // -------------------------------------------------------------------------
    // List
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Products_list_shows_seeded_products()
    {
        _fixture.ProductStore.Clear();
        _fixture.ProductStore.Create("LG OLED C3", 1299.99m, brand: "LG", category: "Electronics");
        _fixture.ProductStore.Create("Dyson V15", brand: "Dyson");

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            var rows = page.Locator("tbody tr");
            await Assertions.Expect(rows).ToHaveCountAsync(2);

            // The mock sorts by Name (alphabetical), so row order is not guaranteed.
            // Assert that the LG row exists anywhere in the table.
            await Assertions.Expect(rows.Filter(new LocatorFilterOptions { HasText = "LG OLED C3" })).ToHaveCountAsync(1);
        }
    }

    [Fact]
    public async Task Products_list_shows_empty_state_when_no_products()
    {
        _fixture.ProductStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
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
    public async Task Search_filters_products_by_name()
    {
        _fixture.ProductStore.Clear();
        _fixture.ProductStore.Create("LG OLED TV");
        _fixture.ProductStore.Create("Dyson Vacuum");

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            var searchInput = page.Locator("#product-search");
            await searchInput.FillAsync("dyson");

            // Wait for debounce + re-render
            await Task.Delay(400, TestContext.Current.CancellationToken);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var rows = page.Locator("tbody tr");
            await Assertions.Expect(rows).ToHaveCountAsync(1);
            await Assertions.Expect(rows.First).ToContainTextAsync("Dyson Vacuum");
        }
    }

    [Fact]
    public async Task Search_shows_no_match_message_when_term_has_no_results()
    {
        _fixture.ProductStore.Clear();
        _fixture.ProductStore.Create("LG OLED TV");

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            var searchInput = page.Locator("#product-search");
            await searchInput.FillAsync("zzznomatch");

            await Task.Delay(400, TestContext.Current.CancellationToken);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Assertions.Expect(page.Locator(".empty-state")).ToContainTextAsync("zzznomatch");
        }
    }

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Critical")]
    public async Task Create_product_navigates_to_detail_on_success()
    {
        _fixture.ProductStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.ClickAsync("#new-product-link");
            await page.WaitForURLAsync("**/products/new");

            await page.FillAsync("#name", "My Laptop");
            await page.FillAsync("#brand", "Dell");
            await page.FillAsync("#price", "999");

            await page.ClickAsync("#product-form-submit");

            // After successful create the component navigates to /products/:id
            await page.WaitForURLAsync(url => url.Contains("/products/") && !url.Contains("/new"));

            var pageText = await page.InnerTextAsync("body");
            Assert.Contains("My Laptop", pageText);
        }
    }

    // -------------------------------------------------------------------------
    // Edit
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Critical")]
    public async Task Edit_product_updates_name_and_returns_to_detail()
    {
        _fixture.ProductStore.Clear();
        var product = _fixture.ProductStore.Create("Original Name", brand: "ACME");

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync($"/products/{product.Id}/edit");
            await page.WaitForURLAsync($"**/products/{product.Id}/edit");

            // Clear the name field and type the new value
            var nameInput = page.Locator("#name");
            await nameInput.ClearAsync();
            await nameInput.FillAsync("Updated Name");

            await page.ClickAsync("#product-form-submit");

            // WaitForURLAsync (glob or lambda) waits for waitUntil:Load internally; SPA pushState never fires
            // a Load event, so it hangs. ToHaveURLAsync polls page.Url directly without waiting for navigation.
            await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($@"/products/{product.Id}$"));

            var pageText = await page.InnerTextAsync("body");
            Assert.Contains("Updated Name", pageText);
        }
    }

    // -------------------------------------------------------------------------
    // Delete
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Critical")]
    public async Task Delete_product_removes_it_from_the_list()
    {
        _fixture.ProductStore.Clear();
        _fixture.ProductStore.Create("Product To Delete");
        _fixture.ProductStore.Create("Product To Keep");

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            // Click Delete on the first row
            await page.ClickAsync("#delete-product-0");

            // Confirm the inline prompt
            await page.ClickAsync("#confirm-delete-product-0");

            // One row should remain
            var rows = page.Locator("tbody tr");
            await Assertions.Expect(rows).ToHaveCountAsync(1);
        }
    }

    // -------------------------------------------------------------------------
    // Not Found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Navigating_to_unknown_product_id_shows_not_found_page()
    {
        _fixture.ProductStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/products/00000000-0000-0000-0000-000000000000");

            await page.WaitForURLAsync("**/products/not-found");
            await Assertions.Expect(page.Locator("h2")).ToContainTextAsync("Product Not Found");
        }
    }
}
