# ProjectManagement 信息架构与导航模型

> Linear：`HAO-432`（M0）
> 目标：让用户从正确的租户/应用工作区进入项目域，在项目中心选择项目后获得稳定、可分享、受权限保护的项目工作区；不重复平台系统管理、应用数据中心、文件、通知或 IM 的既有入口。
> 事实基线：`workspaceRoutes.full.tsx`、`AppRouter.tsx`、`AppLayout.tsx`、当前 ProjectManagement 页面/API，以及 `docs/project-management-product-architecture.md`、`docs/project-management-capability-matrix.md` 和 `HAO-429` 用户场景词汇表。

## 1. 结论与设计原则

ProjectManagement 应以两个一级入口组织，而不是以每一种任务视图、协作能力或治理操作各建一个平级菜单：

1. **项目中心**：从当前工作区选择或创建项目，是进入具体项目的唯一常规入口。
2. **我的工作**：跨当前用户有权项目的个人执行入口，不要求先选定单一项目。

项目一旦被选定，用户进入项目工作区；概览、任务、里程碑、成员、报表和设置属于该工作区的二级导航。任务树/行、卡片、看板、甘特和日历只是“任务”的同级投影，必须共享 URL 查询状态、选中任务、权限和命令，不能变成五个一级菜单。

数据空间、同步、备份、回收站和审计是项目域的**治理入口**，仅在当前应用工作区中显示，且由独立 PermissionCode 控制；它们不复制应用“数据中心”、平台系统管理、全局通知、文件库或 IM 会话的职责。

```text
平台工作区
  └─ 应用中心 / 进入应用             （选择租户与应用，不展示项目数据）

应用工作区：TenantId + AppCode
  ├─ 项目中心                         /projects
  ├─ 我的工作                         /my-work
  ├─ 项目工作区：ProjectId
  │   ├─ 概览                         /projects/:projectId/overview
  │   ├─ 任务（五类视图）             /projects/:projectId/tasks|list|card|board|gantt|calendar
  │   ├─ 里程碑                       /projects/:projectId/milestones
  │   ├─ 成员                         /projects/:projectId/members
  │   ├─ 报表                         /projects/:projectId/reports
  │   └─ 设置                         /projects/:projectId/settings
  └─ 项目治理
      ├─ 回收站                       /project-recycle-bin
      ├─ 数据空间与同步/备份           /project-data-space
      └─ 审计中心                     /project-audit-center
```

## 2. 工作区与数据边界

### 2.1 平台、应用和项目不是同一层级

| 层级 | 负责什么 | 用户如何进入 | 绝不能承担什么 | 当前源码证据 |
|---|---|---|---|---|
| 平台工作区 | 租户、应用、平台级系统管理和“进入应用”。 | `/platform/applications` 等平台路由。 | 不能把某应用的项目列表当作平台全局项目列表。 | `AppRouter.tsx` 注册平台路由；`AppLayout` 仅在存在平台 token 时提供返回平台。 |
| 应用工作区 | 当前会话的 `TenantId + AppCode`，承载项目域的所有路由、权限和数据过滤。 | `/tenants/:tenantId/apps/:appCode/admin/...`。 | 不能由 URL、请求体或已打开标签页替换为另一租户/应用。 | `ApplicationWorkspaceRoute` 仅在路径租户/应用与 `currentWorkspace` 一致时渲染，否则 `Page403`；`AppLayout` 将本地路径转成带前缀显示路径。 |
| 项目工作区 | 当前应用中的一个 `ProjectId`，承载任务、里程碑、成员和项目级协作。 | 从项目中心选择项目，或有效深链直接进入。 | 不能作为跨项目个人待办、整库数据空间或平台用户管理的替代。 | `ProjectManagementTaskWorkspacePage`、概览、成员、里程碑页均从路由参数读取 `projectId`；ORM Data Filter/对象策略负责成员可见性。 |

**规范：** 项目管理页面不得把“应用工作区”缩写成“项目”，也不得把 `ProjectId` 存为跨工作区可复用的全局选择。项目页的缓存键、URL、实时订阅和后端查询必须同时受当前会话的 `TenantId + AppCode` 与对象权限约束。

