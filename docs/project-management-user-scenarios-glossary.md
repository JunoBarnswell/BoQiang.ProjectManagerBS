# ProjectManagement 用户角色、核心场景、任务流与业务词汇表

> Linear：`HAO-429`（M0）
> 适用范围：ProjectManagement 在当前 `TenantId + AppCode` 工作区内的业务语义。
> 基线：本文以现有 Contracts、实体、`ProjectManagementDomainRules`、权限码和 Linear M1–M4 Case 为准；“目标边界”不是对当前实现状态的声明。

## 1. 使用方式与边界

Project 是工作边界，Task 是唯一执行对象；Milestone、ProjectMember 和 TaskDependency 是独立领域对象。任务树、行、卡片、看板、甘特和日历只改变同一任务查询的投影，不能产生另一套权限、状态或写入语义。

一次可用的业务操作必须同时通过四层约束：

```text
会话派生 TenantId + AppCode
  -> 功能 PermissionCode（Controller / 菜单 / 动作）
  -> ORM Data Filter（工作区 + 项目成员可见性）
  -> 项目角色与任务范围的对象级校验
```

请求体、URL 或前端缓存不能覆盖当前工作区。功能权限存在不代表一定有对象权限；反之，项目角色也不绕过功能权限。写命令还必须携带当前 `VersionNo`，通过领域校验后在同一事务内写入业务数据、活动/同步记录和失效事件。

### 当前证据与待补齐边界

| 事项 | 当前可确认的实现事实 | 产品定义 / 后续闭环 |
|---|---|---|
| 项目可见性 | 当前对象策略按 Owner 或有效 `ProjectMember`，且受 `TenantId + AppCode` 限制。平台管理员或 `*` 权限可绕过对象角色判断。 | `HAO-414` 的 ORM Data Filter 是列表、详情、统计、导出和批量操作的统一数据边界。 |
| Lead 范围 | `ProjectManagementProjectMemberEntity.ScopeRootTaskId` 已保存；现有 `EnsureCanManageTaskAsync` 对 Lead 只按角色放行，尚未读取该字段。 | `HAO-424` 必须把 Lead 限制到 `ScopeRootTaskId` 及其后代，并在任务、批量、依赖和分配命令中重校验。本文不得把该限制误写为已生效。 |
| 成员与人员来源 | 成员合同使用 `UserId` / 可选 `EmploymentId`；候选人由平台用户、部门、岗位主数据提供。 | `HAO-419`、`HAO-424`、`HAO-446` 负责启用状态、跨工作区拒绝、历史快照和批量分配约束。 |
| 归档与恢复 | 项目、任务均使用软删除/版本字段；回收站服务已有入口。 | `HAO-438`、`HAO-454`、`HAO-455` 负责归档只读、恢复重建、密码确认和彻底删除闭环。 |
| 同步、备份与审计 | 同步 Journal、操作记录、维护锁、备份和审计 Contracts/服务均已存在。 | M4 的 `HAO-512`、`HAO-514`、`HAO-516`、`HAO-519`–`HAO-526` 负责完整的预检、后台执行、报告、保留与治理验收。 |

## 2. 用户角色与权限边界

### 2.1 角色定义

