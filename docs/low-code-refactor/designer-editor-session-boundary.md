# DesignerEditorSession 最新边界证据

## 根因

旧版 `DesignerEditorSessionStore` 通过对象展开处理 `patch` 和 `replace`。调用方传入的未知字段会被保留到 session，因而可能把 `document`、`editorState`、历史或其他编辑态数据带入 session 快照。旧 `DesignerViewport` 也没有稳定的 `pan` 字段，部分更新只能覆盖或丢失视口平移状态。

## 当前边界

`DesignerEditorSessionStore` 现在只生成以下字段：

- `selectedNodeIds`、`primaryNodeId`、`anchorNodeId`
- `viewport.pan`、`viewport.zoom`、`viewport.width`、`viewport.height`
- `panelState`、`transactionId`、`documentId`、`sessionId`

`replace` 和 `patch` 使用显式字段映射，不复制未知字段；所有返回快照都经过归一化、深复制和冻结。`viewport.pan` 默认归一化为 `{ x: 0, y: 0 }`，部分 pan patch 会与当前 pan 合并后再归一化。session 不负责写入或修改 `DesignerDocument`。

## 验证证据

目标测试：

```text
npm test -- --run src/pages/application-console/development-center/low-code-studio/session/DesignerEditorSessionStore.test.ts
```

覆盖：选择归一化、pan/zoom/尺寸边界、pan JSON 序列化、不可变 replace/patch、订阅去重、嵌入 document/editorState 字段拒绝。
