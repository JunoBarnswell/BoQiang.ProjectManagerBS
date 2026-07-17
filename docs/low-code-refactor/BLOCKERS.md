# Low-Code Studio Coordinator Blockers

本文件是 Dynamic Workflow 的唯一跨模块阻塞登记入口。Worker 只能登记证据，不能绕过 Coordinator 修改共享契约、数据库迁移、RBAC、运行时入口或删除守卫。

## 当前阻塞

| Blocker | 证据 | 恢复条件 | 影响 |
| --- | --- | --- | --- |
| 四数据库真实集成环境不可用 | `data-studio-BLOCKERS.md` | 提供 SQL Server、MySQL、PostgreSQL 与受控 SQLite 连接及授权凭据 | HAO-69～89、HAO-113、HAO-114 |
| 生产维护窗口、备份与回滚演练不可用 | `migration-and-rollback-runbook.md`、`definition-of-done.md` | 运维提供窗口、快照、健康检查、正式产物指针并完成演练 | HAO-21、HAO-99、HAO-101、HAO-114 |
| 完整生产 API/UI 授权与可访问性实测不可用 | `local-browser-smoke-2026-07-12.json` | 使用授权用户完成重启后 API、权限拒绝、租户边界、审计和浏览器验收 | HAO-102、HAO-107～114 |

## 规则

- `In Review` 只表示开发完成；统一联调和全部外部证据通过后才允许 `Done`。
- 不得使用 Mock、匿名 401、占位连接、伪造 seed 或假 UI 结果替代真实证据。
- 每个 Worker 报告必须包含改动文件、关键符号、验证命令、结果、风险和恢复条件。
- 阻塞解除后由 Coordinator 复跑受影响的自动化、API、浏览器和删除守卫，并在对应 Linear 任务追加证据。
