# QueryPlan frontend contract

The backend QueryPlan HTTP contract is available for frontend integration:

- `POST /api/application-data-center/query-datasets/query-plan/diagnose`
- `POST /api/application-data-center/query-datasets/query-plan/preview`
- `POST /api/application-data-center/query-datasets/query-plan/execute`
- Request: `ApplicationQueryPlanRequest`
- Responses: `ApplicationQueryPlanDiagnosticResponse` and `ApplicationQueryPlanResponse`

The frontend must use the shared HTTP, permission, and feedback primitives. It must transmit `dataSourceId`, object name, columns, filters, sorts, paging, typed parameters, and `auditId` through the DTO. It must not submit `rawSql` around the QueryPlan compiler.
