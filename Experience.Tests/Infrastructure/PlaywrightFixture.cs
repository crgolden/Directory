namespace Experience.Tests.Infrastructure;

using System.Text.Json;
using Microsoft.Playwright;

/// <summary>
/// xUnit collection fixture that owns the <see cref="ExperienceWebApplicationFactory"/>,
/// the Playwright browser, and provides per-test page creation with Manuals API route mocks.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private static readonly bool CI =
        bool.TryParse(Environment.GetEnvironmentVariable("CI"), out var isCi) && isCi;

    private static readonly bool Headless =
        !string.Equals(Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADED"), "1", StringComparison.OrdinalIgnoreCase);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightFixture()
    {
        Factory = new ExperienceWebApplicationFactory();
        ChatStore = new InMemoryChatsStore();
        BaseAddress = string.Empty;
    }

    public ExperienceWebApplicationFactory Factory { get; }

    public InMemoryChatsStore ChatStore { get; }

    public string BaseAddress { get; private set; }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        Factory.CreateClient(); // triggers server startup; populates Factory.ServerAddress
        BaseAddress = Factory.ServerAddress;

        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright install failed with exit code {exitCode}.");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Headless
        });

        // Warm up: load /chat once so the server pool and Angular hydration are ready before
        // the first real test runs. NewPageAsync already navigates to /chat and waits for
        // the Angular bootstrap to complete, so no additional waiting is needed here.
        var warmup = await NewPageAsync();
        await using (warmup.Context) { }
    }

    /// <summary>
    /// Creates a new browser context and page, registers Playwright route mocks for
    /// <c>/bff/user</c> (returns a synthetic authenticated session) and all
    /// <c>/manuals/api/**</c> requests (backed by <see cref="ChatStore"/>), then
    /// navigates directly to <c>/chat</c> and waits for Angular to bootstrap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mocking <c>/bff/user</c> at the Playwright layer is simpler and more reliable than
    /// establishing a real Duende BFF session: <c>SignInAsync</c> in a startup filter does
    /// not create a server-side BFF session ticket, so the Angular auth guard would redirect
    /// to <c>/bff/login</c> on every page load.
    /// </para>
    /// <para>
    /// Call <see cref="InMemoryChatsStore.Clear"/> on <see cref="ChatStore"/> BEFORE calling
    /// this method to ensure each test starts with a clean state.
    /// </para>
    /// </remarks>
    public async Task<(IBrowserContext Context, IPage Page)> NewPageAsync()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Browser is not initialized. Ensure InitializeAsync has been awaited.");
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseAddress,
            IgnoreHTTPSErrors = true
        });
        var page = await context.NewPageAsync();

        if (CI)
        {
            page.SetDefaultTimeout(60_000);
        }

        // Intercept /bff/user and return a synthetic authenticated session so the Angular
        // AuthService sees an authenticated user and the auth guard allows navigation to /chat.
        // All Manuals API requests are also mocked, so no real BFF token exchange is needed.
        await page.RouteAsync("**/bff/user", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new object[]
                {
                    new { type = "sub", value = "e2e-user-id" },
                    new { type = "name", value = "E2E Test User" },
                    new { type = "email", value = "e2e@test.invalid" },
                    new { type = "sid", value = "e2e-session" },
                })
            });
        });

        // Register route mocks for all Manuals API calls so no real Manuals server is needed.
        await page.RouteAsync("**/manuals/api/**", async route =>
        {
            try
            {
                await DispatchManualsRouteAsync(route);
            }
            catch
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 500 });
            }
        });

        // Navigate directly to /chat. The /bff/user mock ensures the auth guard passes.
        await page.GotoAsync("/chat");

        // Wait for Angular to finish bootstrapping and the auth guard to allow the chat
        // component to mount. The "Chats" sidebar label is only rendered after the guard
        // passes, so this guarantees the "+" button and chat list are in the DOM.
        await page.WaitForSelectorAsync("span:has-text('Chats')");

        return (context, page);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        await Factory.DisposeAsync();
    }

    // ---------------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------------

    private async Task DispatchManualsRouteAsync(IRoute route)
    {
        var request = route.Request;
        var method = request.Method.ToUpperInvariant();
        var uri = new Uri(request.Url);
        // uri.AbsolutePath → e.g. /manuals/api/chats  or  /manuals/api/chats/{id}/messages/stream
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // segments: [0]="manuals", [1]="api", [2]="chats", [3]=chatId?, [4]="messages"?, [5]="stream"?

        if (segments.Length < 3 || segments[2] != "chats")
        {
            await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
            return;
        }

        var chatId = segments.Length >= 4 ? segments[3] : null;
        var isMessages = segments.Length >= 5 && segments[4] == "messages";
        var isStream = segments.Length >= 6 && segments[5] == "stream";

        if (chatId is null)
        {
            await HandleChatsCollectionAsync(route, method);
        }
        else if (!isMessages)
        {
            await HandleSingleChatAsync(route, method, chatId, request);
        }
        else if (!isStream)
        {
            await HandleMessagesAsync(route, method, chatId, request);
        }
        else
        {
            await HandleStreamAsync(route, chatId, request);
        }
    }

    private async Task HandleChatsCollectionAsync(IRoute route, string method)
    {
        switch (method)
        {
            case "GET":
            {
                var chats = ChatStore.GetChats();
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(chats.Select(c => new
                    {
                        chatId = c.ChatId,
                        title = c.Title,
                        createdAt = c.CreatedAt
                    }))
                });
                break;
            }

            case "POST":
            {
                var chat = ChatStore.CreateChat();
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 201,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new
                    {
                        chatId = chat.ChatId,
                        title = chat.Title,
                        createdAt = chat.CreatedAt
                    })
                });
                break;
            }

            default:
                await route.FulfillAsync(new RouteFulfillOptions { Status = 405 });
                break;
        }
    }

    private async Task HandleSingleChatAsync(IRoute route, string method, string chatId, IRequest request)
    {
        switch (method)
        {
            case "GET":
            {
                var chat = ChatStore.GetChat(chatId);
                if (chat is null)
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
                    return;
                }

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new
                    {
                        chatId = chat.ChatId,
                        title = chat.Title,
                        createdAt = chat.CreatedAt
                    })
                });
                break;
            }

            case "PATCH":
            {
                var body = request.PostData ?? "{}";
                using var doc = JsonDocument.Parse(body);
                var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(title))
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 400 });
                    return;
                }

                ChatStore.UpdateTitle(chatId, title);
                await route.FulfillAsync(new RouteFulfillOptions { Status = 204 });
                break;
            }

            case "DELETE":
                ChatStore.DeleteChat(chatId);
                await route.FulfillAsync(new RouteFulfillOptions { Status = 204 });
                break;

            default:
                await route.FulfillAsync(new RouteFulfillOptions { Status = 405 });
                break;
        }
    }

    private async Task HandleMessagesAsync(IRoute route, string method, string chatId, IRequest request)
    {
        switch (method)
        {
            case "GET":
            {
                var msgs = ChatStore.GetMessages(chatId);
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(msgs.Select(m => new { role = m.Role, text = m.Text }))
                });
                break;
            }

            case "POST":
            {
                var body = request.PostData ?? "{}";
                using var doc = JsonDocument.Parse(body);
                var input = doc.RootElement.TryGetProperty("input", out var i) ? (i.GetString() ?? string.Empty) : string.Empty;
                var chat = ChatStore.CompleteMessage(chatId, input);
                if (chat is null)
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
                    return;
                }

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new
                    {
                        output = InMemoryChatsStore.GetMockResponse(),
                        chatId = chat.ChatId
                    })
                });
                break;
            }

            default:
                await route.FulfillAsync(new RouteFulfillOptions { Status = 405 });
                break;
        }
    }

    private async Task HandleStreamAsync(IRoute route, string chatId, IRequest request)
    {
        var body = request.PostData ?? "{}";
        using var doc = JsonDocument.Parse(body);
        var input = doc.RootElement.TryGetProperty("input", out var i) ? (i.GetString() ?? string.Empty) : string.Empty;
        var (_, sseBody) = ChatStore.CompleteStream(chatId, input);

        await route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/event-stream",
            Body = sseBody
        });
    }
}
