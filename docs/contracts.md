# AsterERP Architecture Contracts

本文件记录仓库级架构契约。后续 P0 / P1 / P2 实施必须先满足这些边界，再进入具体业务功能。

## 1. 总体定位

AsterERP 是模块化单体，不按微服务拆分。所有模块部署在同一个 Web API 和同一个前端应用中，通过清晰分层保持低耦合。

## 2. 依赖方向

后端依赖方向：

```txt
Program.cs
  -> Application
  -> Infrastructure
  -> Modules
  -> AsterERP.Domain / AsterERP.Contracts / AsterERP.Shared
```

约束：

- `AsterERP.Shared` 只放跨模块基础模型、错误码、权限属性与当前用户契约；不引用 SqlSugar、Domain、Application 或 Infrastructure，不做业务 I/O。
- `AsterERP.Domain` 保留 ORM 耦合的 `EntityBase`（SqlSugar 注解），命名空间 `AsterERP.Domain.Common`；不引用 Shared。
- `AsterERP.Contracts` 只放对外请求/响应 DTO，命名空间 `AsterERP.Contracts.*`；仅引用 Shared，不承载业务决策。
- `Application` 编排应用服务，不直接散落端点业务逻辑。
- `Infrastructure` 放数据库、仓储、事务、日志、安全、缓存、文件、编码规则等技术能力。
- `Modules` 按业务域组织实体、领域规则和模块内服务。
- `Endpoints` 只负责 HTTP 路由、参数绑定、权限标记和返回包装。

前端依赖方向：

```txt
src/app
  -> src/pages / src/features
  -> src/shared
  -> src/core
```

约束：

- `core` 放稳定运行时能力，如 http、query、router、permission、responsive、ui-engine。
- `shared` 放可复用页面生产力组件，如响应式页面、表格、表单、字典、权限按钮。
- `pages` 放传统页面装配；`features` 放跨多个路由和 Tab 的业务特性切片，特性内可包含 api/hooks/types/styles/components，但仍不得复制 shared 的分页、查询、权限、字典、响应式高度等通用机制。
- `app` 只负责启动、Provider、布局和路由组合。

## 3. 模块化单体边界

模块之间不直接互相改表或绕过服务访问对方主流程。跨模块协作优先通过应用服务、领域服务或明确的契约 DTO。

允许：

- 系统基础能力被业务模块调用，例如字典、编码规则、权限、操作日志。
- 模块读取自己的聚合和明确定义的基础字典/参数。

禁止：

- 在页面、Endpoint 或 Repository 中堆业务决策。
- 新增 Bridge / Shim / Facade 来承载主链路业务逻辑。
- 一个可复用核心类和另一个可复用核心类放在同一文件。

## 4. 前端建设原则

前端不做 Button / Input / Modal 的普通组件库封装，优先建设 ERP 页面生产力组件：

- `ResponsivePage`
- `AdaptiveSearchForm`
- `ResponsiveToolbar`
- `AutoHeightTable` / 后续 `AdvancedDataGrid`
- `FormRenderer`
- `ModalForm`
- `PermissionButton`
- `DictSelect` / `DictTag` / `DictText`

页面不得重复处理以下能力：

- 统一查询折叠
- 分页与页码状态
- 表格高度计算
- 权限按钮隐藏或禁用
- 字典选项读取与展示
- 错误、空状态、加载状态

## 5. 后端建设原则

后端统一业务底座优先级：

- `ApiResult`
- `PageResult` / 后续 `GridPageResult`
- `PageQuery` / 后续 `GridQuery`
- 全局异常处理
- TraceId
- `Volo.Abp.Users.ICurrentUser` + AsterERP claims 扩展方法
- Permission
- Repository
- UnitOfWork
- OperationLog
- DictService
- CodeRuleService

所有新增 HTTP 接口必须：

- 返回 `ApiResult<T>`。
- 带 `traceId`。
- 业务失败使用统一错误码。
- 需要权限的主流程接口标记权限码。
- 不用匿名 401 代替业务验证结果。

数据权限契约：

- 涉及组织、部门、岗位、角色、人员、租户、归属人或业务范围的数据查询，必须通过 ORM data filter / global query filter 统一实现。
- 禁止在 Controller、Application Service、Repository、前端状态、请求参数或临时 `Where` 拼接中承载数据权限过滤。
- 数据权限必须生成数据库侧谓词，并覆盖列表、详情、导出、批量操作等同一业务范围内的全部读取链路。
- 当前 ORM data filter 能力不足时，必须先补齐 ORM 过滤能力，再实现对应菜单或业务功能。

## 6. 安全与代码推送约束

- 任何 SSH 凭证文件（如 `id_ed25519_git` 及其变体）不得提交到仓库。
- 凭证文件必须在 `.gitignore` 中明确列入并由开发环境外部管理。
- 发现仓库中出现凭证文件时，必须清理后再进行代码推送。

## 7. P0 多租户多应用工作区契约

统一账号登录后必须先进入已授权的租户应用工作区，后续菜单、权限、品牌和 ABP `ICurrentUser` 上下文均以服务端会话中的当前工作区为准。

后端数据契约：

- 平台底座表包含 `system_tenants`、`system_applications`、`system_tenant_apps`、`system_user_tenant_memberships`、`system_user_app_roles`。
- `system_applications` 使用 `AdminDefaultRoutePath` 与 `RuntimeDefaultRoutePath` 分别表达应用后台入口和运行态入口；旧 `DefaultRoutePath` 仅作为历史迁移来源和兼容回写字段，不再作为新链路主读字段。
- `system_roles` 使用 `TenantId + AppCode + RoleCode` 作为同一租户应用内的角色编码唯一边界。
- `system_menus` 使用 `TenantId + AppCode + MenuCode` 作为同一租户应用内的菜单编码唯一边界，父子菜单只能引用同一工作区内的菜单编码。
- `system_auth_sessions` 保存 `CurrentTenantId`、`CurrentAppCode`、`WorkspaceSwitchedAt`，作为当前请求工作区来源。
- 租户编码、应用编码、租户应用安装、用户租户关系、用户应用角色关系必须有数据库唯一约束，重复写入不得产生重复数据。

认证接口契约：

