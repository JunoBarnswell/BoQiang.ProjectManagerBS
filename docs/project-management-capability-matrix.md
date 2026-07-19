# ProjectManagement 能力矩阵与验收基线

本文是 PRD、源码和 Linear Case 的共同基线。状态只表示当前证据，不表示目标承诺：

- `Implemented`：源码入口、权限/数据边界和自动化测试均存在；仍需真实页面/API 验收才能关闭 Case。
- `Partial`：存在部分链路，但缺少一个或多个核心入口、交互、边界或证据。
- `Missing`：没有可运行的生产链路，不得以权限码、空页面或接口声明视为完成。

## 统一产品语义

Project 是工作边界，Task 是唯一执行对象；Project、Membership、Milestone、Task、Dependency 是领域命令边界。所有视图只改变查询投影，不复制业务命令、权限、版本和数据过滤。

产品要求称为“五类视图”：

1. 任务树/行视图（同一查询域的 `tree` 层级模式与 `list` 平铺模式）；
2. 卡片视图（`card`）；
3. 看板视图（`board`）；
4. 甘特视图（`gantt`）；
5. 月/周日历视图（`calendar`）。

因此 API 保留六个 `ViewKey` 投影值：`tree/list/card/board/gantt/calendar`，但产品验收按五类视图执行，`tree` 与 `list` 必须共享筛选、选择和任务命令状态。

## 能力矩阵

| 能力域 | 目标入口 | 当前证据 | 当前状态 | 关闭前必须补齐 |
|---|---|---|---|---|
| ABP 模块、迁移、权限、ORM Data Filter | `AsterErpProjectManagementModule`、`ProjectManagementDataPermissionFilterRegistrar` | HAO-411 父 Issue 与 5 个子项已验收 | Implemented | 仅保留全仓库回归风险记录 |
| 项目 CRUD、成员、角色、里程碑 | `ProjectManagement*Service/Controller` | 后端服务测试 | Partial | 成员/里程碑真实页面、权限允许/拒绝和隔离 API 验收 |
| 项目概览、我的工作 | `/workspace/overview`、项目中心/我的工作页面 | `ProjectManagementOverviewService`、`/api/project-management/overview`、概览/我的工作页面；项目概览已消费后端聚合 DTO | Partial | 完整今日/未来/逾期/阻塞聚合、指标跳转、真实权限浏览器验收和快捷入口 |
| 任务树、行、卡片、看板、甘特、日历 | `ProjectManagementTaskQuery` + 六个 `ViewKey` | 后端统一查询；前端六种真实投影、统一 URL QueryState、租户/应用隔离缓存键 | Partial | 拖动、批量、失败回滚、分页交互及真实浏览器权限验收 |
| 任务依赖、阻塞、WIP、并发 | Task/Dependency service | 53 个 PM 测试 | Partial | 页面依赖编辑、强制绕过审计、真实 API 版本冲突 |
| 保存视图、筛选、分组、列 | SavedView service | 版本化 URL QueryState 保存/恢复入口；后端当前接受 JSON 对象 | Partial | 服务端 QueryState 白名单校验、共享/默认/删除/编辑和权限边界 |
| 回收站、恢复、永久删除 | `ProjectManagementRecycleService`、`ProjectManagementTaskHierarchy`、`/project-recycle-bin` | ORM 数据过滤下的项目/任务分页查询、版本化恢复、项目引用保护永久删除、任务删除/恢复子树、专用页面 | Partial | 任务永久删除、批量操作、保留策略与真实浏览器权限拒绝验收 |
| 评论、Markdown、Mention、活动 | Comment/Activity service | 后端部分链路 | Partial | AST/安全渲染、Mention UI、活动流和统一副作用 |
| 附件、预览、删除 | FileReference/Attachment service | 上传下载后端和部分页面 | Partial | 前端预览/删除、活动、病毒/类型策略和补偿测试 |
| 提醒、通知、SignalR、浏览器通知、IM | `ProjectManagementTaskReminderService`、Notification/Realtime | 任务提醒 CRUD、每接收人幂等键、Hangfire 延迟作业、重试状态、任务删除取消、任务详情提醒区和项目级实时失效事件 | Partial | Hangfire 重启/多实例实测、站内通知中心未读/跳转、浏览器/IM 接线与真实重连验收 |
| 模板、周期自动化、Webhook、工作流 | Template/Job contracts | 一次性模板应用 | Partial | 调度器、幂等重试、领域事件、Webhook 签名和工作流绑定 |
| 搜索与全文索引 | Search service | 三表 `Contains` | Partial | FTS/倒排索引、重建、增量、权限过滤和性能门槛 |
| bqsync 增量/全量、冲突、附件 | Sync service | 核心校验/水位/冲突 | Partial | 流式资源限制、API 集成、跨进程锁和附件补偿 |
| 数据空间、概览、容量治理 | DataSpace service/page | 摘要和备份入口 | Partial | 授权空间列表、容量、后台任务和健康检查 |
| 备份恢复 | Backup service | 真实 SQLite 文件回归 | Partial | 保留策略、下载/删除、后台任务、多实例锁和演练 |
| 报表、Excel/CSV/PDF、导入导出 | `report:export` permission only | 无完整 PM 报表链路 | Missing | 统一报表 DTO、异步导出、注入防护、导入预览/幂等 |
| 审计与治理 | Audit service/page | 活动/操作分页和 CSV | Partial | 详情差异、脱敏、保留策略、真实 HTTP/浏览器证据 |
| M5 安全/性能/迁移/E2E | 全仓库门禁 | 自动化证据按实际结果记录；真实身份浏览器 E2E/UAT 已登记为用户确认的允许例外 | Partial / Allowed Exception | 继续补充领域边界、契约、性能、迁移和权限证据；E2E/UAT 缺失不作为本阶段关闭阻塞，但不得宣称已通过 |
| M6/M7 发布、运维、培训、UAT | 发布与文档 | 无 PM 专属发布/UAT 手册 | Missing | smoke、回滚、监控、演练、培训和验收记录 |

## 实施顺序

1. 先完成 P0：项目概览、成员/里程碑真实入口、回收站、报表基础契约。
2. 再完成统一任务工作区：六个投影、完整 QueryState、拖动/批量/保存视图。
3. 再完成 M3 领域事件与 Outbox 边界，避免通知/活动/SignalR/IM/Webhook 分散侵入业务服务。
4. 再完成 M4 数据中心和报表的流式、资源、容量、恢复语义。
5. 最后执行 M5–M7 可执行的质量、发布和运维证据，并逐 Case 填证据、关闭子项，再关闭父项；真实身份浏览器 E2E/UAT 按用户确认登记为允许例外，不以缺失证据冒充通过。
