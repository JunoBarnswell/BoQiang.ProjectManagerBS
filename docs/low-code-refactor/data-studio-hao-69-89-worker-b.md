# HAO-69~89 Data Studio Worker B 真实 SQLite 审计记录

## 范围

本 Worker 只新增 `backend/AsterERP.Api.Tests` 的 Data Studio 真实 SQLite
集成测试和本证据文档，没有修改生产代码、前端、共享 DTO 或 CI。

测试夹具创建真实的应用 SQLite 数据库和真实的 sandbox 相对路径 SQLite
数据源，使用正式的连接工厂、provider registry、Catalog service、Table
Workbench、View Workbench、Application Service、SqlSugar repository 和
审计写入器；没有 Mock 数据库或伪造 provider。

## 新增测试

- `ApplicationDataStudioSqliteFixture.cs`
  - 创建租户/应用边界内的 `data/application-databases/tenant-a/MES/studio.db`。
  - 初始化真实表、复合主键、索引、触发器和数据。
  - 组装正式 Application/Data Studio 服务链。
- `ApplicationDataStudioSqliteIntegrationTests.cs`
  - Catalog 全量快照、列/PK/index/trigger 读取、版本 lineage、节点刷新和变更记录。
  - SchemaChangePlan 风险、确认、真实建表、计划状态和审计。
  - DDL 失败后的对象保留、计划失败状态和失败审计。
  - View 候选校验、旧视图保留和候选清理。
  - Secret 加密后连接解析、详情与 PublicConfig 不回显明文。
  - sandbox 相对路径、路径穿越和 NUL 路径拒绝。

## 定向验证

命令：

```text
dotnet build backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ApplicationDataStudioSqliteIntegrationTests
```

编译通过。定向测试 6 个 Case 中 2 个通过、4 个失败；失败不是测试环境缺少 SQLite，
而是真实链路暴露出的生产缺口，故不能标记 HAO-69~89 完成：

1. `ApplicationDataSourceSchemaChangePlanEntity.EstimatedAffectedRows` 未声明
   `IsNullable = true`，SqlSugar CodeFirst 在 SQLite 生成 NOT NULL 列，而
   `PersistPlanAsync` 写入 null，导致 DDL 计划无法持久化。
2. SQLite Catalog 对复合主键同时读取 table constraint/index 结果时，
   `sqlite_autoindex_*` 被重复展开，`BuildChanges` 的字典键冲突，Catalog
   刷新无法完成。
3. View Workbench 的 SQLite 创建链将 `CancellationToken` 传入了 SqlSugar
   的参数重载，真实执行报 `The parameter format is wrong`，因此 View 创建
   和后续候选替换无法进入可验证状态。
4. View 更新失败路径在 `ReplacePhysicalViewAsync` 抛出后没有调用失败审计；
   即使修复创建链，仍需补齐失败/取消审计才能满足“所有写操作不可关闭审计链”。

通过的 Case：Secret 不回显、sandbox 路径边界。失败的 Case 保留为回归守卫，
不得用 Skip、Mock 或测试数据改写来伪造 Pass。

## 其他审计结论

- 复合主键并发已有 `ApplicationDataSourceTableRowServiceTests` 的真实 SQLite
  覆盖；本次 Catalog Case 进一步证明复合主键元数据处理仍有缺口。
- Query Model 无 `RawSql` fallback 已由现有
  `ApplicationQueryPlanCompilerTests` 和 Data Studio acceptance tests 覆盖，
  本次未重复建立同义测试。
- SQL Server、MySQL、PostgreSQL 容器和授权凭据本轮不可用，未以匿名 401、Mock
  或占位结果代替真实证据；四数据库集成验收仍为 Blocked。
