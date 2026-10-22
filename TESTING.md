# Testing

## Test tiers

| Tier | Trait | Project | Needs | Runs in CI |
|---|---|---|---|---|
| Unit | `Category=Unit` | `Directory.Tests.Unit` | nothing: a fake `DbConnection` and a mocked Service Bus | every push and pull request |
| Integration | `Category=Integration` | `Directory.Tests.Integration` | a SQL Server catalog carrying the `Directory.Data` schema, named with a `Test` suffix | every push and pull request except Dependabot's |
| Integration, full-text | `Category=Integration` | `Directory.Tests.FullText` | the same catalog, with its full-text index | every push and pull request except Dependabot's |

All three are xUnit v3 projects on Microsoft.Testing.Platform (`global.json` selects the runner). No browser is
involved, so the endpoint tests are integration tests, not end-to-end. Test data comes from the `Shared` package's
`Shared.Testing.Generated`. `Directory.Tests.FullText` compiles the integration project's factory, auth handler and
catalog contract through linked `<Compile>` items, so both run against one fixture.

**What the integration tier alone proves is that each hand-written SQL statement runs against the real schema and
returns the right rows.** The unit tier never asserts SQL text: text pinned against a fake connection passes a wrong
column, a bad join or a missing `fn_HaversineDistance` dependency, and a copy of the statement in a test only checks
that the statement is the statement. It asserts what the code decides instead: which parameters are bound with which
values, and that a command executed (`FakeDbCommand` refuses blank text or an unbound `@parameter`). Filters,
ordering and joins are proved here, by the rows they return. The integration tier also boots `Program.cs`. It does not
validate real tokens (the factory swaps the authentication scheme) and does not reach Service Bus (mocked).

### Unit tier (`Directory.Tests.Unit/Api`)

| File | Covers |
|---|---|
| `ChurchServiceTests.cs`, `ChurchDefaultsTests.cs` | church CRUD, slug generation, reader mapping including the nested children on get-by-slug |
| `ScheduleServiceTests.cs`, `MinistryServiceTests.cs`, `CampusServiceTests.cs` | child create, update and delete: bound parameters, `TimeOnly` parsing, day-of-week bounds |
| `SearchServiceTests.cs` | the parameters each filter binds, query validation, paging, reading the count then the page |
| `DenominationServiceTests.cs`, `AdminServiceTests.cs`, `CrawlingServiceTests.cs` | denominations, CSV import and export, crawl sources and triggers |
| `ModerationServiceTests.cs`, `ModerationApplyCorrectionTests.cs` | the corrections lifecycle and the transactional merge (commit and rollback) |
| `OptionalPatchTests.cs` | `PATCH` telling an absent member from an explicit `null` |
| `EntityJsonTests.cs` | `State` written as its two-letter code and `WorshipStyle` as a number |
| `AuthorizationPoliciesTests.cs`, `SubjectClaimsTests.cs` | policy and claim names, and `sub` parsed as a `Guid` |

### Integration tier (`Directory.Tests.Integration`)

One `*EndpointsTests.cs` class per feature (churches, schedules, ministries, campuses, search, denominations,
crawling, moderation, `/me`) drives the real pipeline over HTTP. `DirectoryWebApplicationFactoryTests.cs` and
`TestDatabaseContractTests.cs` pin the factory's guards.

### Full-text tier (`Directory.Tests.FullText`)

`SearchRelevanceTests.cs` proves keyword relevance order against a populated full-text index: an exact match, then a
prefix match, then a match that repeats both words. It lives in its own project because every run of it waits for
SQL Server to index the rows it seeds (`FullTextSeeding.cs`: a direct `INSERT`, then a deadline-free poll of
`CONTAINSTABLE` until each seeded row is returned, a wait on the condition itself bounded only by the runner's
whole-test budget). In the integration project, Stryker would rerun it, and pay that wait, for every search mutant it
covers; kept apart, only the relevance mutants run it (Mutation testing, below).

## Test infrastructure

### `DirectoryWebApplicationFactory`

Starts the full `Program.cs` in the `Development` environment, then replaces logging with console output, replaces the
Service Bus client with a mock so a correction can be enqueued without a namespace, and registers
`IntegrationAuthHandler` as the default authentication scheme. Nothing else about authorization changes: the
`Directory` and `ChurchesMod` policies under test are the ones `Program.cs` declares. The `DbConnection` `Program.cs`
registers stays, so the tests run against the real database.

- **It refuses to start unless the catalog name ends in `Test` (or `Triage`)**, checked during service registration,
  before any test writes, so a production catalog can never be the target.
- **It sweeps the catalog before each test class and after it**, child tables first (`UserCorrections`,
  `MergeAuditLog`, `ChurchAttributes`, `ServiceSchedules`, `Ministries`, `Campuses`, `CrawlSources`, `Churches`),
  never `Denominations`, which the post-deployment script seeds. `CrawlSources` goes before `Churches` because its
  foreign key declares no `ON DELETE` action. `parallelizeTestCollections: false` makes a per-class sweep safe within
  one run; two runs against one catalog delete each other's rows.

### `IntegrationAuthHandler`

Issues a principal with `sub`, `scope=directory` and `churches.mod=true`, the claim shapes the identity provider
issues: `churches.mod` arrives as its own claim type, never as a `scope` value. Never add a claim here to make a test
pass; a fixture minting a claim the provider never issues makes every reader of it look correct.

