# ProjectManagement 源码能力清单

> 基线：`codex/hao-427-source-audit` 分支工作区的源码快照。本文只陈述能在源码中定位的事实；“缺口”表示当前源码中未找到对应实现或调用链，并不推断产品决策。

## 1. 范围与定位入口

| 范围 | 已存在能力 | 主要证据 |
| --- | --- | --- |
| 宿主与 HTTP | ASP.NET Core 宿主通过 ABP 初始化，注册 Controllers、SignalR，并以 `MapControllers()` 公开 HTTP API。 | `backend/AsterERP.Api/Program.cs` 的 `AddApplicationAsync<AsterErpAbpHostModule>`、`MapControllers`、`MapHub<SystemNotificationHub>` |
| ABP 模块 | 项目管理为独立 `AsterErpProjectManagementModule`；该模块注册应用服务、Hangfire 提醒调度、实时订阅及 ORM 数据过滤。 | `backend/AsterERP.Api/Infrastructure/Abp/ProjectManagement/AsterErpProjectManagementModule.cs`，`ConfigureServices`、`RegisterDataFilters` |
| 数据库 | `ProjectManagementSchemaMigrator` 创建/升级以 `pm_` 前缀的 SQLite schema，并由应用数据库初始化器调用。 | `backend/AsterERP.Api/Infrastructure/Abp/ProjectManagement/ProjectManagementSchemaMigrator.cs`；`backend/AsterERP.Api/Application/ApplicationConsole/ApplicationDatabaseSchemaInitializer.cs` |
| 契约 | 独立 contracts 项目公开项目管理请求/响应 record；前端另有 TypeScript API/types。 | `backend/AsterERP.Contracts/ProjectManagement/`；`frontend/AsterERP.Web/src/api/project-management/` |
| Web UI | React 路由、页面、工作台视图、状态、实时连接均在 `frontend/AsterERP.Web`。 | `frontend/AsterERP.Web/src/app/router/workspaceRoutes.full.tsx`；`src/pages/project-management/`；`src/features/project-management/` |
| 自动化测试 | xUnit 项目包含 27 个 `ProjectManagement*Tests.cs` 文件、80 个 `[Fact]/[Theory]`；前端有 4 个项目管理相关 Vitest 文件。 | `backend/AsterERP.Api.Tests/ProjectManagement*Tests.cs`；`frontend/AsterERP.Web/src/**/projectManagement*.test.*`、`taskWorkspaceState.test.ts`、`taskMoveIntent.test.ts` |

解决方案 `AsterERP.sln` 当前包含 API、DbMigrator、Shared、Contracts、Domain、测试项目及 13 个工作流项目；项目管理本体不再拆为单独 csproj，而是在 `AsterERP.Api` 的 `Application`、`Modules`、`Infrastructure/Abp/ProjectManagement` 和 Controllers 内分层实现。

## 2. 后端模块和调用链

```text
React route/page
  -> src/api/project-management/projectManagement.api.ts
  -> api/project-management/* Controller + [Permission]
  -> IProjectManagement*Service / ProjectManagement*Service
  -> ProjectManagementAccessPolicy + SqlSugar workspace query filter
  -> ProjectManagement*Entity / pm_* schema
  -> activity, notification, sync journal, realtime invalidation (按写入场景)
```

### 2.1 应用层能力

`AsterErpProjectManagementModule.ConfigureServices` 注册 28 个 `ProjectManagement*Service` 实现及 36 个 `IProjectManagement*` 契约/协作接口。可按职责定位如下：

