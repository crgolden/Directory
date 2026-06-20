# SEEDING.md

How to perform the initial bulk load of the nationwide church directory. The crawl pipeline
(`scrape-requests` → `extraction-requests` → ...) discovers and enriches churches one URL at a
time, which is not viable for cold-starting nationwide coverage. The `BulkImportJob` function seeds
the directory in bulk from public datasets, then hands each record to the normal geocoding + upsert
path so seeded rows are indistinguishable from crawled ones.

## Pipeline position

```
BulkImportJob (HTTP) ──► geocoding-requests ──► GeocoderWorker ──► [dbo].[Churches] (insert/link)
```

`BulkImportJob` only parses, deduplicates, and publishes. It never writes to SQL directly.
`GeocoderWorker` remains the single point of all church DB writes (it resolves coordinates via the
US Census Geocoder, falling back to `0,0`, then upserts).

## Trigger

HTTP, `AuthorizationLevel.Admin` (requires the function key):

```
POST /api/bulk-import?source=irs|osm&blobPath=<path-within-imports-container>
```

| Query param | Required | Default | Notes |
|---|---|---|---|
| `source` | no | `irs` | `irs` = IRS Form 990 nonprofit CSV; `osm` = OpenStreetMap Overpass JSON |
| `blobPath` | yes | — | Path to the dataset blob inside the `imports` container. 400 if missing, 404 if the blob does not exist |

The source file is read from the Azure Blob `imports` container on the `crgolden` storage account.
Upload the dataset there first, then call the endpoint with its blob path.

Response body: `{ "published": <n>, "skipped": <n> }`.

## Deduplication

Before publishing, each parsed record is checked against existing data:

```sql
SELECT 1 FROM [dbo].[Churches] WHERE [CanonicalName] = @Name AND [State] = @State
```

A name+state match is skipped (counted in `skipped`); everything else is published to
`geocoding-requests` (counted in `published`). This makes re-running an import idempotent at the
name+state granularity and safe to layer `irs` then `osm` over the same region.

## Expected dataset shapes

### IRS Form 990 (`source=irs`)

CSV with a header row. Column lookup is case-insensitive by name (order-independent):

| Column | Maps to | Required |
|---|---|---|
| `NAME` | `CanonicalName` | yes (row skipped if blank) |
| `STATE` | `State` | yes (row skipped if blank) |
| `STREET` | `Street` | no |
| `CITY` | `City` | no |
| `ZIP` | `Zip` | no |
| `NTEE_CD` | `WorshipStyle` (via NTEE mapping) | no |

NTEE → worship style: `X21` (Catholic) and `X22` (Orthodox) → `5` (Liturgical); everything else
→ `0` (Unknown). Seeded confidence: `0.5`. `PrimaryLanguage` defaults to `English`.

### OpenStreetMap Overpass (`source=osm`)

Overpass JSON export. Records come from the `elements` array; each element must have a `tags`
object:

| OSM tag | Maps to | Required |
|---|---|---|
| `name` | `CanonicalName` | yes (element skipped if blank) |
| `addr:state` | `State` | yes (element skipped if blank) |
| `addr:street` | `Street` | no |
| `addr:city` | `City` | no |
| `addr:postcode` | `Zip` | no |
| `phone` | `PhoneNumber` | no |
| `website` | `Website` | no |
| `email` | `EmailAddress` | no |

`WorshipStyle` is left `0` (Unknown). Seeded confidence: `0.6`. `PrimaryLanguage` defaults to
`English`.

## Recommended cold-start sequence

1. Pick a pilot region (Colorado was the planned first pass) to validate geocoding throughput and
   dedup behavior before going nationwide.
2. Upload the IRS CSV for that region to `imports`, then `POST /api/bulk-import?source=irs&blobPath=...`.
3. Layer the OSM Overpass export over the same region with `source=osm`; name+state dedup prevents
   duplicates against the IRS rows.
4. Watch `geocoding-requests` drain and confirm rows land in `[dbo].[Churches]` with non-zero
   coordinates (a `0,0` coordinate indicates a Census geocode miss, not a failure).
5. Expand region by region.

## Tests

`Functions.Tests/BulkImportJobTests.cs` covers IRS/OSM parsing, the NTEE→worship-style truth table,
dedup-before-insert, and Service Bus publish counts. See [Functions/TESTING.md](../Functions/TESTING.md).
