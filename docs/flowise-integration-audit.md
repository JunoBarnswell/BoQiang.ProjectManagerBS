# Flowise Studio 集成审查记录

日期：2026-06-23

## 范围

- 来源菜单：`C:/Users/kuo13/Downloads/Flowise-main/Flowise-main/packages/ui/src/menu-items/dashboard.js` 的可见菜单。
- 来源路由：`packages/ui/src/routes/MainRoutes.jsx`、`CanvasRoutes.jsx`。
- 集成目标：AsterERP 原生壳层、原生 RBAC、原生 API、原生数据表、`zh-CN/en-US` 国际化。
- 排除项：Flowise 注释掉的 `Files` 菜单、Flowise 登录/注册/认证页、iframe、独立 Flowise Node 服务、业务 Bridge/Shim/Facade。

## 菜单与页面对照

| Flowise 菜单 | AsterERP 路由 | 后端权限 | 当前页面/组件 |
| --- | --- | --- | --- |
| Chatflows | `/flowise/chatflows` | `flowise:chatflows:view/edit/run/share/test` | `FlowiseFlowListPage` |
| Agentflows | `/flowise/agentflows` | `flowise:agentflows:view/edit/run` | `FlowiseFlowListPage` |
| Executions | `/flowise/executions` | `flowise:executions:view/manage` | `FlowiseExecutionsPage` |
| Assistants | `/flowise/assistants` | `flowise:assistants:view/edit` | `FlowiseResourcePage` + `FlowiseAssistantDetailPage` |
| Marketplaces | `/flowise/marketplaces` | `flowise:marketplaces:view/edit` | `FlowiseResourcePage` |
| Tools | `/flowise/tools` | `flowise:tools:view/edit` | `FlowiseResourcePage` |
| Credentials | `/flowise/credentials` | `flowise:credentials:view/edit`、`flowise:secret:reveal` | `FlowiseResourcePage` |
| Variables | `/flowise/variables` | `flowise:variables:view/edit` | `FlowiseResourcePage` |
| API Keys | `/flowise/api-keys` | `flowise:api-keys:view/edit`、`flowise:secret:reveal` | `FlowiseResourcePage` |
| Document Stores | `/flowise/document-stores` | `flowise:document-stores:view/edit/upsert` | `FlowiseResourcePage` + `FlowiseDocumentStoreDetailPage` |
| Datasets | `/flowise/datasets` | `flowise:datasets:view/edit` | `FlowiseResourcePage` + `FlowiseDatasetRowsPage` |
| Evaluators | `/flowise/evaluators` | `flowise:evaluators:view/edit` | `FlowiseResourcePage` |
| Evaluations | `/flowise/evaluations` | `flowise:evaluations:view/edit`、`flowise:run`、`flowise:retry` | `FlowiseResourcePage` + `FlowiseEvaluationResultPage` |
| SSO Config | `/flowise/sso-config` | `flowise:sso:manage`、`flowise:secret:reveal` | `FlowiseResourcePage` |
| Roles | `/flowise/roles` | `flowise:roles:manage` | `FlowiseResourcePage` |
| Users | `/flowise/users` | `flowise:users:manage` | `FlowiseResourcePage` |
| Workspaces | `/flowise/workspaces` | `flowise:workspaces:view/manage` | `FlowiseWorkspacesPage` |
| Login Activity | `/flowise/login-activity` | `flowise:login-activity:view/manage` | `FlowiseResourcePage` |
| Logs | `/flowise/logs` | `flowise:logs:view/manage/read` | `FlowiseResourcePage` |
| Account Settings | `/flowise/account` | `flowise:account:view/edit` | `FlowiseAccountSettingsPage` |

## 依赖页面与别名

| Flowise 原路径 | AsterERP 处理 |
| --- | --- |
| `/canvas/:id` | 重定向到 `/flowise/canvas/:id`，使用 `FlowiseCanvasPage` |
| `/agentcanvas/:id` | 重定向到 `/flowise/agentcanvas/:id`，使用 `FlowiseCanvasPage` |
| `/v2/agentcanvas/:id` | 直达 `/flowise/v2/agentcanvas/:id`，使用 Agent v2 节点与边样式 |
| `/marketplace/:id`、`/v2/marketplace/:id` | AsterERP 画布详情页 |
| `/document-stores/:storeId` 及 chunks/vector/query 依赖页 | `FlowiseDocumentStoreDetailPage` detail tab |
| `/dataset_rows/:id` | `FlowiseDatasetRowsPage` |
| `/evaluation_results/:id`、`/evaluation-results/:id` | `FlowiseEvaluationResultPage` |
| `/assistants/custom/:id` | `FlowiseAssistantDetailPage` |

## API 对照