- `POST /api/auth/login` 只校验账号密码并创建会话，返回 `accessToken`、`user`、`availableWorkspaces`、`currentWorkspace:null`，不返回菜单和权限。
- `GET /api/auth/workspaces` 返回当前用户可进入的工作区列表。
- `POST /api/auth/switch-workspace` 校验租户、应用、租户应用安装、用户租户成员、用户应用角色均有效后，写入 session 当前工作区，并返回 `currentWorkspace`、`menus`、`permissionCodes`、`branding`、`user`。
- `POST /api/auth/switch-platform` 解析当前用户可进入的 `SYSTEM` 工作区，写入 session 当前工作区，并返回平台级菜单、权限、品牌和默认路由 `/platform/applications`。
- `GET /api/auth/current-workspace` 返回 session 当前工作区；未选择时 `currentWorkspace:null`。
- `GET /api/auth/me` 在已选择工作区时返回菜单、权限、品牌和当前用户上下文；未选择时仅返回用户和工作区列表，菜单/权限为空。
- `WorkspaceResponse` 对外保留 `TenantId + AppCode` 主契约，并追加前端系统选择卡片字段：`workspaceId/systemId`、`systemCode/appCode`、`systemName`、`description`、`status`、`isAvailable`、`disabledReason`、`workspaceLevel`、`defaultRoutePath`、`adminDefaultRoutePath`、`runtimeDefaultRoutePath`。
- `CurrentWorkspaceResponse` 对外保留 `TenantId + AppCode` 主契约，并追加当前系统派生字段：`workspaceId`、`systemId`、`systemCode`、`systemName`、`workspaceLevel`、`defaultRoutePath`、`adminDefaultRoutePath`、`runtimeDefaultRoutePath`；`systemName` 优先来自租户应用系统名称，缺失时使用租户名和应用名组合；`workspaceLevel` 由 `appCode === SYSTEM ? platform : application` 派生，不落新表；应用后台默认入口优先使用 `AdminDefaultRoutePath`。
- `POST /api/platform/applications/{appCode}/enter` 用于应用中心进入应用后台，权限码 `platform:application:enter`；请求体为 `tenantId/source`，后端必须校验平台会话、平台权限、租户应用启用、用户租户关系、用户应用角色或租户管理员、应用库绑定和可连接性，成功后写入 `system_auth_sessions.CurrentTenantId + CurrentAppCode` 并返回 `currentWorkspace/user/menus/permissionCodes/branding/defaultRoutePath/adminDefaultRoutePath/runtimeDefaultRoutePath`。
- `GET /api/application-auth/tenants/{tenantCode}/apps/{appCode}/bootstrap` 为匿名应用登录 bootstrap，只返回登录页需要的粗粒度状态；不得返回数据库 provider、displayName、databaseName、连接字符串、原始异常或内部诊断 message。数据库绑定详情仅由已认证且具备应用后台权限的绑定状态接口返回。
- `GET /api/application-console/summary` 用于应用级控制台摘要，权限码 `app:home:view`；接口只读取 session 当前 `TenantId + AppCode + workspaceLevel=application`，不接受前端传入 appCode 或 tenantId 作为授权来源；返回当前应用信息、能力计数、运行指标、最近发布和最近审计摘要。
- 应用级工作区默认固定拥有 5 个控制台入口：`home`、`app-console`、`workbench`、`dev-center`、`data-center`，对应权限码 `app:home:view`、`app:console:view`、`app:workbench:view`、`app:development-center:view`、`app:data-center:view`；`app-center` 已不再是应用 Shell 菜单，`app:application-center:view` 仅作为应用数据库绑定等受保护管理操作的动作权限保留。AI、Workflow、AsterScene、System、IM 等扩展 shell 能力必须由 `system_tenant_apps.ConfigJson.shellCapabilities` 显式启用后才返回菜单、seed 权限和基线菜单。
- 固定应用级 shell 菜单不写入主库 `system_menus`，也不通过主库角色菜单授权生成；后端在非 `SYSTEM` 工作区按 capability 返回应用级 shell 菜单树，并把启用能力对应的 shell 权限注入 session。应用自身的业务菜单、系统设置菜单、角色权限和权限数据归应用自己的数据库初始化链路维护，不落到平台主库菜单/角色表。
- 应用级可见路由以 `/apps/:appCode/admin/*` 为准，前端必须使用显式 application workspace route registry，不得 clone 平台全量路由；应用控制台统一使用 `/console` 和 `app:console:view`，不再恢复已移除的 `/application-center` Shell 页面；`app:application-center:view` 只保护数据库绑定等管理动作。运行时页面前端入口统一为 `/tenants/:tenantId/apps/:appCode/admin/pages/:pageCode`；应用菜单必须优先使用应用库 `system_menus.RoutePath`，不得再把 `PageCode` 强制转换为旧 `/runtime/:pageCode` 前端路由。
- 应用级数据中心固定在 `/tenants/:tenantId/apps/:appCode/admin/data-center/*` 下提供 8 个子入口，不允许落到 `pages/platform/*` 或 `Application/Platform/*` 主链路。前端子路由为 `data-sources`、`connection-tests`、`models`、`entities-fields`、`dictionaries-codes`、`api-services`、`query-datasets`、`integration-tasks`，分别使用 `app:data-center:{module}:view` 进入权限。
- 应用级数据中心 API 前缀为 `/api/application-data-center/*`，运行时发布接口前缀为 `/api/application-data/{**path}`；前端不传 `tenantId/appCode` 作为授权来源，后端统一从 session 当前 `TenantId + AppCode + workspaceLevel=application` 和 `IWorkspaceDatabaseAccessor.RequireApplicationDb()` 解析应用库。
- 应用级数据中心模块权限固定为 `app:data-center:{module}:{action}`，模块为 `data-source`、`connection-test`、`data-model`、`entity-field`、`dictionary-code`、`api-service`、`query-dataset`、`integration-task`，动作覆盖 `view/add/edit/delete/enable/disable/test/preview/import/export/publish/reference`。所有控制器接口必须带对应 `Permission`，前端入口和按钮使用 `PermissionRoute` / `PermissionButton`，直接调用无权限接口返回统一 403 业务响应。
- 应用级数据中心对象表只初始化到应用库，核心表包括 `app_data_sources`、`app_connection_check_tasks`、`app_connection_check_runs`、`app_data_model_designs`、`app_data_entity_definitions`、`app_data_field_definitions`、`app_dictionary_codes`、`app_api_services`、`app_query_datasets`、`app_integration_tasks`、`app_integration_task_runs`、`app_data_object_references`、`app_data_import_batches`。所有核心表必须带 `TenantId/AppCode`、软删除、状态、版本、负责人、配置 JSON 和引用统计字段，并纳入请求级 ORM data filter。
- 应用级数据中心通用 DTO 使用 `ApplicationDataCenterObjectListQuery`、`ApplicationDataCenterObjectListItemResponse`、`ApplicationDataCenterObjectDetailResponse`、`ApplicationDataCenterObjectUpsertRequest`、`ApplicationDataCenterOperationResponse`、`ApplicationDataCenterActionRequest`、`ApplicationDataCenterActionResultResponse`、`ApplicationDataCenterPreviewRequest`、`ApplicationDataCenterPreviewResponse`、`ApplicationDataCenterPublishRequest`、`ApplicationDataCenterReferenceSummaryResponse`、`ApplicationDataCenterNextActionResponse`。保存、启停、发布响应必须返回对象详情、`referenceSummary` 和 `nextActions`。
- 微流管理扩展 `POST /api/application-data-center/microflows/{id}/preview-run`，权限码复用 `app:data-center:microflow:preview`。请求 DTO 为 `ApplicationMicroflowPreviewRequest`：`mode` 仅允许 `draft/published`，`executeRequest` 复用微流运行请求，`draftConfigJson` 只在 `draft` 模式执行当前画布定义且不落库、不发布，`published` 模式只读取已发布版本并沿用已发布运行校验；响应 DTO 为 `ApplicationMicroflowPreviewResponse`，必须返回 `datasets/primaryDatasetKey/trace/variables/rawResult`，其中 `datasets` 将 `result`、`variables.items`、`variables.sqlRows` 和集合变量归一为表格数据集。
- 应用级轻量低代码设计器固定在 `/tenants/:tenantId/apps/:appCode/admin/development-center/business-objects`，入口权限码为 `app:development-center:designer:view`；后端设计器 API 前缀为 `/api/application-development-center/*`，新建业务对象草稿接口为 `POST /api/application-development-center/business-objects`。设计器只从当前 session 与 `RequireApplicationDb()` 解析 `TenantId + AppCode`，不得接收前端传入租户/应用作为授权来源。
- 设计器首批接口固定包含：`GET /overview`、`GET/PUT /app-config`、`GET/POST/PUT /versions`、`POST /versions/{versionId}/publish`、`GET/POST/PUT /modules`、`GET/POST/PUT /pages`、`GET/POST/PUT /shared-resources`、`GET /permission-options`、`GET /pages/{pageId}/preview-schema`、`POST /pages/{pageId}/refresh-preview-menu`、`POST /pages/{pageId}/publish`、`POST /business-objects`，统一位于 `/api/application-development-center/*` 前缀下。
- 业务对象设计和发布必须复用应用级数据中心与运行时原生链路：设计阶段写入 `app_dev_pages/app_business_object_designs` 的 `LayoutDraftJson` 与基础配置，并生成 `ApplicationDraftPreview` 设计预览菜单；预览菜单只附加开发者预览权限和页面归属上下文，预览请求直接编译当前 `LayoutDraftJson`，不读取已发布 `SystemPageSchema`。发布阶段创建或更新 `SystemDataModel`、`SystemPageSchema`、`SystemMenu`、应用库权限码和角色授权。禁止落到 `pages/platform/*`、`Application/Platform/*` 或 Bridge/Adapter/Facade/Shim 主链路。
- 发布权限码规则固定为 `app:runtime-page:{pageCode}:view/add/edit/delete/import/export`；设计预览权限固定为 `app:development-center:designer:preview`，只授予开发角色。无主键表或显式 `WritableMode=ReadOnly` 时只生成只读运行页，并通过 `warnings` 返回原因。重复 `PageCode/MenuCode` 必须阻断，不得静默覆盖。
- 业务对象生成的 PageSchema 固定 `componentKey=runtimeCrudPage`，`props` 必须包含 `modelCode/pageCode/pageName/permissionCode/addPermissionCode/editPermissionCode/deletePermissionCode/importPermissionCode/exportPermissionCode/createRuntimeCrudActions/createImportExport`；Schema 顶层保留 `relations` 与 `detail`，`grid.masterDetail` 保留主子表关系表达，当前不新增旁路运行器。
- 设计器发布的 `designerDocument` 运行页可以声明 `pageMicroflows`；每个绑定必须包含 `alias/flowCode/action`，输入只能使用已注册的 `ExpressionValue`（或固定字面量），输出必须通过 `outputMappings` 写入页面变量或表单字段。`trigger=pageLoad` 仅在页面首次加载执行，表单字段变更动作必须使用变更后的表单快照；微流执行接口必须携带 `pageCode`，并由后端同时校验页面动作权限 `app:runtime-page:{pageCode}:{view|add|edit|delete}` 与微流节点涉及的模型权限。
- MES SQL Script CRUD 约定：列表和详情使用 `query/detail`，新增使用 `create`，修改使用 `change`，删除使用 `delete`；脚本参数来自微流输入定义，禁止前端拼接 SQL。删除脚本必须显式处理主子表约束或返回业务错误；运行页的新增、修改、删除按钮和后端运行时微流权限都必须绑定同一页面动作权限码。
- 当 `CreateWorkflowBinding=true` 时，生成器不自动发布流程模型，必须返回指向 `/workflows/bindings` 的 `configure-workflow-binding` next action，并携带 `formResourceCode/pageCode/menuCode`，由既有 `WorkflowFormResourceAppService` 从当前应用库已发布 `SystemMenu + SystemPageSchema + SystemDataModel` 解析表单资源。
- 数据源凭据只允许加密保存和掩码回显；数据库类连接测试和预览复用现有应用数据库连接能力与 SqlSugar，Excel/CSV 走文件预览识别字段，REST 走 `IHttpClientFactory`，外部 Kafka/RabbitMQ/对象存储按环境可达性做真实 smoke。重复编码、停用对象新引用、删除有引用对象和高风险字段变更必须返回业务错误或风险确认要求，不允许静默保存。
- 数据模型发布时写入当前应用库 `system_data_models`，运行时通过 `IRuntimeDataModelService` 和 `IDataModelProvider` 的 create/update/delete/query/detail 能力执行；应用数据 API 运行时只读取已发布 `ApplicationApiServiceEntity`，按路径、方法、权限和来源调用模型、SQL 查询、外部代理、Webhook 或文件/报表能力，不走平台模块。
- 最近访问本轮为前端本地持久化，key 为 `astererp:application-console:recent-visits:{userId}:{tenantId}:{appCode}`，不作为跨设备同步契约。

请求上下文契约：

