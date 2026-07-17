# AsterERP Low-Code Studio 最新契约

## 唯一数据边界

`DesignerDocument` 只保存可持久化的页面业务结构：节点树、属性、绑定、动作、变量、页面元数据和运行上下文。它不包含选择、视口、面板、撤销栈、保存状态或临时事务。

`DesignerEditorSession` 只保存编辑态：`selectedNodeIds`、`primaryNodeId`、`anchorNodeId`、viewport、panelState 和当前 transactionId。编辑会话不能进入发布产物。

`RuntimeArtifact` 是发布后的不可变输入，只包含已校验文档的编译结果、`revision`、`artifactHash`、`compilerVersion`、`manifestTypes` 和运行所需数据。RuntimeKernel 不读取草稿或 EditorSession。

## 不变量

- 不使用数字 schema 版本语义；契约通过结构校验、compiler/manifest 元数据和迁移 revision 判定。
- 节点 ID 在文档内唯一，父子关系双向一致，每个页面 root 可达且无环。
- 绑定保存稳定 `resourceId`，显示名称只用于 UI。
- 所有文档变更通过 Command Bus 产生 revision 和可逆变更。
- RuntimeArtifact 的 hash 必须覆盖规范化 document payload；篡改、未知组件、未知动作和无效绑定在发布或运行前阻断。

## Stable ResourceRef migration contract

Persisted bindings use `resourceId`, `resourceType`, `valueType`, `displayName`,
optional `fallback`, and optional `conversionPipeline`. `resourceType` is the
runtime provider namespace; `displayName` and path previews are presentation
data only. The one-time migration converts historical `source/path` expressions
and `source::path` identifiers to the canonical `source:path` identity, and
rejects a binding that has neither a resource type nor a resource identity.