### 2.2 与既有系统入口的职责切分

| 既有入口 | 继续拥有的职责 | 项目管理中的正确连接方式 | 禁止重复建设 |
|---|---|---|---|
| 平台应用中心 | 租户、应用与进入应用。 | 从平台选择应用，应用工作区再显示项目中心。 | 在项目中心维护租户/应用或跨应用项目。 |
| 应用数据中心 | 数据源、数据模型、API、查询数据集、通用同步与集成配置。 | 项目“数据空间”只显示项目域摘要及受控的 `.bqsync`、备份/恢复操作；应用级配置仍跳转/保留在数据中心。 | 第二套数据源、模型、连接测试或通用文件下载管理页。 |
| 系统管理 | 用户、部门、岗位、角色、菜单与权限码。 | 成员候选人复用主数据；项目仅维护 `ProjectMember` 关系和角色/范围。 | 独立人员主数据、角色维护、权限管理页面。 |
| 通知中心 | 用户级未读、已读、投递和跳转。 | 顶栏 `ProjectManagementNotificationEntry` 进入后端重新鉴权的项目/任务目标。 | 项目内复制另一套全局收件箱。 |
| IM | 会话、消息、未读、投递与历史。 | 项目/任务只维护关联会话；从任务打开 IM、从消息安全跳回任务。 | 把 IM 消息复制成任务评论，或在项目页重做聊天列表。 |
| 文件能力 | 文件存储、下载/预览基础设施。 | 任务详情以任务关联和项目权限访问附件。 | 项目域单独的文件库或绕过任务权限的 FileId 直链。 |

可追溯：平台/应用路由由 `AppRouter.tsx` 与 `workspaceRoutes.full.tsx` 提供；项目能力适配边界由 `HAO-417`、`HAO-420`，数据空间/治理由 `HAO-517`–`HAO-527`，通知/IM 由 `HAO-491`–`HAO-494` 闭环。

## 3. 一级导航模型

### 3.1 菜单结构与可见性

| 一级入口 | 目标用户结果 | 规范路由 | 入口最小权限 | 当前路由/页面事实 | 后续闭环 |
|---|---|---|---|---|---|
| 项目中心 | 浏览授权项目、搜索、创建项目、选择一个项目。 | `/projects` | `project-management:project:view` | 已有 `ProjectManagementPage`；列出项目、搜索、创建/编辑/删除动作。另有 `/project-management` 指向同一页面，形成重复入口。 | `HAO-418` 路由/菜单，`HAO-458` 列表、收藏、最近项目。规范上 `/projects` 为唯一菜单/深链主路径，旧路径应迁移为重定向，不能双菜单长期共存。 |
| 我的工作 | 在不预选项目时处理“我负责、我参与、提及、今日、未来、逾期、阻塞”等跨项目任务。 | `/my-work` | 目标为 `project-management:task:view`，并经 ORM 数据过滤 | 已有 `ProjectManagementMyWorkPage`、URL 分类/项目/排序参数和授权项目筛选。当前路由外层实际使用 `project-management:project:view`。 | `HAO-460` 完整聚合/快捷入口；`HAO-415` 需令菜单/路由权限和能力语义一致。 |
| 项目治理 | 进入受控的回收站、数据空间、审计；不是普通执行菜单。 | 三个独立治理路径，见第 5 节。 | 各自的查看/动作权限。 | 三条路由及页面均已注册。 | `HAO-454`、`HAO-517`、`HAO-523` 及子 Case。建议在侧栏作为“项目治理”可折叠分组，而不是与项目中心并列复制数据中心。 |

**菜单种子约束：** 前端 `routeMeta` 只是路由/标签元数据；实际左侧菜单来自后端 `system_menus`。因此“路由存在”不等于菜单已播种。`HAO-415` 的菜单种子和拒绝路径必须把上述入口按 PermissionCode 加入当前应用菜单，并避免同时播种 `/project-management` 和 `/projects`。

### 3.2 项目选择与无项目状态

项目选择只能从以下两种方式发生：