- 前端非登录请求自动携带 `X-Tenant-Id`、`X-App-Code` 与 `X-Workspace-Level`。
- 前端本地只持久化 token 与当前系统上下文，不持久化菜单和权限；刷新后必须通过 `/api/auth/me` 重新恢复菜单、权限、品牌和当前系统派生字段。
- 后端以 session 当前工作区作为唯一授权来源，Header 只做一致性校验；`X-Workspace-Level` 必须与 session 中 `CurrentAppCode` 派生层级一致，Header 与 session 不一致时返回 403 业务响应。
- 未选择工作区时，受权限保护的业务接口不得沿用旧全局角色权限。
- Platform 管理接口必须在 `SYSTEM` 工作区且当前用户为平台管理员时可访问。
- 平台管理员开户闭环必须覆盖：创建租户、创建应用、安装租户应用、创建或选择用户、创建目标租户应用菜单、创建目标租户应用角色、给角色分配该工作区菜单权限、给用户配置租户成员关系与应用角色。
- 角色授权必须限定在角色所属 `TenantId + AppCode` 的菜单权限树内，不能把其他租户应用菜单权限授给当前角色。

## 7.1 动态菜单、轻量设计器与运行时页面契约

当前运行时闭环以应用库 `system_menus` 为单一菜单来源：预览菜单和正式运行菜单都写入应用库 `system_menus`，前端均通过 `/admin/pages/:pageCode` 进入 `RuntimePage`。预览入口携带 `previewPageId` 做开发者预览权限和页面归属校验，并从对应 `app_dev_pages.LayoutDraftJson` 编译运行时 artifact；正式入口无 `previewPageId`，只读取已发布 `system_page_schemas.SchemaJson`。

后端数据契约：

- `system_menus` 在 `TenantId + AppCode + MenuCode` 工作区边界基础上扩展 `PageCode`、`PageSchemaId`、`ScopeType`、`ConfigJson`。
- `system_page_schemas` 使用 `TenantId + AppCode + PageCode + Status=Published` 解析运行时页面，软删除记录不得参与解析。
- `app_dev_pages` 保存应用级轻量设计器草稿，`LayoutDraftJson` 只保存受控结构化设计配置和组件投放元数据；带 `previewPageId` 的预览请求从该字段编译临时运行时 artifact，正式运行仍只读取 `system_page_schemas.SchemaJson`。
- 草稿预览菜单 `ScopeType=ApplicationDraftPreview`，`RoutePath=/pages/{pageCode}?previewPageId={pageId}`，`ConfigJson` 保存 `previewPageId/pageId/versionId/templateCode`；正式运行菜单 `ScopeType=ApplicationRuntime`，`RoutePath=/pages/{pageCode}`。
- PageSchema 的 `SchemaJson` 只允许描述可序列化的 UI 结构、组件 key、布局、展示属性和动作 key，不允许 SQL、脚本或任意业务写入逻辑。

运行时接口契约：

- `GET /api/runtime/pages/{pageCode}` 返回当前 session 工作区内已发布的 PageSchema。
- `GET /api/runtime/pages/{pageCode}?previewPageId={pageId}` 校验当前 session 工作区内对应应用级设计器页面归属，并要求 `app:development-center:designer:preview`；返回内容由该页面当前 `LayoutDraftJson` 编译并经过 runtime artifact hash/signature/manifest 校验，不读取 `system_page_schemas`。
- Runtime API 不接受前端传入 `TenantId` 或 `AppCode` 作为授权来源，只使用 session 当前工作区；Header 仍只做 P0 一致性校验。
- PageSchema 配置了 `PermissionCode` 时，后端必须按当前用户权限兜底校验；无权限返回 403 统一 `ApiResult`。

前端契约：

- 菜单点击必须优先使用 `RoutePath`，以保留 `previewPageId`、`tab` 等查询参数；只有没有 `RoutePath` 时才允许用 `PageCode` 兜底为 `/pages/{pageCode}`。
- `RuntimePage` 只负责读取 PageSchema、校验 schema、按注册组件渲染基础运行时页面，并处理 loading、404、403、错误状态。
- `src/apps/<appCode>` 只能注册运行时扩展元数据和渲染组件，不得复制平台通信、权限、表格、表单或承载主业务流程。

Runtime document source rule: preview and publish must read the canonical `app_designer_documents` DesignerDocument. A missing canonical document may be constructed in memory from the current `LayoutDraftJson` for preview, without creating Document, Revision, Migration, or MigrationRun records; `SchemaDraftJson` is permitted only as a one-time migration input and is never a preview or publish source.

## 7.2 P2/P3 Runtime DataModel 与可配置表格契约

Runtime DataModel 闭环动态页面查询真实数据、受控写入、导入导出与远程列视图；应用级轻量设计器只允许编辑受控 `runtimeCrudPage` 草稿并编译为 `PageSchema`，不引入任意 SQL、脚本、自由画布或旁路运行器；审批回调写回只允许走 DataModel 字段白名单和 Provider 受控单行更新。

后端数据契约：

- `system_data_models` 使用 `TenantId + AppCode + ModelCode + Status=Published` 解析运行时数据模型，`SchemaJson` 只描述字段白名单、绑定、查询、排序、渲染、写入和权限元数据。
- `system_tenant_grid_views` 使用 `TenantId + AppCode + PageCode` 保存租户默认列视图。
- `system_user_grid_views` 使用 `UserId + TenantId + AppCode + PageCode` 保存用户个人列视图。
- Runtime 查询只允许访问 DataModel 字段白名单；不可查询字段出现在 filters、不可排序字段出现在 sorts 时返回业务错误。
- Runtime 数据访问必须走 `IDataModelProvider` 注册 Provider；Provider 只能查询固定白名单模型，不接受前端传表名、SQL、租户或应用。
- Runtime 受控写入必须走 `IRuntimeDataModelService.UpdateFieldsAsync(modelCode, id, updates, ct)` 和 `IDataModelProvider.UpdateFieldsAsync(model, id, updates, ct)`；字段必须 `writable=true`、存在 binding、不是主键字段，Provider 必须按 DataModel 主键/目标 key 单行更新。
- 当前已闭环写入 Provider：`system.page-schemas` 仅允许写 `status` 示例字段；`system.menus` 默认不支持写入。业务模块要获得“任意业务表”写回能力，必须发布 DataModel 并注册自己的 Provider。
- 当前已闭环真实 Provider：`system.menus` 查询 `system_menus`，`system.page-schemas` 查询 `system_page_schemas`。
- Runtime 相关工作区表已注册请求级 ORM data filter，租户应用过滤不放在前端或临时业务层兜底。
- 运行时业务数据查询继续使用 `runtime:data:query`；运行时页面/菜单等应用配置资源使用 `runtime:configuration:query`，不得用业务数据查询权限暴露配置入口。

运行时接口契约：

- `POST /api/runtime/models/{modelCode}/query` 返回 `ApiResult<RuntimeQueryResponse>`，请求包含 `pageIndex/pageSize/keyword/filters/sorts`，响应包含 `fields/rows/total/pageIndex/pageSize`。
- `GET /api/runtime/models/{modelCode}/{id}` 返回白名单字段详情。
- `POST /api/runtime/models/{modelCode}`、`PATCH /api/runtime/models/{modelCode}/{id}`、`DELETE /api/runtime/models/{modelCode}/{id}` 分别执行运行时新增、字段更新和删除，均返回 `ApiResult<RuntimeMutationResponse>`；请求可携带 `pageCode`，后端必须从已发布 PageSchema 推导动作权限 `add/edit/delete` 并再次校验，不能只依赖前端按钮隐藏。
- `GET /api/runtime/models/{modelCode}/import-template?pageCode={pageCode}` 下载当前页面可写字段 Excel 模板，后端必须从 PageSchema 推导 `import` 动作权限。
- `POST /api/runtime/models/{modelCode}/import-preview` 使用 multipart form 上传 `file` 与 `pageCode`，返回 `RuntimeImportPreviewResponse`，预览最多解析 200 行，整批上限 1000 行；字段映射只接受 DataModel 白名单且 `writable=true` 的字段。
- `POST /api/runtime/models/{modelCode}/import` 使用同一模板执行批量创建，逐行调用 `IRuntimeDataModelService.CreateAsync`，不得绕过 Provider、字段白名单、主键和当前应用库校验；响应 `createdRows/failedRows/errors`。
- `POST /api/runtime/models/{modelCode}/export` 返回 `RuntimeExportResponse`，请求包含 `pageCode/query/fileName`，导出最多 5000 行且只导出 DataModel `exportable=true`、页面可见的字段；后端必须推导 `export` 动作权限。
- Runtime 写入仍只允许已发布 DataModel、字段白名单 `writable=true`、非主键字段和当前应用库 Provider；跨应用模型访问、不可写字段、主键更新、Provider 不支持写入均返回业务错误。
- Runtime 字段响应包含 `writable`，审批配置页和后端回调校验只能选择 `writable=true` 且非主键字段。
- `GET /api/runtime/grid-views/{pageCode}` 返回列视图，优先级固定为用户个人视图 > 租户默认视图 > PageSchema `grid.columns`。
- `POST /api/runtime/grid-views/{pageCode}/save-user-view` 保存用户个人视图。
- `POST /api/runtime/grid-views/{pageCode}/save-tenant-default` 保存租户默认视图，仅租户管理员、平台管理员或 `*` 权限可用。
- `POST /api/runtime/grid-views/{pageCode}/reset-user-view` 软删除用户个人视图并回退到租户默认或 PageSchema 默认。

前端契约：

- `RuntimePage` 仍只负责加载 PageSchema、校验和注册组件；`runtimeCrudPage` 作为运行时组件承载 P2/P3 数据查询和表格配置。
- Runtime 查询状态归 TanStack Query；当前 workspace 仍归 Zustand；不把 schema、rows、grid view 缓存在全局 store。
- `RuntimeDataGrid` 将 DataModel 字段与 GridView 列配置映射到共享 `DataTable`，不承载后端权限或数据一致性规则。
- `RuntimeCrudPage` 可基于当前 `pageCode/modelCode` 调用审批绑定状态接口，统一挂载审批行操作；各业务运行时页面不得复制审批绑定查询、发起审批和历史入口状态机。
- `DataTable` 仅做向后兼容增强：可选远程列设置、可选列设置变更回调、字段 binding；旧 `columnSettingsKey` 本地列配置行为保持兼容。

