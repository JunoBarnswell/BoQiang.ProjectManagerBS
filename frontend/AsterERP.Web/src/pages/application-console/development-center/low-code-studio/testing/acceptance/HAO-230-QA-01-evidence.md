# HAO-230 / QA-01 Playwright DOM 验收证据

## 环境与入口

- 验收时间：2026-07-16（Asia/Shanghai）
- 前端：`http://127.0.0.1:5173`
- 后端：`http://127.0.0.1:5000`，通过 Vite `/api` 代理访问
- 登录上下文：`tenant-a / MES / admin`
- Designer：`/tenants/tenant-a/apps/MES/admin/development-center/pages/1d77fd8dee5941aeba46fd19a650c360/designer`
- Designer 页面 API：`GET /api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360` → `200 OK`

原始响应证据：[`hao-230-qa-01-page-response.json`](../../../../../../../../../output/playwright/hao-230-qa-01-page-response.json)

## 真实 DOM 结果

- Designer 路由加载成功，标题为 `MES订单管理`，页面树包含 26 个 `role=treeitem`。
- 选择“状态”节点后，DOM 暴露 8 个 resize handle（`northwest`、`north`、`northeast`、`west`、`east`、`southwest`、`south`、`southeast`）。
- `设计器工具`、`页面设计画布`、`属性检查器`、`小地图导航区域` 均存在。
- 四布局按钮均存在：`自由布局`、`Flex`、`Grid`、`约束布局`。初始 `Flex` 为 pressed；点击 `自由布局` 后 `自由布局` 为 pressed。
- 点击 `自由布局` 后，继续点击 `Grid` 或 `约束布局` 均被页面错误横幅拦截，Playwright 精确错误为：`<div role="alert" class="page-studio__error-banner">... intercepts pointer events`。因此四布局切换闭环为 **Blocked**。
- 设备断点切换可见：`Mobile · iPhone 14 · 390×844` 选中时画布显示 `phone-portrait · 390×844`，小地图显示 `165%`；`Tablet · landscape · 1024×768` 选中时显示 `tablet-landscape · 1024×768`，小地图显示 `63%`。两种设备均仍显示不可预览状态。

## 旧布局字段：来源文档与诊断

API 返回的 `documentJson` 仍包含 legacy `layout` 字段。响应中的精确来源包括：

- `editor-panel.layout`: `alignItems`, `flex`, `flexDirection`, `gap`, `layoutMode`, `minHeight`, `minWidth`
- `form-actions.layout`: `gap`, `justifyContent`, `layoutMode`
- `list-panel.layout`: `alignItems`, `flex`, `flexDirection`, `gap`, `layoutMode`, `minHeight`, `minWidth`
- `order-table.layout`: `display`
- `page-header.layout`: `alignItems`, `gap`, `justifyContent`, `layoutMode`, `minHeight`, `width`
- `page_mr7xi5jk_root.layout`: `alignItems`, `flexDirection`, `gap`, `layoutMode`, `minHeight`, `width`
- `search-card.layout`: `alignItems`, `flexDirection`, `gap`, `layoutMode`, `minHeight`, `width`
- `search-form.layout`: `alignItems`, `gap`, `layoutMode`, `minHeight`, `width`
- `work-row.layout`: `alignItems`, `gap`, `layoutMode`, `minHeight`, `width`

Designer 首次加载的可见 `alert` 报告上述字段“已废弃，请使用 LayoutProtocol”。点击 `自由布局` 后，诊断进一步暴露 `customer-name`、`keyword`、`order-no`、`query-button`、`status-filter` 等节点的 `height`、`position`、`width`、`x`、`y` legacy 字段。

画板状态 DOM 为：`当前配置无法预览`；顶部 `预览` button 带 `disabled` 属性。

## 网络与控制台证据

- `GET /api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360` → `200 OK`，响应包含上述 legacy `documentJson`。
- `POST /api/application-development-center/monitoring/events` → `400 Bad Request`，响应：`{"code":40001,"message":"The request field is required.","data":null,...}`。
- Console：`Failed to start the connection: Error: The connection was stopped during negotiation`（`/@vite/client` HMR）；以及 monitoring events `400 Bad Request`。
- 用菜单中的空白页 slug 直接打开 Designer：`GET /api/application-development-center/pages/codex_____232405_20260705152405481_6580` → `404 Not Found`，无法作为隔离编辑页继续验收。

## QA 结论