| 角色 | 业务目标 | 目标对象范围 | 必须禁止的行为 | 当前服务端对象策略证据 |
|---|---|---|---|---|
| 平台管理员 | 维护租户/应用和高风险数据治理。 | 当前租户/应用中由平台权限允许的范围；不以项目成员资格为前提。 | 不能凭前端隐藏或客户端工作区字段跨工作区读取；高风险操作仍须确认、锁、审计。 | `IsAsterErpPlatformAdmin()` 或 `*` 使项目对象策略短路；具体功能仍由对应 PermissionCode 控制。 |
| Owner | 对项目结果、成员和高风险项目级决策负责。创建项目时成为唯一初始 Owner，项目必须始终至少有一个有效 Owner。 | 全项目及其全部任务、里程碑、成员和项目级共享配置。 | 不可移除最后一个有效 Owner；不可绕开版本、数据过滤或高风险确认。 | Owner 可查看并通过 `EnsureCanManageProjectAsync`、`EnsureCanManageMembersAsync`、`EnsureCanManageTaskAsync`。最后 Owner 保护由 `HAO-424` 验收。 |
| Manager | 代表 Owner 进行日常项目管理。 | 全项目；不改变 Owner 的项目归属语义。 | 不应擅自获得平台级整库导入/恢复权限；不能突破租户、应用、项目边界。 | Manager 可管理项目、成员、已删除项目和依赖；任务管理由当前对象策略放行。 |
| Lead | 负责一个主题父任务（及其后代）的交付协调。 | **目标为** `ScopeRootTaskId` 所指根任务和其后代；未绑定范围时不得写入任务。 | 不得管理范围外任务、成员、项目级设置、项目删除/恢复或高风险数据治理。 | 当前已可作为项目成员角色并能管理任务/依赖，但范围字段尚未在策略使用；此差距由 `HAO-424` 闭环。 |
| Member | 完成被分配任务并参与协作。 | 本人是负责人的任务；可参与其有项目可见性的评论、附件、提醒等协作。 | 不得改他人负责的任务、成员、项目设置、依赖/WIP 强制绕过和高风险治理。 | 当前任务策略仅当 `assigneeUserId == 当前用户` 时放行；最终参与人/批量分配约束由 `HAO-446` 闭环。 |
| Viewer | 阅读项目进度、任务和授权的协作信息。 | 当前工作区内其有效成员关系所覆盖的项目。 | 一切项目、成员、里程碑、任务、依赖、附件、同步和恢复写操作。 | 可通过项目可见性校验；现有测试确认 Viewer 不能管理任务。 |

**角色与功能权限是两层而非替代关系。** 例如 Manager 需要同时取得 `project-management:member:manage` 才能进入成员管理功能；拥有该功能码的 Member 仍应被对象级策略拒绝。Lead 的树范围同理必须在后端命令中验证，不能仅隐藏按钮。

### 2.2 操作边界矩阵

下表是交付时应实现的业务权限矩阵；“目标”列中 Lead 的范围限制以 `HAO-424` 完成为前提。

| 操作 | 平台管理员 | Owner | Manager | Lead | Member | Viewer |
|---|---:|---:|---:|---:|---:|---:|
| 查看已授权项目、任务、里程碑、活动 | 是 | 是 | 是 | 仅范围内 | 仅可见项目/本人相关任务 | 是，只读 |
| 新建/编辑项目、项目状态、WIP | 需功能权限 | 是 | 是 | 否 | 否 | 否 |
| 新增/变更/移除项目成员和角色 | 需功能权限 | 是 | 是 | 否 | 否 | 否 |
| 新建/编辑里程碑 | 需功能权限 | 是 | 是 | 否 | 否 | 否 |
| 新建、编辑、移动、删除任务 | 需功能权限 | 是 | 是 | 仅范围内 | 仅本人负责的任务 | 否 |
| 分配负责人/参与人、批量分配 | 需功能权限 | 是 | 是 | 仅范围内 | 否 | 否 |
| 管理任务依赖 | 需功能权限 | 是 | 是 | 仅范围内 | 否 | 否 |
| 强制绕过 WIP | 仅具 `task:override-wip` | 仅具该功能权限 | 仅具该功能权限 | 仅具该功能权限且范围内 | 否 | 否 |
| 个人保存视图 | 需功能权限 | 是 | 是 | 是 | 是 | 否 |
| 项目共享/默认视图 | 需功能权限 | 是 | 是 | 否 | 否 | 否 |
| 评论、@提及、附件、提醒 | 需功能权限及对象可见性 | 是 | 是 | 范围内 | 对授权任务 | 仅查看（无写） |
| 归档、软删除、恢复项目 | 需功能权限 | 是 | 是 | 否 | 否 | 否 |
| 彻底删除 | 需 `project:purge`、风险确认和事务 | 仅具该功能权限 | 仅具该功能权限 | 否 | 否 | 否 |
| 导出当前筛选任务/同步包 | 需相应功能权限 | 需相应功能权限 | 需相应功能权限 | 仅范围/数据过滤允许的数据 | 不默认授予 | 否 |
| 整库导入、备份、恢复 | 平台/明确授权治理角色 | 不因 Owner 身份自动获得 | 不因 Manager 身份自动获得 | 否 | 否 | 否 |
| 审计查询/审计导出 | 全局权限下按工作区 | 仅授权项目且具功能权限 | 仅授权项目且具功能权限 | 否 | 否 | 否 |

### 2.3 PermissionCode 词典

