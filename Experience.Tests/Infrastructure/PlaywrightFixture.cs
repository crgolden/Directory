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

    /// <summary>
    /// <see langword="true"/> when <c>SMOKE_BASE_URL</c> is set — the suite is targeting a real
    /// deployed instance rather than the local <see cref="ExperienceWebApplicationFactory"/>.
    /// </summary>
    public static bool IsSmoke => SmokeBaseUrl is not null;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string? _storageStatePath;

    public PlaywrightFixture()
    {
        // AzureCliCredential's default 13s ProcessTimeout is not enough on this Windows dev
        // machine — `az account get-access-token` spawned as a child of the test host
        // routinely takes 20–40s. Set the timeout before the factory constructs the host
        // so that `builder.Configuration.ToTokenCredentialAsync()` and every subsequent
        // Key Vault call picks up the widened window. Programmatic setting avoids any
        // ambiguity about whether shell env vars propagated through `cmd /c` wrappers.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DefaultAzureCredentialOptions__CredentialProcessTimeout")))
        {
            Environment.SetEnvironmentVariable(
                "DefaultAzureCredentialOptions__CredentialProcessTimeout",
                "00:03:00");
        }

        Factory = SmokeBaseUrl is null ? new ExperienceWebApplicationFactory() : null;
        ChatStore = new InMemoryChatsStore();
        ProductStore = new InMemoryProductsStore();
        CatalogStore = new InMemoryCatalogStore();
        BaseAddress = string.Empty;
    }

    public ExperienceWebApplicationFactory? Factory { get; }

    public InMemoryChatsStore ChatStore { get; }

    public InMemoryProductsStore ProductStore { get; }

    public InMemoryCatalogStore CatalogStore { get; }

    public string BaseAddress { get; private set; }

    private static void Stage(string msg) =>
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] PlaywrightFixture: {msg}");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        Stage("InitializeAsync enter");
        if (SmokeBaseUrl is not null)
        {
            BaseAddress = SmokeBaseUrl.TrimEnd('/');
            Stage($"smoke mode base={BaseAddress}");
        }
        else
        {
            Stage("Factory.StartAsync() enter");
            await Factory!.StartAsync();
            BaseAddress = Factory.ServerAddress;
            Stage($"Factory.StartAsync() done base={BaseAddress}");
        }

        Stage("playwright install chromium enter");
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        Stage($"playwright install exit={exitCode}");
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright install failed with exit code {exitCode}.");
        }

        Stage("Playwright.CreateAsync enter");
        _playwright = await Playwright.CreateAsync();
        Stage("Chromium.LaunchAsync enter");
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Headless
        });
        Stage("Chromium.LaunchAsync done");

        if (SmokeBaseUrl is not null)
        {
            if (TestUsername is null || TestPassword is null)
            {
                throw new InvalidOperationException("TEST_USERNAME and TEST_PASSWORD must be set when SMOKE_BASE_URL is configured.");
            }

            Stage("LoginAsync enter");
            await LoginAsync(); // real OIDC against the deployed app; sets _storageStatePath
            Stage("LoginAsync done");
        }

        // Factory mode: always use the /bff/user mock — real OIDC login is not possible
        // because the Kestrel test server listens on a random port that cannot be registered
        // as a redirect URI. The real auth flow is covered by the smoke tests.

        // Warm up: load /products once so the server pool and Angular hydration are ready
        // before the first real test runs. NewProductsPageAsync already navigates to /products
        // and waits for the Angular bootstrap to complete, so no additional waiting is needed.
        Stage("warmup NewProductsPageAsync enter");
        var warmup = await NewProductsPageAsync();
        Stage("warmup NewProductsPageAsync done");
        await using (warmup.Context) { }
        Stage("InitializeAsync exit");
    }

    /// <summary>
    /// Creates a new browser context and page, optionally registers Playwright route mocks for
    /// <c>/products/api/odata/**</c> requests (backed by <see cref="ProductStore"/>), then
    /// navigates directly to <c>/products</c> and waits for the product list to render.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Route mocks are only registered when running in local E2E mode (no <c>SMOKE_BASE_URL</c>).
    /// In smoke mode the real Products API is contacted so that integration with the live service
    /// is fully exercised.
    /// </para>
    /// <para>
    /// Call <see cref="InMemoryProductsStore.Clear"/> on <see cref="ProductStore"/> BEFORE
    /// calling this method to ensure each test starts with a clean state.
    /// </para>
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

        // Always use a generous default timeout. Cold-start Kestrel + Angular bundle on this
        // machine can legitimately take >30s even when everything is healthy; a tighter cap
        // just produces false negatives.
        page.SetDefaultTimeout(60_000);

        // Log every request/response so we can tell why a navigation is hanging.
        page.Request += (_, req) => Stage($"REQ {req.Method} {req.Url}");
        page.Response += (_, resp) => Stage($"RESP {resp.Status} {resp.Url}");
        page.RequestFailed += (_, req) => Stage($"FAIL {req.Method} {req.Url} err={req.Failure}");

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

        // In smoke mode the real Products API is used — no mock needed.
        if (!IsSmoke)
        {
            await page.RouteAsync("**/products/api/odata/**", async route =>
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
        }

        await page.GotoAsync("/products", new PageGotoOptions
        {
            // "Load" requires EVERY subresource to finish — in tests, a single hanging
            // analytics beacon is enough to exceed the timeout. "DOMContentLoaded" is
            // sufficient for our assertions and avoids that flakiness.
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        });

        // Wait for the product list heading to confirm Angular has bootstrapped and the
        // auth guard has passed.
        await page.WaitForSelectorAsync("h2:has-text('My Products')", new PageWaitForSelectorOptions
        {
            Timeout = 60_000
        });

        return (context, page);
    }

    /// <summary>
    /// Creates a new browser context and page, registers Playwright route mocks for
    /// <c>/catalog/api/odata/**</c> requests (backed by <see cref="CatalogStore"/>), then
    /// navigates to <c>/catalog</c> and waits for the catalog list to render.
    /// </summary>
    /// <remarks>
    /// The catalog is publicly accessible — no auth guard is applied. A <c>/bff/user</c>
    /// mock returning 401 is registered so that <see cref="AuthService"/> sees the user as
    /// anonymous without contacting the real BFF. Omitting this mock in CI causes Duende BFF
    /// 4.x's DPoP negotiation to fire on the anonymous request, which can fail with a
    /// <c>TaskCanceledException</c> and disrupt subsequent BFF proxy calls made by the
    /// catalog resolver.
    /// </remarks>
    public async Task<(IBrowserContext Context, IPage Page)> NewCatalogPageAsync()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Browser is not initialized. Ensure InitializeAsync has been awaited.");
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseAddress,
            IgnoreHTTPSErrors = true,
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(60_000);

        page.Request += (_, req) => Stage($"REQ {req.Method} {req.Url}");
        page.Response += (_, resp) => Stage($"RESP {resp.Status} {resp.Url}");
        page.RequestFailed += (_, req) => Stage($"FAIL {req.Method} {req.Url} err={req.Failure}");

        if (!IsSmoke)
        {
            // Return 401 so AuthService.catchError emits null → isAuthenticated = false.
            await page.RouteAsync("**/bff/user", async route =>
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 401 });
            });

            // AppComponent.ngAfterViewInit sets an iframe with src="/bff/login?prompt=none"
            // whenever isAuthenticated = false. Without this mock the iframe request reaches the
            // real BFF, which tries OIDC silent login to Identity and gets "Invalid redirect_uri"
            // (the random Kestrel test port is not registered). The hanging/errored iframe request
            // prevents Playwright's WaitUntil=Load event from firing and causes WaitForURLAsync
            // to time out. Intercepting the iframe with an HTML page that immediately posts the
            // expected postMessage causes AppComponent.onMessage() to set iframeVisible=false,
            // removing the iframe from the DOM before any assertion runs.
            await page.RouteAsync("**/bff/login**", async route =>
            {
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "text/html",
                    Body = """
                        <!doctype html>
                        <html><body>
                        <script>window.parent.postMessage({ source: 'bff-silent-login', isLoggedIn: false }, '*');</script>
                        </body></html>
                        """
                });
            });

            await page.RouteAsync("**/catalog/api/odata/**", async route =>
            {
                try
                {
                    await DispatchCatalogRouteAsync(route);
                }
                catch
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 500 });
                }
            });
        }

        await page.GotoAsync("/catalog", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        });

        await page.WaitForSelectorAsync("h2:has-text('Product Catalog')", new PageWaitForSelectorOptions
        {
            Timeout = 60_000
        });

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
        await page.GotoAsync("/bff/login?returnUrl=%2Fproducts");

        // Selectors confirmed from Identity.Api/Pages/Account/Login.cshtml.
        await page.FillAsync("input[name='Input.Email']", TestUsername!);
        await page.FillAsync("input[name='Input.Password']", TestPassword!);
        await page.ClickAsync("button#login-submit");

        // Wait for the BFF callback to complete and land on /products.
        await page.WaitForURLAsync("**/products**");

        // Persist session cookies so every per-test context starts authenticated.
        await context.StorageStateAsync(new() { Path = _storageStatePath });
    }

    private async Task DispatchCatalogRouteAsync(IRoute route)
    {
        var request = route.Request;
        var method = request.Method.ToUpperInvariant();
        var uri = new Uri(request.Url);

        var path = uri.AbsolutePath;
        var collectionIndex = path.LastIndexOf("/Products", StringComparison.OrdinalIgnoreCase);
        if (collectionIndex < 0)
        {
            await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
            return;
        }

        var remainder = path[(collectionIndex + "/Products".Length)..];

        if (remainder.Length == 0 || remainder == "/")
        {
            if (method == "GET")
            {
                await HandleCatalogCollectionAsync(route, uri);
            }
            else
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 405 });
            }
        }
        else if (remainder.StartsWith('(') && remainder.EndsWith(')'))
        {
            var idStr = remainder[1..^1];
            if (!Guid.TryParse(idStr, out var id))
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 400 });
                return;
            }

            if (method == "GET")
            {
                var product = CatalogStore.GetProduct(id);
                if (product is null)
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
                    return;
                }

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(CatalogRecordToJson(product))
                });
            }
            else
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 405 });
            }
        }
        else
        {
            await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
        }
    }

    private async Task HandleCatalogCollectionAsync(IRoute route, Uri uri)
    {
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        // Parse $filter for name search
        string? nameFilter = null;
        if (query.TryGetValue("$filter", out var fv))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                fv.ToString(),
                @"tolower\('([^']+)'\)\s*\)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                nameFilter = match.Groups[1].Value;
            }
        }

        // Parse $orderby (e.g. "Name asc", "Price desc")
        var orderBy = "Name";
        var orderDesc = false;
        if (query.TryGetValue("$orderby", out var obv))
        {
            var parts = obv.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
            {
                orderBy = parts[0];
            }

            if (parts.Length >= 2)
            {
                orderDesc = parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
            }
        }

        // Parse $top and $skip
        var top = int.MaxValue;
        var skip = 0;
        if (query.TryGetValue("$top", out var tv) && int.TryParse(tv.ToString(), out var topVal))
        {
            top = topVal;
        }

        if (query.TryGetValue("$skip", out var sv) && int.TryParse(sv.ToString(), out var skipVal))
        {
            skip = skipVal;
        }

        // Parse $count
        var includeCount = query.TryGetValue("$count", out var cv) &&
            cv.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);

        var allProducts = CatalogStore.GetProducts(nameFilter);
        var totalCount = allProducts.Count;

        IEnumerable<InMemoryCatalogStore.CatalogRecord> ordered = orderBy switch
        {
            "Price" when !orderDesc => allProducts.OrderBy(p => p.Price),
            "Price" => allProducts.OrderByDescending(p => p.Price),
            "Brand" when !orderDesc => allProducts.OrderBy(p => p.Brand),
            "Brand" => allProducts.OrderByDescending(p => p.Brand),
            "Category" when !orderDesc => allProducts.OrderBy(p => p.Category),
            "Category" => allProducts.OrderByDescending(p => p.Category),
            _ when orderDesc => allProducts.OrderByDescending(p => p.Name),
            _ => allProducts.OrderBy(p => p.Name),
        };

        var pageItems = ordered.Skip(skip).Take(top).Select(CatalogRecordToJson).ToArray();

        var responseDict = new Dictionary<string, object?>
        {
            ["value"] = pageItems
        };
        if (includeCount)
        {
            responseDict["@odata.count"] = totalCount;
        }

        await route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(responseDict)
        });
    }

    private static object CatalogRecordToJson(InMemoryCatalogStore.CatalogRecord p) => new
    {
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        Brand = p.Brand,
        ModelNumber = (string?)null,
        SerialNumber = (string?)null,
        PurchaseDate = (string?)null,
        Category = p.Category,
        Description = (string?)null,
        ManualUrl = p.ManualUrl,
        CreatedAt = p.CreatedAt,
        UpdatedAt = (DateTimeOffset?)null,
    };

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

        // Absolute path: /products/api/odata/Products  or  /products/api/odata/Products(guid)
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
                    Headers = new Dictionary<string, string>
                    {
                        ["Location"] = $"/products/api/odata/Products({product.Id})"
                    },
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
        Id = p.Id,
        Name = p.Name,
        Price = p.Price,
        Brand = p.Brand,
        ModelNumber = p.ModelNumber,
        SerialNumber = p.SerialNumber,
        PurchaseDate = p.PurchaseDate,
        Category = p.Category,
        Description = p.Description,
        ManualUrl = p.ManualUrl,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
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