## 7.3 Workflow 审批流兼容存储契约

审批流以 AsterERP 原生模块接入，不通过桥接层承载业务主链路；Flow/Workflow 复刻项目只能作为引擎、持久化、模型和服务能力被主模块注册。

后端数据契约：

- API 启动时必须初始化并校验完整 Workflow 兼容物理表族，覆盖 `ACT_GE_*`、`ACT_RE_*`、`ACT_RU_*`、`ACT_HI_*`、`ACT_ID_*`、`ACT_PROCDEF_INFO`，以及 Workflow 审批兼容层依赖的 `tbl_flow_*` 和兼容活动表。
- schema 校验必须 fail-fast；缺表、缺列、核心字段 nullable 不匹配或核心索引缺失时，API 启动失败并输出明确 mismatch 信息。
- 初始化必须幂等，`ACT_GE_PROPERTY` 写入引擎版本、schema history 和 AsterERP 初始化标识，重启不得重复插入或破坏已有部署、实例、任务、历史数据。
- 业务绑定使用 `TenantId + AppCode + MenuCode + BusinessType` 作为唯一边界；业务实例使用 `BusinessType + BusinessKey + ProcessInstanceId` 追踪流程状态。
- `workflow_bindings` 在唯一边界基础上固化审批表单资源字段：`FormResourceCode/PageCode/ModelCode/KeyField/DetailRoute/TitleTemplate/StatusField/BindingConfigJson`。旧绑定字段为空时允许列表展示和编辑补齐，但新强类型绑定必须来自资源目录校验结果。
- 新审批回调契约以 `CallbackConfig` 为 UI/API 强类型入口，并序列化进 `BindingConfigJson`；`StatusField` 仅作为旧配置兼容，运行时无 `CallbackConfig` 时生成一条隐式 `process-completed` 规则写当前表单 `Completed`。
- `CallbackConfig.rules[]` 字段固定为 `ruleId/enabled/trigger/nodeId/target/assignments/sortOrder`；trigger 仅允许 `process-start/node-enter/task-complete/task-reject/task-return/process-completed/process-withdrawn/process-terminated`。
- 回调 target 只能指向已发布且有权限的 DataModel `modelCode`；目标 key 来源仅允许 `businessKey/context/variable/submittedField`，默认使用当前绑定表单 `ModelCode + BusinessKey`。assignment 值来源仅允许 `constant/context/variable/submittedField`，禁止 JS、SQL、脚本或任意表达式。
- `workflow_callback_logs` 记录回调成功和失败，字段至少包含租户、应用、流程实例、任务、流程定义、触发器、节点、规则、目标模型、目标 key、结果和错误摘要；事务回滚后的失败日志需要单独写入。
- 审批表单资源目录来源固定为当前工作区内 `system_menus + system_page_schemas + system_data_models` 的已发布配置；目录不扫描任意数据库表，不接收前端表名或 SQL，且必须过滤当前用户无权限访问的菜单、页面和 DataModel。
- `workflow_business_instances.SubmittedFormJson` 保存发起流程时用户提交变量的不可变快照，必须过滤 `tenantId`、`appCode`、`menuCode`、`starterUserId`、审批动作等系统注入字段；`VariableSnapshotJson` 继续表示运行变量快照，后续 `SetVariablesAsync` 不得改写提交快照。
- 历史实例缺少 `SubmittedFormJson` 时，任务详情只允许从 `VariableSnapshotJson` 回退展示过滤后的只读字段，不得把系统变量暴露给审批人。
- 强类型绑定发起流程前必须通过 `RuntimeDataModelService` 读取 `ModelCode + KeyField + BusinessKey` 对应业务详情；读不到或无 DataModel 权限时返回业务错误，不允许仅凭前端传入变量创建流程实例。
- 身份映射以系统用户、角色、部门、岗位为源；`ACT_ID_USER`、`ACT_ID_GROUP`、`ACT_ID_MEMBERSHIP` 仅用于引擎候选人、候选组和原生兼容，不成为业务权限来源。

后端接口契约：

- Workflow API 统一返回 `ApiResult<T>`，携带 traceId，并使用 `CancellationToken` 贯穿 Controller、Application Service、引擎和数据库访问；附件下载成功响应为文件流但必须携带 `X-Trace-Id`，失败路径仍返回统一业务错误。
- 模型、部署、绑定、实例、任务、历史、参与人接口分别使用 `workflow:model:*`、`workflow:deployment:*`、`workflow:binding:*`、`workflow:instance:*`、`workflow:task:*`、`workflow:history:*`、`workflow:participant:query` 权限码。
- `GET /api/workflows/form-resources` 使用 `workflow:form:query`，分页返回 `WorkflowFormResourceResponse`：`resourceCode/resourceName/menuCode/businessType/routePath/pageCode/modelCode/keyField/permissionCode/fields[]`，字段包含 `writable`。
- BPM 菜单根节点统一为 `workflow`，在每个启用工作区的应用数据库 baseline 中固定四组：`workflow:workspace`、`workflow:management`、`workflow:analytics`、`workflow:settings`；普通租户应用的左侧菜单只允许读取本应用库 `system_menus`，不得从主库菜单兜底、复制或前端硬编码，重复执行 baseline 不得产生重复菜单。
- 个人工作台深链使用 `/workflows/tasks?tab=todo|done|mine|cc`，前端布局必须以 `pathname + search` 作为菜单高亮和页签 key，React Router 匹配仍只使用 pathname。
- `GET/POST/DELETE /api/workflows/drafts` 与 `POST /api/workflows/drafts/{id}/submit` 使用 `workflow:draft:*`，草稿表必须按 `TenantId + AppCode + OwnerUserId` 过滤；提交时必须具备业务主键并调用原生流程发起服务，不得绕过 `WorkflowInstanceAppService`。
- `GET/POST/DELETE /api/workflows/categories` 使用 `workflow:category:*`，分类按 `TenantId + AppCode + CategoryCode` 唯一业务边界维护，用于发起申请和流程设计目录展示。
- `GET /api/workflows/reports/overview` 使用 `workflow:report:query`，只聚合当前工作区审批实例、任务摘要、瓶颈节点和业务类型统计；不得跨租户应用读取。
- `GET/POST/DELETE /api/workflows/delegations` 使用 `workflow:delegation:*`，委托表按当前 OwnerUserId 归属过滤；同一 Owner 在同一工作区内启用规则时间段不得重叠。
- `GET/POST/DELETE /api/workflows/calendars` 使用 `workflow:calendar:*`，工作日历按 `TenantId + AppCode + CalendarDate` 维护，日期必须在受控年份范围内，并用于 SLA 计算口径。
- `POST /api/workflows/bindings` 保存强类型绑定时必须校验表单资源、已发布 DataModel、主键字段、流程定义和回调规则；未知模型、不可写字段、主键字段、无权限 DataModel、非法值类型必须返回统一业务错误，不得静默保存半配置。
- 低代码页面设计器保存审批绑定时，草稿文档必须保存 `processDefinitionId + processDefinitionKey`；页面发布后同步 `workflow_bindings.ProcessDefinitionId` 作为指定审批流版本，运行时不得只按流程 Key 自动切换到最新版。
- `POST /api/workflows/bindings/status` 使用 `workflow:instance:start`，按 `pageCode/modelCode/businessKeys[]` 批量返回当前 Runtime 行可用绑定和最近审批状态。
- 流程发起、任务通过/驳回/退回、节点进入、流程完成、撤回、终止的回调写回必须与业务实例状态更新处于同库事务；任一回调失败时审批动作、实例状态和目标字段全部回滚，并保留失败日志。
- 设计器保存输出 BPMN XML 与 AsterERP 扩展配置；发布前必须由后端校验 BPMN 并通过原生 repository/deployment 能力部署。
- 设计器业务配置可保存 `businessDesign.formContext` 字段快照；节点条件、表单字段权限和变量映射优先从该字段快照选择，仍允许保留高级手输值以兼容旧设计。
- 任务动作必须覆盖原生能力：claim/unclaim、complete、delegate/resolve、assignee/owner、identity link、comment、attachment、变量读写；高级事件和子流程通过 BPMN 高级视图保留。
- `GET /api/workflows/instances/{processInstanceId}` 返回 `WorkflowInstanceResponse.SubmittedForm`，流程追踪页必须展示与待办详情一致的只读提交表单快照和字段标签。
- `GET /api/workflows/tasks/{taskId}/detail` 使用 `workflow:task:query`，返回 `WorkflowTaskDetailResponse`，聚合当前任务摘要、`WorkflowSubmittedFormResponse`、流程附件、审批意见和流程轨迹；服务层必须校验当前用户是任务办理人、候选人、发起人或流程相关参与人。
- `WorkflowAttachmentResponse` 必须包含 `hasContent`、`downloadUrl`、`createdAt`；详情响应不得返回附件二进制内容。
- `GET /api/workflows/tasks/attachments/{attachmentId}/download` 使用 `workflow:task:attachment`，下载前必须通过任务或流程实例可见性校验；内容型附件只读取目标附件内容并返回文件流，URL 型附件由前端按 `url` 打开。

前端契约：