以下代码是当前 `PermissionCodes.ProjectManagement.cs` 的实际字符串，所有菜单和 API 使用同一值：

| 领域 | 权限码 |
|---|---|
| 项目 | `project-management:project:view`、`project-management:project:add`、`project-management:project:edit`、`project-management:project:archive`、`project-management:project:delete`、`project-management:project:restore`、`project-management:project:purge` |
| 成员与里程碑 | `project-management:member:view`、`project-management:member:manage`、`project-management:milestone:view`、`project-management:milestone:manage` |
| 任务 | `project-management:task:view`、`project-management:task:add`、`project-management:task:edit`、`project-management:task:delete`、`project-management:task:restore`、`project-management:task:move`、`project-management:task:assign`、`project-management:task:manage-dependency`、`project-management:task:override-wip` |
| 标签与模板 | `project-management:label:view`、`project-management:label:manage`、`project-management:task-template:manage` |
| 协作 | `project-management:comment:view`、`project-management:comment:add`、`project-management:notification:view`、`project-management:im-conversation:view`、`project-management:im-conversation:manage`、`project-management:attachment:manage`、`project-management:reminder:view`、`project-management:reminder:manage` |
| 数据与治理 | `project-management:report:export`、`project-management:sync:import`、`project-management:sync:export`、`project-management:backup:manage`、`project-management:audit:view`、`project-management:audit:export` |

## 3. 核心用户场景与端到端任务流

### S1：Owner 发起项目并建立可执行计划

**目标用户：** Owner（也可由具项目创建权限的管理员代表发起）。
**完成标准：** 项目有唯一业务编码、至少一个有效 Owner、可供成员在正确工作区内进入；计划通过里程碑和任务树表达，而不是靠页面备注。

```text
选择当前工作区
  -> 提交 ProjectCode / ProjectName / Owner / 日期 / WIP / VersionNo
  -> 后端从会话取得 TenantId + AppCode，校验 project:add 与唯一性
  -> 同一事务创建 Project + Owner Member
  -> 返回 VersionNo 和项目上下文
  -> Owner 添加成员、里程碑、根任务
  -> 查询投影进入任务工作区
```

**成功路径：** `Status=Planning` 的项目被创建；Owner 再将项目变为 `Active`，为每个阶段创建独立 Milestone，并把 Task 关联到至多一个里程碑。
**失败/边界路径：**

- 缺少会话工作区、越权 `project:add`、跨工作区人员、重复项目编码或无 Owner，必须拒绝，不能让客户端指定 `TenantId`/`AppCode` 成功。
- `DueDate < StartDate` 必须拒绝；并发编辑使用 `VersionNo`，不得静默覆盖。
- `Planning -> Active` 是允许的状态迁移；已归档项目不能再作为常规写入目标。

**可追溯实现：** 当前 Project Contracts / `ProjectManagementProjectService`；成员角色与主题范围 `HAO-424`，里程碑 `HAO-425`，并发与归档 `HAO-438`，项目入口/概览 `HAO-458`、`HAO-459`。

### S2：Manager/Lead 拆分里程碑、父子任务并建立依赖

**目标用户：** Owner、Manager；Lead 仅在其授权主题树内。
**完成标准：** 计划可执行、树不成环、同项目、层级受限，依赖阻塞由服务端计算。

```text
创建 Milestone（名称、负责人、日期、状态）
  -> 创建根 Task
  -> 创建子 Task 或移动现有 Task Tree
  -> 校验同项目、非自身/后代、深度、日期、VersionNo
  -> 关联一个 Milestone（可为空）
  -> 添加 Predecessor -> Successor Dependency
  -> 校验依赖类型、无循环、当前角色/范围
  -> 提交任务树、依赖、进度投影、活动与 Journal
```

**成功路径：** 任务使用父子树承载主题/子任务，移动父任务携带整个子树；每个任务最多一个 `MilestoneId`；依赖为 `FinishToStart`、`StartToStart`、`FinishToFinish` 或 `StartToFinish`，可带 `LagMinutes`。

**失败/边界路径：**

