# AsterERP AI 智能中心实施记录

日期：2026-06-17  
执行人：Codex

## 1. 目标与边界

本次目标是将 AI 智能中心主链路统一到 Semantic Kernel / SK Agent 原生能力：管理员配置 OpenAI-compatible 供应商和模型，用户创建多会话聊天，后端以 POST SSE 流式返回思考链与正文，消息、Run、Usage、Call Log、上下文快照、安全策略和权限数据可持久化、可追踪、可回显；协作 Agent、Process、RAG SQLite Vec、MCP/OpenAPI 插件只在官方 SK 包/API 可用时启用，不允许用旧自研链路或桥接层替代。

本次不引入独立微服务，不新增承载主业务的 Bridge/Shim/Facade，不提交任何 API Key 或密钥材料。真实 DeepSeek/GLM 连接测试需要现场录入 API Key；缺少 Key 时仅连接测试为 Blocked，其余驱动协议用单元测试覆盖。

## 2. 职责边界

后端契约：
- `backend/AsterERP.Contracts/Ai/*`：AI DTO、请求、查询和治理契约。
- `backend/AsterERP.Shared/Common/PermissionCodes.Ai.cs`：AI 权限码。
- `backend/AsterERP.Shared/Common/ErrorCodes.cs`：42100+ AI 错误码。

后端数据与模块：
- `backend/AsterERP.Api/Modules/Ai/*`：15 张 AI 表实体。
- `backend/AsterERP.Api/Infrastructure/Modules/AiCenterAppModule.cs`：迁移、种子、服务注册、数据过滤注册。
- `backend/module-file-map.json`：`ai.center` 发布边界。

后端编排与 I/O：
- `backend/AsterERP.Api/Application/Ai/*`：会话、运行、上下文、压缩、SK 能力矩阵、知识库契约、治理。
- `backend/AsterERP.Api/Infrastructure/Ai/*`：密钥保护、模型路由、SK `Kernel` / `ChatCompletionAgent` 运行时、SSE 输出、取消注册。
- `backend/AsterERP.Api/Controllers/Ai*.cs`：`/api/ai/*` HTTP 入口。

前端：
- `frontend/AsterERP.Web/src/api/aiChat.ts`：CRUD 与 POST fetch stream SSE 客户端。
- `frontend/AsterERP.Web/src/pages/ai-chat/*`：AI 对话工作台和 8 个管理页。
- `frontend/AsterERP.Web/src/shared/components/ai-chat/*`：Markdown/表格/代码块/状态徽标。
- `frontend/AsterERP.Web/src/shared/state/aiChatStore.ts`：当前会话、草稿和 stream controller。
- `workspaceRoutes.full.tsx`、`routes.ts`、`messages.ts`：路由、导航和 i18n。

## 3. 契约变化

SSE 事件固定为：
`run_started/context_built/reasoning_started/reasoning_delta/reasoning_completed/content_started/content_delta/content_completed/usage/error/done`。

每个事件体包含：
`event/runId/conversationId/traceId/seq/timestamp/data`。

SK 原生模型与工具契约：
- Ask/Plan 统一走 DI `Kernel` 与 SK `ChatCompletionAgent.InvokeStreamingAsync`。
- 工具接口保留 `/api/ai/tools`，运行时通过 `Kernel.InvokeAsync(pluginName, functionName, arguments)` 调用 `[KernelFunction]`。
- Agent Profile 工具配置迁移为 `allowedFunctions` 结构，字段包含 `pluginName/functionName/permissionCode/autoInvokeAllowed`。
- `GET /api/ai/sk-capabilities` 对外展示 SK 能力覆盖状态；Process、AgentGroupChat、SQLite Vec、MCP/OpenAPI 若官方包/API 不可用则返回 `Blocked` 或 `FrameworkUnavailable`。

权限码：
`ai:chat:view/create/delete/archive/compress/viewAll`、`ai:model:view/edit`、`ai:prompt:view/edit`、`ai:log:view`、`ai:usage:view`、`ai:security:manage`。

## 4. Case 验收记录