| 验收项 | 结论 | 证据 |
| --- | --- | --- |
| Designer 路由与基础 DOM | Pass | 页面标题、树、属性检查器、小地图和 resize handles 可见 |
| Free/Flex/Grid/Constraints 切换 | Blocked | Free 可切换；错误横幅拦截 Grid/Constraints |
| 组件插入/移动/Resize/Overlay | Blocked | Resize handles/Overlay 结构可见；继续变更被错误横幅阻断，未执行持久化编辑 |
| 断点切换 | Partial | iPhone 14 与 tablet landscape 可切换，均仍不可预览 |
| 保存/重载 | Blocked | Save 按钮存在，但当前页面为共享已发布页；在无隔离有效 Designer 页且 Preview 已 disabled 时未持久化修改 |
| 运行预览 | Blocked | DOM 明确 `当前配置无法预览`，`预览` disabled |

截图：[`hao-230-qa-01-designer-blocked.png`](../../../../../../../../../output/playwright/hao-230-qa-01-designer-blocked.png)

本轮仅新增本证据文件及 Playwright 证据产物，未修改生产核心文件；未修复发现的缺陷。

## 2026-07-16 DbMigrator 重启后的复验

### 复验输入

- 官方 DbMigrator 已完成，API 已重启，端口仍为 `5000`；前端端口为 `5173`。
- 重新登录：`tenant-a / MES / admin`。
- Designer API：`GET /api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360` → `200 OK`。
- 当前页面响应中的 `documentJson.revision` 为 `25`；所有 27 个元素的 `layout` 顶层仅包含 `protocol`，不再包含旧的 `display`、`layoutMode`、`width` 等顶层字段。
- 根节点的 canonical protocol 为：`layout.protocol.container.mode=flex`、`layout.protocol.container.flex=@{alignItems=stretch; direction=column; gap=0; justifyContent=start; wrap=nowrap}`、`layout.protocol.size.width=100%`、`layout.protocol.size.minHeight=720px`。

复验原始响应：[`hao-230-qa-01-page-response-rerun.json`](../../../../../../../../../output/playwright/hao-230-qa-01-page-response-rerun.json)

### 复验结果

- **诊断/Preview：Fail。** canonical API 文档已无 legacy 顶层字段，但 Designer 仍显示 `elements.page_mr7xi5jk_root.layout.display`、`layout.layoutMode`、`layout.width` 已废弃；画板显示 `当前配置无法预览`，顶部 `预览` button 仍为 `disabled`。这是“API 已 canonical、前端诊断仍生成 legacy 字段”的精确不一致。
- **组件树/Overlay/Resize：Blocked。** DOM 仍有树和 Overlay；但点击“订单查询”时被其后代“编辑保存”节点拦截（`subtree intercepts pointer events`），点击“状态”时被 `.page-studio__panel-header` 拦截（`intercepts pointer events`），无法稳定选中节点。上一轮可见的 8 个 resize handle 本轮无法通过真实点击再次进入。
- **Free/Flex/Grid/Constraints：Blocked。** 因节点无法选中，布局编辑工具栏未进入可操作状态，无法完成四模式切换验收；未使用脚本强制点击绕过真实命中测试。
- **设备断点：Partial/Pass。** `Mobile · iPhone 14 · 390×844` 切换后显示 `phone-portrait · 390×844`；`Tablet · landscape · 1024×768` 切换后显示 `tablet-landscape · 1024×768`。两者均保留不可预览状态。
- **保存/重载：Blocked。** 保存按钮可见，但当前为共享已发布 MES 页面；在 Preview 与节点选中均失败的情况下未持久化修改，避免污染共享数据。
- **运行预览：Fail/Blocked。** 直接打开 `/tenants/tenant-a/apps/MES/admin/pages/page_mr7xi5jk` 后长期停留“加载中”；运行 API `GET /api/runtime/pages/page_mr7xi5jk` 返回 `400 Bad Request`，响应为 `code=42031, message=运行时页面权限配置无效`。

运行 API 证据：`GET /api/runtime/pages/page_mr7xi5jk` → `400`，`{"code":42031,"message":"运行时页面权限配置无效","data":null,...}`。

### 复验控制台/网络

- Designer 控制台仍有 Vite HMR negotiation error：`Failed to start the connection: Error: The connection was stopped during negotiation`。
- 本轮未再出现上一轮 `monitoring/events` 的 400；Designer 页面 API 为 200。
- 运行预览新增明确业务错误：`/api/runtime/pages/page_mr7xi5jk` 连续返回 400，原因是 `运行时页面权限配置无效`。