- 不能跨项目指定 `ParentTaskId`、`MilestoneId`、前置或后继任务；不能把自身或后代设为父级，不能产生依赖循环。
- 当前 `ProjectManagementDomainRules.MaxTaskDepth` 是 **10**。`HAO-441` 文字中“默认 5 级”的旧目标不得覆盖当前实现常量；若要配置化，应通过该 Case 的明确迁移和回归后再变更本文。
- Lead 范围校验是 `HAO-424` 的未闭环项；在其完成前，不能以 `ScopeRootTaskId` 已持久化为理由宣称范围外写入已被拒绝。

**可追溯实现：** 当前 Task/Milestone/Dependency Contracts 与领域规则；`HAO-425`、`HAO-441`、`HAO-444`、`HAO-476`。

### S3：Member 执行任务、协作并处理阻塞

**目标用户：** Member（本人负责的任务）、Lead/Manager/Owner（各自范围内）。
**完成标准：** 执行者只改变授权任务，状态、进度、实际时间和通知可追溯；阻塞不是前端任意文案。

```text
负责人/参与人从有效项目成员中选择
  -> 负责人打开“我的工作”或项目任务
  -> Todo -> InProgress
  -> 记录进度、实际开始/结束时间、评论/@提及、附件、提醒
  -> 有前置未完成时服务端投影 Blocked / CanStart=false
  -> 解除依赖或经授权强制开始
  -> InProgress -> Done，重算父任务/里程碑/项目进度并通知相关人
```

**成功路径：** Member 编辑其 `AssigneeUserId` 等于当前用户的任务；负责人可同时是参与人。评论、附件、提醒和 IM 关联均仍以项目成员关系和任务可见性为前提。

**失败/边界路径：**

- 非成员、跨租户/应用用户、已停用成员不能被新分配；停用/移除后保留历史显示而不破坏引用。
- Member 修改他人任务、Viewer 写入、无 `task:override-wip` 强制越过 WIP、或用客户端请求解除依赖阻塞，必须拒绝。
- WIP 上限计数的是同项目 `InProgress` 任务；只有具 `project-management:task:override-wip` 的调用可绕过。强制原因与审计记录仍是产品闭环要求。
- 任务详情更新若 `VersionNo` 已变化，返回冲突并保留本地草稿；不可覆盖他人最新值。

**可追溯实现：** 当前 `ProjectManagementTaskResponse` 的 `BlockedByCount`、`CanStart`、`BlockedReason`，任务服务/批量服务，及 `HAO-442`、`HAO-446`、`HAO-480`、`HAO-487`、`HAO-490`、`HAO-491`、`HAO-492`、`HAO-494`。

### S4：多人同步与冲突处置

**目标用户：** 被授予同步导入/导出功能权限的 Owner/Manager 或明确授权治理角色；不是所有项目成员。
**完成标准：** 导入前可预览，数据与 Journal 可靠落地，冲突和失败均可解释、可审计、可重试。

```text
导出：选择当前授权项目/范围
  -> 服务端按 Data Filter 生成 .bqsync（manifest + data + 附件条目）
  -> 记录设备水位与 Journal 序列

导入：上传包
  -> ZIP/manifest/schema/校验和/来源工作区/路径/资源上限预检
  -> 只读预览新增、更新、删除、冲突和警告
  -> 明确 ConflictStrategy + ConfirmRisk
  -> 获取维护锁、按依赖顺序事务导入
  -> 健康检查，提交或回滚
  -> 写同步历史、审计、通知与可下载结果报告
```

**成功路径：** 每个同步变更包含稳定实体 ID、版本、来源用户/设备、`TraceId` 和单调 `SequenceNo`；设备水位只从服务端 Journal 获得。
**失败/边界路径：** 损坏、路径穿越、工作区不匹配、版本不兼容、校验和不符、无导入权限、锁占用或冲突策略非法时，预览/导入必须停止；预览不得写业务数据或同步水位。失败后释放锁并留下失败证据，不能产生部分未知结果。

**可追溯实现：** `ProjectManagementSync*Contracts`、`pm_sync_journal` 设计；`HAO-512`、`HAO-514`、`HAO-516`。

### S5：归档、回收站、恢复与彻底删除

**目标用户：** Owner/Manager；彻底删除还需要 `project-management:project:purge` 和高风险确认。
**完成标准：** 日常可逆、恢复一致、高风险不可误操作、历史可审计。

```text
项目完成/取消 -> Archived（只读）
  -> 日常删除（软删除，隐藏常规查询）
  -> 回收站按项目与任务分别展示
  -> 恢复项目 / 恢复任务（可选择子树）
  -> 重建成员、里程碑、进度、WIP、依赖、搜索/协作关联
  -> 仅在保留策略、密码确认和事务检查均通过时彻底删除
```

