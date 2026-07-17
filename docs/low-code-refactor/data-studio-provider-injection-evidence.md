# Data Studio Provider 注入闭环证据

日期：2026-07-12

## 根因与调用链

`AsterErpApplicationDataCenterModule` 已通过 DI 注册四个
`IApplicationDataSourceProvider` 和一个 `ApplicationDataSourceProviderRegistry`。
但 `ApplicationDataSourceService`、`ApplicationDataSourceTableRowService`、
`ApplicationQueryDatasetService` 将 Registry 声明为可选依赖，并在业务方法内重新
构造四 Provider Registry 作为 fallback，导致同一请求存在多套 Provider 解析入口。

## 实现结果

- 三个服务将 `ApplicationDataSourceProviderRegistry` 改为必需构造依赖。
- `ApplicationDataSourceService` 的诊断、表读取和 Provider 解析统一使用注入 Registry。
- `ApplicationDataSourceTableRowService` 的分页、读写和并发能力判断统一使用注入 Registry。
- `ApplicationQueryDatasetService` 的 QueryPlan Provider 解析统一使用注入 Registry。
- 删除三个服务内的 `new ApplicationDataSourceProviderRegistry`、固定四 Provider 实例和 nullable fallback。
- 测试构造补齐真实四 Provider Registry；未修改 PreviewReader、Resource ID QueryPlan 生产文件、RuntimePageSchemaService 或公共契约。

## 验证

命令：

```text
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ApplicationDataSourceTableRowServiceTests|FullyQualifiedName~ApplicationDataSourceSqliteCatalogDdlViewTests|FullyQualifiedName~ApplicationQueryPlanServiceTests|FullyQualifiedName~ApplicationDataStudioSqliteIntegrationTests|FullyQualifiedName~ApplicationDataCenterProviderTests|FullyQualifiedName~ApplicationDataSourceSchemaChangePlanImpactTests|FullyQualifiedName~ApplicationDataSourceDraftDiagnosticContractTests" --logger "console;verbosity=minimal"
```

结果：69/69 通过。

`git diff --check` 通过。本任务未执行 git commit。
