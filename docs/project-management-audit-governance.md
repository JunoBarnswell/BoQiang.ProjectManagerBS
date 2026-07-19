# 项目管理审计治理策略

## 默认策略

| 项目 | 默认值 | 规则 |
| --- | ---: | --- |
| 在线保留 | 180 天 | 一般活动超过在线期进入归档标记，仍留在 `pm_activities`，继续使用同一租户、应用、项目 ORM 数据权限查询。 |
| 归档保留 | 2555 天 | 归档期满的一般活动执行逻辑删除；归档时清除 `Remark` 字段并将操作者显示为 `[已归档]`，避免个人信息和字段前后值长期暴露。 |
| 清理批次 | 1000 条 | `audit.governance.cleanup` 每次只处理一个受控批次，重复执行可继续推进，失败由 `pm_operations` 记录。 |
| 容量上限 | 100000 条 | 当前租户/应用的活动与操作记录总量达到上限时，治理操作在 `ImpactJson` 标记 `CapacityAlert`。 |

## 不可变高风险审计

备份、恢复、导入、导出、删除、清理、权限、安全、登录、审批、工作流、同步、治理及失败活动不进入归档和删除条件；`pm_operations` 及其事件表不由该清理任务删除。高风险操作的生命周期仍通过 `pm_operations` 和操作事件保留。

## 执行链路与权限

管理员通过 `PermissionCodes.ProjectManagementOperationManage` 读取策略或启动 `POST /api/project-management/audit/governance/cleanup`。请求创建 `pm_operations` 的 `audit.governance.cleanup` 记录，由 `ProjectManagementOperationRunner` 统一调度；任务启动时冻结策略到 `ImpactJson`，完成后写入归档数、删除数、高风险保留数、容量统计和告警状态。清理失败只将该操作标记为 `Failed`，不会删除高风险记录。

审计查询和导出继续经过既有项目对象授权、ORM 数据权限过滤和详情脱敏链路；归档行没有另建旁路数据库，因此授权恢复查询不会绕过现有边界。

## 配置与修改审计

默认设置位于 ABP 设置定义：`ActiveRetentionDays=180`、`ArchiveRetentionDays=2555`、`CleanupBatchSize=1000`、`CapacityLimit=100000`。设置修改沿用系统设置的权限和操作日志链路；治理执行的操作者、TraceId、冻结策略、结果数量和完成状态保存在 `pm_operations`。