**成功路径：** 恢复项目后，任务、里程碑、成员关系重新可用；恢复任务必须仍属于未删除的项目。
**失败/边界路径：** Owner/Manager 之外的用户、已无项目可见性的用户、过期 `VersionNo`、仍被引用的对象、唯一安全恢复点或附件生命周期未满足时，必须拒绝并给出阻塞原因。彻底删除不可留下孤儿关系。

**可追溯实现：** 当前 Recycle Contracts、`EnsureCanManageDeletedProjectAsync`；`HAO-438`、`HAO-454`、`HAO-455`。

### S6：管理员进行数据空间、备份、恢复和审计治理

**目标用户：** 平台管理员或被明确授予治理权限的角色。项目 Owner/Manager 身份本身不自动授予此权。
**完成标准：** 高风险操作具备授权、密码确认、维护锁、可恢复性、后台状态与审计证据。

```text
选择当前授权数据空间
  -> 查看摘要/健康状态（按权限脱敏）
  -> 导出整库或创建备份
  -> 导入/恢复前校验包、空间、Schema 和可用容量
  -> 当前密码 + 风险确认
  -> 自动安全备份 + 维护锁
  -> 后台导入/恢复 + 健康检查
  -> 成功：重建会话/缓存/索引/作业；失败：回滚安全备份
  -> 记录 pm_operations、审计、通知和 TraceId
```

**失败/边界路径：** 非管理员调用、空间未绑定/不可达、锁超时/占用、文件校验失败、磁盘空间不足、恢复健康检查失败，均不得继续写入或留在半切换状态；恢复失败必须回滚且释放锁。审计查询和导出还要避免借治理权限泄露已失去业务可见性的项目内容。

**可追溯实现：** `project-management:backup:manage`、`project-management:audit:view`、`project-management:audit:export`、`pm_operations`、`pm_maintenance_locks`；`HAO-518`–`HAO-526`。

## 4. 统一业务词汇表

### 4.1 核心实体与关系

