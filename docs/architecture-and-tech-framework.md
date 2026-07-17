# AsterERP 项目架构与技术框架（维护版）

更新时间：2026-07-06

## 目标

本文件用于沉淀并维护项目当前的“架构边界 + 功能域 + 技术栈”，作为：
- 新需求评审入口；
- 技术决策对齐基准；
- 接口、页面与底层设施扩展的统一参照。

> 当前以“模块化单体（Backend + Frontend 一套工程）”为准，不引入微服务拆分前提下持续演进。

## 1. 项目定位与分层

### 1.1 后端分层（ASP.NET Core）

```txt
AsterERP.Api（Web 宿主）
  -> Endpoints（Controllers）
    -> Application（应用编排）
      -> Infrastructure（数据库、缓存、日志、安全、文件、队列等）
        -> Modules（领域/功能域实体与规则）
          -> AsterERP.Domain（EntityBase 等 ORM 基类）
          -> AsterERP.Contracts（请求/响应 DTO）
          -> AsterERP.Shared（ApiResult、分页协议、错误码、权限基础设施）
```

后端解决方案项目（`AsterERP.sln`）：

| 项目 | 职责 |
|------|------|
| `AsterERP.Api` | Web 宿主：Controllers、Application、Infrastructure、Modules |
| `AsterERP.Shared` | 跨模块基础模型与 ASP.NET 权限过滤器；零业务 I/O |
| `AsterERP.Contracts` | 对外请求/响应 DTO；仅引用 Shared |
| `AsterERP.Domain` | SqlSugar 实体基类 `EntityBase`；不引用 Shared |
| `AsterERP.Workflow.Processing` | 工作流/图处理通用库；封装图定义、校验、拓扑、路径、影响、差异与执行批次规划 |
| `AsterERP.Api.Tests` | xUnit 集成/冒烟测试；引用 Api + Shared + Contracts + Domain |

核心约束：
- `Controllers` 只做 HTTP 适配（路由、权限标注、请求组装、统一返回）。
- `Application` 承载流程编排，不在控制器堆业务决策。
- `Infrastructure` 承载 I/O 和技术能力。
- `Modules/System` 为当前主要功能域；`Modules` 预留未来业务域扩展。
- `EntityBase` 位于 `AsterERP.Domain`，`Shared` 不得引用 SqlSugar 或 Domain。

### 1.2 前端分层（Vite + React）

```txt
src/app
  -> src/pages
    -> src/shared
      -> src/core
```

核心约束：
- `app` 负责启动、全局 provider、路由壳、布局。
- `pages` 做页面装配与状态协调。
- `shared` 复用查询、表格、表单、字典、权限等通用能力。
- `core` 放运行时基础能力（HTTP、请求、响应式布局、权限、UI 引擎）。

## 2. 技术框架（当前）

### 2.1 后端技术栈

- 运行时：`.NET 10`
- Web 框架：ASP.NET Core Web API
- ORM：SqlSugar (`SqlSugarCore` + `SqlSugar.IOC`)
- 验证：FluentValidation（配合 ASP.NET Core ModelState/验证管道）
- 日志：Serilog（Console + File sink）
- 数据库：SQLite（`data/astererp.db`）
- API 风格：Controller 风格接口 + JSON 统一响应封装（`ApiResult`）
- 常驻服务与中间件链：异常诊断、TraceId、当前用户、操作日志
- ABP：仅作为基础设施内核，当前用户统一注入 `Volo.Abp.Users.ICurrentUser` 并通过 AsterERP claims 扩展读取业务 ID，不接管登录态与 RBAC 模型
- 实时：SignalR（`SystemNotificationHub`）
- AI：Semantic Kernel 抽象接口（`Microsoft.SemanticKernel.Abstractions` 1.77.0）+ DeepSeek/GLM/OpenAI-Compatible 统一流式驱动

### 2.2 前端技术栈

- 运行时：Node + TypeScript
- 构建：Vite
- 框架：React 19 + React Router DOM 7
- 数据状态与请求：TanStack Query 5 / React Query
- 虚拟列表与表格优化：`@tanstack/react-virtual`
- 表单与 UI：Tailwind CSS 4 + 自研组件体系（shared）
- 图标：Phosphor Icons / Lucide
- 图谱画布：`@xyflow/react`，只在 AI Capability Center 知识图谱模块内使用
- 体量与性能：Rollup manualChunks、可选构建分析（`npm run analyze`）

### 2.3 配置与运行口径

- 后端启动端口采用默认 ASP.NET 约定（`http://127.0.0.1:5000` 代理目标）
- 前端开发端口：`5173`
- 前端开发代理：`/api` -> `127.0.0.1:5000`
- 跨域默认允许：`http://localhost:5173` 等本地域名

## 3. 功能域与职责

### 3.1 当前后端功能域（按目录可追溯）

- `Controllers`
  - `Auth`：认证与会话基础能力
  - `Echo` / `Health`：可用性探针与最小示例链路
  - `System*`：系统管理类能力（用户、角色、菜单、部门、字典、参数、日志、文件、通知等）
- `Application`
  - 通用应用层服务：认证、健康检查、平台管理、系统服务编排
- `Modules/Platform`
  - 平台底座：租户、应用、租户应用安装、用户租户关系、用户应用角色关系
- `Modules/System`
  - 业务聚合：组织与权限体系（部门/岗位/角色/用户等）
  - 基础资料：字典、参数、编码/流水号、菜单权限、文件、日志、通知
- `Infrastructure`
  - 全局异常、当前用户、数据库初始化、日志、仓储、单位工作流、安全中间件、SignalR
