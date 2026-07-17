# HAO-148 / HAO-155 Designer migration boundary

## 根因

`ApplicationDesignerDocumentStore.SaveAsync` 原先先读取当前文档，再无条件更新文档行；`expectedHash` 只在应用层比较，未进入 UPDATE 条件。两个编辑器因此可以同时生成相同 revision number，后提交者覆盖先提交者。首次创建依赖唯一索引报错，不能稳定返回领域层 CAS 冲突。

迁移链路已有 `ApplicationDesignerMigrationRunEntity` 记录运行尝试，但没有记录“旧 schema 已完成退役”的持久水位。应用重启只能重复探测迁移输入，schema 初始化也没有把退役状态作为可验证契约。

## 实现边界

- Designer 保存对已有文档使用数据库侧 `DocumentHash` CAS；revision 先落在同一事务中，CAS 失败会删除本次 revision，并返回 `ApplicationDevelopmentPageRevisionConflict`。
- 相同 canonical hash 仍是幂等成功，不新增 revision；已有文档缺少 `expectedHash` 的变更保存被拒绝。
- 新增 `ApplicationDesignerMigrationWatermarkEntity`，以 workspace/database 退役键记录 source/target schema fingerprint、状态和时间；`ApplicationDevelopmentCenterSchemaMigrator` 仅在旧表和旧 draft 列均不存在时写入 `Retired` 水位，并校验重复启动的水位契约。
- schema initializer、ABP data filter、application development schema migrator 和 module-file-map 均声明该实体；不新增 Adapter/Bridge/Shim，也不改变认证、表达式、Workflow 或 Data Center provider 链路。

## 验收 Case

1. 同 canonical hash 重复保存：返回原 revision，revision 数不变（Pass/Fail）。
2. 已有文档缺失或携带旧 `expectedHash` 保存：返回 CAS 冲突，current revision/hash 不变（Pass/Fail）。
3. 数据库写入失败或取消：文档、revision 和 pointer 均不产生半成品（Pass/Fail）。
4. schema initializer/migrator 重复执行：watermark、表和索引保持单份，状态为 `Retired`（Pass/Fail）。
5. 旧 `system_page_schemas` 或 draft 列仍存在：不写退役水位，迁移失败时不宣告完成（Pass/Fail）。

## 验证

```powershell
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --filter "FullyQualifiedName~ApplicationDevelopmentMigrationTests|FullyQualifiedName~ApplicationDevelopmentReliabilityTests"
dotnet build AsterERP.sln
```

## HAO-155 runtime DDL retirement boundary

- `DbMigrationService` invokes `EnsureCurrentSchemaAsync` only. Startup creates
  latest Designer-owned tables/indexes and validates the committed retirement
  watermark; it never reads, renames, migrates, or drops historical schema.
- `RunDeploymentMigrationAsync` is the sole deployment entrypoint (the
  compatibility `MigrateAsync` delegates to it). It records an immutable
  inventory/backup manifest, acquires the global Designer migration-run lock,
  runs the historical document/page migration, retires legacy storage, validates
  latest publish data and commits the `Retired` watermark.
- Missing, pending, or incompatible watermarks fail startup closed. A deployment
  failure records a failed migration run and leaves the watermark non-retired;
  runtime DDL is not used as a recovery mechanism.
- Application Data Center audit writes are insert-only and do not call
  `CodeFirst.InitTables`; application database schema creation remains owned by
  its explicit baseline initializer.
