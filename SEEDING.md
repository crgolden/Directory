# SEEDING.md

Runbook for cold-start seeding the nationwide U.S. church directory (`[dbo].[Churches]` and its
child tables) from public data. The crawl pipeline (`scrape-requests` → `extraction-requests` → …)
discovers and enriches churches one URL at a time, which is not viable for cold-starting nationwide
coverage. Bulk import seeds the directory from public datasets, then hands each record to the normal
geocode → write path so seeded rows are indistinguishable from crawled ones.

The first national run completed **2026-06-24**: **245,548 churches across all 51 jurisdictions**
(50 states + DC), ~**84%** with real coordinates.

## Data sources

| Source | What | Coverage | Coordinates |
|---|---|---|---|
| IRS EO BMF | Exempt Organizations Business Master File, filtered to `NTEE_CD` starting with `X` (religion-related) | ~203,600 church rows | None natively → pre-geocoded via Census batch (~80% hit), remainder geocoded inline at write time |
| OSM Overpass | `amenity=place_of_worship` + `religion=christian` per state | ~61,300 elements | Native (node `lat`/`lon` or way/relation `center`) |

Dedup across sources is by **name + state**, so layering IRS then OSM is safe.

## Scripts (`Tools/Seeding/`)

Run in this order. All are idempotent and default to all 50 states + DC; pass `-States WY` to scope.
The `data/` output cache is gitignored (hundreds of MB) — it is regenerated, never committed.

| # | Script | Purpose |
|---|---|---|
| 1 | `Get-IrsChurchData.ps1` | Downloads the IRS regional BMF files (`eo1`–`eo4`), filters to churches, writes per-state `data/<st>/eo_<st>_churches.csv` (`NAME,STREET,CITY,STATE,ZIP,NTEE_CD`). Caches raw downloads in `data/irs-raw`. |
| 2 | `Get-OsmChurchData.ps1` | Overpass query per state → `data/<st>/<st>_osm.json` (`out center tags`). One state at a time with backoff on 429/504. |
| 3 | `Add-CensusBatchGeocode.ps1` | Pre-geocodes the IRS CSVs via the US Census **batch** geocoder (≤10k addresses/request, far faster than per-record), appending `Latitude`/`Longitude` columns. 1,000-row batches; ~80% match. OSM needs no pass (native coords). Idempotent: skips CSVs already carrying a `Latitude` column unless `-Force`. |
| 4 | `Invoke-DirectorySeeding.ps1` | Uploads each per-state file to the `imports` blob container and POSTs the Functions `bulk-import` endpoint (IRS then OSM), state by state, with a drain delay between states. Needs `-AccountKey`/`$env:SEED_STORAGE_KEY` and the local Functions host running. |

## Pipeline

`Invoke-DirectorySeeding` → `BulkImportJob` (HTTP, `AuthorizationLevel.Admin`,
`POST /api/bulk-import?source=irs|osm&blobPath=<path-in-imports-container>`) parses the file, dedups
against existing name+state keys (one set-based query up front), and publishes `GeocodingRequest`
messages to the **`geocoding-requests`** Service Bus queue in `Chunk(100)` `SendMessagesAsync`
batches. Response body: `{ "published": <n>, "skipped": <n> }`.

```
bulk-import → geocoding-requests → GeocoderWorker → ChurchWriter (parent + denomination + attributes,
              one transaction) → confidence-requests → CalculateConfidenceScore
```

- **`ChurchWriter` is the single DB writer.** `GeocoderWorker` delegates to it; idempotent under
  Service Bus redelivery via the `DuplicateExistsAsync` (name+state) guard. The dedup-then-guard pair
  means a record published twice (e.g. IRS + OSM before the IRS write drained) never duplicates a row.
- **Geocoding precedence:** pre-geocoded IRS coords / OSM native coords are used as-is; otherwise
  `GeocoderWorker` calls the Census **single-record** endpoint at write time. Anything still unmatched
  is stored at `0,0`.

## Field mapping

### IRS (`source=irs`) — CSV with header, columns matched case-insensitively by name

