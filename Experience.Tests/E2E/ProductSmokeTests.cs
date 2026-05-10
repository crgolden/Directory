namespace Experience.Tests.E2E;

using Experience.Tests.Infrastructure;
using Microsoft.Playwright;

/// <summary>
/// Post-deploy smoke tests for the Products feature that exercise the real Products API
/// end-to-end — no Playwright route mocks are registered for <c>/products/api/odata/**</c>
/// when <c>SMOKE_BASE_URL</c> is set (see <see cref="PlaywrightFixture.IsSmoke"/>).
/// </summary>
[Collection(E2ECollection.Name)]
[Trait("Category", "Smoke")]
public sealed class ProductSmokeTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task Products_full_crud_lifecycle()
    {
        const string name = "Smoke Test Product";
        const string updatedName = "Smoke Test Product (Updated)";

        var (ctx, page) = await fixture.NewProductsPageAsync();
        await using (ctx)
        {
            // CREATE: fill form and submit; Angular must navigate to the detail page.
            await page.ClickAsync("a:has-text('+ New Product')");
            await page.WaitForURLAsync("**/products/new");
            await page.FillAsync("#name", name);
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync(url => url.Contains("/products/") && !url.Contains("/new"));

            var productId = page.Url.TrimEnd('/').Split('/').Last();

            // READ: detail page shows the created product.
            await Assertions.Expect(page.Locator("body").First).ToContainTextAsync(name);

            // UPDATE: rename the product, set a manualUrl, and confirm the detail page reflects both.
            await page.GotoAsync($"/products/{productId}/edit");
            await page.WaitForURLAsync($"**/products/{productId}/edit");
            var nameInput = page.Locator("#name");
            await nameInput.ClearAsync();
            await nameInput.FillAsync(updatedName);
            await page.FillAsync("#manualUrl", "https://example.com/smoke-manual.pdf");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync($"**/products/{productId}");
            await Assertions.Expect(page.Locator("body").First).ToContainTextAsync(updatedName);
            await Assertions.Expect(page.Locator("a.btn-primary[target='_blank']")).ToBeVisibleAsync();

            // LIST: product appears when searching the products list.
            await page.GotoAsync("/products");
            await page.WaitForSelectorAsync("h2:has-text('My Products')");
            await page.FillAsync("input[type='search']", updatedName);
            await Task.Delay(400, TestContext.Current.CancellationToken);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var row = page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = updatedName });
            await Assertions.Expect(row).ToHaveCountAsync(1);

            // DELETE: remove the product — serves as both assertion and cleanup.
            await row.Locator("button.btn-outline-danger").ClickAsync();
            await row.Locator("button.btn-danger").ClickAsync();
            await Assertions.Expect(row).ToHaveCountAsync(0);
        }
    }
}
