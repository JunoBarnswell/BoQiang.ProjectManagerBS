# DesignerDocument / EditorSession / RuntimeArtifact boundary

`DesignerDocument` 是可编辑业务文档，`DesignerEditorSession` 只承载编辑器交互状态，`RuntimeArtifact` 是不可变可执行产物。三者不能通过共享 JSON 或页面聚合字段互相夹带状态。

后端正式文档写入 `app_dev_documents`，每次写入同时写入 `app_dev_document_revisions`；文档 hash 使用 canonical JSON 的 SHA-256，已有文档必须同时匹配 expected revision 与 expected hash。运行时只读取 `app_dev_runtime_artifacts`，不读取编辑会话或草稿。

当前页面旧 `LayoutDraftJson` 仍作为历史迁移输入，新的 Document Store 已成为新增文档/修订的正式边界；旧字段清理属于后续迁移 Case，不能作为新业务逻辑入口。
