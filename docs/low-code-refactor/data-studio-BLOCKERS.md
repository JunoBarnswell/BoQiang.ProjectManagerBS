# Data Studio 外部验证阻断

## Four-provider integration gate (2026-07-12)

Status: **In Progress / Blocked for external evidence**.

The provider implementations and local SQLite contract tests are present for
SQL Server, MySQL, PostgreSQL and SQLite, but this workspace has no Docker
executable and no authorized values for:

- `ASTERERP_TEST_SQLSERVER_CONNECTION`
- `ASTERERP_TEST_MYSQL_CONNECTION`
- `ASTERERP_TEST_POSTGRESQL_CONNECTION`
- `ASTERERP_TEST_SQLITE_CONNECTION`

Therefore no external provider Pass is claimed. The remaining gate requires a
real authorized connection for each provider and must capture server/version,
catalog, provider-specific quoting, SchemaChangePlan DDL, typed DML with
concurrency, view replacement/compensation, cancellation/timeout and audit
permission evidence. Mocks, anonymous responses and seeded placeholder output
do not close this blocker.

## Mapping Cache to QueryPlan contract

Status: **Blocked on Coordinator contract decision**.

The backend already persists structured mapping-cache state in
`ApplicationMappingCacheEntity`, `ApplicationMappingCacheColumnEntity` and
`ApplicationMappingCacheParameterEntity`, and
`ApplicationMappingCacheWorkbenchService` reconstructs typed source,
columns and parameters for provider-quoted execution. However,
`ApplicationQueryPlanRequest` currently exposes only `DataSourceId` and
`ObjectName`; `ApplicationQueryPlanCompiler` treats that value as a physical
table/schema identifier. There is no stable mapping-cache reference or
parameter-binding field, so QueryPlan cannot safely consume a mapping cache.

Coordinator must freeze a backend/shared contract such as an explicit
mapping-cache reference and typed parameter bindings before implementation.
Until then the backend must reject/avoid implicit cache-key-as-table or raw SQL
conversion; no frontend bypass closes this blocker.

## Latest Resource ID QueryPlan resolution (2026-07-12)

Status: **Resolved for the shared contract; external provider evidence remains blocked**.

`ApplicationQueryPlanRequest` and the Query Model now use stable table and field
Resource IDs end to end. Catalog table/column responses expose those IDs, and
Query Dataset preview/runtime/publish require the persisted latest QueryPlan.
Legacy object-name and field-code configuration is rejected at the dataset
boundary rather than converted at runtime.
## 当前状态

本地 SQLite Data Studio 链路可执行；四库真实 provider 验证不能在当前工作区宣称通过。

## 阻断证据

- Docker 可执行文件不可用。
- 未提供 `ASTERERP_TEST_SQLSERVER_CONNECTION`。
- 未提供 `ASTERERP_TEST_MYSQL_CONNECTION`。
- 未提供 `ASTERERP_TEST_POSTGRESQL_CONNECTION`。
- 未提供 `ASTERERP_TEST_SQLITE_CONNECTION`。

## 未完成验证

在获得授权的 SQL Server、MySQL、PostgreSQL 和外部 SQLite 连接后，仍需分别验证：

- provider catalog 与 schema/view metadata；
- SchemaChangePlan 的真实 DDL、事务/补偿/回滚和失败审计；
- typed row edit、原值/版本并发与取消；
- view candidate replacement、清理和旧视图恢复；
- 权限过滤、连接诊断和 redacted audit trace。

不得使用 mock、匿名 401、占位连接或伪造 seed 结果替代上述证据；连接串和凭据不得写入仓库或测试输出。