复验截图：

- [`hao-230-qa-01-designer-rerun-blocked.png`](../../../../../../../../../output/playwright/hao-230-qa-01-designer-rerun-blocked.png)
- [`hao-230-qa-01-runtime-rerun-blocked.png`](../../../../../../../../../output/playwright/hao-230-qa-01-runtime-rerun-blocked.png)

### 复验结论

DbMigrator 已使服务端返回的 Designer 文档 canonical 化，但 QA-01 仍未解除：前端 Designer 对 canonical protocol 产生错误 legacy 诊断，导致 Preview disabled；真实 DOM 命中层级还阻断节点选择；运行 API 另有 `42031 运行时页面权限配置无效`。本轮仅更新本 acceptance 证据文件和 Playwright 证据产物，未修改生产代码。

## 2026-07-16 当前登录态 API 与数据库命中核对

### Page API 精确响应

当前登录态为 `tenant-a / MES / admin`，重新访问目标 Designer 后捕获的页面请求为：

```text
GET http://127.0.0.1:5173/api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360
HTTP 200
response.updatedTime = 2026-07-15T18:49:48.3148376
data.document.id = 1d77fd8dee5941aeba46fd19a650c360
data.document.revision = 25
data.document.updatedTime = 2026-07-15T18:49:48.3148376
first element id = amount-form
first element layout top-level keys = ["protocol"]
first element layout.protocol = true
first element layout = {"protocol":{"container":{"mode":"free"},"placement":{"absolute":"@{x=0; y=0}","kind":"absolute"},"size":{"height":"auto","width":"auto"}}}
```

上述结果来自 [`hao-230-qa-01-page-response-rerun.json`](../../../../../../../../../output/playwright/hao-230-qa-01-page-response-rerun.json)。因此当前 API 文档本身已是 canonical LayoutProtocol 形态；不能用旧响应文件中的 `layoutKeys/layoutMode` 代表当前服务端响应。

### tenant、应用与数据库上下文

同一登录态下 `GET http://127.0.0.1:5173/api/application-console/summary` 返回 `HTTP 200`，页面上下文明确为：

```text
application.tenantId = tenant-a
application.appCode = MES
databaseBinding.isBound = true
databaseBinding.isReachable = true
databaseBinding.databaseName = mes11.db
databaseBinding.status = Ready
```

因此本轮已确认命中 `tenant-a / MES / mes11.db`，不是其他 tenant、应用或同名数据库。API 未返回物理绝对路径，但数据库绑定信息与固定验收上下文一致。

### 为什么旧 output 仍是旧 layoutKeys/layoutMode

`output/playwright/hao-230-qa-01-page-response.json` 是 DbMigrator 重启前生成的历史证据文件，保留旧 revision/legacy `layoutKeys/layoutMode`，本轮没有覆盖它。当前复验使用新文件 [`hao-230-qa-01-page-response-rerun.json`](../../../../../../../../../output/playwright/hao-230-qa-01-page-response-rerun.json)，其中 revision 为 `25` 且首个元素顶层仅有 `protocol`。所以“固定数据库已 canonical、旧 output 仍 legacy”是旧产物未刷新造成的时间差，不是当前 Page API 又返回了旧文档。

但 canonical API 与页面状态仍不一致：Designer 当前 alert 仍诊断 root 的 `display/layoutMode/width`，并显示 `当前配置无法预览`、`预览` disabled。这是前端诊断/预览链路仍按旧字段解释或合成状态的独立阻塞。

### Runtime API 精确阻塞

运行预览请求的完整 URL、权限上下文和响应为：

```text
登录上下文 = tenant-a / MES / admin
GET http://127.0.0.1:5173/api/runtime/pages/page_mr7xi5jk
HTTP 400 Bad Request
response = {"code":42031,"message":"运行时页面权限配置无效","data":null,"traceId":"0HNN2K82JR86O:00000001"}
```

运行页 DOM 继续停留在 `加载中` / `正在获取页面数据，请稍候。`。因此当前 QA-01 仍有两个可复现阻塞：Designer canonical 文档被前端错误诊断导致 Preview disabled；Runtime API 在同一有效登录态下返回 `42031` 权限配置无效。

## 2026-07-16 revision 26 最小复验

后端重新编译/重启后，以 `tenant-a / MES / admin` 登录并刷新 Designer：

