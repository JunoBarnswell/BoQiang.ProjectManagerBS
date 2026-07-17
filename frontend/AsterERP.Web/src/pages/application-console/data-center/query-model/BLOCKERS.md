# Query Model contract status

## Resolved in the latest structured contract

`ApplicationQueryPlanRequest` now carries typed `nodes`, `joins`, `columns`, `filters`, `groupBy`, `having`, `sorts`, paging, typed parameters, aggregate/function selections, and controlled-write metadata. `QueryModelDesigner` maps the complete model to this contract; the backend compiler validates joins, quoting, parameters, grouping, aggregates, paging, cancellation, and provider SQL.

The designer no longer reduces JOIN/GROUP BY/HAVING/aggregate models to the first table or routes them through raw SQL. Save, Diagnose, and Preview use the structured QueryPlan endpoints and fail closed on unsupported shapes.

## Remaining explicit boundary

Views and Mapping Caches whose persisted definition is only raw SQL still lack an authoritative structured source-object/column/parameter contract. Their SQL remains a definition-maintenance input, not a normal-user QueryPlan execution bypass; the UI must report this boundary until the backend persists or exposes a typed conversion contract.
