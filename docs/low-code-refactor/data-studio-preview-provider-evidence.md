# Data Studio 预览 Provider 方言闭环

日期：2026-07-12

## 根因

`ApplicationDataPreviewReader.ResolvePreviewSql` 原先直接拼接 `LIMIT`，绕过了
`ApplicationDataSourceProviderRegistry` 和 `IApplicationDataSourceProvider.BuildPreviewSql`。
因此 SQL Server 预览会生成不支持的 SQLite/MySQL/PostgreSQL 方言；Reader 的行数上限和取消链路虽然存在，但没有经过当前数据库 Provider 的预览 SQL 能力。

## 实现

- `ApplicationDataPreviewReader` 注入现有 `ApplicationDataSourceProviderRegistry`。
- 根据 `ISqlSugarClient.CurrentConnectionConfig.DbType` 解析唯一当前 Provider。
- SQL 和表预览统一调用 `provider.BuildPreviewSql`；SQL Server 由 Provider 生成 `TOP`，其他数据库使用其自身实现。
- 表名通过 Provider 的 `QuoteQualified` 生成，schema 能力由 Provider capability 校验。
- 保留原有默认值、`1..100` Reader 行数上限、Provider 上限校验和前后取消检查。
- 更新所有相关测试的手工 Reader 构造，改为注入现有 Provider Registry。

## 验证

通过：

```text
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ApplicationDataCenterProviderTests|FullyQualifiedName~ApplicationDataStudioSqliteIntegrationTests|FullyQualifiedName~ApplicationDataSourceSqliteCatalogDdlViewTests|FullyQualifiedName~ApplicationDataSourceTableRowServiceTests|FullyQualifiedName~ApplicationDataSourceSchemaChangePlanImpactTests|FullyQualifiedName~ApplicationDataSourceDraftDiagnosticContractTests|FullyQualifiedName~ApplicationDataModelOperationPolicyTests"
结果：70/70

dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~RuntimeCompositeDataModelServiceTests|FullyQualifiedName~ApplicationDataCenterProviderTests|FullyQualifiedName~ApplicationDataStudioSqliteIntegrationTests|FullyQualifiedName~ApplicationDataSourceSqliteCatalogDdlViewTests|FullyQualifiedName~ApplicationDataSourceTableRowServiceTests"
结果：87/87
```

`FullyQualifiedName~ApplicationData` 扩大范围验证被仓库其他并行改动阻断：
`backend/AsterERP.Api/Application/Runtime/RuntimePageSchemaService.cs` 当前编译报缺失
`SystemPageSchemaEntity`。该文件不属于本任务范围，本任务未修改它。

本任务未提交代码；QueryPlan Resource ID 文件未修改。