- `AsterERP.Shared` / `Contracts`
  - 错误码、统一返回模型（`ApiResult`）、分页协议、权限属性、当前用户契约
  - `EntityBase`（SqlSugar ORM 基类）保留在 `AsterERP.Api/Modules/Common`
- `Modules/Ai` / `Application/Ai` / `Infrastructure/Ai`
  - 智能中心：模型供应商、模型配置、会话/消息/Run、上下文压缩、多智能体协作、SSE 流、Usage/Log、安全设置
  - DeepSeek/GLM/Zhipu/OpenAI-Compatible 参数归一、思考链解析和 SK 标准接口实现
  - 知识图谱：`AiKnowledgeGraphController`、`Application/Ai/KnowledgeGraph`、`Modules/Ai/AiKnowledgeGraph*Entity` 负责实体关系图、证据、导入导出、重建任务和图分析 API；Flowise 仅作能力参考，不作为运行时服务或桥接层
- `Modules/AsterScene` / `Application/AsterScene` / `Contracts/AsterScene`
  - AsterScene ToC 原生模块：SceneDocument、RuntimeManifest、项目、资产、分片上传、任务、发布/回滚、公开作品、Creator Profile、社区互动、Remix、订阅、用量账本、审核治理、AI Credits、支持工单
  - 数据表统一使用 `asterscene_*` 前缀，按 `TenantId + AppCode + OwnerUserId` 注册 ORM data filter；公开运行态只读已发布 Manifest
- `Modules/ApplicationDataCenter` / `Application/ApplicationDataCenter` / `Contracts/ApplicationDataCenter`
  - 应用级数据中心：数据源、连接检测、数据模型、实体字段、字典编码、API 服务、查询数据集、同步任务 8 条配置链路。
  - 数据中心对象只写当前应用库，复用应用工作区数据库访问、Repository、SqlSugar、DataProtection、ABP `ICurrentUser` 和既有 ORM data filter 能力；发布模型写入 `system_data_models`，API 运行时通过应用级 `/api/application-data/{**path}` 执行，不落平台模块。
  - 应用级轻量低代码设计器复用数据源表结构、数据模型发布、运行时 PageSchema、应用库 `system_menus` 和权限码生成，从 `/admin/development-center/business-objects` 完成“版本 -> 业务对象设计 -> 组件投放 -> Published PageSchema 预览 -> 权限 -> 发布运行页”的应用级闭环，不进入平台模块或新增桥接主链路。

### 3.2 当前前端功能域（按页面结构）

- 系统管理：
  - 用户、角色、菜单、部门、岗位、字典、参数页面（`src/pages/system/*`）
- 平台管理：
  - 租户、应用、租户应用、用户租户关系、用户应用角色页面（`src/pages/platform/*`）
- 系统运行：
  - 仪表盘、引擎壳、设置、认证相关页面
- 运行时平台：
  - `/tenants/:tenantId/apps/:appCode/admin/pages/:pageCode` 统一前端入口、PageSchema 加载、schema 基础渲染、WMS/MES 前端运行时扩展注册；运行时 API 仍使用 `/api/runtime/*`，前端菜单不再使用旧 `/runtime/:pageCode` 路由
- AsterScene：
  - `src/features/aster-scene` 承载 Explore、Templates、Work Detail、Creator Profile、Dashboard、Studio、Player、Assets、Pricing、Admin 六件套闭环。
  - Public 路由为 `/explore`、`/templates`、`/works/:slug`、`/creator/:handle`、`/player/:publishCode`、`/pricing`；工作区路由为 `/dashboard`、`/studio/:projectId`、`/assets`、`/admin/asterscene`。
  - Studio 拆分 Command/Transaction、documentStore、selectionStore、viewportStore、autosave、ResourceRegistry、WorkerManager 和 Three.js Viewport；服务端数据归 TanStack Query，保存/发布/权限/revision 校验归后端原生 API。
- 共享能力：
  - `ResponsivePage`、`AdaptiveSearchForm`、`ResponsiveToolbar`、`DataTable`、`ModalForm`、`PermissionButton` 等

## 4. 主链路（典型）与职责分界

### 4.1 后端链路

`页面/API` -> `Controller` -> `ApplicationService` -> `Repository/Infrastructure` -> `SqlSugar` -> `SQLite`

### 4.2 前端链路

`Route` -> `Page` -> `shared 组件/Hook` -> `http client` -> `Backend Controller`

### 4.3 禁止边界

- 不在页面层写入领域决策、权限核验主逻辑、事务边界控制。
- 不把主业务逻辑放入“兼容桥接”层（`Bridge/Facade Shim`），仅允许极小兼容映射。
- 不在 `Controllers` 中实现跨聚合写入流程、权限策略与复杂映射。

## 5. 部署与运行约束

- 后端变更需配套重启服务后再做链路验收（避免旧进程污染）。
- 涉及后端或全栈改动必须优先确认链路：URL/路由 -> 前端 -> API -> Service -> Repository -> DB。
- 前后端都修改时采用“先后端后前端”验证顺序。

## 6. 文档维护规则（新增/变更时必须同步）

每次新增功能或结构调整，需同时更新以下内容：
1. 本文件：更新“技术栈或功能域”条目；
2. `docs/contracts.md`：更新涉及到的架构契约；
3. `docs/plan-02-architecture-principles.md`：如有职责分工或验收标准变化，更新对应章节；
4. 路径更新记录：在此文件“变更记录”追加条目。

## 6.1 新增菜单功能 RBAC 强制流程（AGENTS 约束）

任何“新增菜单功能”都必须通过以下链路验收，未通过不得纳入发布：