- 审批设计器采用双层体验：业务审批节点配置负责常用审批人、角色、部门、岗位、发起人、上级、部门负责人、表达式、会签/或签、条件分支、超时升级、抄送、子流程；BPMN 高级视图保留原生 BPMN 能力。
- `bpmn-js`、属性面板和小地图只允许在设计器路由 lazy load，不进入首屏基础包。
- 用户管理、角色管理及后续任意表单通过统一 `menuCode + businessType + businessKey` 发起和追踪审批，不在业务页面复制审批状态机。
- 审批配置页必须使用结构化“回调规则”编辑区，字段选择只展示目标资源中 `writable=true` 且非主键字段；列表页展示回调摘要，保存后重新加载必须从 `CallbackConfig` 完整回显。
- 待办页审批动作必须先打开审批抽屉并加载任务详情；审批人提交通过、驳回、转办、委派等动作前必须能看到提交表单快照、流程附件、历史意见和轨迹。
- 提交表单快照在前端只读展示，审批动作只允许提交意见、目标用户和必要变量，不得把提交快照作为可编辑表单回传。
- 前端按钮可见性使用 `PermissionButton` / `PermissionRoute`，后端 Permission 仍是最终授权边界；无权限账号必须出现菜单不可见或 403 业务响应。

## 7.4 AI 智能中心契约

AI 中心以 AsterERP 原生模块 `AiCenterAppModule` 接入模块化单体，不引入独立微服务，不新增承载主链路的 Bridge/Shim/Facade。后端目录边界固定为：

- `backend/AsterERP.Contracts/Ai/*`：公开 DTO 和请求契约。
- `backend/AsterERP.Api/Modules/Ai/*`：AI 实体、表结构、审计字段、软删除字段、租户应用和归属人字段。
- `backend/AsterERP.Api/Application/Ai/*`：会话、运行、上下文、压缩、多智能体、工作台、运行观测、工具管理、安全与设置等应用编排。
- `backend/AsterERP.Api/Infrastructure/Ai/*`：Semantic Kernel 标准接口实现、国产模型协议归一化、密钥保护、SSE 输出和取消注册。
- `backend/AsterERP.Api/Controllers/Ai*.cs`：HTTP 入口，仅做路由、权限、参数绑定和 `ApiResult`/SSE 响应。

公开 DTO 契约：

- 会话与消息：`AiConversationDto`、`AiConversationDetailDto`、`AiMessageDto`、`AiContextSnapshotDto`。
- 运行与 SSE：`AiChatStreamRequest`、`AiRunDto`、`AiRunParticipantDto`、`AiStreamEventDto`。
- Ask / Plan / Agent 任务计划：`AiTaskPlanDto`、`AiTaskPlanItemDto`、`AiTaskPlanEventDto`、`AiTaskPlanItemOutputDto`、`AiTaskPlanUpsertRequest`、`AiTaskPlanGenerateRequest`、`AiTaskPlanItemPatchRequest`、`AiTaskPlanMoveRequest`、`AiTaskPlanItemActionRequest`。
- 模型与提示词：`AiProviderDto`、`AiModelConfigDto`、`AiPromptTemplateDto`。
- 工作台与运行观测：`AiWorkbenchOverviewDto`、`AiUsageQuery`、`AiObservabilitySummaryDto`、`AiObservabilityTrendPointDto`、`AiRunListItemDto`、`AiRunDetailDto`、`AiToolExecutionQuery`、`AiFailureSummaryDto`。
- 安全与设置：`AiSecuritySettingsDto`、`AiSettingsDto`、`AiSettingsExportDto`、`AiSettingsImportRequest`、`AiCleanupRequest`、`AiCleanupResultDto`。
- 多智能体与能力中心：`AiAgentProfileDto`、`AiPromptVersionDto`、`AiToolDefinitionDto`、`AiToolBindingDto`、`AiWorkflowToolBindingDto`、`AiWorkflowOptionDto`。

数据契约：

- 表一次性覆盖 `ai_providers`、`ai_model_configs`、`ai_conversations`、`ai_messages`、`ai_chat_runs`、`ai_run_participants`、`ai_context_snapshots`、`ai_prompt_templates`、`ai_prompt_versions`、`ai_agent_profiles`、`ai_task_plans`、`ai_task_plan_items`、`ai_task_plan_events`、`ai_task_plan_item_outputs`、`ai_task_process_states`、`ai_tool_definitions`、`ai_tool_bindings`、`ai_workflow_tool_bindings`、`ai_tool_execution_logs`、`ai_usage_logs`、`ai_feedbacks`、`ai_quota_policies`、`ai_security_policies`、`ai_system_settings`、`ai_secret_refs`、`ai_audit_events`、`ai_sk_capability_status`、`ai_knowledge_sources`、`ai_knowledge_documents`、`ai_knowledge_chunks`、`ai_knowledge_index_tasks`。
- 所有 AI 业务表包含 `TenantId`、`AppCode`、审计字段和软删除字段；会话、消息、运行、日志、反馈、知识索引任务等归属类表包含 `OwnerUserId`。
- API Key 只能通过 `AiSecretProtector` 加密落库；列表和详情只返回掩码，不返回密文或明文。
- 会话消息序号必须在会话锁内生成，页面刷新按 `ConversationId + Seq` 稳定回显。
- 上下文压缩快照必须保存原始消息范围、摘要、token 统计和操作者；压缩与生成互斥。
- Plan 状态固定为 `Draft/PlanReady/Approved/Running/Paused/Completed/PartialCompleted/Failed/Blocked/Cancelled/Archived/ParseFailed`；Item 状态固定为 `Pending/Ready/InProgress/WaitingUser/Succeeded/Failed/Skipped/Blocked/Cancelled`。
- Plan 编辑使用 `Revision` 与 `ExpectedUpdatedTime` 乐观锁；`Approved` 与 `Running` 期间结构字段冻结，必须先 `unapprove` 或进入非运行态后再改结构。
- `ai_task_plan_events` 是 Plan/Agent/Task 事件事实表，Plan/Agent/Task 内部状态事件只持久化用于审计、恢复和排障，不推送到聊天 SSE，也不由前端工作台直接展示；`ai_task_plan_item_outputs` 保存 Agent/Tool/User 任务输出、证据和错误摘要。
- `ai_tool_execution_logs` 必须写入 `PlanId`、`ItemId`、`ToolCode`，用于工具白名单、审计和失败恢复定位。
- `ai_system_settings` 保存默认供应商、默认模型、默认智能体、默认提示词、通知 JSON、日志保留天数和清理批量；种子只补缺省值，不覆盖用户已保存设置。
- `ai_tool_definitions`、`ai_tool_bindings`、`ai_workflow_tool_bindings` 是能力中心工具矩阵事实表；高风险 Workflow 工具必须 `RequiresConfirmation=true` 并保留确认人与审计记录。

SSE 契约：

- 流式接口固定使用 `POST /api/ai/chat/conversations/{conversationId}/stream`，响应 `text/event-stream`；不用 EventSource，因为请求体、租户头、授权头和 AbortController 都必须被支持。
- 基础聊天事件名固定为 `run_started/context_built/reasoning_started/reasoning_delta/reasoning_completed/content_started/content_delta/content_completed/usage/error/done`。
- 每个事件体都包含 `runId`、`conversationId`、`traceId`、`seq`、`timestamp`、`data`。
- SSE 开始写入后业务失败必须写 `error` 事件并结束到 `done`；开始写入前校验失败仍返回统一 `ApiResult`。
- Plan/Agent/Task 事件查询通过 `GET /api/ai/task-plans/{planId}/events?afterSeq=` 分页读取，仅供审计、日志和排障页面使用；`/ai/chat` 刷新恢复以 Plan detail、items、outputs 后端状态为准，不由前端推导任务状态。
- 思考链只从供应商返回的 `reasoning_content`/等价字段解析为 `ReasoningDelta` 并持久化到 `ReasoningContent`，不得在后续上下文中把 `reasoning_content` 当作用户/助手消息再次发送。

SK 原生能力契约：

- AI 模型调用统一由 DI `Kernel` 与 `ChatCompletionAgent` 驱动，Ask/Plan 使用 `AiKernelChatRuntime` 调用 Semantic Kernel OpenAI-compatible connector，SSE 来源为 `ChatCompletionAgent.InvokeStreamingAsync`。
- 工具目录保留 `/api/ai/tools` 路径，但运行时必须通过 `Kernel.InvokeAsync(pluginName, functionName, arguments)` 调用 `[KernelFunction]` 元数据；权限、审计和参数脱敏由 SK function invocation 过滤链路承接。
- 工具管理扩展固定为 `GET/POST/PUT /api/ai/tools/definitions`、`GET/PUT /api/ai/tools/bindings`、`GET /api/ai/workflow-tools/available-workflows`、`PUT /api/ai/workflow-tools/bindings`，主链路由 `AiToolManagementService` 实现，不允许新增 Bridge/Shim/Facade 承载业务逻辑。
- `AiAgentProfile.AllowedFunctionsJson` 结构化保存 `pluginName/functionName/permissionCode/autoInvokeAllowed`；旧 `ToolsJson` 仅允许在数据库迁移中一次性复制，不作为运行时兼容分支。
- `GET /api/ai/sk-capabilities` 返回 `capabilityCode/status/frameworkType/implementationSymbol/reason`，状态只允许 `Implemented/FrameworkUnavailable/Blocked/NotApplicable`。
- RAG 知识库 API 固定为 `/api/ai/knowledge/sources`、`/documents`、`/reindex`、`/search`；SQLite Vec 只接受官方 Microsoft/SK VectorData provider。当前未确认官方 SQLite Vec provider 时，向量检索返回 `AiVectorStoreUnavailable` 并标记 `FrameworkUnavailable`。
- 知识图谱 API 固定为 `/api/ai/knowledge/graph/*`，只作为 AI Capability Center 的 `knowledge-graph` tab 能力，不新增一级菜单，不复用或改变 RAG `/api/ai/knowledge/*` 语义。
- 知识图谱权限码固定为：`ai:knowledge:graph:view`、`ai:knowledge:graph:search`、`ai:knowledge:graph:edit`、`ai:knowledge:graph:reindex`、`ai:knowledge:graph:import`、`ai:knowledge:graph:export`；后端 `[Permission]` 是最终授权点，前端 `PermissionButton` 只控制入口与动作展示。
- 知识图谱读接口：`GET /overview`、`GET /node-types`、`GET /relation-types`、`GET /nodes/{id}`、`GET /edges/{id}`；分析接口：`POST /query`、`POST /neighborhood`、`POST /paths`、`POST /impact`；写接口：`POST/PUT/DELETE /nodes`、`POST/PUT/DELETE /edges`、`POST /reindex`、`GET /jobs/{id}`、`POST /import`、`POST /export`。
- 知识图谱表族固定为 `ai_knowledge_graph_node_types`、`ai_knowledge_graph_relation_types`、`ai_knowledge_graph_nodes`、`ai_knowledge_graph_edges`、`ai_knowledge_graph_evidence`、`ai_knowledge_graph_build_jobs`；全部工作区/归属表必须注册既有 ORM data filter，数据库侧以 `TenantId + AppCode` 和归属范围隔离。
- 节点唯一性由 `TenantId + NodeKey` 兜底；节点类型和关系类型必须先存在；边写入必须校验两端节点同工作区存在，`Weight` 固定为 `0..1`；默认删除节点拒绝关联边，显式 cascade 才软删除关联边和证据。
- 图查询默认 `limit=200/depth=1`，最大 `limit=500/depth=3`；路径分析默认 `maxDepth=4`，最大 `maxDepth=6`，最多 20 条路径；知识图谱允许环，但路径搜索必须维护 visited，不能无限遍历。
- `POST /reindex` 以 `SourceId + RequestHash` 幂等；构建失败必须记录 `ErrorCode/ErrorMessage`；成功构建通过导入服务事务性 upsert 或按 source Rebuild，不在构建中直接绕过图谱服务写主链路。
- 导入导出以 JSON DTO 为主契约，导入按 `NodeKey` 和端点关系幂等 upsert；导出必须受 source/nodeType/relationType 过滤和当前工作区 ORM data filter 限制。
- Agent 计划执行必须迁移到 SK Process Framework；当前稳定包未提供可用 .NET Process 运行时，因此执行入口标记 `Blocked`，不得回退旧任务状态机。