| Case | 目标 | 判定 |
| --- | --- | --- |
| AI 模块迁移/种子 | 新建 15 张 AI 表、权限、菜单、默认安全策略、默认提示词和 coordinator agent | Pass：后端 build/test 编译模块；运行时迁移待 API smoke |
| 模型供应商与配置 | CRUD、密钥加密/掩码、连接测试、模型默认参数 | Pass：Controller/Service/前端页完成；真实 Key 连接测试 Blocked |
| 单会话 SSE | user message、Run、assistant message、reasoning/content/usage/done 流式事件 | Pass：代码链路完成；真实模型 API smoke Blocked |
| 多会话并发 | 不同会话并发互不阻塞；同会话活跃 Run 互斥 | Pass：`AiRunConcurrencyGuard` + DB 状态检查；需要运行态并发 smoke |
| 多智能体协作 | 多 Agent 并发草稿，coordinator 汇总，失败策略可配置 | Pass：实现完成；真实模型 smoke Blocked |
| 上下文压缩 | 活跃 Run 冲突，终态后模型摘要快照 | Pass：实现完成；真实模型 smoke Blocked |
| Usage/Log | Run、模型、Token、耗时、错误可聚合和分页查询 | Pass：实现完成，前端统计和日志页完成 |
| 权限与数据过滤 | 普通用户仅看自己；`viewAll` 看全量；配置/日志/统计/安全分权 | Pass：Permission + ORM filter 注册完成 |
| 前端刷新恢复 | 会话列表、消息分页、快照和运行事件可回源恢复 | Pass：TanStack Query + store 分责完成 |
| 发布边界 | AI 文件归属 module-file-map | Pass：`ApplicationPublishModuleFileMapTests` 通过 |

## 5. 验证结果

已执行：
- `dotnet build AsterERP.sln`：Pass，0 error；MailKit/MimeKit NU1902 既有漏洞警告。
- `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj`：Pass，41/41。
- `npm run typecheck`：Pass。
- `npm run build`：Pass；Vite 提示既有 workflow-bpmn chunk > 500 kB。
- 后端运行态 smoke：Pass。启动 `dotnet run --project backend/AsterERP.Api/AsterERP.Api.csproj --urls http://127.0.0.1:5000` 后，`GET /api/health`、`POST /api/auth/login`、`POST /api/auth/switch-workspace`、`GET /api/ai/providers/options`、`GET /api/ai/model-configs/options`、`GET /api/ai/governance/security` 均返回 200；临时 dotnet 进程已停止。
- 内置浏览器工作台验收：Pass。启动后端 `http://127.0.0.1:5000` 与前端 `http://127.0.0.1:5173`，使用 in-app Browser 登录 `admin/admin123`，切换到 `AsterERP 系统管理 / SYSTEM / 默认租户` 工作区后逐路由验收：
  - `/ai/chat`：显示 `AI 对话工作台`、`新的智能会话`、`模型参数`、`多智能体`、`上下文与运行`、`SSE 事件`，可见 `刷新`、`新建会话`、`发送`。
  - `/ai/conversations`：显示 `会话管理`。
  - `/ai/providers`：显示 `模型供应商` 与 `新增模型供应商`。
  - `/ai/model-configs`：显示 `模型配置` 与 `新增模型配置`。
  - `/ai/prompt-templates`：显示 `提示词模板`。
  - `/ai/agents`：显示 `智能体配置`。
  - `/ai/usage`：显示 `使用统计`。
  - `/ai/logs`：显示 `调用日志`。
  - `/ai/security`：显示 `安全设置`、`保存设置`、`重新加载`。
  所有页面 `hasForbidden=false`，浏览器控制台错误为空；验收截图已在会话中捕获，临时 API/Vite 进程与浏览器标签页已停止/关闭。
- DeepSeek 真实链路验收：Pass。2026-06-17 使用管理员在页面录入 `DeepSeek / https://api.deepseek.com / deepseek-v4-pro`，Provider 连接测试返回 200；内置浏览器 `/ai/chat` 实测：
  - 思考模式开启：SSE 事件从 `run_started` 连续输出 `reasoning_delta`，最终到 `reasoning_completed/content_completed/usage/done`；页面显示 `总 Token 564 / 思考 512`，会话状态 `Succeeded`。
  - 思考模式关闭：SSE 事件输出 `content_delta/content_completed/usage/done`；页面回显正文 `DeepSeek 正文流式链路测试通过。`，显示 `总 Token 84 / 思考 0`。
  - 运行态修复：`AiRunConcurrencyGuard` 避免 SqlSugar 解析私有字段集合；`SseEventWriter` 只在响应未开始时设置 SSE 头，避免已开始响应后写错误事件触发 header 只读异常。

新增测试：
- `backend/AsterERP.Api.Tests/AiDomesticModelProtocolTests.cs`
  - DeepSeek 思考 payload 不发送 sampling 参数并归一 `reasoning_effort`。
  - GLM 关闭思考时保留 sampling 并传 `tool_stream`。
  - SSE chunk 解析 `reasoning_content/content/usage`。

## 6. 剩余风险

