# Latest-only 删除守卫策略

## 受保护范围

守卫扫描 `backend/AsterERP.Api`、`backend/AsterERP.Contracts`、`frontend/AsterERP.Web/src` 和正式契约目录 `docs/contracts`。迁移 Fixture 位于 `docs/low-code-refactor/fixtures`，属于迁移输入，不是运行实现，也不在正式契约目录。

## 必须阻断

- `DesignerRuntimeRenderer` 或 `RuntimePage.tsx` 对旧 renderer 的导入。
- `runtimeDocumentCodec`、`simulatedWidth`、旧 parser/compiler/runtime 入口。
- 版本化正式契约文件名（例如 `designer-document-v3.schema.json`）和数字文档版本路由。
- `ConfigJson.Contains`/`PublicConfigJson.Contains` 业务类型判断。
- SQLite 旧绝对路径逃逸和测试开关绕过。

## 允许出现的位置

旧字段只允许出现在明确的迁移实现、拒绝输入校验或 `docs/low-code-refactor/fixtures`。扫描器不会把生产源码中的任意 `fixtures` 目录作为自动 allowlist；正式契约必须使用 `designer-document.latest.schema.json`。

## 结果规则

- `Pass`：受保护根目录完整存在，扫描完成且无发现。
- `Fail`：发现禁用语义或正式契约回归；修真实链路后重跑。
- `Blocked`：受保护目录、依赖、测试运行环境或必要真实数据不可用；记录命令和恢复条件。

## 删除/回滚证据

删除旧契约与入口时记录删除 commit、`git show --stat <commit>`、守卫测试输出和上一正式 artifact/数据库备份指针。回滚只恢复上一正式 artifact 和数据库快照，不恢复旧运行入口或旧正式契约。