1. 在项目中心点击“进入项目”，进入 `/projects/:projectId/overview`；当前页面已采用该跳转方式。
2. 通过受服务端对象权限保护的有效项目深链，直接进入该项目的概览或任务投影。

项目中心必须按状态区别处理：

| 状态 | 必须呈现的结果 | 权限/导航处理 | 当前事实与追溯 |
|---|---|---|---|
| 当前工作区没有项目，且有 `project:add` | 空状态说明“还没有项目”，提供创建项目主操作。 | 创建成功后应导航到新项目概览，而不是停留在空列表。 | 当前有 `ProjectManagementPageStateView.empty`，创建表单已经存在；“创建后进入项目”待 `HAO-458`/项目中心交互闭环。 |
| 当前工作区没有项目，且无 `project:add` | 显示无项目说明，不显示无效创建入口。 | 不能把 403 伪装为“没有项目”。 | `PermissionGuard` 可隐藏创建表单；须在浏览器验收确认。 |
| 有项目但无筛选结果 | 显示“未找到匹配项目”，保留/提供清除筛选。 | 不改变当前工作区或项目选择。 | 当前列表有搜索/清空，空态文本尚未区分“零项目”和“零结果”；`HAO-458` 补齐。 |
| 用户从我的工作选择项目过滤 | 下拉只列出被数据过滤后的授权项目。 | 选择仅是 My Work 查询筛选，不建立全局“当前项目”。 | `ProjectManagementMyWorkPage` 已以 URL `projectId` 过滤；`HAO-460` 完整验收。 |
| 项目被归档 | 仍允许具读取权限者从项目中心/深链打开只读项目工作区。 | 不可显示可写任务/成员/设置动作；恢复后重新按角色授权。 | 当前项目状态已存在，但路由/页面尚未形成归档只读壳；`HAO-438` 闭环。 |

`useProjectManagementWorkspaceStore` 仅是当前草案状态，不能作为深链、刷新或权限的真实来源；项目身份必须来自 `:projectId`，工作区身份必须来自当前会话。

## 4. 项目工作区二级导航

### 4.1 导航树

```text
项目中心
  -> [项目名称 / 编码]
      -> 概览
      -> 任务
          -> 树 / 行 / 卡片 / 看板 / 甘特 / 日历
          -> 选中任务详情抽屉（?taskId=...，不是新一级页面）
      -> 里程碑
      -> 成员
      -> 报表
      -> 设置
```

项目标题区应始终展示项目名称、编码、状态（特别是归档）和当前工作区标识；页面可从概览、任务或里程碑进入，但不允许只凭前端已缓存的项目名称作授权判断。

| 二级项 | 规范路由 | 主要职责 | 最小导航权限 | 当前状态与可追溯 |
|---|---|---|---|---|
| 概览 | `/projects/:projectId/overview` | 项目进度、风险/逾期/阻塞摘要、里程碑健康和最近活动；是进入单一项目的默认页。 | `project-management:project:view` + 项目对象可见性。 | 已有 `ProjectManagementOverviewPage`。当前通用路由把它按 `task:view` 保护，需由 `HAO-415` 对齐到实际读取能力；概览能力追溯 `HAO-459`。 |
| 任务 | `/projects/:projectId/tasks` | 默认树投影和任务命令中心。 | `project-management:task:view` + 项目对象可见性。 | 已有 `ProjectManagementTaskWorkspacePage`；`HAO-462`、`HAO-463`、`HAO-466`。 |
| 任务投影 | `/list`、`/card`、`/board`、`/gantt`、`/calendar` | 同一 Task Workspace 的查询投影；切换时保持筛选、排序、选中任务和工作区。 | 同“任务”。 | 现有 `resolveView(pathname)` 解析六个 `ViewKey`，`useTaskWorkspaceUrlState` 共享 URL 状态。产品按五类验收，tree/list 是一类。相关 `HAO-465`、`HAO-469`–`HAO-477`、`HAO-478`。 |
| 里程碑 | `/projects/:projectId/milestones` | 管理独立里程碑、健康、日期与任务关联。 | `milestone:view`；写操作另需 `milestone:manage` 和对象角色。 | 已有页面，但外层当前按 `task:view`。`HAO-425` 负责完整业务/页面验收。 |
| 成员 | `/projects/:projectId/members` | 显示成员、角色和 Lead 主题范围。 | `member:view`；写操作另需 `member:manage` 和对象角色。 | 已有页面，但外层当前按 `task:view`。`HAO-424` 负责范围/成员实时性。 |
| 报表 | `/projects/:projectId/reports` | 项目范围的指标、导出和可下载报告；不替代平台治理审计。 | 查看权限须先明确；导出使用 `report:export`。 | 路由已注册但当前由通用 Task Workspace 承载，不能视为已实现的报表页。`HAO-505`–`HAO-510` 交付。 |
| 设置 | `/projects/:projectId/settings` | 项目级设置（如项目元数据、归档、成员管理入口、共享视图策略）；不能放系统用户/角色/数据源配置。 | `project:edit`/对应动作权限 + Owner/Manager 对象角色。 | 路由已注册但当前由通用 Task Workspace 承载，不能视为项目设置页。项目生命周期 `HAO-438`、共享视图 `HAO-504` 提供后续边界。 |