| Column | Maps to |
|---|---|
| `NAME` | `CanonicalName` (row skipped if blank) |
| `STATE` | `State` (row skipped if blank) |
| `STREET` / `CITY` / `ZIP` | `Street` / `City` / `Zip` |
| `NTEE_CD` | `WorshipStyle` (`X21`/`X22` → `5` Liturgical, else `0`); `DenominationName` (`X22` → `Roman Catholic`, else null); emitted as a `ntee_code` `ChurchAttribute` (source `irs`) |
| `Latitude` / `Longitude` | pre-geocoded coords (optional; added by `Add-CensusBatchGeocode.ps1`; `0,0` treated as not-geocoded) |

Seeded confidence `0.5`; `PrimaryLanguage` defaults to `English`.

### OSM (`source=osm`) — Overpass JSON `elements[]`, each with a `tags` object

| OSM tag | Maps to |
|---|---|
| `name` | `CanonicalName` (skipped if blank) |
| `addr:state` | `State` — **normalized** to a 2-letter code (skipped if unmappable; see gotchas) |
| `addr:city` / `addr:postcode` | `City` / `Zip` — **both required** (`NCHAR` NOT NULL) or the element is skipped |
| `addr:housenumber` + `addr:street` | combined into `Street` |
| `phone` / `website` / `email` | `PhoneNumber` (first number only, dropped if >20 chars) / `Website` / `EmailAddress` |
| `denomination` | `DenominationName` via slug map (e.g. `baptist` → `Baptist`); emitted with `website`/`phone`/`email` as `ChurchAttribute`s (source `osm`) |

Seeded confidence `0.6`; coordinates from node `lat`/`lon` or way/relation `center`.

## Local-host seeding (temporary prod-write config)

The seed runs the Functions project **locally** (`func start`) pointed at **production** SQL + Service
Bus + storage, because the seed-pipeline functions only exist in the working tree (not yet deployed).
This requires temporary, **uncommitted** changes that MUST be reverted afterward:

1. `Functions/local.settings.json`:
   - `SqlConnectionStringBuilder__*` → prod (`DataSource=crgolden.com`, SQL auth `directory` user) instead of localhost/IntegratedSecurity.
   - `AzureWebJobsStorage` → the real `crgolden` storage connection string (not `UseDevelopmentStorage=true`; use the real account rather than running Azurite).
   - `ServiceBusConnection` / `StorageConnectionString` → shared-key connection strings.
2. `Functions/host.json`: a `"functions": [ ... ]` allow-list naming only the seed-pipeline functions
   (`BulkImportJob`, `GeocoderWorker`, `CalculateConfidenceScore`, `ReGeocodeJob`) to quiesce the
   scraper/extractor/enrichment/email/timer functions locally.
3. Enable shared-key access on the locked-down prod resources (normally disabled):
   ```powershell
   az storage account update --name crgolden --resource-group crgolden --allow-shared-key-access true
   az servicebus namespace update --name crgolden --resource-group crgolden --disable-local-auth false
   ```

### Run

```powershell
$env:SEED_STORAGE_KEY = (az storage account keys list --account-name crgolden --query "[0].value" -o tsv)
# 1-3: acquire + pre-geocode data (skip if data/ already populated)
Tools/Seeding/Get-IrsChurchData.ps1
Tools/Seeding/Get-OsmChurchData.ps1
Tools/Seeding/Add-CensusBatchGeocode.ps1
# Start the host (from Functions/Functions), wait for "Host lock lease acquired"
func start
# 4: drive the seed
Tools/Seeding/Invoke-DirectorySeeding.ps1 -AccountKey $env:SEED_STORAGE_KEY
```

Watch `geocoding-requests` drain and confirm no dead-letters:

```powershell
az servicebus queue show --namespace-name crgolden --resource-group crgolden --name geocoding-requests `
  --query "countDetails.{active:activeMessageCount, dead:deadLetterMessageCount}" -o json