| 能力组 | 应用服务/关键协作符号 | 说明 |
| --- | --- | --- |
| 项目与权限 | `ProjectManagementProjectService`、`ProjectManagementMemberService`、`ProjectManagementMemberCandidateService`、`ProjectManagementAccessPolicy` | 项目 CRUD、成员/候选人、项目角色与操作授权。 |
| 计划与任务 | `ProjectManagementMilestoneService`、`ProjectManagementTaskService`、`ProjectManagementTaskHierarchy`、`ProjectManagementTaskProgressProjector`、`ProjectManagementTaskBatchService` | 里程碑、树形任务、移动/排序、进度投影、批量更新与 WIP 规则。 |
| 任务协作 | `ProjectManagementTaskDependencyService`、`ProjectManagementTaskParticipantService`、`ProjectManagementTaskCommentService`、`ProjectManagementTaskAttachmentService`、`ProjectManagementTaskTimeLogService` | 依赖、参与者、线程评论/@ 提及、文件引用和工时。 |
| 工作方式 | `ProjectManagementTaskTemplateService`、`ProjectManagementTaskReminderService`、`ProjectManagementReminderExecutionService`、`ProjectManagementSavedViewService` | 模板/重复 occurrence、Hangfire 提醒、保存视图。 |
| 查询与呈现 | `ProjectManagementOverviewService`、`ProjectManagementMyWorkService`、`ProjectManagementSearchService`、`ProjectManagementReportService`、`ProjectManagementActivityService` | 总览、我的工作、全文分组检索、CSV/XLSX 导出、活动流。 |
| 通知与实时 | `ProjectManagementNotificationService`、`ProjectManagementRealtimePublisher`、`ProjectManagementRealtimeSubscriptionRegistry` | 站内通知、SignalR 工作区/项目/用户维度订阅及失效通知。 |
| 数据空间 | `ProjectManagementSyncService`、`ProjectManagementSyncJournalWriter`、`ProjectManagementBackupService`、`ProjectManagementMaintenanceLock`、`ProjectManagementAuditService`、`ProjectManagementOperationWriter` | 同步包、watermark/journal、备份恢复、维护锁、审计和操作日志。 |
| IM 关联 | `ProjectManagementImConversationService` | 管理项目/任务与 IM 会话的业务关联；IM 聚合仍在 IM 模块。 |

对应源码目录为 `backend/AsterERP.Api/Application/ProjectManagement/`。领域约束集中在 `backend/AsterERP.Api/Modules/ProjectManagement/ProjectManagementDomainRules.cs`，事务边界由 `ProjectManagementMutationTransaction.RunAsync` 提供。

### 2.2 权限和数据边界

* 权限代码在 `backend/AsterERP.Shared/Common/PermissionCodes.ProjectManagement.cs`，包括 project/member/milestone/task、comment、notification、IM、attachment、reminder、report、sync、backup、audit 共 37 个操作代码；菜单/固定权限目录由 `ApplicationShellPermissionCatalog` 与 `ApplicationShellMenuCatalog` 提供。
* Controller 使用 `Permission` 特性。例如 `ProjectManagementProjectsController` 将查看、创建、编辑、恢复、删除分别绑定到 `ProjectManagementProject*` 权限；`ProjectManagementTasksController` 分离 view/add/edit/move/delete/restore。
* `ProjectManagementDataPermissionFilterRegistrar.TryRegister` 为项目、成员、里程碑、任务及其关联实体注册 SqlSugar 数据库侧过滤；非平台管理员且非 `ALL` 数据范围的用户仅可访问自己拥有或有效成员关系可见的项目。通知额外按 `RecipientUserId` 限定。
* `AsterErpProjectManagementModule.RegisterDataFilters` 把 24 个项目管理持久化类型注册为工作区过滤对象；工作区键是 tenant + app，实体和 schema 均含 `TenantId`、`AppCode`。

## 3. 实体与数据库清单

所有实体继承 `AsterERP.Domain.Common.EntityBase`，定义在 `backend/AsterERP.Api/Modules/ProjectManagement/`，并由 `ProjectManagementSchemaMigrator.CreateTables` 维护。下表使用 `SugarTable`/迁移 SQL 的真实表名。

