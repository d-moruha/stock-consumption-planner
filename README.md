# stock-consumption-planner

Creates consumption plans from current stock reported by SAP.

A planner imports an SAP stock report, creates consumption plans against that stock, and exports the plans for analytical tools.

## Glossary

**Material movement document** — one row of the SAP MB51 report, identified by its document id and carrying a current quantity.

**Current stock** — the quantity recorded on a material movement document in that report.

**Plan** — a consumption plan for a stock row and a target month (year + month), recording the planned quantity. A plan is linked to stock by the SAP compound key (material document year, number, and item), not by the stock row id, which changes on every import.

**Orphaned plan** — a plan whose stock record is missing from the current (Active) stock report.

## Features

### Current stock import

Current stock is imported from an `.xlsx` file. The file is a report from a custom SAP ERP transaction based on MB51 material movement documents. The report includes each material movement document and its current quantity.

- **Input:** the `.xlsx` report.
- **Result:** stock rows that can be planned against, each identified by material movement document id and carrying its current quantity.

### Consumption plans

Plans support create, read, update, and delete. Each plan is related to stock through the material movement document key.

- **Input:** the stock record key, the target month, and the planned quantity.
- **Result:** a stored plan that can be read, updated, and deleted.

Rules:

- **One plan per stock record per month** (unique index on key + target month).
- **Planned quantity** must be greater than zero and no more than the current stock quantity when saved. If a later import lowers the stock below the plan, the plan is kept and flagged as exceeding stock.
- **Create:** allowed for the current month and any future month.
- **Update and delete:** allowed during the first 7 days after the plan was created, or otherwise while more than 14 days remain before the target month starts. Month boundaries use the `Europe/Kyiv` time zone.
- **Orphaning:** after each stock import, plans without an Active stock record are marked orphaned. Orphaned plans are read-only (they can still be deleted under the rule above) and re-link automatically when their stock record appears in a later import.
- **Cleanup:** a background job deletes plans that have been orphaned for 6 months or more. It runs at startup and then daily.

### Plans export

Plans can be exported for further processing by analytical tools.

- **Input:** the plans to export.
- **Result:** an export of those plans.

## Data

Stock and plans meet on one field: the material movement document id.

| Record | Known fields |
| --- | --- |
| Stock | Material movement document key (year, number, item), current quantity |
| Plan | Stock key, target year and month, planned quantity, material snapshot, created/updated/orphaned timestamps, version |

## Solution Structure (Vertical Slice Architecture)

- `src/StockConsumptionPlanner.App` — Single runnable ASP.NET Core (.NET 10) project organized by feature slices under `Features/`:
  - `Features/StockReport/InspectStockReport/` — `InspectStockReportEndpoint`, `InspectStockReportHandler` (`DocumentFormat.OpenXml` inspection & Ukrainian/English header auto-suggestion), and inspection DTOs.
  - `Features/StockReport/ImportStockReport/` — `ImportStockReportEndpoint`, `ImportStockReportHandler` (mapped-only row parsing), and import/mapping DTOs.
  - `Features/StockReport/GetCurrentStockReport/` — `GetCurrentStockReportEndpoint` and `CurrentStockReportDto`.
  - `Features/StockReport/Shared/` — `StockReportField`, `StockReportFieldCatalog`, `StockReportBatch` / `StockReportItem` models (`StockLifecycleStatus`), stock entity configurations, `IStockReportRepository` (`EfCoreStockReportRepository`), `IStockPromotionParticipant` (hook that runs inside the import promotion transaction), and `ExcelWorksheetHelper`.
  - `Features/StockReport/StockImportPage.razor` — Co-located Blazor UI page (`/stock-import`).
  - `Features/StockReport/StockReportFeatureExtensions.cs` — `AddStockReportFeature()` (persistence provider selection) and `MapStockReportEndpoints()`.
  - `Features/ConsumptionPlans/` — `CreatePlan/`, `UpdatePlan/`, `DeletePlan/`, `GetPlans/`, `GetPlanningSheet/` (endpoint, handler, contracts per slice); `ReconcileOrphans/` (orphan marking and re-linking on import); `PurgeOrphans/` (background cleanup); `Shared/` (`ConsumptionPlan` entity, `PlanEditPolicy`, options, errors, read model); Blazor pages `ConsumptionPlansPage.razor` (`/consumption-plans`) and `OrphanedPlansPage.razor` (`/consumption-plans/orphaned`); `AddConsumptionPlansFeature()` and `MapConsumptionPlanEndpoints()`.
