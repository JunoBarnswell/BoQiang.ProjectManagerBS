# RuntimeArtifact 发布与消费闭环证据

## 根因

正式页面链路仍有消费者从旧页面 schema 读取，导致设计文档、发布产物和运行时资源之间没有唯一指针。回滚虽然切换了发布记录，但运行资源、工作流表单资源和平台发布依赖快照仍可能读取旧 schema。

## 本次实现

- `ApplicationDevelopmentCenterService` 的正式详情、列表、发布和菜单指针统一使用 `ApplicationDesignerDocumentEntity.PublishedArtifactId`。
- `ApplicationDesignerArtifactRollbackService` 只校验并切换已发布 `ApplicationDesignerRuntimeArtifactEntity`，同时更新文档发布指针、页面运行产物指针、菜单运行产物指针和不可关闭审计记录。
- `WorkflowFormResourceAppService` 按租户、应用、页面、文档、产物五层边界读取已发布 RuntimeArtifact，并校验 hash、manifest、signature、runtimeContext.pageCode 和最新契约。
- `PlatformApplicationPublishRunner` 的发布依赖快照从已发布 Page + DesignerDocument + RuntimeArtifact 生成，不再查询旧页面 schema；发布前拒绝缺失、跨工作区、篡改或契约过期的产物。

## 验证

```text
dotnet build AsterERP.sln --configuration Release --no-restore
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ApplicationDesignerArtifactRollbackTests|FullyQualifiedName~ApplicationDevelopmentCenterAbpModuleTests|FullyQualifiedName~ApplicationPublishRuntimeContractTests|FullyQualifiedName~PlatformPublishAbpModuleTests" -m:1
```

结果：构建 0 警告、0 错误；定向测试 26/26 通过。

## 风险与回滚

- 旧数据库字段仍作为一次性迁移期间的物理字段存在，但不再作为正式运行读取源；删除旧字段必须等迁移、全量回归和维护窗口演练完成后执行。
- 若正式产物校验失败，发布任务进入阻断状态，不会回退到旧 schema；按发布记录中的上一 `ArtifactId` 执行受审计回滚。