- `GET http://127.0.0.1:5173/api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360` → `HTTP 200`。
- Designer 响应 `updatedTime=2026-07-15T19:13:00.6041994`；`documentJson.revision=26`；元素 layout 均为 canonical `protocol` 结构。
- 刷新后的页面不再显示此前的根节点 legacy alert，但顶部 `预览` 仍为 disabled。
- 通过真实 `treeitem` role 点击 `编辑保存` 成功进入布局编辑工具栏；DOM 显示 8 个 resize handles：`northwest/north/northeast/west/east/southwest/south/southeast`。
- 四布局按钮均可真实切换并出现 pressed 状态：`自由布局 → Flex → Grid → 约束布局 → 自由布局`。切换后页面产生新的 `当前配置无法预览`，并报告 `布局字段 height 已废弃，请使用 LayoutProtocol`；本轮未点击保存，未写回共享页面。
- 直接点击 `订单查询` 或页面标题类 `treeitem` 仍被画布中最上层 `编辑保存` treeitem 的 pointer events 拦截；`编辑保存` 是当前可真实命中的 treeitem。
- 设备断点 `Tablet · landscape · 1024×768` 成功选择，画布显示 `tablet-landscape · 1024×768`。尝试使用错误 option value `iphone-14` 选择 iPhone 14 被 DOM 明确拒绝；实际 option value 经页面 DOM 核对为 `phone-portrait`，未继续操作。

### Runtime API revision 26

当前登录态下运行页已恢复：

```text
GET http://127.0.0.1:5173/api/runtime/pages/page_mr7xi5jk
HTTP 200 OK
response.code = 200
data.tenantId = tenant-a
data.appCode = MES
data.versionNo = 26
artifact.document.revision = 26
artifact.migrationRevision = latest
```

运行页 DOM 已正常渲染 `MES订单管理`、订单表格及真实数据；随后 `POST /api/runtime/microflows/mes_order_list_sql/execute` → `HTTP 200`。此前 `42031` 已解除。

### 本轮结论

- **后端/Runtime：Pass**：target document、published artifact、Runtime API 均为 revision 26，运行权限错误 `42031` 不再出现。
- **Designer 基础加载：Partial Pass**：刷新后旧根节点 alert 消失，但 Preview 仍 disabled。
- **布局切换/Resize：Partial Pass**：四布局按钮可切换，8 handles 可见；切换布局会重新引入 `height` legacy 诊断并阻断 Preview。
- **设备断点：Partial Pass**：tablet landscape 已真实切换；iPhone 本轮因 option value 误用而未继续验证。

本轮仅更新 acceptance 证据及 Playwright 运行产物，未修改生产代码、未保存布局变更。

## 2026-07-16 最新 canonical 修复后的最小复验

本轮重新登录 `tenant-a / MES / admin` 后刷新 Designer，并直接访问 Runtime 页面：

- Designer 页面 API 成功加载，但页面仍出现 `height must be a finite number, px, or percentage` / `width must be a finite number, px, or percentage` 的 legacy-style alert；顶部 `预览` 仍为 `disabled`。因此“无 legacy alert / Preview 可用”本轮 **Fail**。
- 真实 `treeitem` 尚未继续点击布局按钮：刷新后的 alert 与 Preview disabled 已构成明确阻塞，按要求停止扩展，避免继续产生本地布局状态变更。
- Runtime 页面真实渲染成功：`MES订单管理`、已发布标识、订单表格和数据均可见，无 `加载中`。
- Runtime 请求：`GET http://127.0.0.1:5173/api/runtime/pages/page_mr7xi5jk` → `HTTP 200 OK`；此前 `42031` 未复现。

本轮结论：后端/Runtime **Pass**；Designer canonical 诊断与 Preview **仍 Blocked**。本轮未修改生产代码、未保存任何页面变更。

## 2026-07-16 最终代码修复后的只读复验

- 刷新后先执行“环境检测”：状态为“就绪”；这是 Page Studio 解锁 Preview 的前置条件。
- Designer：未再出现 legacy layout alert；Preview 已解除 disabled，状态 **Pass**。
- 真实 treeitem 点击成功；8 个 resize handles 可见；已验证 Free 切换成功。为避免污染共享 MES 页面，本轮未保存布局变更。
- Runtime：最近一次有效登录态请求为 `GET /api/runtime/pages/page_mr7xi5jk` → `HTTP 200 / code=200`，42031 未复现，页面正常渲染。
- 四布局逐一切换、设备断点逐一复验：本轮浏览器操作在 Free/handles 后收敛，未完成全部 DOM 轮次，标记为 **Blocked/未完全验证**；对应四布局原子迁移及组件矩阵由 `layoutCommands.test.ts`、`LayoutEditorToolbar.test.tsx` 与低代码 52 文件/323 测试覆盖。

