namespace Experience.Tests.E2E;

using Experience.Tests.Infrastructure;
using Microsoft.Playwright;

/// <summary>
/// E2E tests for the embedded Manual Chat panel on the product create/edit form.
/// All Manuals API calls are intercepted by Playwright route mocks backed by
/// <see cref="InMemoryChatsStore"/> — no real Manuals service is contacted.
/// </summary>
[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class ProductManualChatTests
{
    private readonly PlaywrightFixture _fixture;

    public ProductManualChatTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Manual_chat_panel_toggle_is_visible_on_create_form()
    {
        _fixture.ProductStore.Clear();
        _fixture.ChatStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/products/new");
            await page.WaitForURLAsync("**/products/new");

            await Assertions.Expect(page.Locator("button.manual-chat-toggle")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator(".manual-chat-panel")).ToHaveCountAsync(0);
        }
    }

    [Fact]
    public async Task Manual_chat_panel_opens_and_closes()
    {
        _fixture.ProductStore.Clear();
        _fixture.ChatStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/products/new");
            await page.WaitForURLAsync("**/products/new");

            await page.ClickAsync("button.manual-chat-toggle");
            await Assertions.Expect(page.Locator(".manual-chat-panel")).ToBeVisibleAsync();

            await page.ClickAsync(".manual-chat-panel .btn-close");
            await Assertions.Expect(page.Locator(".manual-chat-panel")).ToHaveCountAsync(0);
            await Assertions.Expect(page.Locator("button.manual-chat-toggle")).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task Sending_message_streams_response_and_shows_url_chip()
    {
        _fixture.ProductStore.Clear();
        _fixture.ChatStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/products/new");
            await page.WaitForURLAsync("**/products/new");

            // Fill product name so productContext carries it.
            await page.FillAsync("#name", "Test Laptop");

            // Open the chat panel.
            await page.ClickAsync("button.manual-chat-toggle");
            await Assertions.Expect(page.Locator(".manual-chat-panel")).ToBeVisibleAsync();

            // Send a message.
            await page.FillAsync(".manual-chat-panel textarea", "Where is the manual?");
            await page.ClickAsync(".manual-chat-panel button:has-text('Send')");

            // The assistant reply (mocked) contains MockManualUrl; a "Use this URL" chip should render.
            var chip = page.Locator(".manual-chat-panel button.url-chip");
            await Assertions.Expect(chip).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
            {
                Timeout = 10_000
            });
            await Assertions.Expect(chip).ToHaveAttributeAsync("title", InMemoryChatsStore.MockManualUrl);
        }
    }

    [Fact]
    public async Task Clicking_url_chip_populates_manual_url_field()
    {
        _fixture.ProductStore.Clear();
        _fixture.ChatStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/products/new");
            await page.WaitForURLAsync("**/products/new");

            await page.FillAsync("#name", "Test Laptop");
            await page.ClickAsync("button.manual-chat-toggle");

            await page.FillAsync(".manual-chat-panel textarea", "Find the manual please");
            await page.ClickAsync(".manual-chat-panel button:has-text('Send')");

            var chip = page.Locator(".manual-chat-panel button.url-chip").First;
            await Assertions.Expect(chip).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
            {
                Timeout = 10_000
            });
            await chip.ClickAsync();

            await Assertions.Expect(page.Locator("#manualUrl")).ToHaveValueAsync(InMemoryChatsStore.MockManualUrl);
        }
    }

    [Fact]
    public async Task Submitting_form_after_chip_click_persists_manual_url_on_product()
    {
        _fixture.ProductStore.Clear();
        _fixture.ChatStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/products/new");
            await page.WaitForURLAsync("**/products/new");

            await page.FillAsync("#name", "Chip Persist Product");
            await page.ClickAsync("button.manual-chat-toggle");

            await page.FillAsync(".manual-chat-panel textarea", "Manual link?");
            await page.ClickAsync(".manual-chat-panel button:has-text('Send')");

            var chip = page.Locator(".manual-chat-panel button.url-chip").First;
            await Assertions.Expect(chip).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions
            {
                Timeout = 10_000
            });
            await chip.ClickAsync();

            await Assertions.Expect(page.Locator("#manualUrl")).ToHaveValueAsync(InMemoryChatsStore.MockManualUrl);

            await page.ClickAsync("button[type='submit']");

            // After successful create the form navigates to /products/:id
            await page.WaitForURLAsync(url => url.Contains("/products/") && !url.Contains("/new"));

            var created = _fixture.ProductStore.GetProducts(null)
                .FirstOrDefault(p => p.Name == "Chip Persist Product");
            Assert.NotNull(created);
            Assert.Equal(InMemoryChatsStore.MockManualUrl, created!.ManualUrl);
        }
    }
}