并发与多智能体契约：

- 同一会话默认只允许一个非终态父 Run；同会话并发提交必须返回既有 Run 或明确 `RunConflict`，不得产生重复有效 Run。
- 会话级锁 key 固定为 `ai:conversation:{conversationId}`；优先使用分布式锁，单机降级为内存 `SemaphoreSlim`。锁内只处理消息序号、Run 状态和上下文快照短事务，模型流式调用不得持有长事务。
- 重复 `clientMessageId/idempotencyKey` 不得重复写 user message。
- 不同会话、不同用户、不同智能体可并发运行，但受租户、用户、Provider、模型和 `MaxParallelAgents` 限制，默认单个协作 Run 最多 3 个 Agent 并行。
- 多智能体模式固定为 `Single | Collaborative`；`Single` 通过 `ChatCompletionAgent` 流式输出，`Collaborative` 必须使用 SK `AgentGroupChat` selection/termination strategy。当前旧手写并行协调链路已删除，在 `AgentGroupChat` 未落地前返回框架能力不可用错误。
- 取消父 Run 必须级联取消参与 Agent；参与者失败必须记录到 `ai_run_participants`，并按安全设置 `SkipFailed` 或 `FailAll` 处理。

权限与错误码契约：

- 智能中心可见菜单固定为 5 个：`/ai/workbench`、`/ai/capability`、`/ai/observability`、`/ai/security`、`/ai/settings`。旧 `/ai/chat`、`/ai/conversations`、`/ai/providers`、`/ai/model-configs`、`/ai/prompt-templates`、`/ai/agents`、`/ai/usage`、`/ai/logs`、`/ai/knowledge`、`/ai/sk-capabilities` 只能在前端作为 `Navigate` 重定向，不再承载旧业务页面。
- AI 菜单权限码固定为 `ai:workbench:view`、`ai:capability:view`、`ai:observability:view`、`ai:security:view`、`ai:settings:view`。
- AI 动作权限码覆盖 `ai:conversation:*`、`ai:task-plan:*`、`ai:model:*`、`ai:provider:*`、`ai:prompt:*`、`ai:agent:*`、`ai:knowledge:*`、`ai:tool:*`、`ai:security:edit`、`ai:settings:edit`；旧 `ai:chat:*`、`ai:usage:view`、`ai:log:view`、`ai:security:manage` 保留为历史兼容权限码，不作为新菜单直出入口。
- 后端 `Permission` 是最终授权点；前端 `PermissionRoute`/`PermissionButton` 只做入口和按钮展示控制。
- `ai:chat:viewAll` 才允许查看全量会话；普通用户的会话、消息、Run、日志等归属数据通过 ORM data filter 限定 `OwnerUserId`。
- AI 错误码使用 `42100+` 专属区间，至少覆盖 ProviderMissing、ModelDisabled、ProviderRequestFailed、RunConflict、QuotaExceeded、ContextTooLarge、StreamInterrupted、ToolConfirmationRequired、SecurityPolicyViolation、AiPlanParseFailed、AiPlanNotApproved、AiPlanRevisionConflict、AiPlanRunningReadonly、AiTaskInvalidStatusTransition、AiTaskToolNotAllowed、AiPermissionDenied、AiKernelFunctionNotFound、AiModelServiceUnavailable、AiVectorIndexFailed、AiVectorStoreUnavailable、AiFrameworkCapabilityUnavailable 等失败路径。

## 8. 响应式一屏布局原则

ERP 页面默认以一屏工作台为目标：

- 应用壳使用视口高度。
- 页面主体内部滚动。
- 查询区可折叠。
- 表格自动高度。
- 分页固定在表格底部区域。
- 弹窗在小屏自动适配。

## 9. 实施节奏

第 2 章先落地边界和组合方式，然后逐步进入：

1. P0 基础底座。
2. 字典管理 CRUD 闭环。
3. 系统管理模块。
4. 复杂 ERP / MES 能力。

## 10. 任务调度契约

任务调度属于系统运维能力，采用自研管理页面 + Hangfire 执行引擎：

- 客户页面不得直接暴露 Cron 表达式，必须使用友好周期配置：每隔 N 分钟/小时、每天、每周、每月。
- 允许在线新增的任务类型仅限 `Preset` 和 `HttpCallback`，禁止配置任意程序集、类名或方法名。
- 任务定义持久化在 `system_scheduled_jobs`，执行结果持久化在 `system_scheduled_job_logs`，Hangfire 队列和状态使用 `Scheduler:HangfireStoragePath` 指向的 SQLite 文件存储，默认 `./data/astererp-hangfire.db`。
- 任务配置接口必须返回 `ApiResult<T>` 并使用权限码：`system:scheduled-job:query/add/edit/delete/trigger/log`。
- HTTP 回调只允许 GET/POST，URL 主机必须命中 `Scheduler:AllowedHosts`，不得保存 Authorization、token、secret、password 等敏感 Header。
- 任务调度是系统级配置，不按部门、岗位、人员或租户做数据范围过滤；若未来引入租户级任务，必须先补齐 ORM data filter 契约。
- 后端启动后必须从业务表同步启用状态任务到 Hangfire；暂停或删除任务必须移除对应 recurring job，但保留历史执行日志。

## 11. ABP 基础设施契约

ABP 只作为基础设施能力内核引入，AsterERP 原生权限、统一返回、SqlSugar、文件记录、任务中心和审计页面不被 ABP 接管。

Workflow/Flow 模块的 ABP 接入口径：

- `AsterERP.Workflow.Approval.Core`、`AsterERP.Workflow.Forms.Core`、`AsterERP.Workflow.Core`、`AsterERP.Workflow.Persistence`、`AsterERP.Workflow.DependencyInjection` 必须显式引用 ABP 基础包并以 `AbpModule` 方式注册。
- Workflow/Flow 的当前用户、时间、GUID、设置读取、UnitOfWork 与后台周期任务可以使用 ABP 能力，但业务登录态仍由 AsterERP 会话控制，不能反向改写认证来源。
- 工作流通知与轮询任务继续由 AsterERP 业务设置开关控制；后台执行方式可以落在 ABP 依赖的调度设施之上，但不允许把工作流主链路改写成桥接层。

当前用户上下文契约：

- `CurrentUserMiddleware` 仍以 AsterERP `AuthSessionService` 和 `ResolvedAuthenticatedUser` 作为唯一登录态来源；不得改为由 ABP Identity、ABP Permission 或外部 claims 反向决定 AsterERP 登录状态。
- 已认证请求会把 AsterERP 用户上下文投影到 `HttpContext.User`，供 ABP `ICurrentUser` / claims 读取；未认证请求只生成未认证 principal。
- ABP 标准 claim 覆盖 `AbpClaimTypes.UserId`、`UserName`、`Name`、`TenantId`、`Role`；其中 ABP `Role` 使用 AsterERP `RoleCode`，数据库主键保留在 `astererp:role_id`。
- AsterERP 原始上下文额外保留在 `astererp:*` claims：`user_id/tenant_id/tenant_name/app_code/app_name/dept_id/position_id/role_id/role_code/permission_code/data_scope/is_platform_admin/is_tenant_admin`。
- 生产代码只能注入 `Volo.Abp.Users.ICurrentUser` 获取当前登录身份；不得再注入或恢复 AsterERP 自定义 `ICurrentUser`，不得从 `HttpContext.Items` 或 `IHttpContextAccessor` 在业务层读取当前用户。
- AsterERP RBAC、数据过滤、审计字段和平台/租户管理员判断统一通过 `AbpCurrentUserAsterErpExtensions` 读取 `astererp:*` claims；认证判断使用 `IsAsterErpAuthenticated()`，权限模型和业务决策仍归 AsterERP `PermissionAttribute`、`TenantAdminPermissionAttribute`、Guard、Service 各自职责层。
- AsterERP 租户、部门、岗位等 ID 可以是业务字符串，业务持久化与审计必须读取 `GetAsterErpUserId()`、`GetAsterErpTenantId()` 等字符串扩展方法，不能直接依赖 ABP 原生 `Id/TenantId` 的 `Guid?` 语义。