## 2026-07-16 HAO-230 最后一轮真实验收（环境检测后）

验收上下文：复用 Playwright `default` Browser，登录 `tenant-a / MES / admin`；目标 Designer 为 `1d77fd8dee5941aeba46fd19a650c360`。本轮未点击保存草稿、发布或其他持久化操作。

### Designer、环境检测与 Preview

- 刷新后 Page API 为 `HTTP 200`，`documentId=1d77fd8dee5941aeba46fd19a650c360`，`revision=26`，`updatedTime=2026-07-15T19:13:00.6041994`。
- 初始页面无 alert/布局诊断，但 Preview 因环境检测尚未通过而 disabled。
- 点击 aria-label `环境检测` 后页面状态为 `就绪 / 当前环境: development`，Preview button 移除 `disabled`：**Pass**。
- 四布局切换过程均未出现 alert 或 `当前配置无法预览`：Free、Flex、Grid、Constraints 均可看到对应 pressed 状态：**Pass**。
- 设备切换会使 Preview 暂时重新 disabled；最后再次执行环境检测后 Preview 恢复 enabled：**Pass**。

### treeitem、Resize 与布局模式

通过真实 `treeitem` role 点击 `编辑保存`，未点击 canvas 文本：

- 布局模式：`Free → Flex → Grid → Constraints` 逐一切换并确认 pressed 状态：**Pass**。
- 8 个 resize handles 全部存在：`northwest / north / northeast / west / east / southwest / south / southeast`：**Pass**。
- 未对 handle 执行拖动，避免生成或保存共享数据变更。

### 设备断点

- Desktop `1440×900` / `desktop`：**Pass**。
- Tablet portrait `768×1024` / `tablet-portrait`：**Pass**。
- Tablet landscape `1024×768` / `tablet-landscape`：**Pass**。
- Mobile portrait `390×844` / `phone-portrait`：**Pass**。
- 移动端 landscape：设备预览下拉选项未提供 mobile-landscape，仅提供 mobile portrait 型号；该项 **Blocked（产品当前无可选项）**。

### 保存前后只读完整性

- 保存前 DOM 显示 `Published / 已保存`，`保存草稿` 仅作为可见按钮，未点击：**Pass（只读）**。
- 布局和设备操作后以当前登录态只读 GET Page API，仍为 `HTTP 200 / revision=26 / updatedTime=2026-07-15T19:13:00.6041994`。
- 网络记录中未出现页面文档保存 POST/PUT/PATCH；仅出现 `preview-artifact` 与 `environment-check` 请求。共享 MES 数据未写入：**Pass**。
- 真正保存操作按“禁止修改共享 MES 数据”要求不执行：**Blocked（有意阻止）**。

### Runtime、console 与诊断

当前登录态只读调用：

```text
GET http://127.0.0.1:5173/api/runtime/pages/page_mr7xi5jk
HTTP 200
response.code = 200
data.versionNo = 26
data.tenantId = tenant-a
data.appCode = MES
```

Runtime 页面 DOM 正常渲染 `MES订单管理`、订单表格和真实数据，Runtime：**Pass**。

最终 Designer 无 alert、无 `当前配置无法预览`，诊断状态正常。Console 仍有非业务阻塞项：Vite HMR negotiation stopped，以及 `POST /api/application-development-center/monitoring/events` 返回 `400`；未影响本轮 Page/Runtime/布局验收，记录为 **Blocked（监控上报噪声）**。

本轮结论：Designer/环境检测/Preview/四布局/Resize/桌面与平板及移动 portrait/Runtime 均 **Pass**；移动 landscape 与真实保存分别因无产品选项、共享数据保护而 **Blocked**。本轮未修改生产代码或共享 MES 数据。

本轮结构化证据：[`hao-230-qa-01-final-round-r26.json`](../../../../../../../../../output/playwright/hao-230-qa-01-final-round-r26.json)

## 2026-07-16 phone-se-landscape 定向复验

