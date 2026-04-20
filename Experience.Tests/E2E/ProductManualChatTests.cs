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

    /// <summary>
    /// Layout regression guard. When the chat transcript grows taller than the panel,
    /// the message-list must scroll inside the panel instead of extending past its
    /// bottom edge. A missing <c>:host</c> flex style on <c>manual-chat.component</c>
    /// caused the whole transcript to spill below the visible panel with no way to
    /// scroll to the bottom — this test would fail in that broken state because
    /// <c>.message-list</c>'s rendered clientHeight would exceed the panel's clientHeight.
    /// </summary>
    [Fact]
    public async Task Message_list_scrolls_inside_panel_when_content_overflows()
    {
        _fixture.ProductStore.Clear();
        _fixture.ChatStore.Clear();

        var (ctx, page) = await _fixture.NewProductsPageAsync();
        await using (ctx)
        {
            await page.GotoAsync("/products/new");
            await page.WaitForURLAsync("**/products/new");

            await page.FillAsync("#name", "Scroll Test");
            await page.ClickAsync("button.manual-chat-toggle");
            await Assertions.Expect(page.Locator(".manual-chat-panel")).ToBeVisibleAsync();

            // Send several messages so the transcript definitely exceeds panel height
            // (each round produces a user bubble + 4-line assistant reply + URL chip).
            for (var i = 0; i < 6; i++)
            {
                await page.FillAsync(".manual-chat-panel textarea", $"message {i}");
                await page.ClickAsync(".manual-chat-panel button:has-text('Send')");
                var chips = page.Locator(".manual-chat-panel button.url-chip");
                await Assertions.Expect(chips).ToHaveCountAsync(i + 1, new LocatorAssertionsToHaveCountOptions
                {
                    Timeout = 10_000
                });
            }

            // Measure the panel and the inner scrollable message list. Using scalar
            // evaluate calls avoids any JSON deserialization ambiguity.
            var panelClientHeight = await page.EvaluateAsync<double>(
                "() => document.querySelector('.manual-chat-panel').clientHeight");
            var panelBottom = await page.EvaluateAsync<double>(
                "() => document.querySelector('.manual-chat-panel').getBoundingClientRect().bottom");
            var viewportHeight = await page.EvaluateAsync<double>("() => window.innerHeight");
            var listClientHeight = await page.EvaluateAsync<double>(
                "() => document.querySelector('.manual-chat-panel .message-list').clientHeight");
            var listScrollHeight = await page.EvaluateAsync<double>(
                "() => document.querySelector('.manual-chat-panel .message-list').scrollHeight");

            // The message list must fit INSIDE the panel (no overflow past the bottom).
            Assert.True(
                listClientHeight <= panelClientHeight,
                $"Message list ({listClientHeight}px) must fit within panel " +
                $"({panelClientHeight}px) — otherwise content spills past the panel's bottom edge.");

            // With 6 rounds of content, the list SHOULD have overflow (scrollable).
            Assert.True(
                listScrollHeight > listClientHeight,
                $"Expected message-list to be scrollable after 6 messages: scrollHeight " +
                $"({listScrollHeight}) should exceed clientHeight ({listClientHeight}).");

            // The panel's bottom edge must not extend past the viewport.
            Assert.True(
                panelBottom <= viewportHeight + 1,
                $"Panel bottom ({panelBottom}) must not exceed viewport height ({viewportHeight}).");
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