1. 菜单级权限定义
- 先定义菜单对应的页面查看权限（例如 `system:xxx:query`）；
- 再定义按钮级权限（新增/编辑/删除/导入/导出/配置等）；
- 统一在 `PermissionCodes` 及初始化权限库完成配置。

2. 接口级权限落地
- 新增/复用的后端接口必须使用 `Permission` 特性；
- 至少包含“查询”与“变更”两类权限控制点；
- 新增接口必须有 TraceId 与统一 `ApiResult` 响应链条。

3. ORM Data Filter 数据权限过滤
- 涉及组织、部门、岗位、角色、人员、租户、归属人或业务范围的数据查询，必须通过 ORM data filter/global query filter 实现数据权限；
- 禁止在 Controller、Application Service、Repository、前端状态、请求参数或临时 `Where` 拼接中实现数据权限过滤；
- 数据权限过滤必须由 ORM filter 生成数据库侧谓词，列表、详情、导出、批量操作必须使用一致的数据范围规则；
- 当前仓库如尚未实现对应 ORM data filter 能力，必须先补齐 ORM 过滤能力，再交付该菜单功能；
- 如某菜单无需数据权限过滤，需在交付说明中记录原因。

4. 前端展示与交互约束
- 菜单可见性、入口按钮和行内操作须使用当前用户权限集合控制；
- 按钮需采用共享权限能力（`PermissionButton`/权限判断封装）；
- 禁止仅靠前端隐藏实现安全控制，必须后端鉴权兜底。

5. 验收与文档闭环
- 完成功能验收、数据库写入/访问链路验证；
- 更新“菜单功能清单”追加该功能项；
- 在 `docs/contracts.md` 增补必要契约约束。

## 6.2 菜单功能清单（新增需追加）

> 说明：每次新增菜单时在表尾追加一行，新增功能必须先定义 RBAC 权限码并补齐前后端权限控制。

