# Low-Code reliability and monitoring contract

## Latest event contract

The current contract is defined by `runtime-monitoring-event.schema.json` and
`frontend/AsterERP.Web/src/runtime-kernel/RuntimeMonitoringContract.ts`.
The only valid event names are:

`designer.command`, `designer.command.failed`, `designer.save`,
`designer.publish`, `designer.migration`, `runtime.render`, `runtime.action`,
`runtime.binding.error`, `dataStudio.connection.test`,
`dataStudio.catalog.refresh`, `dataStudio.query.execute`,
`dataStudio.schema.deploy`, and `dataStudio.data.write`.

Each event uses `eventName` plus a typed `context`; legacy `operation` and
`phase` fields are not part of the latest contract. Runtime diagnostics now
emits the `runtime.render`, `runtime.action`, and `runtime.binding.error`
shapes and validates cancellation, timeout, outcome, required context, and
unknown-field rules. Designer and Data Studio producers still require
production call-site wiring and API-side ingestion before this quality gate
can be marked Done. Until that evidence exists, the gate remains In Review or
Blocked rather than claiming complete production observability.

本契约定义低代码 Designer、Runtime 和 Data Studio 的统一可观测性事件格式。事件契约的机器可读定义位于 [`runtime-monitoring-event.schema.json`](./runtime-monitoring-event.schema.json)，TypeScript 强类型定义位于 `frontend/AsterERP.Web/src/runtime-kernel/RuntimeMonitoringContract.ts`。

## 统一事件集合

事件必须使用以下 13 个 `eventName` 之一。不得新增未登记事件，不得恢复旧的 `operation/phase` 双轨字段，也不得引入 v3/v4、Bridge、Shadow Rendering 或兼容占位事件。

| eventName | 必填上下文 | 适用范围 |
| --- | --- | --- |
| `designer.command` | `commandId`、`commandType` | Designer 命令开始/完成结果 |
| `designer.command.failed` | `commandId`、`commandType` | Designer 命令失败 |
| `designer.save` | `documentId`、`revision` | Designer 文档保存 |
| `designer.publish` | `documentId`、`revision`、`artifactHash` | Designer 发布 artifact |
| `designer.migration` | `documentId`、`migrationId` | Designer 迁移 |
| `runtime.render` | `documentId`、`revision`、`artifactHash` | Runtime artifact 加载/渲染 |
| `runtime.action` | `actionId`、`actionType` | Runtime 动作执行 |
| `runtime.binding.error` | `bindingPath` | Runtime 绑定或依赖重算错误 |
| `dataStudio.connection.test` | `connectionId` | Data Studio 连接测试 |
| `dataStudio.catalog.refresh` | `connectionId`、`catalogId` | Data Studio Catalog 刷新 |
| `dataStudio.query.execute` | `connectionId`、`queryId` | Data Studio 查询执行 |
| `dataStudio.schema.deploy` | `connectionId`、`schemaName` | Data Studio Schema 部署 |
| `dataStudio.data.write` | `connectionId`、`resourceKind`、`affectedRows` | Data Studio 数据写入 |

## 事件结构

每个事件必须包含：

- `eventId`：非空事件标识。
- `eventName`：上表中的精确字面量。
- `occurredAt`：ISO 8601 日期时间。
- `outcome`：只能是 `succeeded`、`failed`、`cancelled` 或 `timedOut`。
- `durationMs`：大于等于 0 的耗时。
- `cancellationRequested`：取消状态布尔值。
- `context`：对象，只能包含契约登记的上下文字段。

`context` 可按事件适用范围携带以下脱敏字段：`tenantId`、`appCode`、`traceId`、`documentId`、`revision`、`artifactHash`、`commandId`、`commandType`、`migrationId`、`actionId`、`actionType`、`bindingPath`、`connectionId`、`catalogId`、`queryId`、`schemaName`、`resourceKind`、`affectedRows`、`auditId`、`requestHash` 和 `backupSha`。不得写入 Secret、原始 SQL 参数、完整连接字符串或业务载荷。

`errorCode` 只允许是稳定、可聚合的错误编码：`failed`、`cancelled` 和 `timedOut` 必须提供；`succeeded` 不得提供。`timedOut` 必须提供大于 0 的 `timeoutMs`，`cancelled` 必须满足 `cancellationRequested=true`；非取消事件不得设置 `cancellationRequested=true`。

