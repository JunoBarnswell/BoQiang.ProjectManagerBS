# Data Studio latest implementation acceptance

## Scope

The verified chain is:

`authorized request -> ABP Application Service -> provider capability/quoting ->
SchemaChangePlan/query plan -> cancellable execution -> typed result -> immutable audit`

The provider layer is limited to database syntax, metadata and driver differences.
Permissions, tenant/application scope, risk, confirmation, transaction decisions,
row limits and audit decisions remain in the Application layer. Secret responses
contain only `secretRef`, `hasSecret` and `updatedAt`; SQLite paths are resolved by
the tenant/application sandbox or an explicitly audited approval.

## Automated evidence in this checkout

The following evidence is real local execution, not a mock or anonymous HTTP result:

- `ApplicationDataCenterProviderTests`: all four provider implementations are
  checked for identifier quoting, single-statement read classification, bounded
  pagination, DDL validation, preview limits and provider-specific view behavior.
- `ApplicationDataProviderCapabilityMatrixTests`: the matrix is parsed and compared
  field-by-field with the current provider capability objects and catalog queries;
  missing external credentials is asserted as `Blocked` policy.
- `ApplicationDataSourceConnectionFactoryTests`: MySQL/PostgreSQL/SQL Server
  secure defaults and invalid TLS modes are checked, and a canceled SQLite build
  is rejected before sandbox resolution.
- `ApplicationDataSourceSecurityBoundaryTests` and
  `ApplicationDataStudioSqliteIntegrationTests`: protected Secret summaries never
  echo plaintext/ciphertext; SQLite sandbox traversal/absolute paths are rejected;
  real catalog refresh, SchemaChangePlan DDL, failed-plan preservation, view
  candidate replacement, typed Secret resolution and audit rows are exercised.
- `ApplicationDataSourceTableRowServiceTests`: real SQLite typed insert/update/
  delete, composite keys, original-value/version concurrency, conflict audit and
  read-only DML rejection are exercised.
- `ApplicationDataSourceExternalProviderGateTests`: when a real provider
  connection is supplied, it executes `SELECT 1`, provider-specific DDL and DML.
  When an environment variable is absent, the test prints an explicit
  `BLOCKED` line and does not claim a provider Pass. When a supplied connection
  is unreachable or the probe fails, the test fails the command; that failure
  must be triaged as a blocked external environment, not relabeled as Pass.

## External provider gate

| Provider | Required connection variable | Current status | Evidence requirement |
|---|---|---|---|
| SQL Server | `ASTERERP_TEST_SQLSERVER_CONNECTION` | **Blocked** | real connection, server/image version, catalog, DDL/DML, cancellation and audit trace |
| MySQL | `ASTERERP_TEST_MYSQL_CONNECTION` | **Blocked** | real connection, server/image version, catalog, DDL/DML, cancellation and audit trace |
| PostgreSQL | `ASTERERP_TEST_POSTGRESQL_CONNECTION` | **Blocked** | real connection, server/image version, catalog, DDL/DML, cancellation and audit trace |
| SQLite | `ASTERERP_TEST_SQLITE_CONNECTION` | **Blocked** for external gate; local sandbox suite passes | resolved sandbox path, approval audit, catalog, DDL/DML, cancellation and audit trace |

Probe performed in the current workspace on 2026-07-12:

- Docker executable: **missing** (`Get-Command docker` returned no executable).
- `ASTERERP_TEST_SQLSERVER_CONNECTION`: **missing**.
- `ASTERERP_TEST_MYSQL_CONNECTION`: **missing**.
- `ASTERERP_TEST_POSTGRESQL_CONNECTION`: **missing**.
- `ASTERERP_TEST_SQLITE_CONNECTION`: **missing**.

Therefore no SQL Server/MySQL/PostgreSQL external result is claimed. The gate is
`Blocked`, not Pass, and the Linear tasks must remain open until authorized real
containers or approved servers and credentials are available. Credentials and
connection strings must never be committed or printed in evidence.

## Security and failure policy

- Missing container, unavailable endpoint, missing credential or failed connection
  diagnostic is `Blocked` with provider, stage and sanitized reason.
- Invalid identifier, unsafe SQL fragment, unsafe TLS mode, Secret echo, SQLite
  path traversal, unconfirmed write, stale optimistic-concurrency value or excess
  affected rows is a failing safety case; it must not be downgraded to Blocked.
- DDL/DML, physical view changes and controlled SQL must pass an audit-sink
  schema availability check before the external operation starts. The check is
  read-only and must not create/delete audit evidence. A missing audit writer,
  unavailable audit table, failed business audit insert or failed audit
  finalization is fail-closed: the operation must not be reported as successful;
  committed or unknown external state must be marked for recovery. Every
  persisted audit row is server-scoped to tenant/app and carries trace/operator
  identity.
- View audit permission codes must match the endpoint action: QueryDataset Add,
  Edit or Delete respectively. A broad DataSource publish permission is not a
  substitute for the action-specific minimum permission.
- Anonymous `401`, mocked provider clients, seeded fake server output and
  placeholder success are not acceptance evidence.

## Acceptance status

Code and local SQLite automated coverage may move to `In Review`. HAO-18,
HAO-69–89, HAO-107 and HAO-113 cannot move to `Done` until the external provider
gate and authorized API/UI traces are executed and attached to the corresponding
Linear issues.

Local SQLite evidence is `EvidencePresent` for the named automated cases, not a
release `Pass`. The external gate remains `Blocked` until real provider and
authorized API/UI evidence is retained.

## Executable decision record

| Evidence slice | Command or artifact | Current decision | Release pass condition | Blocked condition |
| --- | --- | --- | --- | --- |
| Local Data Studio | `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ApplicationData` | EvidencePresent | SQLite catalog, DDL, view, typed row, secret, sandbox, cancellation and audit cases pass with retained output | Test project/build/output unavailable |
| HAO-113 boundary | `npm test --prefix frontend/AsterERP.Web -- --run src/pages/application-console/development-center/low-code-studio/testing/acceptance/pageStudioHao113Acceptance.test.ts` | EvidencePresent | Structured query request, current editor formatting and typed parameters pass | Vitest cannot start or assertion fails |
| Four-provider gate | `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~ApplicationDataSourceExternalProviderGateTests` with all `ASTERERP_TEST_*_CONNECTION` variables | Blocked | Four real providers return catalog, DDL/DML, cancellation and audit evidence | Docker or authorized credential/endpoint is unavailable |
| Authorized browser/API | Controlled restarted Release API + Vite browser trace | Blocked | Tenant/RBAC, overview, source catalog, row edit, view/DDL failure and audit trace are retained | No authorized trace, screenshot, or production-like provider evidence |

The external gate is intentionally not inferred from local SQLite, test-source
existence, anonymous `401`, or a browser page that merely loaded successfully.
