namespace Experience.Tests.E2E;

using Experience.Tests.Infrastructure;
using Microsoft.Playwright;

/// <summary>
/// Browser-based E2E tests covering chat CRUD operations.
/// All tests in this class also carry the Smoke trait and run in the post-deploy smoke job.
/// </summary>
[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Category", "Smoke")]
public sealed class ChatCrudTests(PlaywrightFixture fixture)
{
    [Fact]
    public async Task CanLoadChatPage()
    {
        // Arrange: clean slate so no existing chats show.
        fixture.ChatStore.Clear();
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Assert: the sidebar label and "+" button are visible.
            await page.WaitForSelectorAsync("text=Chats");
            await Assertions.Expect(page.Locator("button:has-text('+')")).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task EmptyStateShowsNoChatMessage()
    {
        // Arrange
        fixture.ChatStore.Clear();
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Assert
            await Assertions.Expect(page.GetByText("No chats yet.")).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task CanCreateChat()
    {
        // Arrange
        fixture.ChatStore.Clear();
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Act: click "+" to create a new chat.
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "+" }).ClickAsync();

            // Assert: at least one .chat-item button appears in the sidebar.
            await Assertions.Expect(page.Locator(".chat-item").First).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task CanDeleteChat()
    {
        // Arrange: pre-create a chat so there is something to delete.
        fixture.ChatStore.Clear();
        fixture.ChatStore.CreateChat();
        var (ctx, page) = await fixture.NewPageAsync();
        await using (ctx)
        {
            // Wait for the chat item to render.
            await Assertions.Expect(page.Locator(".chat-item").First).ToBeVisibleAsync();

            // Act: select the chat then delete it via the mock route (DELETE is called
            // only if the component has a delete button; for now verify sidebar clears).
            // The Angular component does not expose a delete button in the current UI;
            // deletion is triggered programmatically. Route the mock DELETE manually.
            var chatId = fixture.ChatStore.GetChats()[0].ChatId;
            fixture.ChatStore.DeleteChat(chatId);

            // Reload the page so Angular re-fetches the chat list.
            await page.ReloadAsync();
            await page.WaitForURLAsync("**/chat**");

            // Assert: sidebar now shows "No chats yet."
            await Assertions.Expect(page.GetByText("No chats yet.")).ToBeVisibleAsync();
        }
    }
}