**路由兼容规则：** 当前各任务投影是独立深链，以维持浏览器前进/后退和收藏链接；导航 UI 可将它们呈现为“任务”内部的视图切换，不能把 `board/gantt/calendar` 再播种成左侧一级菜单。

### 4.2 任务详情、抽屉和 URL

任务详情是任务工作区内的选择态，不是项目二级菜单。规范 URL 采用：

```text
/projects/:projectId/tasks?taskId=:taskId&q=&status=&assignee=&milestoneId=&groupBy=&dueFrom=&dueTo=&sortBy=&sortDirection=&page=&pageSize=
```

- `taskId` 用于选中任务并打开详情/协作面板；任务不再属于当前查询结果、被删除或无权时，必须清除该参数并显示可恢复提示。
- 查询参数由 `useTaskWorkspaceUrlState` 白名单化；投影路径改变不创建第二份筛选状态。
- 评论、附件、提醒和关联 IM 是详情分区；它们不生成另一个项目层级路由，也不因 URL 含 TaskId 绕过服务端项目成员校验。

可追溯：当前 URL State/选择清理逻辑位于 `useTaskWorkspaceUrlState.ts` 和 `ProjectManagementTaskWorkspacePage.tsx`；统一工作区边界由 `HAO-478`，协作区由 `HAO-479`–`HAO-494`。

## 5. 项目治理导航

| 治理项 | 规范路由 | 面向对象 | 入口/动作权限 | 不能替代 | 当前事实与后续 Case |
|---|---|---|---|---|---|
| 回收站 | `/project-recycle-bin` | 当前工作区内可见的软删除项目与任务。 | 路由至少 `project:view`；恢复/彻底删除分别要求 `project:restore`、`task:restore`、`project:purge` 与对象角色。 | 归档列表、系统级垃圾箱、永久删除确认页。 | 已有页面/路由，当前外层为 `project:view`；`HAO-454`、`HAO-455` 交付恢复和彻底删除边界。 |
| 项目数据空间 | `/project-data-space` | 当前应用工作区的项目域摘要、`.bqsync` 预览/导入导出、备份恢复。 | 页面摘要 `project:view`；导出 `sync:export`、导入 `sync:import`、备份/恢复 `backup:manage`，并需要高风险确认。 | 应用数据中心的数据源/模型管理或任意整库管理入口。 | 已有 `ProjectManagementDataSpacePage`，动作已使用 `PermissionButton/Guard`；完整授权空间、容量、后台/恢复治理由 `HAO-517`–`HAO-522`。 |
| 审计中心 | `/project-audit-center` | 项目域活动、操作、同步、导入导出和高风险治理证据。 | `project-management:audit:view`；导出另需 `project-management:audit:export`，且结果受项目可见性和脱敏限制。 | 任务活动时间线、普通项目报表、系统日志页面。 | 已有 Audit 页面/路由且外层使用 `project-management:audit:view`；`HAO-523`–`HAO-527` 负责详情、导出与保留策略。 |