| 术语 | 统一含义 | 现有契约/实体 | 不能混用为 |
|---|---|---|---|
| 工作区（Workspace） | 当前认证会话派生的 `TenantId + AppCode` 数据与权限边界。 | 所有 `pm_*` 实体均存储 `TenantId`、`AppCode`。 | URL 中的自报 tenant/app、项目、数据空间。 |
| 项目（Project） | 计划、成员、里程碑、任务、WIP、进度和生命周期的工作边界。 | `ProjectManagementProjectEntity` / `ProjectManagementProjectResponse`。 | 任务集合的无权限视图或数据空间。 |
| 项目成员（ProjectMember） | 用户在单一项目内的有效角色与可选主题树范围。 | `ProjectManagementProjectMemberEntity`，字段 `RoleCode`、`ScopeRootTaskId`、`IsActive`。 | 平台系统角色、任务参与人或人员主数据副本。 |
| Owner | 对项目拥有最终责任的项目成员；项目至少一个有效 Owner。 | `OwnerUserId` + 成员 `RoleCode=Owner`。 | 自动获得整库治理权限的管理员。 |
| Manager | 项目日常管理角色。 | `RoleCode=Manager`。 | 平台管理员或项目 Owner 的替代。 |
| Lead | 主题树交付协调角色；目标范围为根任务及后代。 | `RoleCode=Lead`、`ScopeRootTaskId`。 | 当前已强制执行范围限制（尚待 `HAO-424`）。 |
| Member | 任务执行成员，编辑权以本人负责的任务为中心。 | `RoleCode=Member`、任务 `AssigneeUserId`。 | 可管理整个项目或他人任务的角色。 |
| Viewer | 项目只读成员。 | `RoleCode=Viewer`。 | 没有项目成员关系的匿名用户。 |
| 里程碑（Milestone） | 独立的阶段/目标实体，可归集任务进度和健康状态。 | `ProjectManagementMilestoneEntity` / `ProjectManagementMilestoneResponse`。 | 任务的布尔标记、父任务或项目状态。 |
| 任务（Task） | 系统中唯一的执行对象，可有父任务、负责人、参与人、标签、依赖和工时。 | `ProjectManagementTaskEntity` / `ProjectManagementTaskResponse`。 | 里程碑、评论、IM 消息或审批实例。 |
| 父任务 / 子任务 | 同一项目内的 Task 树关系；`ParentTaskId=null` 为根任务。 | `ParentTaskId`、`Depth`、`SortOrder`。 | 跨项目的关联、依赖关系。 |
| 负责人（Assignee） | 一个任务的单一执行责任人。 | `AssigneeUserId`、`AssigneeEmploymentId`。 | 项目 Owner、任务参与人。 |
| 参与人（Participant） | 被显式邀请协作任务的有效项目成员；可与负责人重合。 | `ProjectManagementTaskParticipantEntity`。 | 项目成员的替代或任务多负责人。 |
| 依赖（Dependency） | 前置任务与后继任务之间的排程/阻塞关系。 | `PredecessorTaskId`、`SuccessorTaskId`、`DependencyType`、`LagMinutes`。 | 父子层级、标签或评论回复。 |
| 阻塞（Blocked） | 任务当前不可正常开始/推进的状态或投影，尤其来自未满足依赖。 | `Status=Blocked`、`BlockedByCount`、`CanStart`、`BlockedReason`。 | 前端任意标红或人为绕过依赖。 |
| WIP | 同一项目中处于 `InProgress` 的任务数量上限。 | 项目 `WipLimit`；任务服务按 `InProgress` 计数。 | 用户个人待办数或里程碑容量。 |
| 活动（Activity） | 给业务用户阅读的项目域变化时间线。 | `ProjectManagementActivityEntity`。 | 平台审计的完整治理证据。 |
| 审计（Audit） | 面向治理的操作、来源、结果、字段差异和 TraceId 记录。 | `ProjectManagementAudit*Contracts`、`pm_operations`。 | 用户可编辑的评论或活动摘要。 |
| 同步 Journal | 可同步变更的服务端顺序记录。 | `ProjectManagementSyncJournalEntity`、`SequenceNo`、设备水位。 | 客户端自报完成序号或完整数据库快照。 |
| 数据空间（Data Space） | 当前应用受管理数据库/数据治理上下文。 | `ProjectManagementDataSpace*Contracts`。 | Project 或前端路由工作区。 |
| 回收站（Recycle Bin） | 软删除项目/任务的受权限保护的恢复入口。 | `ProjectManagementRecycle*Contracts`。 | 彻底删除或归档列表。 |
| 维护锁（Maintenance Lock） | 高风险同步、导入、恢复期间的工作区级互斥控制。 | `pm_maintenance_locks`、`ProjectManagementMaintenanceLock`。 | 浏览器本地加载遮罩或长事务的替代。 |

### 4.2 状态、日期、进度与依赖语义

| 分类 | 枚举/规则 | 统一语义 |
|---|---|---|
| 项目状态 | `Planning`、`Active`、`Paused`、`Completed`、`Canceled`、`Archived` | 当前允许：`Planning -> Active/Canceled/Archived`；`Active -> Paused/Completed/Canceled`；`Paused -> Active/Canceled`；`Completed/Canceled -> Archived`。`Archived` 是终态且只读。 |
| 任务状态 | `Todo`、`InProgress`、`Blocked`、`Done`、`Cancelled` | 当前允许：`Todo -> InProgress/Cancelled`；`InProgress -> Blocked/Done/Cancelled`；`Blocked -> Todo/InProgress/Cancelled`。`Done` 与 `Cancelled` 不允许再次转换。 |
| 里程碑状态 | `Planned`、`Active`、`Completed`、`Archived` | 当前允许：`Planned -> Active/Archived`；`Active -> Completed/Archived`；`Completed -> Archived`。 |
| 里程碑健康 | `OnTrack`、`AtRisk`、`OffTrack`、`Done` | 当前服务按状态/进度和截止日期计算：已完成或进度 `>=100` 为 Done；截止日期早于当前 UTC 日期为 OffTrack；七天内到期且进度 `<80` 为 AtRisk；其余 OnTrack。 |
| 优先级 | 当前 Contracts 默认 `Medium` | 本模块现有 Contracts 未定义统一优先级枚举；新接口/UI 不得擅自引入另一套字符串，需先补领域规则与迁移。 |
| 日期 | `StartDate`、`DueDate`、`CompletedAt`；任务另有 `ActualStartAt`、`ActualEndAt` | 开始/截止日期可以为空；两者同时存在时必须满足 `DueDate >= StartDate`。当前 Contracts 使用 `DateTime`，里程碑健康按 `DateTime.UtcNow.Date` 计算；用户时区/纯日期显示尚无字段级契约，提醒与日历实现不得暗中按浏览器时区重写原值。 |
| 进度 | `ProgressPercent` 为 0–100 的 decimal；任务另有 `Weight` | 叶子任务使用自身进度；父任务和里程碑/项目由加权汇总投影，父任务不得重复计入。`Weight` 默认 1；`HAO-442` 负责完整汇总与取消/软删配置。 |
| 工时 | `EstimateMinutes`、`ActualMinutes`，时间日志含起止时间和分钟数 | 预计工时可为空；实际工时是执行记录，不等同进度或 WIP。 |
| 依赖类型 | `FinishToStart`、`StartToStart`、`FinishToFinish`、`StartToFinish` | 前置与后继任务必须同项目；`LagMinutes` 是依赖的时间偏移。依赖是排程关系，不改变父子结构。 |
| 版本 | `VersionNo` | 每次可并发修改的命令携带版本；不匹配返回冲突，客户端刷新/合并后重试。 |