| 日期 | 菜单功能 | 后端控制器/关键接口 | 推荐权限码（菜单级/按钮级） | RBAC 状态 | 数据权限状态 |
| --- | --- | --- | --- | --- | --- |
| 2026-06-03 | 用户管理 | SystemUserController | system:user:query / system:user:add / system:user:edit / system:user:delete | 已收录 | 待按 ORM data filter 评审 |
| 2026-06-03 | 角色管理 | SystemRoleController | system:role:query / system:role:add / system:role:edit / system:role:delete | 已收录 | 待按 ORM data filter 评审 |
| 2026-06-03 | 菜单管理 | SystemMenuController | system:menu:query / system:menu:add / system:menu:edit / system:menu:delete | 已收录 | 待按 ORM data filter 评审 |
| 2026-06-03 | 部门管理 | SystemDepartmentController | system:dept:query / system:dept:add / system:dept:edit / system:dept:delete | 已收录 | 待按 ORM data filter 评审 |
| 2026-06-03 | 岗位管理 | SystemPositionController | system:position:query / system:position:add / system:position:edit / system:position:delete | 已收录 | 待按 ORM data filter 评审 |
| 2026-06-03 | 字典管理 | SystemDictionaryController / SystemFoundationController | system:dict:query / system:dict:add / system:dict:edit / system:dict:delete | 已收录 | 无数据范围，需交付复核 |
| 2026-06-03 | 参数管理 | SystemParameterController | system:parameter:query / system:parameter:add / system:parameter:edit / system:parameter:delete | 已收录 | 无数据范围，需交付复核 |
| 2026-06-15 | ABP 基础设施 | InfrastructureSettingsController | system:abp-setting:query / system:abp-setting:edit / system:abp-setting:test | 已收录 | 系统运维配置，无组织数据范围；接口后端权限兜底 |
| 2026-06-03 | 日志与审计 | SystemFoundationController | system:operation-log:query | 已收录 | 待按 ORM data filter 评审 |
| 2026-06-27 | 文件中心 | SystemFileController | system:file:query / system:file:upload / system:file:preview / system:file:download / system:file:delete | 已收录 | 本次作为系统管理文件库，列表和操作按系统权限控制；不接入用户/角色附件归属，暂不需要业务数据权限过滤 |
| 2026-06-27 | 打印中心 | SystemPrintCenterController | system:print:query / system:print:add / system:print:edit / system:print:delete / system:print:publish / system:print:use / system:print:default | 已收录，作为系统设置下的离线打印模板中心；业务页打印动作与模板配置入口均受后端 Permission 和前端 PermissionButton 双侧控制；模板目标统一按 menuCode 识别并绑定 list/detail 场景 | 模板与自定义元素按当前 TenantId + AppCode 工作区隔离；用户/角色/文件实际数据继续走原生服务权限链，打印运行时不新增旁路数据权限 |
| 2026-06-03 | 任务调度 | SystemScheduledJobController | system:scheduled-job:query / system:scheduled-job:add / system:scheduled-job:edit / system:scheduled-job:delete / system:scheduled-job:trigger / system:scheduled-job:log | 已收录 | 系统运维配置，无组织数据范围 |
| 2026-06-03 | 租户管理 | PlatformTenantController | platform:tenant:query / platform:tenant:add / platform:tenant:edit / platform:tenant:enable / platform:tenant:disable / platform:tenant:delete | 已收录，限 SYSTEM 平台管理员 | 平台级配置，由 PlatformAccessGuard 限定 |
| 2026-06-03 | 应用管理 | PlatformApplicationController / PlatformApplicationPublishController | platform:application:query / platform:application:add / platform:application:edit / platform:application:enable / platform:application:disable / platform:application:delete / platform:application:enter / platform:application:publish / platform:application:publish-task / platform:application:publish-log / platform:application:publish-artifact-download / platform:application:publish-artifact-delete | 已收录，限 SYSTEM 平台管理员；进入后台动作由 `platform:application:enter` 双侧控制，后端校验平台会话、租户应用、用户租户关系、应用角色/租户管理员和应用库绑定后写入工作区 session，并返回 `AdminDefaultRoutePath` 优先的应用后台入口 | 平台级配置由 PlatformAccessGuard 限定；进入后台使用 `system_auth_sessions.CurrentTenantId + CurrentAppCode` 作为唯一授权来源；发布任务按 AppCode 互斥，不使用应用启停状态；`module-file-map.json` 发布前 fail-fast 校验真实路径、闭包和 target aliases，声明路径缺失不得静默跳过；成功任务可由平台重新打包 zip，裁剪包运行地址随发布任务写入 |
| 2026-06-28 | 应用级控制台 | ApplicationConsoleController / GET /api/application-console/summary | app:home:view / app:console:view / app:workbench:view / app:development-center:view / app:data-center:view（数据库绑定动作另受 app:application-center:view 保护） | 已收录，应用工作区默认固定 5 个控制台入口；AI/Workflow/AsterScene/System/IM 等扩展 shell 必须通过 `system_tenant_apps.ConfigJson.shellCapabilities` 显式启用后才 seed 菜单和权限；前端 `/apps/:appCode/admin/*` 使用显式 application route registry 与 PermissionRoute 校验，应用控制台统一使用 `/console`，不再恢复 `/application-center` Shell 页面 | 摘要接口只读当前 session 工作区，不接收前端 tenant/app 参数；已启用 shell capability 的权限注入应用工作区 session，未启用能力的历史菜单由应用库 baseline 软删除；应用自身业务菜单/角色权限数据归应用库维护；读取页面、模型、流程、发布和审计摘要均限定当前 `TenantId + AppCode`，不新增业务数据权限旁路 |
| 2026-06-28 | 应用级数据中心 | ApplicationDataCenter*Controller / `/api/application-data-center/{data-sources|connection-tests|models|microflows|entities-fields|dictionaries-codes|api-services|query-datasets|integration-tasks}` / ApplicationDataApiRuntimeController `/api/application-data/{**path}` / RuntimeMicroflowsController `/api/runtime/microflows/{flowCode}/execute` | app:data-center:{data-source|connection-test|microflow|data-model|entity-field|dictionary-code|api-service|query-dataset|integration-task}:{view/add/edit/delete/enable/disable/test/preview/import/export/publish/reference} + `app:data-center:mutation-recovery` | 已收录；models 与 api-services 均为独立资源管理页面和 Controller，复用统一对象 DTO 但不复用微流页面；模型发布写入当前应用运行时 DataModel，API 服务发布维护当前应用 API 目录；前端入口、路由和按钮使用 PermissionRoute/PermissionButton；后端接口全部 Permission 兜底；受控写 ledger 查询/恢复额外使用 mutation-recovery 权限 | 数据中心表只初始化到应用库并全部带 TenantId/AppCode；ORM data filter 覆盖两类资源的列表、详情、启停、删除、发布和引用；API 路由按当前应用内 `RoutePath + HttpMethod` 唯一校验，来源对象必须属于当前工作区；运行时 API 只解析已发布服务，不接收前端 tenant/app 授权参数；引用、风险确认和状态约束由应用服务统一校验；受控 DML 先登记 mutation ledger，外部结果未知时只进入 RecoveryRequired，必须提交业务证据恢复 |
| 2026-06-29 | 应用级轻量低代码设计器 | ApplicationDevelopmentCenterController / `/api/application-development-center/*`; RuntimePageController; RuntimeGridViewController; RuntimeDataModelController mutation APIs; RuntimeDataImportController | app:development-center:designer:view/edit/delete/preview/publish/permission-edit + app:runtime-page:{pageCode}:view/add/edit/delete/import/export | 已收录，开发中心路由 `/development-center/business-objects` 改为轻量设计器；后端草稿接口生成 `ApplicationDraftPreview` 菜单，前端左侧菜单点击 `/pages/{pageCode}?previewPageId={pageId}` 即时预览；页面删除通过独立 `designer:delete` 权限执行事务化软删除，并同步移除预览/正式菜单、DesignerDocument、RuntimeArtifact、发布记录；发布后生成 `ApplicationRuntime` 正式菜单、PageSchema、DataModel、PermissionCodes 和角色授权；运行时 create/update/delete/import/export API 动态校验页面动作权限 | 设计器只使用当前 session 工作区与应用库 DB，不接受前端 tenant/app 授权参数；页面存在子页面或业务对象设计引用时禁止删除；草稿预览只授开发预览权限，正式运行只按发布权限码和角色授权暴露；运行数据访问继续走已发布 DataModel、Provider、字段白名单和 ORM data filter；无主键表降级只读并返回 warnings；导入逐行走 RuntimeDataModelService 创建，导出限定 exportable 字段和行数上限 |
| 2026-07-15 | MES 订单 SQL 微流运行页 | RuntimePageController / RuntimeMicroflowsController / `ApplicationMicroflowRuntimePermissionService`；`RuntimeArtifact` pageMicroflows | app:runtime-page:page_mr7xi5jk:view/add/edit/delete | 已收录；MES 订单页由设计器发布为正式运行页，列表、详情、新增、修改、删除均绑定已发布 `SQL Script` 微流；页面动作和微流模型权限由后端双重校验，前端表单/工具栏按 PermissionButton 控制 | SQL 脚本只允许通过已发布微流执行，当前工作区由 session + 应用库解析；脚本输入使用参数绑定，删除脚本保护存在明细的订单；运行页列表、详情和变更链路继续复用应用数据中心 SQL 执行、审计与工作区边界，不在前端拼接 SQL |
| 2026-06-03 | 租户应用安装 | PlatformTenantAppController | platform:tenant-app:query / platform:tenant-app:install / platform:tenant-app:enable / platform:tenant-app:disable / platform:tenant-app:uninstall | 已收录，限 SYSTEM 平台管理员 | 平台级配置，由 PlatformAccessGuard 限定 |
| 2026-06-03 | 用户租户关系 | PlatformUserTenantController | platform:user-tenant:query / platform:user-tenant:edit | 已收录，限 SYSTEM 平台管理员 | 平台级配置，由 PlatformAccessGuard 限定 |
| 2026-06-03 | 用户应用角色 | PlatformUserAppRoleController | platform:user-app-role:query / platform:user-app-role:edit | 已收录，限 SYSTEM 平台管理员 | 平台级配置，由 PlatformAccessGuard 限定 |
| 2026-06-03 | 运行时页面 | RuntimePageController | PageSchema 内 permissionCode 动态校验 | 已收录，按当前工作区 PageSchema 动态校验 | 租户应用配置隔离；P1 不涉及业务数据查询 |
| 2026-06-03 | 运行时数据查询与配置 | RuntimeDataModelController | DataModel 内 permissionCode 动态校验 / runtime:data:query / runtime:configuration:query | 已收录，按当前工作区 DataModel 动态校验；运行时页面/菜单配置改用 runtime:configuration:query 与业务运行时数据查询拆权 | system_menus / system_page_schemas 通过 ORM data filter 做租户应用隔离 |
| 2026-06-03 | 运行时列视图 | RuntimeGridViewController | runtime:grid-view:save-user / runtime:grid-view:save-tenant | 已收录，用户视图与租户默认视图分别校验 | GridView 表通过 ORM data filter 做租户应用隔离；租户默认保存需管理员身份 |
| 2026-06-13 | 审批流模型与设计器 | WorkflowModelsController / WorkflowDeploymentsController | workflow:model:query / workflow:model:add / workflow:model:edit / workflow:model:delete / workflow:model:publish / workflow:model:suspend / workflow:deployment:query / workflow:deployment:read | 已收录，后端 Permission 与前端 PermissionRoute/PermissionButton 双侧校验 | 模型按 TenantId + AppCode 绑定工作区；Workflow 兼容物理表初始化与 schema fail-fast 校验 |
| 2026-06-16 | 审批配置与表单资源目录 | WorkflowBindingsController / WorkflowFormResourcesController | workflow:binding:query / workflow:binding:edit / workflow:binding:delete / workflow:instance:start | 已收录，审批配置、资源目录、Runtime 行审批入口和回调规则保存均由后端 Permission 与前端 PermissionButton 双侧校验；SYSTEM/WMS/MES 均以“审批中心”作为业务入口，菜单排序优先审批工作台与审批记录，再展示审批模板与审批配置；开发 seed 中 `wms_admin/mes_admin` 具备业务应用内审批模板、配置、工作台、记录入口，`wf_no_permission` 保留拒绝路径验证 | workflow_bindings 保留 TenantId + AppCode + MenuCode + BusinessType 唯一边界，并固化 FormResourceCode/PageCode/ModelCode/KeyField/CallbackConfig；资源目录仅来自当前工作区已发布 system_menus + system_page_schemas + system_data_models，回调写回只允许 DataModel `writable=true` 字段和 Provider 单行更新，Runtime 发起前走 DataModel Provider 与 ORM data filter |
| 2026-06-17 | BPM 标准菜单架构 | WorkflowFormResourcesController / WorkflowDraftsController / WorkflowCategoriesController / WorkflowReportsController / WorkflowDelegationsController / WorkflowCalendarsController / WorkflowInstancesController / WorkflowTasksController | workflow:form:query / workflow:draft:query/edit/delete/submit / workflow:category:query/edit/delete / workflow:report:query / workflow:delegation:query/edit/delete / workflow:calendar:query/edit/delete / workflow:task:query / workflow:instance:query/start/terminate | 已收录，应用数据库 baseline 统一写入 `workflow` 根菜单和个人工作台、流程管理、统计报表、系统与基础设置四大分组；普通业务应用左侧菜单只读本应用库 `system_menus`，不允许主库兜底或前端硬编码；前端页面使用 PermissionRoute/PermissionButton 校验；`pathname + search` 作为菜单高亮与页签 key，待办/已办/我发起/抄送通过 `?tab=` 深链进入 | Workflow 新表按 TenantId + AppCode 做 ORM data filter，草稿与委托叠加 OwnerUserId 归属过滤；应用库 baseline 重复执行幂等恢复审批菜单/RBAC；发布裁剪在闭包含 `/workflows/*` 时生成 Workflow lazy routes，包含 `/workflows/bindings`，避免业务工作区 404 |
| 2026-06-13 | 审批实例、待办与历史 | WorkflowInstancesController / WorkflowTasksController / WorkflowHistoryController / WorkflowParticipantsController | workflow:instance:query / workflow:instance:start / workflow:instance:withdraw / workflow:instance:terminate / workflow:task:query / workflow:task:claim / workflow:task:approve / workflow:task:reject / workflow:task:transfer / workflow:task:delegate / workflow:task:attachment / workflow:task:comment / workflow:history:query / workflow:participant:query | 已收录，菜单页、行操作与 API 全部权限绑定 | 身份来源映射系统用户/角色/部门/岗位；待办审批抽屉通过任务详情接口展示提交快照、附件、意见与轨迹；附件下载由任务/流程可见性兜底；ACT_ID_* 仅作引擎兼容，不作为业务权限来源 |
| 2026-06-18 | 智能中心一屏化菜单 | AiWorkbenchController / AiChatController / AiTaskPlansController / AiConversationsController / AiModelProvidersController / AiModelConfigsController / AiPromptTemplatesController / AiAgentProfilesController / AiToolsController / AiObservabilityController / AiSecurityController / AiSettingsController / AiKnowledgeController | ai:workbench:view / ai:capability:view / ai:observability:view / ai:security:view/edit / ai:settings:view/edit / ai:conversation:* / ai:task-plan:* / ai:model:* / ai:provider:* / ai:prompt:* / ai:agent:* / ai:knowledge:* / ai:tool:* | 已收录，智能中心仅保留 AI 工作台、能力中心、运行观测、安全治理、设置中心 5 个可见菜单；旧 URL 全部前端 Navigate 重定向；旧 AiGovernanceController 与 IAiGovernanceService 已删除，安全与观测走新 Controller；前端业务代码迁入 `features/ai-center` | AI 表按 TenantId + AppCode 做工作区过滤；会话、消息、Run、计划、任务、事件、输出、日志、知识索引任务等归属表按 OwnerUserId 做 ORM data filter；工具定义、绑定、设置、提示词版本、密钥引用走工作区过滤；密钥仅掩码回显 |
| 2026-06-17 | AI Workflow 工具能力 | AiToolsController / AiWorkflowController / WorkflowModelsController(import-ai-draft) | ai:tool:workflow:view/read/draft/validate/simulate/diagnose/importDraft/publishRequest + workflow:model:add/edit/query + workflow:binding:query + workflow:notification:*:query + workflow:instance/task:query | 已收录，工具入口后端 Permission 与 `AiToolPermissionService` 双检；前端 Workflow Inspector 与导入按钮使用 PermissionButton 控制；L4 发布/启停/审批/绑定应用工具只注册审计并返回 `AiWorkflowHighRiskBlocked` | AI Workflow 草稿、校验、模拟、诊断表按 TenantId + AppCode + OwnerUserId 做 ORM data filter；正式导入只生成 Workflow 草稿，不发布、不启停、不审批、不发送通知；读取 Workflow 原生数据继续沿用既有工作区和任务/实例可见性边界 |
| 2026-06-17 | AI 系统管理函数能力 | AiToolsController / AiKernelFunctionService / SystemAdminToolHandlers / 既有 System* Application Service | ai:tool:system-admin:view/read/write/grant/operate + system:user/dept/position/menu/role/dict/parameter/announcement/operation-log/login-log/online-user/scheduled-job:* | 已收录，AI 函数权限与原系统权限双检；前端 AI 工作台按系统管理菜单组显式选择函数码，默认不启用系统管理函数；日志类仅查询/详情，不提供伪 CRUD | 函数作用域固定当前 TenantId + AppCode；用户/AI 表继续走 ORM data filter，角色/菜单调用既有服务工作区校验，部门/岗位/字典/参数/公告/在线/调度沿用既有 Service 边界；函数参数和输出对密码、token、secret、apiKey、authorization、headers 做脱敏 |
| 2026-06-23 | AI 知识图谱 | AiKnowledgeGraphController | ai:knowledge:graph:view / ai:knowledge:graph:search / ai:knowledge:graph:edit / ai:knowledge:graph:reindex / ai:knowledge:graph:import / ai:knowledge:graph:export | 已收录，作为 AI Capability Center `knowledge-graph` tab 集成；后端 Controller 全部 `[Permission]`，前端图谱画布、导入导出、编辑和重建动作使用 `PermissionButton` | `ai_knowledge_graph_*` 表按 TenantId + AppCode/OwnerUserId 注册既有 ORM data filter；查询、详情、导出、导入和重建均在数据库侧工作区边界内执行 |
| 2026-06-23 | Flowise Studio 原生集成 | AiFlowiseController / AiFlowiseCanvasController / AiFlowiseNodesController / AiFlowisePredictionsController / AiFlowiseDocumentStoresController / AiFlowiseEvaluationsController / FlowiseResourceService / FlowiseCanvasService / FlowiseExecutionService / FlowisePredictionService / FlowiseDocumentStoreService / FlowiseEvaluationService | flowise:view/edit/run/import/export/manage/secret:reveal/retry/share/schedule/webhook + flowise:chatflows:* / flowise:agentflows:* / flowise:credentials:* / flowise:api-keys:* / flowise:workspaces:* / flowise:document-stores:upsert / flowise:logs:read 等菜单动作码 | 已收录，后端 Controller `[Permission]` + 服务层资源精确权限双检；前端 `/flowise/*` 页面使用 `PermissionRoute`、`PermissionButton`，菜单由后端 seed 驱动并按 Flowise 原可见顺序显示；内容区文案接入 zh-CN/en-US | `ai_flowise_*` 表按 TenantId + AppCode + OwnerUserId 注册 AI ORM data filter；资源、画布、节点定义、聊天消息、反馈、Lead、文档库文件/chunk、向量配置、评测结果、执行、工作区、审计均在数据库侧工作区边界内执行，密钥加密保存且 reveal 单独审计 |