治理入口默认不应在项目工作区二级导航里重复出现：它们的范围是当前应用工作区或多个授权项目，页面顶部应明确展示 `TenantId / AppCode`、高风险状态和返回项目中心的路径。

## 6. 面包屑、标签页与路径规范

### 6.1 目标层级

| 页面类别 | 目标面包屑 | 标签标题 | 标签策略 |
|---|---|---|---|
| 项目中心 | `应用名称 > 项目管理 > 项目中心` | `项目中心` | 默认/不可关闭或回到工作区首页。 |
| 我的工作 | `应用名称 > 项目管理 > 我的工作` | `我的工作` | 可保持筛选查询的单个标签。 |
| 项目概览 | `应用名称 > 项目管理 > {项目名称}` | `{项目名称}` | 一个项目一个概览标签；以完整工作区路径为 key。 |
| 项目工作区 | `应用名称 > 项目管理 > {项目名称} > 任务 > {视图}` | `{项目名称} · 任务` 或 `{项目名称} · 看板` | 同项目各视图/查询可共享一个项目工作区标签，或者使用完整 `pathname + search` 保留独立查询态；实现需选定一种并稳定化。 |
| 里程碑/成员/报表/设置 | `应用名称 > 项目管理 > {项目名称} > {子项}` | `{项目名称} · {子项}` | 详情型标签应保留 `ProjectId`，关闭后返回同项目概览。 |
| 回收站/数据空间/审计 | `应用名称 > 项目管理 > 项目治理 > {子项}` | `{子项}` | 作用域是应用工作区，不应附带任意 `ProjectId`。 |

### 6.2 当前实现差距与实施约束

`AppLayout` 目前只生成一个面包屑标签，且 ProjectManagement 路由的 `routeMeta` 大多共用 `nav.projectManagement`；所有这些路由默认 `tabMode=menu`、`cachePolicy=tab-alive`。因此，当前 UI **尚未**展示项目名称层级，也没有项目级详情标签命名，不能把“路由已注册”当成面包屑/标签页验收完成。

后续实现必须：

1. 从已授权的项目查询结果取得显示名称，仅用于展示；对象权限仍由路由/API 重验。
2. 以工作区显示路径（含 `/tenants/:tenantId/apps/:appCode/admin`）作为标签 key 前缀，防止同 `ProjectId` 在不同工作区的标签混淆。
3. 为项目详情路由设定明确 `tabMode=detail` 或等价的项目上下文元数据；不能依赖当前通用 `nav.projectManagement`。
4. 保持当前 `pathname + search` 可用于任务筛选/选中状态的能力；关闭标签的回退地址应优先是同项目概览，否则才是 `/projects`。
5. 菜单高亮使用工作区本地路径，显示/跳转使用工作区前缀路径，沿用 `AppLayout.toWorkspaceLocalPath` / `toWorkspaceDisplayPath` 的分工。

可追溯：现有标签/路径转换在 `AppLayout.tsx`，路由元数据在 `routeMeta.ts`，固定应用路由测试在 `applicationWorkspaceRoutes.test.tsx`；项目导航/路由基线为 `HAO-418`，任务工作区状态为 `HAO-478`。

## 7. 深链、刷新与异常状态

