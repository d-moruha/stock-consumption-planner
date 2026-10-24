# stock-consumption-planner

Creates consumption plans from current stock reported by SAP.

A planner imports an SAP stock report, creates consumption plans against that stock, and exports the plans for analytical tools.

## Glossary

**Material movement document** — one row of the SAP MB51 report, identified by its document id and carrying a current quantity.

**Current stock** — the quantity recorded on a material movement document in that report.

**Plan** — a consumption plan for a stock row. A plan is linked to stock by material movement document id.

## Features

### Current stock import

Current stock is imported from an `.xlsx` file. The file is a report from a custom SAP ERP transaction based on MB51 material movement documents. The report includes each material movement document and its current quantity.

- **Input:** the `.xlsx` report.
- **Result:** stock rows that can be planned against, each identified by material movement document id and carrying its current quantity.

### Consumption plans

Plans support create, read, update, and delete. Each plan is related to stock through the material movement document id.

- **Input:** the plan and the material movement document id of the stock it applies to.
- **Result:** a stored plan that can be read, updated, and deleted.

### Plans export

Plans can be exported for further processing by analytical tools.

- **Input:** the plans to export.
- **Result:** an export of those plans.

## Data

Stock and plans meet on one field: the material movement document id.

| Record | Known fields |
| --- | --- |
| Stock | Material movement document id, current quantity |
| Plan | Material movement document id of the related stock |

## Solution Structure

- `src/StockConsumptionPlanner.App` — Runnable ASP.NET Core (.NET 9) host with Blazor UI (`/`, `/stock-import`), module registration, and `/healthz` health check endpoint.
- `src/Modules/StockReport/StockConsumptionPlanner.Modules.StockReport.Contracts` — Public DTOs, target field catalog (`StockReportField`), and column mapping contracts.
- `src/Modules/StockReport/StockConsumptionPlanner.Modules.StockReport` — Domain models, `IStockReportRepository` (`InMemoryStockReportRepository`), `ClosedXmlStockReportService` inspector/parser, and `/api/stock-reports` Minimal API endpoints.
- `tests/StockConsumptionPlanner.Modules.StockReport.Tests` — xUnit unit and integration tests.

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
- `GET /healthz` — Returns service health and active persistence provider (`InMemory`).

## Running with Docker

```bash
docker compose up --build
```

The application listens on `http://localhost:8080`.