- 设备预览实际 option value `phone-se-landscape` 选择成功，DOM 显示 `phone-se-landscape · 667×375`：**Pass**。
- 选择设备后 Preview 先因环境检测状态重置为 disabled；重新点击 `环境检测` 后状态为 `就绪`，Preview 恢复 enabled，页面当时无 alert/`当前配置无法预览`：**Pass**。
- 随后按要求用真实 `treeitem` 点击 `编辑保存` 做快速布局复核；8 个 handles 仍可见，但页面出现 `elements.*.layout.order: 布局字段 order 已废弃，请使用 LayoutProtocol`，并显示 `当前配置无法预览`：**Blocked**。未继续切换或保存，避免扩大共享数据影响。
- 此前本轮已逐一观察 Free/Flex/Grid/Constraints pressed 状态，四布局结论仍为 **Pass**；本次 treeitem 选择后触发的 `order` 诊断是新的 UI 状态阻塞，不代表服务端文档写入。
- Runtime 只读调用：`GET http://127.0.0.1:5173/api/runtime/pages/page_mr7xi5jk` → `HTTP 200 / code=200 / versionNo=26`：**Pass**。
- 未点击保存草稿/发布；final-round JSON 已更新 mobile landscape 为 Pass，并记录 treeitem 选择后出现的 `order` 诊断与 Preview blocked 状态。

本轮结构化证据：[`hao-230-qa-01-final-round-r26.json`](../../../../../../../../../output/playwright/hao-230-qa-01-final-round-r26.json)

## 2026-07-16 fresh Designer tab 修复复验

针对上一轮旧 `order` 证据，本轮新开 Designer tab 重新加载 Vite 模块后复验，未复用旧 tab DOM：

- 新 tab 刷新后先执行环境检测，状态为 `就绪`，Preview enabled：**Pass**。
- 真实 `treeitem=编辑保存` 点击后，布局编辑工具栏正常出现，8 个 handles 全部可见；Preview 仍 enabled：**Pass**。
- treeitem 选择后的快照中未出现 `layout.order`、`当前配置无法预览` 或 alert：**Pass**。上一轮 `order` 阻断已解除。
- Free、Flex、Grid、Constraints 四按钮在新 tab 中逐一切换并确认 pressed 状态：**Pass**。
- 实际 option value `phone-se-landscape` 选择成功，DOM 显示 `phone-se-landscape · 667×375`；无 alert，Preview 保持 enabled：**Pass**。
- Runtime 只读调用：`GET http://127.0.0.1:5173/api/runtime/pages/page_mr7xi5jk` → `HTTP 200 / code=200 / versionNo=26 / tenantId=tenant-a / appCode=MES`：**Pass**。
- 未点击保存草稿或发布；本轮只读验证未修改共享 MES 数据。

本轮结论：最新 Vite 模块下 `layout.order` legacy 诊断已消失，Designer、Preview、四布局、8 handles、phone-se-landscape 与 Runtime 均 **Pass**。

## 2026-07-16 lifecycle roundtrip（当前轮）

- Designer Page API baseline：`GET /api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360` → `HTTP 200`；`revision=26`；`updatedTime=2026-07-15T19:13:00.6041994`；`documentJson` length `24197`；raw SHA-256 `aba8d191c84f0493195dd0f99e075a18aa7c621634252171633e1e764201f1`；baseline canonical stable SHA-256 `e4859104642de2aa8dc18c6843985aaa94f58e2b8b0bc9815f3908f632e63ba6`。
- Baseline published artifact：`artifactId=fd77084a4fb041a5ba130acc50cfd8ae`、`revision=26`、`artifactHash=sha256:e4859104642de2aa8dc18c6843985aaa94f58e2b8b0bc9815f3908f632e63ba6`、`manifestHash=56424a1be613be6a8b7e2e934fd46d12869cef3af2157f1d2fc571e7b4f17bfd`：**Pass / rollback anchor**。
- 真实 treeitem 选择后切到 Flex，环境检测为就绪，点击保存草稿：`PUT /api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360` → `HTTP 200`；新开 Designer 重新读取 `revision=27`，Flex placement 已持久化：**Pass**。
- 再切回 Free，环境检测为就绪，点击保存草稿：同一 PUT → `HTTP 200`；新开 Designer 重新读取 `revision=28`：保存/重载链路 **Pass**。
- 最终 canonical parity：**Fail / Blocked**。忽略 revision 后最终 `documentJson` SHA-256 为 `ffa170151aedc52a4278cffa2a8ef8ac25816e6f75d46a43b9b641c25d67132d`，baseline published document SHA-256 为 `0780548ae30760de5091576a671e728498e11c968a9c963f3d023f77bed65592`，不一致；最终 `update-button` 为 `container.mode=free`、`placement.absolute={x:160,y:0}` 且无 `protocol` wrapper，而 baseline 为 `protocol.container.mode=free`、`protocol.placement.absolute={x:0,y:0}`。
- 因最终页面语义未恢复 baseline，**未执行发布**，避免把漂移内容写入 artifact；因此 publish HTTP 200 未取得，不能宣称 Pass。当前 published artifact revision/hash 仍为 baseline revision 26，可作为只读回滚锚点。未触碰菜单或其他业务数据。