- `src/StockConsumptionPlanner.App/Persistence/` — shared `PlannerDbContext` (each feature contributes `IEntityTypeConfiguration` classes) and the startup schema initializer (`EnsureCreated`).
- `tests/StockConsumptionPlanner.Tests` — xUnit unit and integration tests organized under `Features/StockReport/` and `Features/ConsumptionPlans/`.

## Persistence Configuration (`Sqlite` | `Postgres`)

Persistence is configured via `Persistence:Provider` in `appsettings.json` or environment variables:

- **Single deployment unit (Default — `Sqlite`)**:
  - `Persistence:Provider`: `"Sqlite"`
  - `ConnectionStrings:Sqlite`: `"Data Source=data/stock-consumption-planner.db"`
- **Multi-instance / PostgreSQL deployment (`Postgres`)**:
  - `Persistence:Provider`: `"Postgres"`
  - `ConnectionStrings:Postgres`: `"Host=postgres;Port=5432;Database=stock_planner;Username=postgres;Password=postgres"`

The schema is created on startup with `EnsureCreated`, which does nothing if any table already exists. A database created by an older build (before consumption plans) must be deleted once — the SQLite `.db` file or the `sqlite_data` / `postgres_data` Docker volumes — so it is recreated with the new tables.

## Consumption Plan Configuration (`ConsumptionPlans`)

| Setting | Default | Meaning |
| --- | --- | --- |
| `EditDaysAfterCreation` | `7` | Days after creation during which a plan can always be changed. |
| `LockDaysBeforeTargetMonth` | `14` | Otherwise, a plan can be changed until this many days before its target month starts. |
| `TimeZoneId` | `Europe/Kyiv` | Time zone in which target months start. |
| `OrphanRetentionMonths` | `6` | Orphaned plans older than this are deleted. |
| `PurgeInterval` | `1.00:00:00` | How often the cleanup job runs; `00:00:00` disables it. |

## Target Material Document Fields

| Field Key | Type | Notes |
|---|---|---|
| `MaterialDocumentNumber` | `string` | Preserves leading zeros |
| `MaterialDocumentItem` | `string` | Preserves leading zeros (e.g. `0001`) |
| `MaterialDocumentYear` | `int` | 4-digit year (also matches `MaterialDccoumentYear`) |
| `MaterialNumber` | `string` | Material / nomenclature code |
| `MaterialDescription` | `string` | Ukrainian / English material description |
| `MaterialQuantity` | `decimal` | Supports `.` and `,` decimal separators |
| `MaterialValue` | `decimal` | Supports `.` and `,` decimal separators |

## API Endpoints

- `POST /api/stock-reports/inspect` — Inspects an uploaded `.xlsx` file, returns worksheet names, detected columns with sample values, and heuristic Ukrainian/English column mapping suggestions.
- `POST /api/stock-reports/import` — Parses only the 7 mapped columns starting from `DataStartRowNumber`, ignores all unmapped columns, stores the current batch in `IStockReportRepository`, and returns summary metrics and row-level errors.
- `GET /api/stock-reports/current` — Returns the currently stored stock report batch and summary totals.
- `GET /api/consumption-plans?year=&month=&status=all|linked|orphaned&search=&page=&pageSize=` — Lists plans with current stock quantity, orphan state, purge date, and `editableUntilUtc`.
- `GET /api/consumption-plans/{id}` — Returns one plan.
- `GET /api/consumption-plans/planning-sheet?year=&month=&search=&page=&pageSize=` — Returns current stock rows with each row's plan for the month, plus whether new plans are allowed.
- `POST /api/consumption-plans` — Creates a plan `{ materialDocumentYear, materialDocumentNumber, materialDocumentItem, targetYear, targetMonth, plannedQuantity }`.
- `PUT /api/consumption-plans/{id}` — Updates `{ plannedQuantity, version }`.
- `DELETE /api/consumption-plans/{id}?version=` — Deletes a plan.
- Plan errors are returned as ProblemDetails with a `code` field: 400 `Validation`, 404 `NotFound`, 409 `DuplicatePlan` / `StaleVersion`, 422 `EditPeriodClosed`, `TargetMonthInPast`, `StockNotActive`, `AmbiguousStock`, `QuantityExceedsStock`, `PlanOrphaned`.
- `GET /healthz` — Returns service health and active persistence provider (`Sqlite` by default, or `Postgres`).

## Running with Docker

Single-container deployment with persistent SQLite volume (`sqlite_data` mounted at `/app/data`):

```bash
docker compose up --build
```

With PostgreSQL (`postgres` profile):

```bash
PERSISTENCE_PROVIDER=Postgres docker compose --profile postgres up --build
```

The application listens on `http://localhost:8080`.