- 真实 DeepSeek/GLM 连接测试需要管理员在 UI 中录入 API Key 后执行；仓库未也不应包含密钥。
- API smoke 需要登录态、切换工作区和已授权 AI 权限；未使用匿名 401 作为业务验证。
- Vite 大 chunk 提示来自既有 BPMN 工作流包，不影响本次 AI 页面构建。

## 7. Ask / Plan / Agent v2 实施记录

日期：2026-06-17  
执行人：Codex

本次将 `/ai/chat` 按 PRD v2.0 全新实现 Ask / Plan / Agent 三模式，旧任务计划执行接口下线，不再保留 Demo 执行逻辑。计划、任务、事件、输出、工具日志、审计、权限和刷新恢复均落回 AsterERP 原生后端分层；Agent 运行器引入 `Microsoft.SemanticKernel` 与 `Microsoft.SemanticKernel.Agents.Core` 1.77.0，使用 `ChatCompletionAgent`，但状态机、权限、工具白名单、事件持久化和审计不交给外部框架。

完成内容：
- 后端扩展 `ai_task_plans`、`ai_task_plan_items`，新增 `ai_task_plan_events`、`ai_task_plan_item_outputs`，扩展 `ai_tool_execution_logs` 的 `PlanId/ItemId/ToolCode`。
- 新增 Plan/Item 固定状态机、revision 乐观锁、结构冻结、循环依赖与深度校验、事件先持久化后输出、User 任务 WaitingUser、Tool 白名单执行与日志。
- 重写 `AiTaskPlansController` 与 `AiTaskPlanService`，补齐 plan list/detail/events/outputs、generate/create/update/replan/duplicate/delete、item patch/add/move/delete、approve/unapprove、execute/pause/resume/cancel、mark-complete/retry/skip/block/unblock。
- `AiStreamService` 三模式分流：Ask 只做普通对话；Plan 生成可审阅计划并处理 `PlanParseFailed`；Agent 只执行 Approved/PartialCompleted 计划。
- 前端 `/ai/chat` 接入新 DTO/API，右侧任务面板展示任务树、实施计划、验收标准、风险、进度、运行事件；Ask/Plan/Agent 三模式由后端状态驱动。
- `AiMarkdownContent` 支持有序列表，实施步骤按 todo 分行显示；刷新后任务计划事件从后端 `events` 接口恢复显示。

内置浏览器人工验收：
- 登录 `admin/admin123`，选择 `AsterERP 系统管理 / SYSTEM` 工作区，从左侧菜单进入 `/ai/chat`。
- Ask：发送“请只回复一句：Ask 手工验收通过。”，页面追加用户消息与助手回复，旧计划保持 `PlanReady / 0%`，SSE 显示 `content_delta/content_completed/usage/done`。
- Plan 失败：512 token 下生成长 JSON 被截断，页面显示 `PlanParseFailed`，SSE 只保留必要 `done` 终态，未错误生成可执行计划。
- Plan 成功：调高输出 Token 后生成 `AI 工作台三模式快速验收`，右侧显示 `PlanReady`、3 个任务、风险和实施计划，SSE 只保留内容流、`usage` 和 `done`。
- 批准：点击“应用计划”后状态变为 `Approved`，结构按钮禁用，执行入口启用。
- Agent：点击执行并发送执行指令后，计划变为 `PartialCompleted`，3 个 User 任务进入 `WaitingUser`，未被自动标成功；Agent/Task 内部事件只写入后端事件表，不再推送到聊天 SSE。
- 刷新恢复：刷新并重新登录后，计划状态 `PartialCompleted`、3 个 `WaitingUser` 任务恢复；右侧不再展示 SSE 事件区。
- UI 修复：截图中“实施”下 `1. 检查 Ask 模式 2. 检查 Plan 模式 3. 检查 Agent 模式` 已改为有序列表分行显示。
- UI 简化：按 2026-06-17 反馈移除 `/ai/chat` 右侧 `SSE 事件` 面板，并同步移除 Plan/Agent/Task 内部事件向聊天 SSE 的推送。

验证结果：
- `dotnet build AsterERP.sln`：Pass，仍有既有 MailKit/MimeKit NU1902 警告。
- `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj`：Pass，44/44。
- `npm run typecheck`：Pass。
- `npm run test -- --run`：Pass，1 个测试文件、2 个用例；npm 对 `--run` 参数有既有警告。
- `npm run build`：Pass；Vite 仍提示既有 workflow-bpmn chunk 大于 500 kB。
- `npm run lint`：Blocked，仓库 ESLint 环境存在大量既有 `AbortSignal/fetch/localStorage/process` 等全局未声明和历史 import-order 问题，本次已修正触碰文件中的新增 import/unused 噪音。