## 2026-07-16 lifecycle rerun after latest HMR

- 重新登录 tenant-a/MES/admin 后打开最新 Designer tab；页面 API baseline `GET /api/application-development-center/pages/1d77fd8dee5941aeba46fd19a650c360` → `HTTP 200`，document `revision=26`，`updatedTime=2026-07-15T20:13:37.015528`，documentJson length `24197`，raw SHA-256 `aba8d191c84f0493195dd0f99e075a18aa7c621634252171633e1e764201f1`，canonical SHA-256 `e4859104642de2aa8dc18c6843985aaa94f58e2b8b0bc9815f3908f632e63ba6`；published artifact `fd77084a4fb041a5ba130acc50cfd8ae` revision `26`，hash `sha256:e4859104642de2aa8dc18c6843985aaa94f58e2b8b0bc9815f3908f632e63ba6`：**Pass**。
- 真实 treeitem `编辑保存` 选择后，环境检测 `POST .../environment-check` → `HTTP 200`；切到 Flex 并保存草稿 `PUT .../pages/1d77fd8dee5941aeba46fd19a650c360` → `HTTP 200`。新 tab reload 后 GET `HTTP 200`，document `revision=29`，目标 `update-button` 为 canonical Flex placement，anchor rect `x=0,y=0,width=160,height=48`：**Pass，未出现 x=160**。
- 再次新 tab reload，真实 treeitem 选择后切回 Free；环境检测 → `HTTP 200`，保存草稿 PUT → `HTTP 200`。再次新 tab reload 后 GET `HTTP 200`，document `revision=30`，目标 `update-button` 为 Free absolute `x=0,y=0`：**Pass，未出现 x=160**。
- 语义 canonical parity：**Fail / Blocked**。按每个节点 LayoutProtocol 规范化、忽略 document revision/documentHash 后，当前 document SHA-256 `ee952cd3c4d341b06ec8a3978d4241f0bd019aed6cd58786b8c722754c25983b`，published baseline document SHA-256 `0780548ae30760de5091576a671e728498e11c968a9c963f3d023f77bed65592`；27 个节点中仅 `form-actions` 有语义差异：baseline `container.mode=flex`、当前为 `container.mode=free`。目标按钮坐标本轮已修复为 `x=0`。
- 按规则在 parity 失败后停止：未执行 preview/compile 的后续验收、publish、发布后 Runtime、两个 artifact A/B rollback 或 baseline document 恢复/重新发布。当前 published artifact revision `26` 仍为安全锚点；页面草稿当前 revision `30`，尚未恢复 baseline。未修改菜单或业务数据。

## 2026-07-16 lifecycle parity read-only diff confirmation

- 当前 Page GET 仍为 `HTTP 200`；current/published document top-level keys 相同，element 数均为 `27`。
- `update-button` 的 raw layout 确实存在 `protocol` wrapper（published）与 direct `container/placement/size`（current）的形状差异，但去 wrapper 后两者完全相等，均为 Free absolute `x=0,y=0`、`size auto/auto`；因此本轮 `x=0` 已确认，hash mismatch **不是仅由 wrapper 造成**。
- 对全 document 做 LayoutProtocol wrapper 归一化、忽略 `revision` 与 `documentHash` 后，只有一个节点差异：`form-actions`。Published 为 `container.mode=flex`、`placement.kind=flex-item`、`flexItem={alignSelf:auto,basis:auto,grow:0,order:0,shrink:1}`；current 为 `container.mode=free`、`placement.kind=absolute`、`absolute={x:0,y:0}`。`size` 相同，未发现 anchor 或其他节点/业务字段差异。
- 结论：**Blocked**。应修复 Flex→Free roundtrip 对父容器 `form-actions` 的语义恢复/保存链路；仅修 serializer persisted wrapper 不足以解除 parity。未执行任何新的保存、发布或 rollback。

