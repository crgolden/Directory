# Testing

The Directory test suite uses xUnit v3 (`xunit.v3.mtp-v2`, exe runner), split into two tiers, each in its own
project: `Unit` in `Directory.Tests.Unit` (service/domain tests against a fake `DbConnection` and mocked Service
Bus, with no live SQL Server, Key Vault, or Azure credential in the loop) and `Integration` in
`Directory.Tests.Integration` (the `*EndpointsTests.cs` files, which drive the real `Program.cs` pipeline over
HTTP through `DirectoryWebApplicationFactory` against a real SQL Server schema deployed via `SqlPackage` in CI).
No browser is involved, so the endpoint tier is Integration, not E2E ([AGENTS/TESTING.md](../AGENTS/TESTING.md)).

Unit test coding standards (MockBehavior.Strict, argument verification, SetupSequence, no control-flow in
tests, etc.) are in the workspace-level [Unit Test Standards](../AGENTS/TESTING.md#unit-test-standards).

## Test tiers

| Tier | Trait | Project | Requires Azure / SQL? | Runs in CI |
|------|-------|---------|-----------------------|------------|
| Unit | `Category=Unit` | `Directory.Tests.Unit` | No — fake DB + mocked Service Bus | Every push/PR |
| Integration | `Category=Integration` | `Directory.Tests.Integration` | Yes — real SQL Server (schema deployed via `SqlPackage`) | Every push/PR |

- **Unit — service / domain tests** (`Api/*ServiceTests.cs`, `Domain/ConfidenceScoreCalculatorTests.cs`) — test
  the feature services and the confidence-score calculator directly with a mocked `DbConnection`.
- **Integration — endpoint tests** (`Directory.Tests.Integration/*EndpointsTests.cs`) — drive the real
  `Program.cs` pipeline through `DirectoryWebApplicationFactory`, exercising routing, model binding, and
  authorization against a real SQL Server database.
- `TestSupport/TestValues.cs` lives in the unit project and is compiled into the integration project as a
  linked `<Compile>`; `DirectoryWebApplicationFactory`, `IntegrationAuthHandler` and `TestRequests` live in
  `Directory.Tests.Integration/TestSupport/`, and `FakeDb` stays with the unit tier.

---

## Running Tests Locally

For running tests (`dotnet test` from the repo root — never the workspace root) and the exe-runner flags,
see the workspace-level [TESTING.md](../AGENTS/TESTING.md).

User Secrets ID: `61549613-3239-4c31-8300-39334a7c2657` (not needed for unit tests, which never boot
`Program.cs`).

```powershell
dotnet build Directory.Tests.Unit --configuration Debug
.\Directory.Tests.Unit\bin\Debug\net10.0\Directory.Tests.Unit.exe -trait "Category=Unit" -showLiveOutput

dotnet build Directory.Tests.Integration --configuration Debug
.\Directory.Tests.Integration\bin\Debug\net10.0\Directory.Tests.Integration.exe -trait "Category=Integration" -showLiveOutput
```

---

## Test infrastructure

### `DirectoryWebApplicationFactory`

`WebApplicationFactory<Program>` used by the endpoint tests. It starts the full `Program.cs` (in the
`Development` environment `WebApplicationFactory` supplies by default), then:

- replaces `ILoggerFactory` with console logging;
- replaces `IAzureClientFactory<ServiceBusClient>` with a Moq-backed `ServiceBusClient` / `ServiceBusSender`
  (loose) so `SubmitCorrectionAsync` can enqueue without a real namespace;
- registers `IntegrationAuthHandler` as the default authentication scheme, and nothing else about authorization:
  the `Directory` and `ChurchesMod` policies under test are the ones `Program.cs` declares.

The `DbConnection` registered by `Program.cs` is left in place, so the endpoint tests run against the real
SQL Server database it names; `OpenTestConnectionAsync` opens a second connection to that same database for
seeding and assertions. (`FakeDbConnection` is used only by the service-level unit tests, never by this
factory.)

### `IntegrationAuthHandler`

An `AuthenticationHandler` registered as the default scheme by the factory. It issues a principal carrying
`sub`, `scope=directory` and `churches.mod=true`, satisfying both the `Directory` and `ChurchesMod`
policies. Tests that need the anonymous or unauthorized path assert against the endpoints that don't
require those claims, or vary the request accordingly.

**The handler issues only claim shapes Identity actually mints.**
`churches.mod` is an ApiScopeClaim on the `directory` scope (`Tools/Identity/ApiScopes.sql`), so it arrives
under its own claim type while the `scope` claim carries `directory`; `scope=churches.mod` is a shape the IdP
cannot produce. A fixture that mints a claim the IdP never issues makes every reader of that claim look
correct under test, so two readers that disagree about it both pass. Never add a claim here to make a test
pass.

### `FakeDb`

`FakeDbConnection` (`TestSupport/FakeDbConnection.cs`, with its `FakeDbCommand`, `FakeDbParameter`, `FakeDbParameterCollection` and `FakeDbTransaction` siblings, one type per file) is a hand-rolled `DbConnection` / `DbCommand` / `DbDataReader`
test double. Because all Directory data access is BCL ADO.NET over an abstract `DbConnection`, the fake lets
the service tests assert generated SQL/parameters and feed canned reader rows without a database.

---

## Test coverage

| Area | File | What it covers |
|------|------|----------------|
| Church endpoints | `Directory.Tests.Integration/ChurchEndpointsTests.cs` | Routing + auth for list / get-by-slug / create / update / patch / delete |
| Church service | `Api/ChurchServiceTests.cs` | CRUD, slug generation, reader mapping (incl. nested `schedules`/`ministries`/`campuses` on get-by-slug), confidence recalculation |
| Child curation services | `Api/ScheduleServiceTests.cs`, `Api/MinistryServiceTests.cs`, `Api/CampusServiceTests.cs` | `ChurchesMod` create/update/delete for `ServiceSchedules`/`Ministries`/`Campuses` — SQL + parameter generation, `TimeOnly` parse/validation, day-of-week bounds |
| Search endpoints / service | `Directory.Tests.Integration/SearchEndpointsTests.cs`, `Api/SearchServiceTests.cs` | Filter toggles, Haversine distance ordering, parameter binding, schedule filter SQL generation (`dayOfWeek`, `startTimeBefore`, `startTimeAfter`) |
| Denomination service | `Api/DenominationServiceTests.cs` | `GetAllAsync` (connection open/closed, empty table, ORDER BY in SQL) |
| Admin import/export | `Api/AdminServiceTests.cs` | `ParseCsv` (field mapping, skip-on-missing-name/state, empty/header-only, multiple rows); `ImportCsvAsync` (publish count, empty CSV); `ExportCsvAsync` (connection auto-open, row count, ORDER BY clause) |
| Crawling endpoints / service | `Directory.Tests.Integration/CrawlingEndpointsTests.cs`, `Api/CrawlingServiceTests.cs` | Crawl-source CRUD and trigger |
| Moderation endpoints / service | `Directory.Tests.Integration/ModerationEndpointsTests.cs`, `Api/ModerationServiceTests.cs` | Corrections lifecycle, transactional merge (commit vs rollback) |
| User endpoint | `Directory.Tests.Integration/UserEndpointsTests.cs` | `/me` identity projection, and that it carries no moderation flag |
| Confidence score | `Domain/ConfidenceScoreCalculatorTests.cs` | Score derivation from populated attributes |

See the workspace `COVERAGE/Directory.md` for the demand-driven MC/DC tables behind the service
test selection.

---

## CI pipeline

The GitHub Actions workflow (`.github/workflows/main_crgolden-directory.yml`) runs on every push and PR:

1. Build solution (`dotnet build --no-incremental --configuration Release`) — also compiles
   `Directory.Data.sqlproj` to a `.dacpac`
2. Unit tests with coverage (`dotnet coverlet … --filter-trait Category=Unit`, OpenCover →
   `coverage.opencover.xml`); TRX written to `TestResults/unit-tests.trx`
3. Deploy the integration test database schema (`SqlPackage` against the `DB_NAME_E2E` database)
4. Integration tests with coverage (`dotnet-coverage collect … --project Directory.Tests.Integration
   --filter-trait Category=Integration`, VS Coverage XML → `coverage-integration.xml`),
   `ASPNETCORE_ENVIRONMENT=CI` against the real SQL Server; TRX written to
   `Directory.Tests.Integration/…/TestResults/integration-tests.trx`
5. SonarCloud analysis
6. Publish the web app and upload the app + dacpac artifacts

The deploy job deploys the dacpac (via `SqlPackage`) and then the app to `crgolden-directory`.

---

## Local SonarCloud analysis

Directory is C#, so the analysis runs through `dotnet-sonarscanner` (the MSBuild integration) with the
build between `begin` and `end`, exactly as the workflow does. The standalone `sonar-scanner` CLI indexes
the files but analyses no C#; it uploads an empty analysis that still shows a green gate. Pass the same
`/d:` arguments as the workflow's "Begin Sonar analysis" step, including `sonar.coverage.exclusions`.
Unit coverage is OpenCover (branch-bearing, via `coverlet.console` pinned in `dotnet-tools.json`); integration
coverage is Visual Studio Coverage XML (via `dotnet-coverage` against a real SQL Server), fed to Sonar as
a second, separate report. Run from `Directory/`.

```powershell
dotnet tool restore
dotnet-sonarscanner begin /k:"<project key>" /o:"<organization>" /d:sonar.token="<token>" `
  /d:sonar.host.url="https://sonarcloud.io" `
  /d:sonar.cs.opencover.reportsPaths="coverage.opencover.xml" `
  /d:sonar.cs.vscoveragexml.reportsPaths="coverage-integration.xml" `
  /d:sonar.exclusions="**/bin/**,**/obj/**" `
  /d:sonar.coverage.exclusions="**/Program.cs"

dotnet build --no-incremental --configuration Release

dotnet coverlet Directory.Tests.Unit\bin\Release\net10.0 `
  --target "dotnet" `
  --targetargs "test --project Directory.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit" `
  --format opencover --output "coverage.opencover.xml" `
  --skipautoprops --exclude-by-attribute GeneratedCodeAttribute `
  --exclude-by-file "**/obj/**" --exclude-by-file "**/Program.cs" `
  --does-not-return-attribute DoesNotReturnAttribute --include "[Directory]*"

dotnet-coverage collect `
  "dotnet test --project Directory.Tests.Integration --no-build --configuration Release -- --filter-trait Category=Integration" `
  -f xml -o "coverage-integration.xml" -s "coverage.settings.xml"

dotnet-sonarscanner end /d:sonar.token="<token>"
```

Required coverage files: `coverage.opencover.xml` (unit, OpenCover). A missing report uploads 0% coverage
rather than failing, so confirm both files exist before `end`.

### When to build a truth table

The coverage **score is read from SonarCloud, never hand-maintained** here. Build a per-method table in
the workspace `COVERAGE/Directory.md` only when SonarCloud flags a method with **cognitive complexity > 15 AND uncovered
conditions > 0**: the table is escalation for the gnarly few, not a per-class deliverable. See
the workspace `COVERAGE/METHOD.md`.