### `FakeDb`

`FakeDbConnection` and its `FakeDbCommand`, `FakeDbParameter`, `FakeDbParameterCollection` and `FakeDbTransaction`
siblings (`Directory.Tests.Unit/TestSupport/`) are a hand-rolled ADO.NET double. Because all data access goes through
an abstract `DbConnection`, the unit tests assert the bound parameters and feed canned reader rows.
`FakeDbCommand.WithReaders` supplies the second result set the search query reads.

## Running tests locally

From the repo root.

### Unit

```powershell
dotnet build Directory.slnx --configuration Release
dotnet test --project Directory.Tests.Unit --no-build --configuration Release -- --filter-trait "Category=Unit"
```

### Integration

Needs a full SQL Server instance; LocalDB lacks the full-text features the schema uses. Publish the schema to a
catalog whose name ends in `Test`, then point the tier at it:

```powershell
dotnet tool install --global microsoft.sqlpackage
dotnet build Directory.Data/Directory.Data.sqlproj --configuration Release
sqlpackage /Action:Publish /SourceFile:Directory.Data/bin/Release/Directory.Data.dacpac /TargetConnectionString:"Server=localhost;Database=DirectoryTest;Integrated Security=true;TrustServerCertificate=true"

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:SqlConnectionStringBuilder__InitialCatalog = "DirectoryTest"
dotnet test --project Directory.Tests.Integration --no-build --configuration Release -- --filter-trait "Category=Integration"
```

`appsettings.Development.json` points at `localhost` with integrated security. `Program.cs` also requires
`ServiceBusConnectionString` outside Production, so set it (a User Secret or an environment variable); the factory
replaces the Service Bus client, so the tier sends nothing. README.md, "Configuration", lists the rest. A catalog published from an older schema fails every temporal read with
`InvalidCastException: Unable to cast object of type 'System.DateTime' to type 'System.DateTimeOffset'`; publish the
current dacpac to a fresh catalog.

## CI pipeline

The `build` job in `.github/workflows/main_crgolden-directory.yml`:

1. Release build, which also compiles `Directory.Data.sqlproj` to a `.dacpac`; `dotnet jb inspectcode`.
2. Unit tier under coverlet (`coverage.opencover.xml`).
3. Publish the schema to the integration catalog (`DB_NAME_E2E`) with `SqlPackage`.
4. Integration tier under `dotnet-coverage` (`coverage-integration.xml`) with `ASPNETCORE_ENVIRONMENT=CI`, then the
   full-text tier the same way (`coverage-fulltext.xml`).
5. SonarCloud analysis with the quality gate waited, then publish the app and upload the app and dacpac.

The job's `concurrency` group (`directory-integration-database`, `queue: max`) runs one build at a time, because every
run shares the integration catalog. The `mutation` job runs Stryker.NET's two passes. The deploy job publishes the dacpac to the
production catalog, then deploys the app.

## Mutation testing (Stryker)

**Thresholds are 100 (high, low and break).** Every mutant is killed by a test of either tier, marked equivalent with
`// Stryker disable once <Mutator> : <reason>` on the line above, or excluded in `stryker-config.json`'s `mutate` list:

| Excluded | Why |
|---|---|
| `obj/**/*.cs` | build-generated sources |
| `Program.cs` | startup wiring, excluded from coverage the same way |
| `**/*Endpoints.cs` | `[ExcludeFromCodeCoverage]` route wiring only the integration tier reaches; the services it calls are mutated |
| `**/Search/RelevanceOrder.cs` | mutated by the second pass instead, below |

**Two passes, so no mutant pays the full-text wait unless it needs to.** Stryker reruns a test for every mutant that
test covers. The keyword relevance test waits for SQL Server to index its rows on every run, so in the main pass it
would pay that wait once for each search mutant. `stryker-config.json` runs the unit and integration projects over
everything but the relevance ordering; `stryker-relevance-config.json` mutates only `Search/RelevanceOrder.cs` and runs
the unit and full-text projects, so only that file's few mutants rerun the keyword test. Both passes hold
thresholds of 100.

The integration and full-text projects run under Stryker, so a mutant in a SQL statement is killed by the statement
failing or returning the wrong rows against the real schema. That needs the integration tier's catalog: publish the schema and set
`SqlConnectionStringBuilder__InitialCatalog` first (Running tests locally, Integration). `concurrency` is `1` because
the factory sweeps the one catalog per test class, so two test runs at once delete each other's rows; the CI
`mutation` job shares the build job's `directory-integration-database` concurrency group for the same reason. Run it
from the `Directory` project folder, as CI does: `dotnet tool restore`, then
`dotnet stryker --config-file ../stryker-config.json`, then `dotnet stryker --config-file ../stryker-relevance-config.json`.
The report is local (`html`, `json`, `progress`).

## Local SonarCloud analysis

The analysis needs the repo's SonarCloud token and project, which only maintainers have. It runs through
`dotnet-sonarscanner` with the build between `begin` and `end`, as the workflow does; the standalone `sonar-scanner`
CLI analyzes no C#. It reads the unit report (OpenCover) and the integration and full-text reports (VS Coverage XML) and excludes
`Program.cs` from coverage.
