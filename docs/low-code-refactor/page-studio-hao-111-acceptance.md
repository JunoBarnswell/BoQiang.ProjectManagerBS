# HAO-111 Page Studio 验收证据

## 范围与结论

本验收针对最新 `DesignerDocument` / `DesignerCommandBus` / Inspector transaction / Binding / Canvas / Layout / Responsive / Shortcut 链路。测试只调用当前生产模块，不引入 v3/v4、双轨、Bridge 或 Mock，不修改生产实现。

新增定向验收测试：

- `frontend/AsterERP.Web/src/pages/application-console/development-center/low-code-studio/testing/acceptance/pageStudioHao111Acceptance.test.ts`

浏览器 UI/E2E 当前为 **Blocked**：仓库没有 Page Studio 专用 E2E 配置/脚本，且本次验收环境未提供可确认的 Page Studio 登录态与运行 URL。因此 DOM、截图、真实拖拽/快捷键、权限拒绝页面和后端保存接口未宣称通过；纯逻辑验收结果见下表。

## Case 矩阵

| Case | 覆盖内容 | 结果 |
| --- | --- | --- |
| PS-01 | 节点插入、移动、属性编辑、绑定、复制子树、删除、撤销/重做 | Pass |
| PS-02 | Inspector 合并事务、多选切换、框选、整事务撤销 | Pass |
| PS-03 | Canvas 缩放锚点、拖动、平移、resize、布局对齐、响应式断点覆盖 | Pass |
| PS-04 | 有效路径/资源绑定、类型兼容；无效表达式和不兼容类型阻断 | Pass |
| PS-05 | Copy/Undo/Redo/Delete/Arrow 快捷键；序列化保存、重载、hash 恢复 | Pass |
| PS-UI-01 | 真实浏览器页面身份、非空、无 overlay、console、截图、交互 | Blocked：无可用 Page Studio URL/登录态/E2E 入口 |
| PS-UI-02 | 权限拒绝真实页面、错误绑定真实 Inspector 提示 | Blocked：需真实 UI 与权限上下文 |
| PS-UI-03 | 浏览器宽度断点、真实拖动 resize、保存 API 后重载 | Blocked：需真实 UI/运行 API |

## 真实入口与数据流定位

- 文档状态：`low-code-studio/document/DesignerDocument.ts`、`DesignerDocumentStore.ts`。
- 命令入口：`low-code-studio/commands/DesignerCommandBus.ts`；节点操作位于 `createDesignerCommands.ts`。
- Inspector：`inspector/propertyTransactions.ts`；本测试用 CommandBus 的 merge transaction 验证同一撤销边界。
- Binding：`binding/typeCompatibility.ts`、`runtime-kernel/BindingResolver.ts`；表达式校验位于 `expression/expressionGraph.ts`。
- Canvas：`canvas/coordinateSystem.ts`、`pointerTransaction.ts`、`selectionModel.ts`。
- Layout/Responsive：`layout/layoutOperations.ts`、`responsive/responsiveModel.ts`。
- 保存/重载：当前可验证的是 canonical JSON + document hash 身份恢复；真实后端保存接口在本验收环境待浏览器/API 入口确认。

## 执行与判定

在 `frontend/AsterERP.Web` 执行：

```powershell
npm test -- --run src/pages/application-console/development-center/low-code-studio/testing/acceptance/pageStudioHao111Acceptance.test.ts
npm run typecheck
```

Pass 条件：定向测试全部通过且 TypeScript 检查通过；Fail 条件：任一 Case 断言失败；Blocked 仅用于上述真实浏览器/API 证据缺失，不用匿名 401 替代业务结论。

本次未提交 commit，未修改生产实现、公共契约、Runtime/Data Studio 或用户已有文件。
