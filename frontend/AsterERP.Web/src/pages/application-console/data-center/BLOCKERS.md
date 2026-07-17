# QueryPlan frontend boundary

## Completed

- Query Dataset list preview and execution use the QueryPlan preview and execute endpoints.
- Frontend API exposes typed QueryPlan request, diagnostic, and response DTOs and three routes.
- QueryPlan requests do not contain `rawSql`; fields, filters, parameters, sorts, and paging come from structured Query Dataset configuration.
- View preview loads column metadata before calling QueryPlan preview and never falls back to raw SQL.

## Blocked: Mapping Cache

The backend Mapping Cache model currently contains `cacheKey`, `sql`, and cache metadata, but no proven source object, column list, type information, or structured filter/parameter contract. The frontend therefore blocks raw SQL execution and reports the boundary instead of guessing that `cacheKey` is a table or putting SQL into `RawSql`.

Required backend contract:

1. Persist a structured QueryPlan source object, columns, and parameters for each Mapping Cache; or
2. Expose an authoritative service-side conversion endpoint with explicit permission, audit, and response semantics.

This is a real integration blocker and keeps the related task open.