| 2026-06-19 | AsterScene ToC 原生模块 | AsterSceneProjectsController / AsterSceneAssetsController / AsterSceneJobsController / AsterScenePublishingController / AsterSceneRuntimeController / PublicAsterSceneController / CommunityAsterSceneController / AsterSceneSubscriptionsController / AsterSceneUsageController / AsterSceneModerationAdminController / AsterSceneSupportController | asterscene:project:list/create/read/delete / asterscene:studio:open/save / asterscene:asset:view/upload/delete / asterscene:publish:create/rollback / asterscene:community:react/remix/report / asterscene:subscription:view/manage / asterscene:usage:view / asterscene:ai:generate / asterscene:support:create/view/comment/close / asterscene:admin:view/moderate | 已收录，使用 `dashboard`、`assets`、`admin/asterscene` 后台菜单与 `/explore`、`/templates`、`/works/:slug`、`/creator/:handle`、`/studio/:projectId`、`/player/:publishCode`、`/pricing` 原生路由；前后端均不新增 Bridge/Shim/Facade | `asterscene_*` 表按 TenantId + AppCode + OwnerUserId 注册 ORM data filter；public slug、creator handle、publishCode、reaction、remix、ledger、job、asset version、support ticket/comment mutation 均由唯一索引或追加式账本约束；公开接口只读 Published 状态且 Visibility 非 Private 的作品和 Manifest |

