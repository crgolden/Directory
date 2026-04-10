namespace Experience.Tests.E2E;

using Experience.Tests.Infrastructure;
using Microsoft.Playwright;

/// <summary>
/// Browser-based E2E tests covering message sending and streaming.
/// </summary>
[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class ChatMessagingTests(PlaywrightFixture fixture)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CanSendMessageAndSeeResponse()
    {
        // Arrange
        fixture.ChatStore.Clear();
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Create a new chat.
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "+" }).ClickAsync();
            await Assertions.Expect(page.Locator(".chat-item").First).ToBeVisibleAsync();

            // Type a message and send via stream endpoint.
            const string input = "Hello from the E2E test";
            await page.GetByPlaceholder("Ask about a product manual…").FillAsync(input);
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Send" }).ClickAsync();

            // Assert: the assistant bubble appears with the mock response text.
            await Assertions.Expect(
                page.GetByText(InMemoryChatsStore.GetMockResponse())).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task AutoTitleSetsAfterFirstMessage()
    {
        // Arrange
        fixture.ChatStore.Clear();
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Create a chat.
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "+" }).ClickAsync();
            await Assertions.Expect(page.Locator(".chat-item").First).ToBeVisibleAsync();

            // Send a message with a short title (≤60 chars).
            const string input = "What manuals are available for my refrigerator?";
            await page.GetByPlaceholder("Ask about a product manual…").FillAsync(input);
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Send" }).ClickAsync();

            // Wait for streaming to complete (streaming indicator disappears).
            await Assertions.Expect(page.GetByText("Responding…")).ToBeHiddenAsync();

            // Assert: the sidebar item shows the input text as the title.
            await Assertions.Expect(page.Locator(".chat-item").GetByText(input)).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task StreamingResponseAppearsAfterSend()
    {
        // Arrange
        fixture.ChatStore.Clear();
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Create a chat and send a message via the stream endpoint.
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "+" }).ClickAsync();
            await Assertions.Expect(page.Locator(".chat-item").First).ToBeVisibleAsync();

            await page.GetByPlaceholder("Ask about a product manual…").FillAsync("Stream me something");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Send" }).ClickAsync();

            // Assert: the assistant bubble eventually shows the accumulated response.
            // (All deltas concatenated equal MockResponse.)
            await Assertions.Expect(
                page.GetByText(InMemoryChatsStore.GetMockResponse())).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task MessageHistoryReloadsOnResume()
    {
        // Arrange: pre-populate a chat with messages.
        fixture.ChatStore.Clear();
        var chat = fixture.ChatStore.CreateChat();
        fixture.ChatStore.CompleteMessage(chat.ChatId, "Previously asked question");
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Act: select the pre-existing chat from the sidebar.
            await Assertions.Expect(page.Locator(".chat-item").First).ToBeVisibleAsync();
            await page.Locator(".chat-item").First.ClickAsync();

            // Assert: the previously stored messages are displayed.
            // Scope to .message-list to avoid ambiguity with the sidebar chat label,
            // which also shows the message text as the chat title.
            await Assertions.Expect(page.Locator(".message-list").GetByText("Previously asked question")).ToBeVisibleAsync();
            await Assertions.Expect(
                page.Locator(".message-list").GetByText(InMemoryChatsStore.GetMockResponse())).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task MultipleChatsOrderedNewestFirst()
    {
        // Arrange: create 3 chats with distinct titles via pre-seeding.
        fixture.ChatStore.Clear();
        var chat1 = fixture.ChatStore.CreateChat();
        fixture.ChatStore.CompleteMessage(chat1.ChatId, "Alpha query");
        await Task.Delay(2, TestContext.Current.CancellationToken); // ensure different timestamps
        var chat2 = fixture.ChatStore.CreateChat();
        fixture.ChatStore.CompleteMessage(chat2.ChatId, "Beta query");
        await Task.Delay(2, TestContext.Current.CancellationToken);
        var chat3 = fixture.ChatStore.CreateChat();
        fixture.ChatStore.CompleteMessage(chat3.ChatId, "Gamma query");

        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Assert: sidebar shows 3 items; newest (Gamma) is first.
            var items = page.Locator(".chat-item");
            await Assertions.Expect(items).ToHaveCountAsync(3);
            await Assertions.Expect(items.First).ToContainTextAsync("Gamma");
            await Assertions.Expect(items.Last).ToContainTextAsync("Alpha");
        }
    }
}