第二阶段基础设施统一入口契约：

- 设置读取统一走 ABP setting provider + AsterERP `system_parameters` value provider；设置写入仍由 AsterERP 设置服务落库并刷新缓存。
- 缓存统一优先 `IDistributedCache<T>`，缓存 key、TTL、失效策略必须在服务内显式定义。
- 文件内容统一走 `IBlobContainer<TContainer>`；文件元数据、权限、业务关联继续归 AsterERP 文件服务。
- 异步入队统一走 `IBackgroundJobManager`；调度表、任务中心、执行日志继续归现有 Hangfire 体系。
- 审计/追踪统一使用 ABP correlation/current-user 语义补齐 AsterERP 操作日志字段，不引入 ABP Pro UI。
- 互斥任务、导入、发布优先使用 `IAbpDistributedLock`；通知、审计、消息发送解耦优先使用 `ILocalEventBus`，需要跨实例时再接分布式事件总线。

后端接口契约：

- `/api/system/infrastructure-settings` 返回 `ApiResult<InfrastructureSettingsResponse>`，按 `email/sms/objectStorage/cache/jobs/audit` 分组。
- 写入接口支持部分更新；secret 字段为空表示保持不变，`clear=true` 表示清空，非空 `value` 表示覆盖。
- `/email/test`、`/sms/test`、`/object-storage/test` 统一返回 `ApiResult<InfrastructureTestResult>`，失败路径只返回业务错误和摘要，不向前端暴露原始外部异常。
- `/message-logs` 必须分页，默认按时间倒序，支持 Provider、通道、结果和 TraceId 查询。

数据与安全契约：

- 设置持久化继续使用 `system_parameters`，由 SqlSugar/schema 初始化负责迁移；ABP 不接管数据库迁移。
- 发送日志持久化到 `system_message_send_logs`，必须包含 `Channel`、`Provider`、`MaskedTarget`、`TraceId`、`CorrelationId`、`Result`、`ErrorSummary`、`DurationMs`、`CreatedTime`。
- 敏感字段不得返回明文，不得进入普通日志；前端只能显示已配置/未配置状态。
- `system:abp-setting:query/edit/test` 必须同时作用于后端 `[Permission]`、菜单种子和前端 `PermissionRoute`/`PermissionButton`。
- 该菜单属于系统运维配置，不按组织、部门、岗位、人员或租户做数据范围过滤；若未来拆成租户级基础设施配置，必须先补齐 ORM data filter 契约。

## 12. 应用中心发布契约

应用发布属于 Platform Application 应用用例，不复用 `system_applications.Status`，发布状态独立记录在发布任务表中。

后端数据契约：

- 发布配置、任务、日志、产物分别持久化在 `system_application_publish_profiles`、`system_application_publish_tasks`、`system_application_publish_logs`、`system_application_publish_artifacts`。
- `system_application_publish_tasks.Status` 只允许表达发布任务生命周期：`Pending`、`Running`、`Succeeded`、`Failed`、`Blocked`。
- 同一 `AppCode` 同时只允许一个 `Pending` 或 `Running` 发布任务；重复提交必须返回业务错误，不得覆盖最后成功产物。
- 任务日志入库保存摘要，完整进程日志落盘到任务输出目录。
- 发布输出目录必须位于配置的发布根目录内；递归删除、产物删除和任务目录创建前必须校验绝对路径归属。
- 应用发布边界按 `TenantId + AppCode` 的菜单、PageSchema、DataModel、权限码和 ProviderKey 推导模块闭包；依赖缺失或权限码/ProviderKey 无法映射时进入 `Blocked`。
- `PermissionCodes` 必须按模块 partial 文件归属；core/runtime 权限可以进入 skeleton，system/platform/tenant/publish 权限文件只能随对应模块进入裁剪源码。
- `backend/module-file-map.json` 是发布源码裁剪的强契约：skeleton、module patterns 和 frontend target aliases 必须指向当前仓库真实路径；发布前必须校验模块路径、依赖闭包和 target alias。声明文件缺失默认 fail-fast；只有显式 `optional:` pattern 才允许跳过。

运行接口契约：

- `POST /api/platform/applications/{id}/publish` 创建发布任务，权限码 `platform:application:publish`。
  - 请求体支持 `backendHost`、`backendPort`、`frontendBasePath`、`frontendApiBaseUrl`，用于写入裁剪 release 的后端监听地址、前端访问路径和前端 API 地址；`frontendBasePath` 必须是单段子路径，不能占用 `/api` 或 `/hubs`。
- `GET /api/platform/applications/{id}/publish-tasks`、`GET /api/platform/application-publish-tasks/{taskId}` 查询任务，权限码 `platform:application:publish-task`。
- `GET /api/platform/application-publish-tasks/{taskId}/logs` 分页查询日志，权限码 `platform:application:publish-log`。
- `POST /api/platform/application-publish-tasks/{taskId}/package` 对成功任务重新生成 zip 产物，权限码 `platform:application:publish`；失败、阻断或运行中任务不得打包。
- `GET /api/platform/applications/{id}/publish-artifacts`、`GET /api/platform/application-publish-artifacts/{artifactId}/download` 查询和下载产物，权限码 `platform:application:publish-artifact-download`。
- `DELETE /api/platform/application-publish-artifacts/{artifactId}` 删除产物，权限码 `platform:application:publish-artifact-delete`。
- Controller 只负责路由、权限、参数绑定和 `ApiResult` 包装；应用校验、任务状态、依赖扫描、构建编排和清理策略归 `PlatformApplicationPublishService` / 发布 Runner。

产物契约：

- 发布任务生成 `source`、`release`、`manifest`、`publish-logs`、`artifacts` 目录。
- 业务应用发布使用精确裁剪源码：`source` 只能包含 skeleton 与闭包模块文件；`SYSTEM` 应用允许全量发布。
- 后端构建固定使用 `dotnet publish -c Release -r win-x64 --self-contained true` 的白名单命令参数形态，实际 RID 和 self-contained 可由 profile 覆盖。
- 前端构建输出到 `release/wwwroot/<FrontendBasePath首段>`，构建时写入 `VITE_APP_BASE_PATH=<FrontendBasePath>`、`VITE_APP_TARGET_APP_CODE=<AppCode>`、`VITE_APP_API_BASE_URL=<FrontendApiBaseUrl>`。
- 裁剪 release 必须包含 `publish-runtime.json`、`start-<AppCode>.cmd`、`start-<AppCode>.ps1`，并在 `manifest/runtime-config.json` 和 `publish-manifest.json.runtimeConfig` 中记录后端监听地址、前端路径和 API 地址。
- 前端业务应用发布必须先生成 target 路由/导航/i18n/runtime registry，再通过 esbuild metafile 校验可达图；reachability 脚本必须兼容 Vite alias、目录 index 和 worker query 解析；`PUBLISH_REACHABILITY_PRUNE=1` 时必须删除 `src` 下不可达源码并记录 pruned 列表。
- 发布末段必须执行泄漏扫描：对 `release/wwwroot` 前端静态文件和后端 DLL/EXE 扫描被排除模块的权限码、ProviderKey、API 路由与类型/组件名；命中时任务进入 `Failed`，不得登记成功产物。
- `publish-manifest.json`、`frontend-purity-report.json`、`leak-scan-report.json`、`checksum-manifest.json` 必须能够解释源码与静态产物的包含/排除原因和校验值。
- 产物 zip 必须包含 `source`、`release`、`manifest`，不得包含 `artifacts` 自身或 `publish-logs` 内部过程日志。
- 无菜单、无 PageSchema 或依赖图无法闭合时任务必须进入 `Blocked`，不得生成“假成功”产物。
- `Program.cs` 可以托管 `wwwroot/<AppCode>/index.html` 的 SPA fallback，但不得吞掉 `/api` 与 `/hubs` 路由。

## 13. AsterScene 原生 ToC 契约

AsterScene 是当前唯一的 ToC 场景创作与播放主链路。后端只走 `Controllers/AsterScene*`、`Application/AsterScene`、`Modules/AsterScene`、`Contracts/AsterScene`，前端只走 `src/features/aster-scene`。历史实现已退出路由、菜单、权限、模块发现、文档契约和验收口径；新能力不得通过 Bridge、Shim、Facade 或兼容代理承载。

核心文档契约：

- `SceneDocument` 固定包含 `meta/revision/identity/assets/actors/components/materials/geometries/uv/interactions/timeline/runtime/publish/quality/extensions`；保存统一要求 `expectedRevision + clientMutationId + documentHash`，旧 revision 返回冲突错误，不覆盖草稿。
- `RuntimeManifest` 固定包含 `publishCode/documentHash/entrySceneId/capabilityPolicy/assetVariants/preload/lazyGroups/security/analytics`；Player 只读已发布 Manifest，不读取草稿。
- AsterScene 自有文档不得携带版本字段；后端保存前必须校验产品名、入口场景、actor/component 引用、几何/材质引用和资产引用完整性；前端 hash 与后端 hash 均以规范化文档内容为准。

API 与数据契约：

- 原生 API 面为 `/api/public/asterscene/*`、`/api/creator/*`、`/api/asterscene/projects|assets|jobs|runtime/*`、`/api/asterscene/projects/{projectId}/publish`、`/api/community/asterscene/*`、`/api/subscriptions/asterscene/*`、`/api/usage/asterscene/*`、`/api/admin/asterscene/moderation/*`、`/api/asterscene/support/tickets|tickets/{id}|tickets/{id}/comments|tickets/{id}/close`。
- 全部接口返回 `ApiResult<T>` 并携带 traceId；写接口必须幂等，重复 `clientMutationId` 返回已提交结果或冲突原因。
- 表族统一使用 `asterscene_*`，覆盖 project/document/asset/version/upload/job/publish/public/community/moderation/subscription/usage/analytics/support/ai credit；历史表名不得作为 AsterScene 主存储。
- 工作区业务表实现 `TenantId + AppCode + OwnerUserId` 边界并注册 ORM data filter；public slug、creator handle、publishCode、reaction、follow、ledger、asset version 等唯一性由数据库索引兜底。