## 7. 变更记录

| 日期 | 变更人 | 内容 |
| --- | --- | --- |
| 2026-07-11 | Codex | ABP 基础设施统一入口：锁由 `DistributedLock.Redis` 提供跨进程实现，Development 无 Redis 仅允许显式同机文件锁，Production 缺 Redis 配置直接 fail-fast；会话/字典缓存统一使用 `IDistributedCache`，业务后台入队统一使用 `IBackgroundJobManager` typed async job，消息发送日志通过 `ILocalEventBus` 处理。配置入口为 `AsterERP.Cache.Redis.Configuration` / `Cache:Redis:Configuration` 与 `DistributedLock:DevelopmentFilePath`。 |
| 2026-07-06 | Codex | PRD 整改闭环：平台应用中心进入后台改为专用 `platform:application:enter` 用例，应用默认入口拆分 `AdminDefaultRoutePath` / `RuntimeDefaultRoutePath`，匿名应用登录 bootstrap 不再暴露数据库细节，应用 shell 改为默认 6 核心入口 + `shellCapabilities` 显式扩展，前端应用路由改为显式 registry，发布裁剪 `module-file-map.json` 和 reachability 解析改为 fail-fast。 |
| 2026-06-26 | Codex | Workflow/Flow 核心模块统一接入 ABP：新增 `AsterErpWorkflowApprovalCoreModule` / `AsterErpWorkflowFormsCoreModule` / `AsterErpWorkflowCoreModule` / `AsterErpWorkflowPersistenceModule` / `AsterErpWorkflowDependencyInjectionModule`，当前用户投影为 `WorkflowCurrentUserContext`，通知轮询切换为 Hangfire `RecurringJobManager` 承载的周期任务，并继续读取 AsterERP ABP settings 开关。 |
| 2026-06-23 | Codex | 新增 Flowise Studio 原生集成，按 Flowise 可见菜单顺序 seed `/flowise/*` 菜单和路由，补齐 `flowise:*` RBAC、`ai_flowise_*` 表族、资源/画布/节点目录/Prediction/文档库/评测 API、前端 Flowise 专属流列表/Canvas/详情页面、zh-CN/en-US 文案和发布裁剪路由生成。 |
| 2026-06-23 | Codex | 新增 `AsterERP.Workflow.Processing` 图处理库与 AI 知识图谱模块，补齐 `ai:knowledge:graph:*` RBAC、`ai_knowledge_graph_*` 表族、AI Capability Center 图谱 tab 和导入导出/重建/路径/影响分析契约。 |
| 2026-06-19 | Codex | 原名增强 AsterScene ToC 原生模块，冻结 SceneDocument/RuntimeManifest、`asterscene_*` 表族、`asterscene:*` RBAC、公开增长/商业治理/AI/Support API，并从菜单、路由、权限、表清理和文档验收口径中移除历史实现。 |
| 2026-06-18 | Codex | 文档切片补齐 AsterScene Studio 独立 Shell、timeline DSL、资产管线、`expectedRevision` 必填并发契约，以及云展 RBAC 与 ORM data filter 验收口径。 |
| 2026-06-17 | Codex | AI 对话工作台升级 Ask / Plan / Agent v2，新增任务计划、任务事件、任务输出、工具执行关联、SK `ChatCompletionAgent` 运行器、任务计划 RBAC 和刷新恢复契约。 |
| 2026-06-18 | Codex | 智能中心整合为 5 个可见菜单，新增 Workbench/Observability/Security/Settings/Tool Management 原生 API，删除旧治理 Controller/接口与旧前端 AI 页面目录，旧 URL 改为重定向。 |
| 2026-06-17 | Codex | AI 对话工作台新增 Workflow 工具能力扩展，覆盖工具目录、草稿 artifact、BPMN/画布、校验/模拟/诊断报告、人工导入正式草稿与 L4 高风险拦截。 |
| 2026-06-17 | Codex | AI 对话工作台新增系统管理工具能力，覆盖用户、部门、岗位、菜单、角色、字典、参数、公告、日志、在线用户和任务调度的工具注册、权限双检、参数脱敏和前端菜单组选择。 |
| 2026-06-17 | Codex | BPM 菜单架构统一为四大分组并覆盖 SYSTEM、tenant-a WMS/MES、tenant-b WMS；新增 Workflow 草稿、分类、报表、委托、工作日历原生 DTO/API/页面与发布裁剪路由生成。 |
| 2026-06-17 | Codex | 新增 AI 智能中心原生模块，覆盖 DeepSeek/GLM/OpenAI-Compatible 统一协议抽象、SK 标准接口、SSE 流式会话、多智能体协作、上下文压缩、Usage/Log、安全设置、RBAC 和数据过滤。 |
| 2026-06-15 | Codex | 统一当前用户入口为 ABP `ICurrentUser` + AsterERP claims 扩展方法，权限、数据过滤、审计字段、平台/租户 Guard 不再读取旧自定义当前用户。 |
| 2026-06-16 | Codex | 审批配置升级为表单资源目录驱动，新增强类型绑定字段、Runtime 行审批入口、设计器表单上下文和发起前 DataModel 详情校验，并补齐业务应用管理员的审批中心/RBAC seed。 |
| 2026-06-16 | Codex | 审批配置新增结构化回调规则，审批事件可在同库事务内写回已发布 DataModel 的可写字段，并补齐 `writable` 字段契约、Provider 批量写入接口和回调日志。 |
| 2026-06-16 | Codex | SYSTEM/WMS/MES 审批菜单统一调整为业务优先的“审批中心”，首页工作台补齐审批摘要与最近待办入口，并将运行时页面/菜单配置从 `runtime:data:query` 拆到 `runtime:configuration:query`。 |
| 2026-06-15 | Codex | 补齐 AsterERP 当前用户到 ABP claims 的单向映射，确保 ABP 基础设施可读取用户、租户、部门、角色上下文但不接管登录态和 RBAC。 |
| 2026-06-15 | Codex | 新增 ABP 基础设施设置菜单能力，覆盖邮件、短信、对象存储、缓存、任务、审计配置与发送日志，补齐 RBAC 清单。 |
| 2026-06-03 | Codex | 新建《AsterERP 项目架构与技术框架（维护版）》并补充后端/前端分层、技术栈、功能域与维护规则。 |
| 2026-06-03 | Codex | 增加“新增菜单功能 RBAC 强制流程”与“菜单功能清单”条目，补齐现有菜单权限台账。 |
| 2026-06-03 | Codex | 收紧数据权限要求：禁止业务层/Repository 临时拼接，必须通过 ORM data filter/global query filter 实现。 |
| 2026-06-03 | Codex | 完成 ORM data filter 使用排查：历史代码未注册 SqlSugar data filter，用户管理曾在业务层拼接数据范围；优化后以请求级 ORM data filter 作为 `SystemUserEntity` 数据权限入口。 |
| 2026-06-03 | Codex | 新增任务调度菜单能力，采用 Hangfire + SQLite 持久化，补齐自研调度页面、执行日志、RBAC 和无数据范围说明。 |
| 2026-06-03 | Codex | 新增 P0 多租户多应用工作区底座：租户、应用、租户应用、用户租户关系、用户应用角色、工作区选择和 Platform 管理菜单。 |
| 2026-06-03 | Codex | 将角色与菜单扩展为 `TenantId + AppCode` 工作区维度，补齐平台开户闭环：租户、应用、安装、用户、菜单、角色权限、用户应用角色分配。 |
| 2026-06-03 | Codex | 新增 P1 动态菜单与 RuntimePage：菜单绑定 PageCode，PageSchema 表驱动运行时页面，WMS/MES 前端运行时扩展注册；当前前端统一入口已收敛为 `/pages/:pageCode`，旧 `/runtime/:pageCode` 不再作为菜单路由。 |
| 2026-06-03 | Codex | 新增 P2/P3 Runtime DataModel 查询与可配置表格：DataModel 表、Provider、Runtime 查询接口、字段白名单、租户/应用 ORM filter、RuntimeCrudPage、RuntimeDataGrid、用户/租户列视图。 |
| 2026-06-12 | Codex | 应用中心新增按应用发布能力：发布任务/日志/产物表、发布权限、Release 构建与前端 wwwroot/<AppCode> 静态托管链路。 |
| 2026-06-28 | Codex | 应用中心新增进入应用后台与返回平台级链路：`/api/platform/applications/{appCode}/enter`、`/api/auth/switch-platform`、`/apps/:appCode/admin/*` 应用级可见路由与 `X-Workspace-Level` 一致性校验。 |
| 2026-06-28 | Codex | 应用级控制台落地固定 5 个控制台入口、summary 摘要接口、应用级首页/控制台/工作台/开发中心/数据中心首页；原应用中心 Shell 页面已由统一 `/console` 控制台取代，系统设置菜单由应用库动态菜单和 RBAC 承载。 |
| 2026-06-28 | Codex | 应用级数据中心落地 8 个子入口、应用库 `app_data_*` 表族、`app:data-center:*` RBAC、数据源检测/预览/发布/引用风险链路、运行时模型发布与 `/api/application-data/{**path}` 应用级 API 运行入口。 |
| 2026-06-29 | Codex | 应用级业务对象构建工作台落地 scaffold API、Runtime Page/Menu/Permission 生成、Runtime CRUD mutation API、开发中心入口和数据源表双入口。 |
| 2026-06-30 | Codex | 应用级业务对象构建工作台补齐 Runtime 导入导出、Workflow 绑定引导、PageSchema 主子表关系表达和文档契约。 |
| 2026-06-12 | Codex | 应用发布升级为按 ABP 模块闭包精确裁剪：后端模块文件归属、前端 target 可达图 prune、产物泄漏扫描与源码+release+manifest 包审计。 |
| 2026-06-14 | Codex | 审批待办动作升级为详情抽屉，补齐提交表单快照、流程附件下载、审批意见与时间线的前后端契约说明。 |
