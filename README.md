# Stock Consumption Planner

Modular monolith ASP.NET Core (.NET 9) application with Blazor Server UI and Minimal APIs for importing current stock reports (`.xlsx`) with Ukrainian and English headers and interactive column mapping.

## Solution Structure

- `src/StockConsumptionPlanner.App` — Runnable ASP.NET Core host with Blazor UI (`/`, `/stock-import`), module registration, and `/healthz` health check endpoint.
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