前端与路由契约：

- Public 路由为 `/explore`、`/templates`、`/works/:slug`、`/creator/:handle`、`/player/:publishCode`、`/pricing`；工作区路由为 `/dashboard`、`/studio/:projectId`、`/assets`、`/admin/asterscene`。
- `src/features/aster-scene/api` 只封装真实 HTTP；`model` 固定 DTO；`core` 放 Command/Transaction、document kernel、ResourceRegistry、WorkerManager；`state` 分离 document/selection/viewport；`pages` 只装配真实 API 与组件。
- Studio 必须具备真实项目创建、文档读取、命令修改、autosave、手动保存、发布、资产登记、撤销/重做、刷新恢复；Player 只消费 `RuntimeManifest`。

权限、治理和商业契约：

- 权限码统一使用 `asterscene:*`：项目、Studio、资产、发布、社区、订阅、用量、AI、治理、Admin 均需要后端 `Permission` 兜底，前端用 `PermissionRoute/PermissionButton` 做体验控制。
- 订阅、配额和用量走只追加或冲正的 usage ledger；AI Credits 先扣减后创建受控 job，失败必须退款，结果进入人工 Apply 门禁，不允许未审核 AI 输出直接污染已发布资源。
- 举报、审核、移除/恢复、申诉、支持工单均写真实表；匿名只允许读取公开且审核允许的作品与 Manifest。

验收契约：

- 后端验证：停止锁定的 `AsterERP.Api` 进程后执行 `dotnet build AsterERP.sln`、`dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj`，后端改动需重启 API 后做真实接口冒烟。
- 前端验证：`npm run typecheck && npm run lint && npm run test -- --run && npm run build`。
- 全链路验收必须覆盖 Create、Explore、Player、Assets、Cloud、Admin：新建项目、打开 Studio、保存冲突、分片上传、发布、回滚、公开作品页、点赞/收藏、Remix、配额、AI job、举报审核和用量查询均需有 Pass/Fail/Blocked 记录。

## 14. Flowise Studio 原生集成契约

Flowise Studio 在 AsterERP 中作为原生工作区能力集成。Flowise 源码仅用于菜单、页面和交互能力参考；不得 iframe、不得直连独立 Flowise Node 服务、不得新增 Bridge/Shim/Facade 承载业务主链路。

菜单与前端契约：

- 顶级菜单为 `Flowise`，子菜单按 Flowise `dashboard.js` 可见顺序：Chatflows、Agentflows、Executions、Assistants、Marketplaces、Tools、Credentials、Variables、API Keys、Document Stores、Evaluations 分组、User & Workspace Management 分组、Others 分组。
- 工作区路由统一使用 `/flowise/*`；Flowise 原始路径如 `/chatflows`、`/agentflows`、`/canvas/:id` 仅做前端别名或重定向，不替换 AsterERP 登录、注册、认证或系统设置页。
- 前端实现位于 `frontend/AsterERP.Web/src/features/flowise-studio`，列表页、详情页、画布页必须复用 AsterERP `PermissionButton`、`httpClient`、React Query、统一反馈和主题 token；标准表格页可继续使用 `CrudPage/DataTable`，Flowise 原生卡片/画布/详情页使用模块内专属组件。
- 画布页使用 `@xyflow/react`，节点卡、输入输出 handle、palette、检查器、聊天测试、dirty 状态、保存和运行反馈均按 Flowise 功能结构实现，视觉 token 采用 AsterERP。
- 所有 Flowise 内容区可见文案必须接入 `zh-CN/en-US`，新增 key 位于 `frontend/AsterERP.Web/src/features/flowise-studio/i18n` 并汇入 `src/core/i18n/messages.ts`。

后端 API 契约：

- 统一路由前缀 `/api/ai/flowise`，所有响应返回 `ApiResult<T>`，Controller 只负责路由、参数绑定、权限和响应包装。
- 核心资源 API 使用各资源的强类型路由，覆盖 Chatflows、Agentflows、Assistants、Marketplaces、Tools、Credentials、Variables、API Keys、Document Stores、Datasets、Evaluators、Evaluations、SSO Config、Roles、Users、Login Activity、Logs；不得以一个通用资源表或兼容投影承载不同资源的业务语义。
- 节点目录 API：`GET /nodes/definitions`、`GET /canvas/nodes`，返回 Flowise 风格 `inputParams/inputAnchors/outputAnchors/icon/tags`。
- 画布 API：`GET /canvas/{resourceId}`、`POST|PUT /canvas`、`POST /canvas/validate`，保存时节点 key 必须唯一，边两端节点必须存在，并持久化原生 `flowData` roundtrip。
- Prediction API：`POST /prediction`、`POST /prediction/feedback`、`POST /prediction/lead`，必须生成 execution、chat message、source documents、feedback/lead 持久化记录。
- 执行 API：`GET /executions`、`GET /executions/{id}`、`POST /executions/run`、`DELETE /executions/{id}`；执行记录必须包含 `Status`、`TraceId`、`DurationMs`、`InputJson`、`OutputJson`、`SourceDocumentsJson`、`ErrorCode`、`ErrorMessage`。
- Document Store API：`GET /document-stores/{storeId}/detail`、`/files`、`/chunks`、`/vector-config`、`POST /document-stores/query`。
- Evaluation API：`GET /datasets/{id}/detail`、`GET /datasets/{id}/rows`、`GET /evaluators/{id}/detail`、`GET /evaluations/{id}/detail`、`GET /evaluations/{id}/result`、`POST /evaluations/{id}/run-again`。
- 工作区 API：`GET/POST/PUT/DELETE /workspaces`；账号 API：`GET/PUT /account/settings`。
- 导入导出 API：`POST /{resourceType}/import`、`POST /{resourceType}/export`；重复导入按 resource type + key 幂等 upsert 或 skip。

数据与安全契约：

- 表族统一使用 `ai_flowise_*` 专用实体；Chatflows/Agentflows 的画布以原生 Flowise `FlowiseChatFlowEntity.FlowData` 单字段 roundtrip 持久化，不恢复已删除的 `resources`、`canvases`、`canvas_nodes`、`canvas_edges` 兼容投影；其余工作区、执行、审计、节点目录、消息、反馈、Lead、文档、向量、数据集和评估结果分别使用专用实体。
- 所有业务表必须包含 `TenantId`、`AppCode`、`OwnerUserId`、`IsDeleted`，涉及工作区的表额外包含 `WorkspaceId`；数据边界通过 AI ORM data filter 注册到数据库侧谓词。
- 各专用资源按自身业务 key 建立 `TenantId + AppCode + OwnerUserId` 范围内的唯一约束；工作区 key 使用 `TenantId + AppCode + WorkspaceKey`；Chatflow/Agentflow 与其 `FlowData` 为一对一原生存储关系。
- API Key 与 Credential 明文不得普通回显；`SecretCipherText` 加密保存，`SecretMask` 默认展示，reveal 必须有 `flowise:secret:reveal` 并写审计。
- 执行请求支持 `IdempotencyKey`；同一资源同一 key 只返回一个执行记录。

RBAC 契约：

- 菜单级权限使用 `flowise:*:view` 或对应 manage 权限；动作级权限覆盖 `flowise:edit`、`flowise:run`、`flowise:import`、`flowise:export`、`flowise:secret:reveal`、`flowise:manage`。
- 后端 Controller 必须使用 `[Permission]` 兜底；服务层再按具体资源类型执行精确权限判断。
- 前端只把权限用于隐藏或禁用动作，不作为安全边界；直接访问无权限 API 必须返回 403 或统一业务错误。

验收契约：

- 构建验证：`dotnet build AsterERP.sln`、`npm run typecheck`、`npm run build`。
- 接口冒烟需覆盖：`/api/ai/flowise/overview`、`/chatflows`、`/agentflows`、`/canvas/nodes`、`/nodes/definitions`、`/canvas/{resourceId}`、`/canvas/validate`、`/prediction`、`/executions`、`/credentials`、`/document-stores/{storeId}/detail`、`/document-stores/query`、`/evaluations/{id}/result`、`/workspaces`、`/logs`。
- 浏览器验收需覆盖：`/flowise/chatflows`、`/flowise/agentflows`、`/flowise/canvas/:id`、`/flowise/document-stores/:storeId`、`/flowise/evaluation-results/:id`；菜单、tab、权限、布局、保存和刷新都必须可验证。

## 应用发布边界补充契约（Task E）

- `POST /api/platform/applications/{id}/publish` 必须明确提供 `TenantId`，并校验目标租户已启用该 `AppCode` 且未过期；缺失 `TenantId` 必须拒绝，禁止 Runner 以空租户聚合所有租户的菜单、PageSchema、DataModel、权限码或 ProviderKey。
- Runner 必须在 `TenantId + AppCode` 边界内生成依赖闭包，并将真实 `AppCode`、`FrontendBasePath`、`FrontendApiBaseUrl` 传递给 reachability/build；前端产物必须写入 `release/wwwroot/<FrontendBasePath 首段>`。
- `manifest/runtime-config.json`、`release/publish-runtime.json`、`appsettings.json:PublishedRuntime` 和 `start-<AppCode>.cmd/.ps1` 必须记录或传递真实 `TenantId`、`AppCode`、`FrontendBasePath` 与 API 地址；构建命令不得使用空的 `VITE_APP_TARGET_APP_CODE`。