| 业务域 | 实体 | 表 |
| --- | --- | --- |
| 项目骨架 | `ProjectManagementProjectEntity`、`ProjectManagementProjectMemberEntity`、`ProjectManagementMilestoneEntity` | `pm_projects`、`pm_project_members`、`pm_milestones` |
| 任务与关系 | `ProjectManagementTaskEntity`、`ProjectManagementTaskDependencyEntity`、`ProjectManagementTaskParticipantEntity` | `pm_tasks`、`pm_task_dependencies`、`pm_task_participants` |
| 标签、工时、模板 | `ProjectManagementLabelEntity`、`ProjectManagementTaskLabelEntity`、`ProjectManagementTaskTimeLogEntity`、`ProjectManagementTaskTemplateEntity`、`ProjectManagementTaskOccurrenceEntity` | `pm_labels`、`pm_task_labels`、`pm_task_time_logs`、`pm_task_templates`、`pm_task_occurrences` |
| 协作 | `ProjectManagementActivityEntity`、`ProjectManagementTaskCommentEntity`、`ProjectManagementNotificationEntity`、`ProjectManagementTaskReminderEntity`、`ProjectManagementTaskAttachmentEntity`、`ProjectManagementImConversationLinkEntity` | `pm_activities`、`pm_task_comments`、`pm_notifications`、`pm_task_reminders`、`pm_task_attachments`、`pm_im_conversation_links` |
| 视图与数据空间 | `ProjectManagementSavedViewEntity`、`ProjectManagementSyncJournalEntity`、`ProjectManagementSyncDeviceEntity`、`ProjectManagementMaintenanceLockEntity`、`ProjectManagementBackupEntity`、`ProjectManagementOperationEntity` | `pm_saved_views`、`pm_sync_journal`、`pm_sync_devices`、`pm_maintenance_locks`、`pm_backups`、`pm_operations` |

关键 schema 约束包括项目编码唯一、项目成员唯一、任务编码唯一、同级排序、任务依赖唯一、标签及任务标签唯一、模板 occurrence 唯一、通知幂等键唯一等；证据在 `ProjectManagementSchemaMigrator.CreateIndexes`。

### 已定位的实体/schema 不一致

`ProjectManagementTaskCommentMentionEntity` 声明为 `[SugarTable("pm_task_comment_mentions")]`，并已登记数据过滤；但当前 `ProjectManagementSchemaMigrator` 没有创建 `pm_task_comment_mentions`，且全仓未找到该实体被服务读写。当前评论服务把提及者存于 `ProjectManagementTaskCommentEntity.MentionUserIdsJson`（见 `ProjectManagementTaskCommentService.CreateAsync/UpdateAsync`）。因此它是待澄清或清理的持久化残留，不能作为“已落库的评论提及明细表”交付。

## 4. HTTP API 清单

项目管理目前有 28 个 Controller 源文件、29 个 `[Route]` 根路径（`ProjectManagementLabelsController.cs` 含项目标签和任务标签两个 Controller）。所有入口位于 `backend/AsterERP.Api/Controllers/ProjectManagement*.cs`，`Program.cs` 的 `MapControllers()` 为统一发布入口。

