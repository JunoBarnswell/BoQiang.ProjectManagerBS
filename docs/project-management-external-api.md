# 项目管理对外 API V1

基础路径为 `/api/project-management/external/v1`。调用使用既有 OAuth/会话身份；不会接受租户、工作区或数据范围作为请求参数。

读取接口：

- `GET /projects`：分页项目查询，需要 `project-management:project:view`。
- `GET /projects/{projectId}/tasks`：分页任务查询，需要 `project-management:task:view`。
- `GET /projects/{projectId}/milestones`：分页里程碑查询，需要 `project-management:milestone:view`。

写入接口：创建/更新任务、创建/更新评论、上传并关联任务附件。每个写入均要求 `Idempotency-Key`（8 至 160 字符）；更新还要求 `If-Match`，且必须与 body 中 `VersionNo` 一致。重复的相同请求返回首次结果并标记 `data.replayed=true`；相同键的不同请求、正在执行的请求或已失败请求返回 `409` 和错误码 `42068`。

可选 `X-Integration-Source` 记录调用来源（最长 128 字符）。所有响应包含现有 `ApiResult` 的 `traceId`，并返回 `X-Trace-Id` 与 `X-API-Version: v1`。写入按已认证用户分区限流，默认每 60 秒 60 次，可由 `ProjectManagement:ExternalApiRateLimitPermitCount` 与 `ProjectManagement:ExternalApiRateLimitWindowSeconds` 配置。

对外入口只编排既有 Application 服务，因此授权、项目成员访问策略与 ORM 数据权限过滤和 UI 使用同一实现。`pm_external_api_requests` 账本记录调用用户、来源、操作、请求摘要、TraceId、资源、成功结果或失败信息，避免重复写入并保留审计证据。