未知事件名、未知顶层字段、未知上下文字段、缺失公共字段、缺失事件专属上下文或结果状态不一致，均为契约校验失败。TypeScript 校验入口为 `validateRuntimeMonitoringEvent`，类型守卫为 `isRuntimeMonitoringEvent`。

## 当前接入边界

### 已接入 RuntimeDiagnostics

`RuntimeDiagnostics` 已使用统一 `eventName/context` 结构输出 Runtime 诊断事件：

- artifact 加载成功/失败映射为 `runtime.render`；
- 动作成功/失败/取消/超时映射为 `runtime.action`；
- 依赖重算取消映射为 `runtime.binding.error`，并保留稳定错误码；
- `RuntimeDiagnostics.exportMonitoring()` 输出的事件和计数均为诊断快照，不改变业务状态，也不负责重试或回滚业务操作。

该接入只覆盖 RuntimeDiagnostics 已有入口，不代表 Designer 或 Data Studio 的业务操作已经自动产生事件。

### 尚未接入的生产调用方

以下生产调用方当前尚未接入统一事件发射：

- Designer：命令、保存、发布和迁移业务流程尚未在真实成功/失败/取消/超时边界调用对应 `designer.*` 事件；
- Data Studio：连接测试、Catalog 刷新、查询执行、Schema 部署和数据写入业务流程尚未调用对应 `dataStudio.*` 事件；
- 后端审计持久化、真实 API/UI 链路和 Data Studio 数据库审计仍需由各自业务模块完成，不能以 RuntimeDiagnostics 快照或本地契约测试替代。

因此，本次契约工作完成的是统一规范、Schema、TypeScript 校验和 RuntimeDiagnostics 既有事件映射；Designer/Data Studio 生产接入不在当前改动范围内，不能据此宣称端到端事件链路已完成。

## 状态与恢复不变量

| 场景 | 成功状态 | 失败/取消/超时状态 | 必须保留的恢复指针 |
| --- | --- | --- | --- |
| Document 保存 | canonical JSON、hash、revision 一起提交 | 文档、revision、hash 保持旧值 | `expectedHash`、旧 revision、traceId |
| Runtime 发布 | artifact、publish record、页面发布指针同一事务提交 | 不留下半个 artifact 或错误发布指针 | previous artifact、previous revision、backup location |
| Data Studio Schema 部署 | SchemaChangePlan 按事务执行并写审计 | 回滚脚本或补偿动作完成后保留旧 schema | plan hash、数据库 provider、auditId |
| Data Studio 数据写入 | 影响行数符合服务端确认值 | 取消/超时回滚当前事务并写入最终审计结果 | auditId、request hash、affected rows |
| Designer 迁移 | 应用分组全部迁移并通过健康检查 | 整组回滚，migration run 标记 `Failed` | backup SHA、rollback pointer、health check |

## 超时、取消与幂等

- 后端 I/O 必须将 `CancellationToken` 传到 ORM、连接、命令和审计写入边界；审计写入是独立安全义务，业务取消后仍须尽力写入最终事件。
- 命令超时必须产生 `timedOut`，客户端主动终止必须产生 `cancelled`，两者不得以普通 `failed` 替代。
- 保存按 canonical hash 幂等；相同 hash 不新增 revision；过期 `expectedHash` 必须拒绝并保持旧文档。
- 发布按 `(tenantId, appCode, documentId, artifactHash)` 幂等；重复请求不得重复生成发布记录或改变上一正式 artifact 指针。
- Data Studio DDL/DML 重试必须携带同一 `auditId`/request hash，并重新通过权限、风险和影响行数确认；不得通过客户端重试绕过确认。

## 验证与证据边界

- `Pass`：契约 Schema/TypeScript 校验和直接测试通过，并有对应真实生产调用方的授权 API/UI、数据库或发布环境证据。
- `Fail`：事件名、字段、状态一致性、权限、审计、回滚、取消或超时任一条件不满足。
- `Blocked`：真实 Designer/Data Studio 调用方未接入、维护窗口、授权 operator、真实数据库凭据、百万行数据或正式回滚演练不可用。Blocked 必须记录缺失证据和恢复条件，禁止伪造生产结果。

当前状态：RuntimeDiagnostics 契约测试和 TypeScript typecheck 已通过；Designer/Data Studio 生产事件接入及真实端到端证据保持 `Blocked`，本地契约测试不能替代该证据。