| 路由根 | 方法能力 | Controller / service 入口 |
| --- | --- | --- |
| `/api/project-management/projects` | 项目列表、创建、更新、软删除、恢复 | `ProjectManagementProjectsController` / `IProjectManagementProjectService` |
| `/projects/{projectId}/members`、`/member-candidates` | 成员 CRUD、候选人分页检索 | `ProjectManagementMembersController`、`ProjectManagementMemberCandidatesController` |
| `/projects/{projectId}/milestones` | 里程碑 CRUD | `ProjectManagementMilestonesController` |
| `/tasks`、`/tasks/batch` | 任务查询、创建、更新、移动、删除/恢复、批量更新 | `ProjectManagementTasksController`、`ProjectManagementTaskBatchController` |
| `/projects/{projectId}/task-dependencies`、`/tasks/{taskId}/participants` | 依赖查询/新增/删除、参与者查询/新增/删除 | `ProjectManagementTaskDependenciesController`、`ProjectManagementTaskParticipantsController` |
| `/projects/{projectId}/labels`、`/tasks/{taskId}/labels` | 项目标签 CRUD、任务标签查询/替换 | `ProjectManagementLabelsController` |
| `/projects/{projectId}/task-templates` | 模板查询/创建/更新/应用 | `ProjectManagementTaskTemplatesController` |
| `/tasks/{taskId}/time-logs`、`/reminders` | 工时查询/新增/删除；提醒查询/创建/更新/取消/删除 | `ProjectManagementTaskTimeLogsController`、`ProjectManagementTaskRemindersController` |
| `/tasks/{taskId}/comments`、`/attachments` | 评论/提及候选人、附件上传/列表/下载/删除 | `ProjectManagementTaskCommentsController`、`ProjectManagementTaskAttachmentsController` |
| `/projects/{projectId}/saved-views`、`/projects/{projectId}/activities` | 保存视图 CRUD、活动流 | `ProjectManagementSavedViewsController`、`ProjectManagementActivitiesController` |
| `/overview`、`/my-work`、`/search` | 总览、我的工作、分组检索 | `ProjectManagementOverviewController`、`ProjectManagementMyWorkController`、`ProjectManagementSearchController` |
| `/notifications` | 通知列表、单条/全部已读、安全跳转 | `ProjectManagementNotificationsController` |
| `/projects/{projectId}/im-conversation`、`/im-conversations/{conversationId}/target` | 项目/任务 IM 会话获取/确保、从会话回查项目任务目标 | `ProjectManagementImConversationsController`、`ProjectManagementImConversationTargetsController` |
| `/recycle` | 回收站查询、项目/任务恢复、项目永久删除 | `ProjectManagementRecycleController` |
| `/reports` | `projects.csv`、`projects.xlsx` 导出 | `ProjectManagementReportsController` |
| `/sync`、`/data-space`、`/backups` | watermark/changes/acknowledge/export/preview/apply，空间摘要，备份列举/创建/恢复 | `ProjectManagementSyncController`、`ProjectManagementDataSpaceController`、`ProjectManagementBackupController` |
| `/audit` | 审计分页、操作记录、CSV 导出 | `ProjectManagementAuditController` |

请求/响应 DTO 的逐领域入口见 `backend/AsterERP.Contracts/ProjectManagement/ProjectManagement*Contracts.cs`：其中任务查询/写入为 `ProjectManagementTaskContracts.cs`，同步为 `ProjectManagementSyncContracts.cs`，备份/审计为 `ProjectManagementBackupContracts.cs` 和 `ProjectManagementAuditContracts.cs`。

## 5. 前端路由与已接入能力

路由定义位于 `frontend/AsterERP.Web/src/app/router/workspaceRoutes.full.tsx` 的 `projectManagementRoutePaths`，通过 `PermissionRoute` 设置项目、任务或审计查看权限：

| UI 路由 | 页面/表现 |
| --- | --- |
| `projects` | `ProjectManagementPage`：项目列表/项目基本操作。 |
| `my-work` | `ProjectManagementMyWorkPage`：我的工作查询。 |
| `projects/:projectId/overview`、`members`、`milestones` | 各自对应 `ProjectManagementOverviewPage`、`ProjectManagementMembersPage`、`ProjectManagementMilestonesPage`。 |
| `projects/:projectId/tasks`、`list`、`card`、`board`、`gantt`、`calendar` | 同一 `ProjectManagementTaskWorkspacePage`；`resolveView` 与 `TaskWorkspaceProjection` 实现 tree/list、卡片、看板、日期条目式甘特和按截止日分组日历。 |
| `project-recycle-bin`、`project-data-space`、`project-audit-center` | `ProjectManagementRecycleBinPage`、`ProjectManagementDataSpacePage`、`ProjectManagementAuditPage`。 |
| `projects/:projectId/reports`、`settings` | 当前也被路由选择到 `ProjectManagementTaskWorkspacePage`；`resolveView` 不识别这两个 suffix，因而回退为 `tree`。 |

工作台实际接入任务、批量更新、保存视图、评论、附件上传、提醒、成员候选人、里程碑、标签、项目/任务 IM 会话和 SignalR 数据失效通知，证据为 `ProjectManagementTaskWorkspacePage` 的 imports、queries、mutations 与 `useProjectManagementRealtimeConnection`。

## 6. 已定位缺口与实施入口