## 5. 跨场景验收与拒绝路径清单

以下不是可选 UX 提示，而是后续 Case 的共同验收条件：

1. **工作区隔离：** 修改请求体中的 `TenantId`、`AppCode`、ProjectId，或从深链/缓存带入其他工作区数据，均不能读取或写入对象。
2. **权限拒绝：** UI 隐藏只是体验层；无 PermissionCode 或无对象角色/范围的直接 API 调用必须返回拒绝，不泄露对象是否存在。
3. **成员变更即时性：** 用户被移除、停用或角色/范围收窄后，Data Filter、API、SignalR 分组、附件/IM 访问和缓存均要收敛；历史活动仍保留必要快照。
4. **树与依赖完整性：** 任何创建、移动、批量更新、导入和恢复都必须校验同项目、深度、父子循环、依赖循环、WIP、版本和引用关系。
5. **一致性：** 批量操作要么全量成功，要么明确逐项结果且无未知部分状态；副作用只在业务提交后发布，失败留下 TraceId/审计证据。
6. **并发与实时：** `VersionNo` 冲突不静默覆盖；实时事件只含安全 ID/版本/类型，乱序或丢失后重新拉取，不覆盖更新值。
7. **高风险操作：** 同步导入、整库导入、恢复和彻底删除需要服务端预检、确认、密码（适用时）、维护锁、自动安全备份（适用时）、回滚、审计和通知；不能因前端已二次确认而跳过。

## 6. Case 追溯索引

| 业务能力 | 后续/当前 Case |
|---|---|
| 模块、权限码、数据过滤 | `HAO-411`、`HAO-414`、`HAO-415` |
| 项目、成员、角色、里程碑 | `HAO-422`、`HAO-424`、`HAO-425`、`HAO-438` |
| 任务树、状态、进度、排序、批量 | `HAO-439`、`HAO-441`、`HAO-442`、`HAO-444` |
| 负责人、参与人、标签、依赖、模板 | `HAO-445`、`HAO-446`、`HAO-447`、`HAO-450` |
| 活动、回收站、恢复与彻底删除 | `HAO-452`、`HAO-453`、`HAO-454`、`HAO-455` |
| 项目中心、概览与我的工作 | `HAO-457`、`HAO-458`、`HAO-459`、`HAO-460` |
| 协作、通知、实时与 IM | `HAO-479`、`HAO-480`、`HAO-487`、`HAO-490`–`HAO-494` |
| 保存视图、导出、同步与数据空间 | `HAO-504`、`HAO-506`、`HAO-512`、`HAO-514`、`HAO-516`、`HAO-517`、`HAO-518`、`HAO-519`、`HAO-520`、`HAO-521`、`HAO-522` |
| 审计与治理 | `HAO-523`、`HAO-524`、`HAO-525`、`HAO-526`、`HAO-527` |

本文的术语和边界是产品、设计、开发和测试的共同输入。后续 Case 若改变 Contracts、状态机、角色或 PermissionCode，必须先同步更新本文和 `docs/architecture-and-tech-framework.md` 的菜单/RBAC 清单，再将新行为作为验收依据。