```

### Teardown (re-lock — do this when seeding is verified)

1. Revert the `local.settings.json` and `host.json` temp changes.
2. Re-disable shared-key access (return to the normal locked-down state):
   ```powershell
   az storage account update --name crgolden --resource-group crgolden --allow-shared-key-access false
   az servicebus namespace update --name crgolden --resource-group crgolden --disable-local-auth true
   ```
3. Restart the deployed app: `az functionapp restart --name crgolden-functions --resource-group crgolden`.
   It uses **Managed Identity** for storage + Service Bus (`*__credential` / `*__fullyQualifiedNamespace`),
   so disabling shared-key does not affect it.

## Gotchas & operational notes

- **Service Bus is Basic tier**: 256 KB max message/batch, 1 GB queue, 14-day TTL, max-delivery 10.
  `Chunk(100)` of these small messages stays well under the batch limit; the SDK's default retry
  policy absorbs transient `ServiceBusy` throttling. The whole ~200k-message backlog (<200 MB) fits
  in the queue at once, so a fast publisher can't overflow it.
- **OSM `addr:state` is inconsistent** — some records carry a full state name (`Ohio`), a hand-typed
  abbreviation (`W. Va.`), or junk (`-IL`). `[dbo].[Churches].State` is `NCHAR(2)`, so unnormalized
  values fail with *"String or binary data would be truncated … column 'State'"* and dead-letter.
  `BulkImportJob.ParseOsm` normalizes via `NormalizeState` (2-letter passthrough → full-name map →
  punctuation-strip → null-and-skip). If a batch of OSM dead-letters with this signature predates the
  fix, re-publish the affected states' OSM (`-Source osm -States ...`) on the fixed build — dedup
  skips everything already written, only the previously-failed rows insert.
- **Geocoding ceiling ≈ 84%.** A meaningful share of the `0,0` rows are genuinely Census-unmatchable
  (PO boxes, rural routes, malformed/incomplete street data — verified directly against the Census API
  for real samples). `ReGeocodeJob` (admin HTTP `POST /api/re-geocode?max=N`) re-runs the Census
  single-record lookup on `0,0` rows. Its query originally had no `ORDER BY`, so once a batch of a given
  size failed 100%, every identical-size retry deterministically returned the *same* stuck rows forever
  — small repeated batches could look like a ~0% recovery rate even though larger one-off calls (e.g.
  `max=5000`) found real matches further into the table. Fixed with `ORDER BY NEWID()` so each call
  samples fresh rows. A second, larger issue found afterward: PO Box addresses have no street-level
  TIGER/Line match, so Census's address geocoder can *never* resolve them — a live production sample
  found roughly three-quarters of all `0,0` rows were PO Boxes, so a random batch was overwhelmingly
  wasted on permanently-unresolvable rows even with fresh sampling. `ReGeocodeJob`'s candidate query
  now excludes PO Box addresses (`NOT LIKE 'PO BOX%'` etc., the same exclusion already used in
  `DeduplicationJob`), so retries spend their budget only on rows that can plausibly resolve. The true
  steady-state recovery rate on the remaining real-street-address rows hasn't been re-measured
  post-fix. A better geocoder (Google/Mapbox) remains the path to materially higher coverage beyond
  whatever this recovers — and neither this nor a better geocoder does anything for PO Box rows, which
  need a fundamentally different strategy (e.g. ZIP-centroid fallback) if they're ever to leave `0,0`.
- **Dead-letter purge**: `az` cannot purge DLQ messages. Receive+complete them via the SDK — load
  `Azure.Messaging.ServiceBus.dll` (from the Functions output) in PowerShell, create a receiver with
  `SubQueue = DeadLetter` on `geocoding-requests`, and complete in a loop until empty.

## Verification

```sql
SELECT COUNT(*) AS Total,
       COUNT(DISTINCT State) AS States,                                    -- expect 51
       SUM(CASE WHEN Latitude<>0 OR Longitude<>0 THEN 1 ELSE 0 END) AS Geocoded,
       SUM(CASE WHEN LEN(State)<>2 THEN 1 ELSE 0 END) AS BadState          -- must be 0
FROM dbo.Churches;
```

## Tests

`Functions.Tests/BulkImportJobTests.cs` covers IRS/OSM parsing, the NTEE→worship-style and
denomination truth tables, OSM `addr:state` normalization, dedup-before-insert, and Service Bus
publish counts. See [Functions/TESTING.md](../Functions/TESTING.md).
