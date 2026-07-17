# Worker C 发布、权限、监控与安全证据

## 真实链路覆盖

| 失败路径 | 证据 | 判定 |
| --- | --- | --- |
| 发布 artifact 内容被篡改 | `ApplicationDevelopmentPublishSecurityReliabilityTests.Publisher_rejects_tampered_artifact_before_any_publish_rows_are_inserted` | 发布器在 artifact 入库前校验 canonical hash；SQLite 中 artifact、publish record 和正式指针均不产生变更 |
| 已发布运行 artifact 被篡改 | `RuntimePageSchemaServiceTests.PublishedPage_RejectsArtifactContentTamperingBeforeReturningRuntimeSchema` | 真实 RuntimePageSchemaService 在返回运行数据前拒绝 hash 不匹配内容 |
| 重复发布相同内容 | `ApplicationDevelopmentPublishSecurityReliabilityTests.Publisher_reuses_the_same_artifact_and_publish_record_for_duplicate_content` | 内容寻址复用同一 artifact 和 publish record，正式指针不漂移 |
| 发布请求取消 | `ApplicationDevelopmentPublishSecurityReliabilityTests.Cancelled_publish_does_not_insert_artifact_or_advance_the_previous_pointer` | 已取消 token 不插入产物/记录，不推进上一正式 artifact 指针 |
| 发布事务失败 | `ApplicationDevelopmentReliabilityTests.Publish_failure_inside_the_service_transaction_does_not_update_the_document_pointer` | SQLite trigger 注入失败后 artifact、record 和文档状态均回滚 |
| 运行权限拒绝 | `RuntimePageSchemaServiceTests.PublishedPage_DeniesUserWithoutViewPermission` | 已认证但没有页面 view permission 的用户收到 `PermissionDenied`；不是匿名 401 证据 |
| 租户/应用隔离 | `RuntimePageSchemaServiceTests.PublishedPage_IsBoundToCurrentTenantAndApplication` | 当前工作区不能读取其他租户的正式页面 |
| 监控敏感字段 | `ApplicationDevelopmentPublishSecurityReliabilityTests.Monitoring_schema_allows_only_redacted_context_fields`、`HAO107SecurityGateTests.Secret_summary_is_public_metadata_only_and_never_contains_ciphertext_or_plaintext` | 监控 context 采用显式白名单，禁止 Secret、ciphertext、连接字符串、原始 SQL、参数和业务 payload；Secret 公开摘要只包含 metadata |

## 验证命令

```powershell
dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ApplicationDevelopmentPublishSecurityReliabilityTests|FullyQualifiedName~RuntimePageSchemaServiceTests|FullyQualifiedName~ApplicationDevelopmentReliabilityTests|FullyQualifiedName~HAO107SecurityGateTests"
```

测试必须在 API 已停止、测试数据库为临时 SQLite 的条件下执行；不得使用匿名 401、Mock 成功或占位 provider 替代真实服务/SQLite 结果。

## 未伪造的阻断项

- 取消路径已由真实 SQLite 验证；确定性的数据库/Provider 超时需要受控真实 provider 或生产拥有的 fault-injection/timeout seam，当前 Worker C 只允许修改测试与证据文件，不能伪造超时通过。
- HAO-99 的正式维护窗口、授权 operator、真实备份恢复、重启后 API/UI 冒烟和上一正式 artifact 回滚演练仍需外部运维环境；本地测试不能把它们标记为 Pass。
- Designer/Data Studio 监控事件的生产发送与 API 侧接收尚无真实调用证据；本文件只记录 schema/Runtime 诊断守卫，不宣称端到端观测链路已完成。
