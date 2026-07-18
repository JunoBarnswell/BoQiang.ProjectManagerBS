# ProjectManagement 统一任务工作区架构

## 目标与边界

本设计解决当前任务工作区将路由、查询、编辑、评论、附件、保存视图和六类投影堆叠在单页的分叉问题。任务是唯一业务对象；`tree/list/card/board/gantt/calendar` 只改变投影和交互，不拥有独立的任务语义、权限判断、缓存命名或写入路径。

本设计覆盖 Linear `HAO-478`，并为 `HAO-444`（移动/批量事务）、`HAO-460`（我的工作）、`HAO-492`（实时失效）提供唯一的前端状态和后端契约边界。

## 产品语义

一个项目工作区有且只有一个可分享的 `TaskWorkspaceState`：

- 工作区身份：由当前认证会话的 `TenantId + AppCode` 派生，前端不能通过 URL 或请求体覆盖它。
- 上下文：`ProjectId`、可选 `MilestoneId`、选中的 `TaskId`。
- 查询：关键词、状态、负责人、日期范围、是否包含已完成、分组、排序、分页。
- 投影：`tree/list/card/board/gantt/calendar`。其中 tree/list 共享层级/筛选/选择和命令状态，构成产品定义的“任务树/行视图”。
- URL：承载可分享的查询字段与选中任务；临时输入、弹窗、上传进度和未保存表单不进入 URL。

切换投影不创建第二份筛选或选中状态；删除、权限拒绝或查询结果不再包含选中任务时，统一清除选择并显示可恢复的提示。

## 组件边界

```text
ProjectManagementTaskWorkspaceRoute
  └─ TaskWorkspaceController
      ├─ useTaskWorkspaceUrlState
      ├─ useProjectManagementTaskQuery
      ├─ useProjectManagementTaskCommands
      ├─ useProjectManagementRealtimeConnection
      ├─ TaskWorkspaceToolbar
      ├─ TaskWorkspaceSelectionPanel
      └─ TaskProjection
          ├─ TaskTreeProjection
          ├─ TaskListProjection
          ├─ TaskCardProjection
          ├─ TaskBoardProjection
          ├─ TaskGanttProjection
          └─ TaskCalendarProjection
```

- 页面路由只解析 `ProjectId` 与投影类型，并处理 403/404。
- Controller 维护 `TaskWorkspaceState`，驱动查询、命令结果和精确缓存失效。
- 每个 Projection 只接收已查询任务、选择状态和可调用命令；不得请求 API、拼接权限码或保存第二份筛选状态。
- 评论、附件和任务编辑位于 `TaskWorkspaceSelectionPanel`，只针对选中任务加载，关闭/删除后释放其查询。

## API 与领域契约

所有视图使用同一 `ProjectManagementTaskQuery` 和 `ProjectManagementTaskResponse`。服务端负责项目对象访问、数据过滤、排序、分页、阻塞投影和并发版本；ViewKey 只允许影响投影所需的有界排序/字段，不得改变权限或任务状态机。

所有写入继续走同一任务命令边界：创建、更新、移动、批量更新、删除、恢复。命令必须使用当前 `VersionNo`，在同一事务中写入任务、进度/里程碑投影、活动、同步日志；提交后才发布实时失效事件。

保存视图的 `QueryJson` 改为受版本控制的结构化 `TaskWorkspaceState`，而非页面自定义 JSON。服务端对 JSON 白名单字段、ViewKey、分页边界、排序与分组做反序列化验证；共享视图仅 Owner/Manager 可写。

## 缓存和实时规则

ProjectManagement 的所有 React Query key 都必须含会话派生的 `tenantId` 和 `appCode`，之后才是资源、`ProjectId` 与规范化查询状态。认证工作区切换必须清除 ProjectManagement 根缓存。

实时事件包含 `ProjectId`、聚合类型、聚合 ID、版本和 TraceId。接收端仅失效同一工作区和项目的任务查询、概览、保存视图、选中任务评论/附件；不得以空前缀全局清缓存。

## 权限、状态与故障处理

- 路由：`project-management:task:view`。
- 任务命令：后端对象级角色策略为准，前端仅用 PermissionButton/PermissionGuard 提升可用性。
- 403：不保留受限任务内容；404/软删除：清除选中任务并回到同项目安全投影。
- 409：保留表单草稿，显示服务器版本并允许用户刷新后重试；不得静默覆盖。
- Loading、Empty、Error、网络重连和未保存离开均由 Controller 的统一状态驱动。

## 迁移与验收顺序

1. 建立 `TaskWorkspaceState`、URL 编解码、工作区作用域 QueryKey 和工作区切换缓存清理测试。
2. 将当前单页拆为 Controller、选择面板和六个 Projection；先确保 tree/list/card/board 使用相同 state/commands，再接入 gantt/calendar 的真实日期投影。
3. 将保存视图替换为版本化结构化状态，并补共享/默认/冲突/非法 JSON 测试。
4. 将 SignalR 精确失效接到统一 key；补项目切换、任务删除、权限变化和断线重连测试。
5. 最后接入移动、批量、撤销/重做和浏览器 E2E。只有 URL、权限拒绝、跨工作区缓存隔离、五类投影一致性与真实业务写入都通过，`HAO-478` 才可关闭。

## 验收矩阵

| 场景 | 证据 |
|---|---|
| 视图切换 | 相同 ProjectId、筛选、选中任务和 URL 状态；无二次业务查询语义 |
| 工作区切换 | 旧 tenant/app 查询缓存被移除，接口头与 QueryKey 同时切换 |
| 实时变更 | 仅本项目受影响 key 失效；其他项目缓存保持 |
| 失败与冲突 | 403/404/409/网络失败不泄露数据、不丢表单草稿、不保留无效选中 |
| 保存视图 | 结构化状态可恢复，非法/越权/版本冲突被服务端拒绝 |
| 全功能验收 | Tree/List、Card、Board、Gantt、Calendar 的读取与任务命令口径一致 |
