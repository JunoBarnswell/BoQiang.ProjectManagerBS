# 审批流端到端可视化测试计划

## 目标

在 `tenant-a/MES` 应用库中完成建模、发布、绑定、发起、审批、历史追踪和权限隔离验证。所有测试账号、组织、岗位、角色和审批菜单权限必须来自 `backend/AsterERP.Api/data/application-databases/tenant-a/MES/mes11.db`，不允许主库兜底。

证据目录默认使用 repo 外路径：`D:\Code\AsterERP-evidence\workflow-approval-YYYYMMDD-HHmm`。每个 Case 保存账号、步骤、页面截图、关键 API 结果和必要的 SQLite 查询结果。

## 测试账号

| 账号 | 密码 | 用途 |
| --- | --- | --- |
| `wf_starter` | `starter123` | 发起申请、验证发起人审批 |
| `wf_user_approver` | `approve123` | 指定用户审批、上一审批人 |
| `wf_role_approver` | `roleapprove123` | 角色候选审批 |
| `wf_dept_approver` | `deptapprove123` | 部门候选审批 |
| `wf_position_approver` | `positionapprove123` | 岗位候选审批 |
| `wf_manager_approver` | `managerapprove123` | 部门领导、发起人上级 |
| `wf_delegate` | `delegate123` | 审批代理人 |
| `wf_no_permission` | `noperm123` | 无 workflow 权限隔离 |

## Case 0：应用库基线

1. 重启后端。
2. 登录 `tenant-a/MES`，依次验证 `wf_*` 账号可登录。
3. 查询 `mes11.db`：`system_users`、`system_roles`、`system_user_roles`、`system_user_app_roles`、`system_departments`、`system_positions`。
4. 验证 `wf-finance` 等部门 `LeaderUserIdsJson` 包含 `wf_manager_approver`。

Pass：账号和组织权限均在应用库，`wf_no_permission` 无 workflow 菜单。

## Case 1：流程设计器完整性

1. 管理员打开 `/workflows/models`。
2. 新建流程，逐个切换：业务设计、审批人、表单绑定、流程配置、BPMN、发布版本。
3. 验证空审批人、空表单字段、自定义表达式为空、无发布版本时保存/发布有明确错误。
4. 配置 `commentRequired` 与 `attachmentPolicy`，发布后重新打开确认回显。

Pass：6 个 Tab 不溢出，BPMN 可加载，保存后配置不丢失。

## Case 2：指定用户审批

1. 管理员配置审批人为 `wf_user_approver` 并发布绑定。
2. `wf_starter` 发起审批。
3. `wf_user_approver` 登录，待办可见并审批通过。
4. 检查已办、审批历史和实例详情。

Pass：只有指定用户可处理，历史记录审批人为 `wf_user_approver`。

## Case 3：角色审批

1. 节点配置角色 `wf_role_approver`。
2. `wf_starter` 发起。
3. `wf_role_approver` 可见候选待办并认领/审批。
4. 非该角色账号不可见或不可处理。

Pass：候选组权限正确，处理后不可重复审批。

## Case 4：部门审批

1. 节点配置部门 `wf-finance`。
2. `wf_dept_approver` 可见候选待办。
3. 非财务部门账号不可见。

Pass：部门候选组按应用库部门成员过滤。

## Case 5：岗位审批

1. 节点配置岗位 `wf-position-manager`。
2. `wf_position_approver` 可见并完成审批。

Pass：岗位候选组按应用库岗位成员过滤。

## Case 6：发起人审批

1. 节点配置发起人。
2. `wf_starter` 发起后待办回到 `wf_starter`。

Pass：发起人变量解析为当前发起账号。

## Case 7：部门领导 / 发起人上级

1. 部门管理为发起人部门设置最多三位部门领导，第一位为 `wf_manager_approver`。
2. 节点配置部门负责人或发起人上级。
3. `wf_starter` 发起后 `wf_manager_approver` 可见待办。
4. 清空部门领导后重试发起。

Pass：有领导时只领导可处理；无可解析领导时发起失败并提示。

## Case 8：上一审批人

1. 两个连续审批节点：第一节点指定 `wf_user_approver`，第二节点配置上一审批人。
2. `wf_user_approver` 完成第一节点。
3. 第二节点仍回到 `wf_user_approver`。

Pass：`previousApproverUserId` 变量写入并被下一节点解析。

## Case 9：附件与意见策略

1. 节点配置“意见必填 + 附件必填”。
2. 不填意见提交，应被前端和后端拒绝。
3. 填意见但不上传附件，应被拒绝。
4. 上传附件后审批通过，历史详情可下载。
5. 节点配置 `attachmentPolicy=none`，上传附件后提交应被拒绝。

Pass：策略在运行时强制执行，不只是设计器保存。

## Case 10：审批代理

1. `wf_manager_approver` 在审批委托页设置代理给 `wf_delegate`，起止时间覆盖当前时段。
2. 流程任务分配给 `wf_manager_approver`。
3. `wf_delegate` 登录可见领导待办并审批。
4. 查询历史：任务 `Owner` 保留原领导，实际审批人为 `wf_delegate`。
5. 修改代理结束时间为过去，再登录 `wf_delegate`。

Pass：有效期内代理可见可处理，到期自动不可见；无主库兜底。

## Case 11：权限隔离

1. `wf_no_permission` 登录。
2. workflow 菜单不可见。
3. 直接请求待办、审批、附件 API。

Pass：前端菜单隐藏，后端 API 返回权限拒绝；非候选审批人不能查看或处理他人任务。
