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

    private static readonly string? TestUsername = Environment.GetEnvironmentVariable("TEST_USERNAME");
    private static readonly string? TestPassword = Environment.GetEnvironmentVariable("TEST_PASSWORD");
    private static readonly string? SmokeBaseUrl = Environment.GetEnvironmentVariable("SMOKE_BASE_URL");

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string? _storageStatePath;

    public PlaywrightFixture()
    {
        Factory = SmokeBaseUrl is null ? new ExperienceWebApplicationFactory() : null;
        ChatStore = new InMemoryChatsStore();
        ProductStore = new InMemoryProductsStore();
        BaseAddress = string.Empty;
    }

    public ExperienceWebApplicationFactory? Factory { get; }

    public InMemoryChatsStore ChatStore { get; }

    public InMemoryProductsStore ProductStore { get; }

    public string BaseAddress { get; private set; }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        if (SmokeBaseUrl is not null)
        {
            BaseAddress = SmokeBaseUrl.TrimEnd('/');
        }
        else
        {
            Factory!.CreateClient(); // triggers server startup; populates Factory.ServerAddress
            BaseAddress = Factory.ServerAddress;
        }

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

        if (SmokeBaseUrl is not null)
        {
            if (TestUsername is null || TestPassword is null)
            {
                throw new InvalidOperationException("TEST_USERNAME and TEST_PASSWORD must be set when SMOKE_BASE_URL is configured.");
            }

            await LoginAsync(); // real OIDC against the deployed app; sets _storageStatePath
        }

        // Factory mode: always use the /bff/user mock — real OIDC login is not possible
        // because the Kestrel test server listens on a random port that cannot be registered
        // as a redirect URI. The real auth flow is covered by the smoke tests.

        // Warm up: load /chat once so the server pool and Angular hydration are ready before
        // the first real test runs. NewPageAsync already navigates to /chat and waits for
        // the Angular bootstrap to complete, so no additional waiting is needed here.
        var warmup = await NewPageAsync();
        await using (warmup.Context) { }
    }

    /// <summary>
    /// Creates a new browser context and page, registers Playwright route mocks for all
    /// <c>/manuals/api/**</c> requests (backed by <see cref="ChatStore"/>), then navigates
    /// directly to <c>/chat</c> and waits for Angular to bootstrap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>TEST_USERNAME</c> and <c>TEST_PASSWORD</c> are set the context is loaded with
    /// the real BFF session cookies saved by <see cref="LoginAsync"/>. Otherwise a synthetic
    /// <c>/bff/user</c> Playwright route mock is registered as a local development fallback.
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
            IgnoreHTTPSErrors = true, // Kestrel test server uses a self-signed certificate
            StorageStatePath = _storageStatePath // null locally → no pre-loaded state; real cookie in CI
        });
        var page = await context.NewPageAsync();

        if (CI)
        {
            page.SetDefaultTimeout(60_000);
        }

        // Only mock /bff/user when no real session is available (local development fallback).
        if (_storageStatePath is null)
        {
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
        }

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

        // Navigate directly to /chat.
        await page.GotoAsync("/chat");

        // Wait for Angular to finish bootstrapping and the auth guard to allow the chat
        // component to mount. The "Chats" sidebar label is only rendered after the guard
        // passes, so this guarantees the "+" button and chat list are in the DOM.
        await page.WaitForSelectorAsync("span:has-text('Chats')");

        return (context, page);
    }

    /// <summary>
    /// Creates a new browser context and page, registers Playwright route mocks for all
    /// <c>/products/odata/**</c> requests (backed by <see cref="ProductStore"/>), then
    /// navigates directly to <c>/products</c> and waits for the product list to render.
    /// </summary>
    /// <remarks>
    /// Call <see cref="InMemoryProductsStore.Clear"/> on <see cref="ProductStore"/> BEFORE
    /// calling this method to ensure each test starts with a clean state.
    /// </remarks>
    public async Task<(IBrowserContext Context, IPage Page)> NewProductsPageAsync()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Browser is not initialized. Ensure InitializeAsync has been awaited.");
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseAddress,
            IgnoreHTTPSErrors = true,
            StorageStatePath = _storageStatePath
        });
        var page = await context.NewPageAsync();

        if (CI)
        {
            page.SetDefaultTimeout(60_000);
        }

        if (_storageStatePath is null)
        {
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
        }

        await page.RouteAsync("**/api/products/odata/**", async route =>
        {
            try
            {
                await DispatchProductsRouteAsync(route);
            }
            catch
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 500 });
            }
        });

        await page.GotoAsync("/products");

        // Wait for the product list heading to confirm Angular has bootstrapped and the
        // auth guard has passed.
        await page.WaitForSelectorAsync("h2:has-text('My Products')");

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
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_storageStatePath is not null && File.Exists(_storageStatePath))
        {
            File.Delete(_storageStatePath);
        }
    }

    // ---------------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------------

    private async Task LoginAsync()
    {
        _storageStatePath = Path.GetTempFileName();

        await using var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseAddress,
            IgnoreHTTPSErrors = true // Kestrel test server uses a self-signed certificate
        });
        var page = await context.NewPageAsync();

        if (CI)
        {
            page.SetDefaultTimeout(60_000);
        }

        // Kick off the OIDC flow. BFF redirects to the Identity server login page.
        await page.GotoAsync("/bff/login?returnUrl=%2Fchat");

        // Selectors confirmed from Identity.Api/Pages/Account/Login.cshtml.
        await page.FillAsync("input[name='Input.Email']", TestUsername!);
        await page.FillAsync("input[name='Input.Password']", TestPassword!);
        await page.ClickAsync("button#login-submit");

        // Wait for the BFF callback to complete and land on /chat.
        await page.WaitForURLAsync("**/chat**");

        // Persist session cookies so every per-test context starts authenticated.
        await context.StorageStateAsync(new() { Path = _storageStatePath });
    }

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

    private async Task DispatchProductsRouteAsync(IRoute route)
    {
        var request = route.Request;
        var method = request.Method.ToUpperInvariant();
        var uri = new Uri(request.Url);

        // Absolute path: /products/odata/Products  or  /products/odata/Products(guid)
        var path = uri.AbsolutePath;
        var collectionIndex = path.LastIndexOf("/Products", StringComparison.OrdinalIgnoreCase);
        if (collectionIndex < 0)
        {
            await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
            return;
        }

        var remainder = path[(collectionIndex + "/Products".Length)..];

        // remainder == "" → collection; remainder == "(guid)" → keyed entity
        if (remainder.Length == 0 || remainder == "/")
        {
            // Parse optional $filter from query string for name search
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            var filter = query.TryGetValue("$filter", out var fv) ? fv.ToString() : null;
            string? nameFilter = null;
            if (!string.IsNullOrEmpty(filter))
            {
                // Extract the search term from: contains(tolower(Name), tolower('term'))
                var match = System.Text.RegularExpressions.Regex.Match(
                    filter,
                    @"tolower\('([^']+)'\)\s*\)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    nameFilter = match.Groups[1].Value;
                }
            }

            await HandleProductsCollectionAsync(route, method, nameFilter, request);
        }
        else if (remainder.StartsWith('(') && remainder.EndsWith(')'))
        {
            var idStr = remainder[1..^1];
            if (!Guid.TryParse(idStr, out var id))
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 400 });
                return;
            }

            await HandleSingleProductAsync(route, method, id, request);
        }
        else
        {
            await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
        }
    }

    private async Task HandleProductsCollectionAsync(IRoute route, string method, string? nameFilter, IRequest request)
    {
        switch (method)
        {
            case "GET":
            {
                var products = ProductStore.GetProducts(nameFilter);
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new
                    {
                        value = products.Select(ProductToJson)
                    })
                });
                break;
            }

            case "POST":
            {
                var body = request.PostData ?? "{}";
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var product = ProductStore.Create(
                    name: root.TryGetProperty("name", out var n) ? n.GetString() : null,
                    price: root.TryGetProperty("price", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetDecimal() : null,
                    brand: root.TryGetProperty("brand", out var br) ? br.GetString() : null,
                    modelNumber: root.TryGetProperty("modelNumber", out var mn) ? mn.GetString() : null,
                    serialNumber: root.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null,
                    purchaseDate: root.TryGetProperty("purchaseDate", out var pd) ? pd.GetString() : null,
                    category: root.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    description: root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    manualUrl: root.TryGetProperty("manualUrl", out var mu) ? mu.GetString() : null);
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 201,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(ProductToJson(product))
                });
                break;
            }

            default:
                await route.FulfillAsync(new RouteFulfillOptions { Status = 405 });
                break;
        }
    }

    private async Task HandleSingleProductAsync(IRoute route, string method, Guid id, IRequest request)
    {
        switch (method)
        {
            case "GET":
            {
                var product = ProductStore.GetProduct(id);
                if (product is null)
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
                    return;
                }

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(ProductToJson(product))
                });
                break;
            }

            case "PUT":
            {
                var body = request.PostData ?? "{}";
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var existing = ProductStore.GetProduct(id);
                if (existing is null)
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
                    return;
                }

                var replacement = new InMemoryProductsStore.ProductRecord(
                    id,
                    root.TryGetProperty("name", out var n) ? n.GetString() : null,
                    root.TryGetProperty("price", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetDecimal() : null,
                    root.TryGetProperty("brand", out var br) ? br.GetString() : null,
                    root.TryGetProperty("modelNumber", out var mn) ? mn.GetString() : null,
                    root.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null,
                    root.TryGetProperty("purchaseDate", out var pd) ? pd.GetString() : null,
                    root.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    root.TryGetProperty("manualUrl", out var mu) ? mu.GetString() : null,
                    existing.CreatedAt,
                    DateTimeOffset.UtcNow);
                var updated = ProductStore.Put(id, replacement);
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(ProductToJson(updated!))
                });
                break;
            }

            case "PATCH":
            {
                var body = request.PostData ?? "{}";
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var updated = ProductStore.Patch(
                    id,
                    name: root.TryGetProperty("name", out var n) ? n.GetString() : null,
                    price: root.TryGetProperty("price", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetDecimal() : null,
                    brand: root.TryGetProperty("brand", out var br) ? br.GetString() : null,
                    modelNumber: root.TryGetProperty("modelNumber", out var mn) ? mn.GetString() : null,
                    serialNumber: root.TryGetProperty("serialNumber", out var sn) ? sn.GetString() : null,
                    purchaseDate: root.TryGetProperty("purchaseDate", out var pd) ? pd.GetString() : null,
                    category: root.TryGetProperty("category", out var cat) ? cat.GetString() : null,
                    description: root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    manualUrl: root.TryGetProperty("manualUrl", out var mu) ? mu.GetString() : null);
                if (updated is null)
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
                    return;
                }

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(ProductToJson(updated))
                });
                break;
            }

            case "DELETE":
            {
                var deleted = ProductStore.Delete(id);
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = deleted ? 204 : 404
                });
                break;
            }

            default:
                await route.FulfillAsync(new RouteFulfillOptions { Status = 405 });
                break;
        }
    }

    private static object ProductToJson(InMemoryProductsStore.ProductRecord p) => new
    {
        id = p.Id,
        name = p.Name,
        price = p.Price,
        brand = p.Brand,
        modelNumber = p.ModelNumber,
        serialNumber = p.SerialNumber,
        purchaseDate = p.PurchaseDate,
        category = p.Category,
        description = p.Description,
        manualUrl = p.ManualUrl,
        createdAt = p.CreatedAt,
        updatedAt = p.UpdatedAt,
    };

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
