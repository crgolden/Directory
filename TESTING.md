# Testing

The Directory test suite uses xUnit v3 (`xunit.v3.mtp-v2`, exe runner). Every test is tagged
`[Trait("Category", "Unit")]` and runs with **no external dependencies** — the in-process host uses a fake
`DbConnection` and a mocked Service Bus, so there is no live SQL Server, Key Vault, or Azure credential in the
loop.

Unit test coding standards (MockBehavior.Strict, argument verification, SetupSequence, no control-flow in
tests, etc.) are in the workspace-level [Unit Test Standards](../TESTING.md#unit-test-standards).

## Test tiers

| Tier | Trait | Project | Requires Azure / SQL? | Runs in CI |
|------|-------|---------|-----------------------|------------|
| Unit | `Category=Unit` | `Directory.Tests` | No — fake DB + mocked Service Bus | Every push/PR |

The suite has two flavors of unit test, both `Category=Unit`:

- **Endpoint tests** (`Api/*EndpointsTests.cs`) — drive the real `Program.cs` pipeline through
  `DirectoryWebApplicationFactory`, exercising routing, model binding, and authorization against a fake DB.
- **Service / domain tests** (`Api/*ServiceTests.cs`, `Domain/ConfidenceScoreCalculatorTests.cs`) — test the
  feature services and the confidence-score calculator directly with a mocked `DbConnection`.

---

## Running Tests Locally

For the `.NET 10 SDK xUnit caveat` (why `dotnet test` doesn't work) and the exe-runner flags, see the
workspace-level [TESTING.md](../TESTING.md).

User Secrets ID: `61549613-3239-4c31-8300-39334a7c2657` (not needed for unit tests — the factory injects
in-memory configuration).

```powershell
dotnet build Directory.Tests --configuration Debug
.\Directory.Tests\bin\Debug\net10.0\Directory.Tests.exe -trait "Category=Unit" -showLiveOutput
```

---

## Test infrastructure

### `DirectoryWebApplicationFactory`

`WebApplicationFactory<Program>` used by the endpoint tests. It starts the full `Program.cs`, then:

- injects in-memory configuration (`OidcAuthority`, a dummy `SqlConnectionStringBuilder`, a dummy
  `ServiceBusConnectionString`) so startup succeeds without real infrastructure;
- replaces the scoped `DbConnection` with a singleton **`FakeDbConnection`** (`FakeDb`), so tests script the
  reader rows and command results directly;
- replaces `IAzureClientFactory<ServiceBusClient>` with a Moq-backed `ServiceBusClient` / `ServiceBusSender`
  (loose) so `SubmitCorrectionAsync` can enqueue without a real namespace;
- registers `IntegrationAuthHandler` as the default authentication scheme and re-declares the `Directory` and
  `ChurchesMod` authorization policies.

### `IntegrationAuthHandler`

An `AuthenticationHandler` registered as the default scheme by the factory. It issues a principal carrying
`sub`, `scope=directory`, `scope=churches.mod`, and `churches.mod=true`, satisfying both the `Directory` and
`ChurchesMod` policies. Tests that need the anonymous or unauthorized path assert against the endpoints that
don't require those claims, or vary the request accordingly.

### `FakeDb`

`FakeDbConnection` (`TestSupport/FakeDb.cs`) is a hand-rolled `DbConnection` / `DbCommand` / `DbDataReader`
test double. Because all Directory data access is BCL ADO.NET over an abstract `DbConnection`, the fake lets
service and endpoint tests assert generated SQL/parameters and feed canned reader rows without a database.

---

## Test coverage

| Area | File | What it covers |
|------|------|----------------|
| Church endpoints | `Api/ChurchEndpointsTests.cs` | Routing + auth for list / get-by-slug / create / update / patch / delete |
| Church service | `Api/ChurchServiceTests.cs` | CRUD, slug generation, reader mapping, confidence recalculation |
| Search endpoints / service | `Api/SearchEndpointsTests.cs`, `Api/SearchServiceTests.cs` | Filter toggles, Haversine distance ordering, parameter binding |
| Crawling endpoints / service | `Api/CrawlingEndpointsTests.cs`, `Api/CrawlingServiceTests.cs` | Crawl-source CRUD and trigger |
| Moderation endpoints / service | `Api/ModerationEndpointsTests.cs`, `Api/ModerationServiceTests.cs` | Corrections lifecycle, transactional merge (commit vs rollback) |
| User endpoint | `Api/UserEndpointsTests.cs` | `/me` identity projection |
| Configuration extensions | `Api/ConfigurationExtensionsTests.cs` | `GetRequired<T>` binding helpers |
| Confidence score | `Domain/ConfidenceScoreCalculatorTests.cs` | Score derivation from populated attributes |

See [COVERAGE-TRUTH-TABLES.md](COVERAGE-TRUTH-TABLES.md) for the demand-driven MC/DC tables behind the service
test selection.

---

## CI pipeline

The GitHub Actions workflow (`.github/workflows/main_crgolden-directory.yml`) runs on every push and PR:

1. Build solution (`dotnet build --no-incremental --configuration Release`) — also compiles
   `Directory.Data.sqlproj` to a `.dacpac`
2. Unit tests with coverage (`dotnet coverlet … --filter-trait Category=Unit`, OpenCover →
   `coverage.opencover.xml`), `ASPNETCORE_ENVIRONMENT=CI`; TRX written to `TestResults/unit-tests.trx`
3. SonarCloud analysis
4. Publish the web app and upload the app + dacpac artifacts

The deploy job deploys the dacpac (via `SqlPackage`) and then the app to `crgolden-directory`.

---

## Local SonarCloud analysis

Generate coverage first, then run from `Directory/`. Unit coverage is OpenCover (branch-bearing, via
`coverlet.console` pinned in `dotnet-tools.json` — restore with `dotnet tool restore`; see the workspace
`TESTING.md` for the command rationale). Directory has no integration/E2E suite, so OpenCover is the only report.

```powershell
dotnet build Directory.Tests --configuration Release
dotnet tool restore
dotnet coverlet Directory.Tests\bin\Release\net10.0 `
  --target "dotnet" `
  --targetargs "test --project Directory.Tests --no-build --configuration Release -- --filter-trait Category=Unit" `
  --format opencover --output "coverage.opencover.xml" `
  --skipautoprops --exclude-by-attribute GeneratedCodeAttribute `
  --exclude-by-file "**/obj/**" --exclude-by-file "**/Program.cs" `
  --does-not-return-attribute DoesNotReturnAttribute --include "[Directory]*"

$env:SONAR_TOKEN = "<token>"
& "$env:SystemDrive\sonar-scanner-8.0.1.6346-windows-x64\bin\sonar-scanner.bat" `
  "-Dsonar.projectKey=crgolden_Directory" `
  "-Dsonar.organization=crgolden" `
  "-Dsonar.sources=Directory" `
  "-Dsonar.tests=Directory.Tests" `
  "-Dsonar.exclusions=**/bin/**,**/obj/**" `
  "-Dsonar.cs.opencover.reportsPaths=coverage.opencover.xml"
```

Required coverage files: `coverage.opencover.xml` (unit, OpenCover).

### When to build a truth table

The coverage **score is read from SonarCloud, never hand-maintained** here. Build a per-method table in
`COVERAGE-TRUTH-TABLES.md` only when SonarCloud flags a method with **cognitive complexity > 15 AND uncovered
conditions > 0**: the table is escalation for the gnarly few, not a per-class deliverable. See
`../DESIGN-LANGUAGE.md` and `../TESTING-COVERAGE.md`.
