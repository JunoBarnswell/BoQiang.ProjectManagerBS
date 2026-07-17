# Data Studio Table Workbench Provider 注入证据

日期：2026-07-12

## 根因与调用链

`AsterErpApplicationDataCenterModule` 已通过 DI 注册
`ApplicationDataSourceProviderRegistry`，并注册
`ApplicationDataSourceTableWorkbenchService`。但 Table Workbench 将 Registry
声明为可选依赖，`ResolveProvider` 在缺少注入时重新构造四个 Provider，形成业务链路
内的第二套解析入口。

## 实现结果

- `ApplicationDataSourceTableWorkbenchService` 将 Registry 改为必需构造依赖。
- `ResolveProvider` 统一调用注入的 `providerRegistry.Resolve(sourceType)`。
- 删除业务服务内的 Registry、SQLite、MySQL、PostgreSQL、SQL Server fallback 实例化。
- 未修改其他服务、QueryPlan、Runtime、PreviewReader 或公共契约。
- 生产 DI 已完整提供该 Registry；现有 Table Workbench 测试继续传入真实四 Provider Registry。
- `TableWorkbench_RequiresProviderRegistryDependency` 固定构造参数不可选且无默认值；`NonTransactionalDdlFailurePersistsManualRecoveryAndAuditEvidence` 覆盖注入 Registry 的 Provider 能力实际进入 Workbench 链路。

## 验证

命令：

```text
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ApplicationDataSourceSqliteCatalogDdlViewTests|FullyQualifiedName~ApplicationDataSourceSchemaChangePlanImpactTests|FullyQualifiedName~ApplicationDataStudioSqliteIntegrationTests|FullyQualifiedName~ApplicationDataSourceTableRowServiceTests|FullyQualifiedName~ApplicationDataCenterProviderTests" --logger "console;verbosity=minimal"
```

结果：定向测试通过（含新增构造依赖契约测试）。

`git diff --check` 通过。本任务未执行 git commit。