| 能力 | API |
| --- | --- |
| 概览 | `GET /api/ai/flowise/overview` |
| 资源类型 | `GET /api/ai/flowise/resource-types` |
| 工作区 | `GET/POST/PUT/DELETE /api/ai/flowise/workspaces` |
| 通用资源 CRUD | `GET/POST /api/ai/flowise/{resourceType}`、`GET/PUT/DELETE /api/ai/flowise/{resourceType}/{id}` |
| 密钥查看 | `POST /api/ai/flowise/{resourceType}/{id}/reveal` |
| 导入导出 | `POST /api/ai/flowise/{resourceType}/import`、`POST /api/ai/flowise/{resourceType}/export` |
| 画布节点目录 | `GET /api/ai/flowise/canvas/nodes`、`GET /api/ai/flowise/nodes/definitions` |
| 画布读取/保存/校验 | `GET /api/ai/flowise/canvas/{resourceId}`、`POST|PUT /api/ai/flowise/canvas`、`POST /api/ai/flowise/canvas/validate` |
| Prediction/chat test | `POST /api/ai/flowise/prediction`、`POST /api/ai/flowise/prediction/feedback`、`POST /api/ai/flowise/prediction/lead` |
| 执行 | `GET /api/ai/flowise/executions`、`GET /api/ai/flowise/executions/{id}`、`POST /api/ai/flowise/executions/run` |
| 文档库详情 | `GET /api/ai/flowise/document-stores/{storeId}/detail`、`/files`、`/chunks`、`/vector-config`、`POST /api/ai/flowise/document-stores/query` |
| 评测详情 | `GET /api/ai/flowise/datasets/{id}/detail`、`GET /api/ai/flowise/datasets/{id}/rows`、`GET /api/ai/flowise/evaluators/{id}/detail`、`GET /api/ai/flowise/evaluations/{id}/detail`、`GET /api/ai/flowise/evaluations/{id}/result`、`POST /api/ai/flowise/evaluations/{id}/run-again` |
| 账号设置 | `GET/PUT /api/ai/flowise/account/settings` |

## 数据边界审查

- `ai_flowise_*` 表必须包含 `TenantId`、`AppCode`、`OwnerUserId`、`IsDeleted`。
- `AiCenterAppModule.RegisterDataFilters` 注册 Flowise workspace/owned data filter。
- `DataPermissionFilterRegistrar` 为 Flowise 表添加数据库侧 `TenantId + AppCode` 与 `OwnerUserId` 谓词。
- 资源删除为软删；画布保存对子节点/边采用事务性重建，避免同一 canvas 的残留边污染当前定义。
- 新增表覆盖节点定义、聊天消息、反馈、Lead、文档库文件/chunk、向量配置、dataset rows、evaluation results。

## 当前差异与风险

| 项 | 当前状态 | 风险/下一验证 |
| --- | --- | --- |
| Flowise 原生视觉像素级对齐 | 已重建 Flowise 功能结构和 AsterERP token 风格；尚未做截图差异量化 | 需要浏览器截图与原 Flowise 页面逐页对照 |
| 真实 LLM/工具运行时 | Prediction 生成 execution、message、source docs、feedback、lead；未接独立 Flowise Node runtime，按边界要求不引入 | 后续接入 AsterERP AI 能力时继续保持原生服务，不新增 bridge |
| Document Store loader process | 已有 detail/files/chunks/vector query 契约与页面；loader 上传/process 的完整后端处理仍需接口烟测确认 | API smoke 需覆盖空态、非法 store、chunk 查询 |
| Evaluation 结果图表 | 已有 result/metrics/version 页面；图表为 AsterERP 轻量统计视图 | 浏览器验收需确认 metrics、版本、run-again 状态 |
| 国际化 | `flowise.*` 文案已接入 `zh-CN/en-US`；旧通用资源页若仍有历史硬编码需扫尾 | 前端构建和切换语言人工验收必须检查裸 key/硬编码 |

## 验证状态

| 验证项 | 状态 | 证据/备注 |
| --- | --- | --- |
| `dotnet build AsterERP.sln --no-restore` | Pass | 2026-06-23 当前运行通过；仅保留既有 NuGet 漏洞警告 |
| `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj` | Pass | 2026-06-23 当前运行通过，88 passed / 0 failed / 0 skipped；保留既有 NuGet 漏洞警告 |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass | 2026-06-23 当前运行通过 |
| `cd frontend/AsterERP.Web && npm run build` | Pass | 2026-06-23 当前运行通过；动态 import 混用警告已清理，保留既有大 chunk 提示 |
| 认证 API smoke | Pass | 使用开发种子账号 `admin/admin123` 登录并切换 `tenant-system/SYSTEM`；验证 overview、chatflows、agentflows、canvas nodes、node definitions、executions、credentials、logs、创建 Chatflow、保存两节点一边画布、run execution、export 均返回 200；`wf_no_permission/noperm123` 访问 Flowise 返回 403 |
| 浏览器验收 | Pass | 使用 5173 Vite 默认代理 + 5000 API 进程登录进入系统管理；验证 `/flowise/chatflows`、进入 `/flowise/canvas/{id}`，截图保存在 `output/playwright/flowise-chatflows.png`、`output/playwright/flowise-canvas.png`；无 console error，画布检查器与校验提示已本地化 |

## 完成判定

- 上表所有 Pending 项必须变为 Pass 或写明 Blocked 原因。
- 不得以菜单可见、页面不白屏或匿名 401 代替业务验收。
- 若浏览器截图发现与 Flowise 原生功能结构不一致，需回到对应页面/组件补齐后再更新本文件。