| 状态 | 事实与影响 | 后续实施的起点 |
| --- | --- | --- |
| 前端 API 契约漂移 | `projectManagement.api.ts#getProjectManagementWorkspaceOverview` 调用 `/project-management/workspace/overview`；后端 29 个项目管理根路由中没有该路径，且前端其余代码未调用该函数。若重新使用会得到 404。 | 先决定删除遗留 client/type，或增加对应 Controller + `IProjectManagementOverviewService` 方法；不要直接在页面调用。 |
| 后端能力尚未发现前端消费者 | 全前端源码未找到 `task-dependencies`、`tasks/{taskId}/participants`、`time-logs`、`task-templates`、`project-management/search`、`project-management/reports` 的调用。相应 Controller、service、contracts 与后端测试均已存在。 | 以 `projectManagement.api.ts` 补齐受类型约束的客户端函数，再在任务工作台/独立页面按权限接入；同时为 UI 流补 Vitest/E2E。 |
| 路由语义未落地 | `projects/:projectId/reports`、`settings` 已注册，但目前都渲染任务树，没有专用报表/项目设置 UI。 | `workspaceRoutes.full.tsx` 的条件页面选择；报表优先使用 `ProjectManagementReportsController`，设置需先明确允许修改的项目字段和权限。 |
| 实体与迁移残留 | `ProjectManagementTaskCommentMentionEntity` 已声明并注册过滤，但无迁移表且无应用服务读写；当前功能使用评论 JSON 字段。 | 在演进前选择“创建明细表并迁移读写”或“删除实体/过滤注册”；两者都应增加 schema 回归测试。 |
| 测试边界 | 项目管理后端已有 service、schema、权限特性和数据过滤测试，前端已有项目管理页面与功能测试；当前未在源码中定位项目管理真实 HTTP + 浏览器 E2E/UAT 测试。该缺口已按用户明确确认登记为允许例外，不代表 E2E/UAT 已通过。 | 继续补充可执行的领域边界、契约、权限和性能回归；真实身份浏览器 E2E/UAT 不作为本阶段关闭阻塞，若后续纳入范围须单独补证据。 |

## 7. 测试能力和可复用证据

| 测试组 | 已覆盖的可复用断言 |
| --- | --- |
| 模块、权限、过滤 | `ProjectManagementAbpModuleTests`、`ProjectManagementPlatformCapabilityContractTests`、`ProjectManagementDataPermissionFilterTests`、`ProjectManagementAccessPolicyTests`：模块装配、权限目录/菜单、workspace 数据过滤、成员授权。 |
| 项目/计划/任务 | `ProjectManagementProjectServiceTests`、`ProjectManagementMemberMilestoneServiceTests`、`ProjectManagementTaskServiceTests`、`ProjectManagementTaskBatchServiceTests`、`ProjectManagementTaskDependencyServiceTests`：租户/应用边界、乐观并发、树/循环检测、WIP、原子事务与进度投影。 |
| 协作 | `ProjectManagementTaskCommentServiceTests`、`ProjectManagementTaskReminderServiceTests`、`ProjectManagementTaskTimeLogServiceTests`、`ProjectManagementLabelServiceTests`、`ProjectManagementActivityServiceTests`、`ProjectManagementNotificationServiceTests`、`ProjectManagementRealtimeSubscriptionRegistryTests`。 |
| 视图与查询 | `ProjectManagementOverviewRecycleTests`、`ProjectManagementSavedViewServiceTests`、`ProjectManagementSearchServiceTests`、`ProjectManagementReportTests`。 |
| 运维与数据空间 | `ProjectManagementSchemaMigratorTests`、`ProjectManagementSyncServiceTests`、`ProjectManagementBackupServiceTests`、`ProjectManagementMaintenanceLockTests`、`ProjectManagementAuditServiceTests`。 |
| 前端 | `projectManagementQueryKeys.test.ts`（tenant/app query key 隔离）、`taskWorkspaceState.test.ts`（URL/保存视图状态）、`taskMoveIntent.test.ts`（移动命令）、`ProjectManagementPageState.test.tsx`（页面状态）。 |

建议最低验证组合：后端行为改动执行 `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --filter "ProjectManagement"`；前端改动执行 `npm test -- --run`（在 `frontend/AsterERP.Web`）和 `npm run build`，并用有效身份走一次 Route -> Page -> API -> Service -> ORM filter -> DB 的冒烟验证。
