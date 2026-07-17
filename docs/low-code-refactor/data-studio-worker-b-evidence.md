# Data Studio Worker B 真实链路证据

## 范围

本证据只覆盖 HAO-69～HAO-89 中可由本地 SQLite 真实文件验证的 provider、catalog、DDL、view、typed row、Secret 和路径 sandbox 链路。测试没有使用 Mock 数据库、匿名 401 或占位结果；测试代码只位于 `backend/AsterERP.Api.Tests`。

## 已通过

| Case | 测试 | 结果 |
| --- | --- | --- |
| SQLite Secret 不回显 | `ApplicationDataSourceSecurityBoundaryTests.SecretProtector_PublicSummaryNeverReturnsCipherOrPlaintext` | Pass |
| SQLite sandbox 真实文件 | `ApplicationDataSourceSecurityBoundaryTests.SQLiteConnectionFactory_UsesWorkspaceSandboxForRealDatabaseFile` | Pass；实际创建并查询 sandbox 内 SQLite 文件 |
| 路径逃逸与未审批绝对路径 | `ApplicationDataSourceSecurityBoundaryTests.SQLiteSandbox_RejectsTraversalAndAbsolutePathWithoutApproval` | Pass |
| 复合主键双编辑器并发 | `ApplicationDataSourceTableRowServiceTests.CompositePrimaryKey_ConcurrentEditorsRejectStaleOriginalValues` | Pass；第二次陈旧原值写入返回 Conflict，并写入失败审计 |
| 复合主键类型转换 | `ApplicationDataSourceTableRowServiceTests.CompositePrimaryKey_ConvertsValuesAndRequiresExactConfirmation` | Pass |

## 本次修复结果

四个真实 SQLite 失败 Case 均通过生产链路验证，测试没有改为 Skip 或“断言当前异常”：

1. `ApplicationDataSourceSqliteCatalogDdlViewTests.TableSchemaChangePlan_DeploysRealSqliteTableAndAuditsAppliedStatus`
   - `EstimatedAffectedRows` 由实体可空映射和 schema migrator 的重建逻辑共同保证为 nullable；Unknown 计划可真实持久化并部署。
2. `ApplicationDataSourceSqliteCatalogDdlViewTests.TableSchemaChangePlan_RequiresConfirmationAndFailedDuplicateLeavesAuditEvidence`
   - 重复部署进入真实 SQLite 事务失败路径，旧对象保留并写入失败审计。
3. `ApplicationDataSourceSqliteCatalogDdlViewTests.CatalogRefresh_ReadsRealSqliteKeysIndexesTriggersAndNodeChanges`
   - SQLite provider 只将显式索引建模为 indexes，排除 `sqlite_autoindex_*`；复合主键 Catalog 刷新和 Change Diff 可用。
4. `ApplicationDataSourceSqliteCatalogDdlViewTests.ViewWorkbench_UsesCandidateValidationAndCompensationOnInvalidReplacement`
   - View DDL 使用正确的 SqlSugar 无参数执行重载；候选验证/替换失败会保留旧视图并写入失败审计，创建 DDL 失败也写入失败审计。

## 验证命令

```powershell
dotnet build backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~ApplicationDataSourceSecurityBoundaryTests|FullyQualifiedName~ApplicationDataSourceSqliteCatalogDdlViewTests|FullyQualifiedName~ApplicationDataSourceTableRowServiceTests"
```

当前定向结果：15 tests，15 passed；其中 `ApplicationDataSourceSqliteCatalogDdlViewTests` 为 5/5，原始四个失败 Case 为 4/4。安全、sandbox、typed row 和新增的 View 创建失败审计 Case 均已实际通过。

本次使用干净构建验证：

```text
dotnet clean backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj -p:UseSharedCompilation=false -m:1
已成功生成。0 个警告，0 个错误
dotnet build backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1
已成功生成。0 个警告，0 个错误
dotnet test ... --filter "FullyQualifiedName~ApplicationDataSourceSqliteCatalogDdlViewTests"
测试总数: 5， 通过数: 5
dotnet test ... --filter "FullyQualifiedName~ApplicationDataSourceSecurityBoundaryTests|FullyQualifiedName~ApplicationDataSourceSqliteCatalogDdlViewTests|FullyQualifiedName~ApplicationDataSourceTableRowServiceTests"
失败: 0，通过: 15，跳过: 0，总计: 15
```

## 交付边界

本次只修改 Data Studio TableWorkbench、Catalog、ViewWorkbench 及直接 provider 依赖、对应真实链路测试和本证据文档；未修改公共 DTO、前端、CI 或用户数据库文件。
