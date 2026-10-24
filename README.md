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