| 触发条件 | 必须行为 | 当前源码事实 | 后续验收 |
|---|---|---|---|
| 有效的应用工作区项目深链 | 通过应用前缀进入；重建路由、项目查询、任务 URL 状态和实时订阅。 | `ApplicationWorkspaceRoute` 在路径与当前工作区匹配时渲染；任务查询参数已可解析。 | `HAO-418`、`HAO-478`：浏览器刷新、深链和对象权限 E2E。 |
| 路径租户/应用与当前会话不一致 | 返回 403，不自动切换到 URL 指定工作区。 | `ApplicationWorkspaceRoute` 返回 `Page403`。 | 保持该拒绝路径；工作区切换由 `HAO-521` 收敛缓存/订阅/未保存状态。 |
| 项目不存在、已软删除或当前用户不再是成员 | 不展示项目名称、任务缓存或附件；给出“项目不存在或无权访问”，提供返回项目中心。 | 概览当前以 `PageError` 显示，不是专用 404；任务工作区需依赖查询结果处理。 | `HAO-414`、`HAO-454`、`HAO-478`：区分 403、404、软删除，清除选中任务与缓存。 |
| 未有路由 | 显示 `Page404`，保留返回工作区首页/项目中心入口。 | `workspaceRoutes.full.tsx` 已有 `path: '*' -> Page404`。 | `HAO-418` 浏览器验收。 |
| 无功能权限 | 路由层显示 `Page403`，侧栏不显示菜单；直接 API 仍由后端拒绝。 | `PermissionRoute` 已在路由层使用；项目动作另用 `PermissionButton/Guard`。 | `HAO-415` 全量菜单/API 拒绝测试。 |
| 归档项目深链 | 可读时进入明确的“归档，只读”壳；写操作隐藏且后端仍拒绝。 | 状态和页面路由存在，但目前没有归档工作区壳。 | `HAO-438`。 |
| 未保存表单后切换项目/工作区/标签 | 阻止或确认离开；确认后取消本地草稿和旧工作区缓存。 | 项目中心已有 `beforeunload` 提示，尚未覆盖完整项目工作区切换。 | `HAO-478`、`HAO-521`。 |

## 8. 路由与导航追溯矩阵

| 导航能力 | 当前源码入口 | 状态 | Linear 追溯 |
|---|---|---|---|
| 系统与应用工作区前缀、403、404 | `AppRouter.tsx`、`ApplicationWorkspaceRoute.tsx`、`workspaceRoutes.full.tsx` | 已有基础机制 | `HAO-418`（应用菜单/路由验收） |
| 项目中心/我的工作路径 | `projectManagementRoutePaths`、`ProjectManagementPage.tsx`、`ProjectManagementMyWorkPage.tsx` | 路由与部分页面已存在；菜单去重/权限细化待完成 | `HAO-415`、`HAO-418`、`HAO-458`、`HAO-460` |
| 项目工作区与六投影 | `ProjectManagementTaskWorkspacePage.tsx`、`useTaskWorkspaceUrlState.ts` | 已有统一状态基础 | `HAO-462`、`HAO-463`、`HAO-465`、`HAO-469`–`HAO-478` |
| 概览、成员、里程碑 | 对应三个页面与 `/projects/:projectId/*` 路由 | Partial；外层权限粒度/真实浏览器验收待补齐 | `HAO-424`、`HAO-425`、`HAO-459` |
| 项目报表/设置 | 已有路由，但当前落到通用任务页 | 未实现专属页面，不能对外宣称可用 | `HAO-438`、`HAO-504`、`HAO-505`–`HAO-510` |
| 回收站/数据空间/审计 | 对应页面与治理路由 | Partial | `HAO-454`、`HAO-517`–`HAO-527` |
| 面包屑、项目详情标签、项目选择空态 | `AppLayout.tsx`、`ProjectManagementPageState.tsx` | 有通用标签/空态，缺项目层级模型的实现 | `HAO-418`、`HAO-432`、`HAO-458`、`HAO-478` |

## 9. HAO-432 交付后的实施准入

本文件定义的是可实施模型，不表示每个页面已经完成。任一后续导航实现要进入 Done，至少应验证：

1. 在系统工作区与应用工作区下，菜单、路由显示路径、面包屑和标签页都指向同一当前工作区；URL 不能切换工作区。
2. 项目中心、我的工作、项目工作区和治理入口不重复系统管理、应用数据中心、通知、IM 或文件菜单。
3. 每个可见菜单、子导航和动作都有准确 PermissionCode；没有权限时菜单隐藏、深链 403、API 仍拒绝。
4. 项目不存在、无权、归档、软删除、无项目、无筛选结果、加载、错误、冲突和未保存离开均有明确且不泄露数据的状态。
5. 五类任务视图共享同一 ProjectId、查询状态、对象权限、版本和命令；刷新/深链保留可分享状态。
6. 全部浏览器验收使用最新工作区会话与后端进程；不得用匿名 401 代替项目对象权限或导航结论。