## 2026-07-16 lifecycle rerun after Codec persisted serializer fix

- 重新登录并等待最新 HMR 后 baseline GET → `HTTP 200`，document revision `26`，artifact `fd77084a4fb041a5ba130acc50cfd8ae` revision `26`，artifact hash `sha256:e4859104642de2aa8dc18c6843985aaa94f58e2b8b0bc9815f3908f632e63ba6`。
- Flex 阶段：环境检测 `POST .../environment-check` → `200`；保存草稿 PUT → `200`；新 tab reload GET → `200`，revision `31`。目标 layout 使用 persisted `protocol` wrapper，Flex anchor rect 为 `x=0,y=0,width=160,height=48`，未出现 `x=160`：**Pass**。
- Free 阶段：环境检测 → `200`；保存草稿 PUT → `200`；新 tab reload GET → `200`，revision `32`，目标 `protocol.placement.absolute.x=0`：**Pass，未出现 x=160**。
- 整文档 canonical parity：**Fail / Blocked**。规范化 wrapper 并忽略 revision/documentHash 后，current SHA-256 `ee952cd3c4d341b06ec8a3978d4241f0bd019aed6cd58786b8c722754c25983b`，published SHA-256 `7686819c5ee18901e2214560aba119fb9fa706af7785fdb2337a9e9b0a663d64`；唯一差异仍是 `form-actions`：current `container.mode=free + placement.kind=absolute`，baseline `container.mode=flex + placement.kind=flex-item`。目标坐标与 persisted wrapper 已正确。
- 按规则停止后续写操作：未执行 preview/environment 后续编译验收、publish、A/B artifact rollback、baseline restore/re-publish。当前 published artifact 仍为 revision `26` 锚点；未修改菜单或业务数据。

## 2026-07-16 lifecycle final after HttpClient CSRF fix

- 单一登录 tab 下 baseline/detail GET → `HTTP 200`；B 草稿 revision `42`，published pointer 仍为 A：`fd77084a4fb041a5ba130acc50cfd8ae` / revision `26` / hash `sha256:e4859104642de2aa8dc18c6843985aaa94f58e2b8b0bc9815f3908f632e63ba6`。
- A/B：Flex gap `8` 保存 PUT → `HTTP 200`，publish B → `HTTP 200/code=200`；B artifact `0ec2aaa5b21947d18259d96ab0027e0c` / revision `42` / hash `sha256:d2f23a6ada176f3c7fd4ca11caa86ea4e4833b7834d57c5086611e5d6be09524`；B Runtime API → `HTTP 200/code=200`。
- B→A rollback → `HTTP 200/code=200`，`previousArtifactId=B`，`publishedArtifactId=A`，audit `048fcb3989964c4fa89123aa403190fe`：**Pass**。rollback 后立即 Runtime 返回 `HTTP 400/code=42031`，响应为 `运行时页面权限配置无效`，已记录并继续完成最终 baseline restore。
- 从 A artifact 恢复 canonical 草稿 PUT → `HTTP 200`；form-actions 恢复 Flex `alignItems=start, justifyContent=end, gap=0, placement.flexItem.alignSelf=auto`，create/update buttons 保持 Free，update-button `x=0,y=0`，migration 未定义。
- 最终 environment-check → `HTTP 200/code=200/passed=true`；最终 publish A′ → `HTTP 200/code=200`，artifact `24a50f4087e140569f27122fb564129e` / revision `43` / hash `sha256:42dbe7e914db6319110fd46cc11b75fdf17df8b8b28d7816cae66ebe0546818d`，manifest hash保持 `56424a1be613be6a8b7e2e934fd46d12869cef3af2157f1d2fc571e7b4f17bfd`。
- 最终 Page GET → `HTTP 200`、Status=`Published`、draft/published semantic parity=`true`；Runtime GET → `HTTP 200/code=200`，artifact `24a50f4087e140569f27122fb564129e` / version `43` / document revision `43` / 27 elements。真实 Runtime DOM 显示 `MES订单管理`、`SQL Script · 已发布`、订单表格及操作按钮，无 alert：**Pass**。
- 证据结论：保存/重载、Free↔Flex 语义恢复、publish A/B、rollback B→A、最终 baseline restore/re-publish、Runtime API/DOM 均完成。rollback 瞬时 `42031` 已被最终重新发布解除；共享 MES 菜单/业务数据未修改。控制台残留 3 条非业务 HMR/监控错误，未阻断页面或 Runtime。
