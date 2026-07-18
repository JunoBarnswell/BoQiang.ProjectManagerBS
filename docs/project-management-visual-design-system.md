# ProjectManagement 视觉语言、状态色、组件与主题规范

## 1. 目的与现有系统边界

本文落实 Linear `HAO-433`，为 ProjectManagement 提供可实施的视觉与组件规则，并与以下已存在的实现保持同一体系：

| 已有基础 | 本规范的约束 |
|---|---|
| `frontend/AsterERP.Web/src/shared/styles/tokens.css` | 继续以 `--app-*` 作为全局颜色、文字、边框、阴影和圆角的唯一基础 token。 |
| `frontend/AsterERP.Web/src/app/styles/theme.css` | 继续把现有 `--app-primary-*`、`--app-gray-*` 映射给 Tailwind；项目域不重建色板。 |
| `frontend/AsterERP.Web/src/core/theme/ThemeProvider.tsx` 和 `core/state/themeStore.ts` | 保持 `data-theme` 的主题应用方式；浅色、深色、brand、kingdee、yonyou 已存在。 |
| `frontend/AsterERP.Web/src/core/responsive/breakpoint.ts`、`responsiveTokens.ts`、`responsive.css` | 复用现有断点、密度、模态和布局 token，不另起响应式体系。 |
| `ResponsivePage`、`ResponsiveToolbar`、`AdaptiveSearchForm`、`DataTable`、`ModalForm`、`ResponsiveModal`、`useMessage`、`useConfirm`、`PageStateShell` | 标准页面、查询、表格、表单、反馈和页面状态必须优先复用这些共享组件。 |

本规范不规定独立组件库、第三方主题或图表框架。只有当共享 token 或项目域复合组件无法表达下述语义时，才允许增量扩展；扩展必须先在共享层建立 token/组件，再被页面使用。

### 1.1 HAO-429 业务状态边界

视觉表达必须采用 HAO-429 的统一领域语义，不能把 UI 列名或颜色当作业务状态：

- 任务状态：`Todo`、`InProgress`、`Blocked`、`Done`、`Cancelled`。
- 进度：`0–100%` 的数值与可访问文本；父任务由子任务汇总，不用颜色推断完成度。
- 计划：开始日期、截止日期、里程碑、依赖和阻塞原因是不同维度；逾期是日期风险角标/筛选条件，**不是**可拖入的看板状态列。
- 风险：`OnTrack`、`AtRisk`、`OffTrack`、`Done`；与任务状态分开显示。
- 权限与异常：403、409、删除/归档、同步/备份风险、无数据和加载失败均是显式状态，而不是仅隐藏按钮或置灰内容。

## 2. Token 使用与项目域语义别名

### 2.1 基础原则

1. 组件只能消费语义 token 或已有 `--app-*` token；禁止在 ProjectManagement JSX 中新增 `#hex`、`rgb()`、`bg-blue-*`、`text-gray-*` 等只在浅色成立的视觉语义。
2. 现有 `--app-*` token 已随着 `data-theme='dark'` 重定义。项目域新增 token 必须是这些 token 的别名，写入共享 token 样式，而不是将同一颜色复制到每个页面。
3. 文本、边框、背景和焦点分别用 token 表达；不能只替换“状态色”而保留浅色的白底、灰边、蓝字等硬编码。
4. Token 名描述用途，不描述具体色相；例如 `--pm-status-blocked-*` 合法，`--pm-orange-*` 不合法。

### 2.2 建议新增的项目域语义 token

下表是对现有 token 的**增量别名规范**，不是已经存在的实现。实现时应先在共享样式定义这些别名，并在浅/深主题下从 `--app-*` 自动取值。

| 用途 | token | 基础 token/规则 |
|---|---|---|
| 容器 | `--pm-surface`、`--pm-surface-subtle`、`--pm-border`、`--pm-text`、`--pm-muted` | 分别别名 `--app-card`、`--app-bg-subtle`、`--app-border`、`--app-text`、`--app-muted`。 |
| 选择与焦点 | `--pm-selected-bg`、`--pm-hover-bg`、`--pm-focus-ring` | 分别别名 `--app-table-active-bg`、`--app-table-hover-bg`、`--app-focus-ring`。焦点不得只靠背景变化。 |
| 主操作与进度 | `--pm-accent`、`--pm-accent-soft` | 分别别名 `--app-accent`、`--app-accent-soft`。 |
| 成功/完成 | `--pm-success`、`--pm-success-soft` | 主值别名 `--app-success`；soft 背景必须由该主题下可读的低对比表面生成。 |
| 警告/风险 | `--pm-warning`、`--pm-warning-soft` | 主值别名 `--app-warning`；用于风险、待确认，不代替阻塞文本。 |
| 危险/逾期 | `--pm-danger`、`--pm-danger-soft` | 主值别名 `--app-danger`；用于错误、逾期、不可逆操作和严重风险。 |
| 中性/已取消 | `--pm-neutral`、`--pm-neutral-soft` | 以 `--app-muted`、`--app-gray-*` 推导；不得暗示已完成。 |

### 2.3 状态映射

每一个状态组件采用“**图标 + 本地化文本 + token + 可读属性**”四件套。状态不只依靠颜色；颜色是辅助扫描线索，不是唯一信息源。

| 语义 | 标签示例 | 视觉 token | 必需的非颜色表达 |
|---|---|---|---|
| Todo | `待开始` | `--pm-neutral-*` | 未完成图标与完整文字。 |
| InProgress | `进行中` | `--pm-accent-*` | 进行中图标、文字和进度百分比。 |
| Blocked | `已阻塞` | `--pm-warning-*` | 阻塞图标、文字、`blockedByCount` 或原因；不能只显示黄点。 |
| Done | `已完成` | `--pm-success-*` | 完成图标、文字、`100%` 或完成时间。 |
| Cancelled | `已取消` | `--pm-neutral-*` | 取消图标、文字和不可继续操作语义。 |
| 逾期 | `已逾期 N 天` | `--pm-danger-*` | 相对时长、精确截止日期和可筛选标记；不改变任务状态列。 |
| OnTrack / AtRisk / OffTrack | `正常 / 有风险 / 已偏离` | 成功 / 警告 / 危险 token | 风险文字、图标与计算来源（日期、进度或依赖）。 |
| 里程碑 | `未开始 / 进行中 / 已完成` | 中性 / 主色 / 成功 token | 菱形或专用图形加文字和日期，不能只依赖形状或颜色。 |
| 权限与冲突 | `无权限 / 存在版本冲突` | 危险或警告 token | 明确标题、原因、下一步操作和 `role='alert'`（适用时）。 |

标签、优先级和人员头像同样不得单靠颜色：优先级必须有 `低/中/高/紧急` 文字或可访问名称；项目标签需保留名称；头像必须有用户显示名或等效 `aria-label`。

## 3. 主题规范

### 3.1 浅色与深色

- 所有 ProjectManagement 容器、表格、任务卡、看板列、甘特日期条、日历单元、表单和反馈优先使用第 2 节 token；不得将 `bg-white`、`border-gray-*`、`text-blue-*` 作为领域语义。
- 深色主题下，图表、进度条和状态徽标必须同时校验文字、边界、轨道和悬停/选择态的可读性。只验证主色本身不够。
- 文本与其背景的正常正文对比度至少为 4.5:1；仅用于大字号标题的文字可按 3:1 评估。边框不能是唯一的焦点或错误提示。
- `brand`、`kingdee`、`yonyou` 是现有主题变体，项目域仅消费语义别名，因此不需要为每个页面写品牌特例。

### 3.2 跟随系统主题是待实现差距

PRD 和 `HAO-431` 要求“跟随系统主题”，但当前 `ThemeMode` 只有 `light | dark | brand | kingdee | yonyou`；`ThemeProvider` 仅将所选值写入 `documentElement.dataset.theme`，没有 `system` 模式或 `prefers-color-scheme` 监听。

因此在实现前必须满足以下规则：

1. 将 `system` 作为明确的用户偏好值，而不是把当前计算出的 light/dark 值持久化为偏好。
2. `system` 解析为浏览器 `prefers-color-scheme` 的 light/dark，监听系统改变并更新生效主题；用户显式选择 light/dark 时停止跟随。
3. `data-theme` 继续只存放解析后的实际主题值，或另行增加不影响现有选择器的偏好标记；不得破坏现有 CSS 选择器。
4. 项目管理设置、任务视图、通知浮层和弹窗都必须在主题切换后立即使用同一 token，不保留局部浅色覆盖。

该扩展由 `HAO-433` 定义 token 与验收口径，具体全页面主题/交互验证归入 `HAO-431`、`HAO-434`、`HAO-528`。

## 4. 组件使用规范

| 场景 | 必须复用/扩展 | 视觉与状态规则 | 后续交付 |
|---|---|---|---|
| 页面、标题、导航、工具栏 | `ResponsivePage`、`ResponsivePageHeader`、`ResponsiveToolbar` | 标题、描述、主操作和溢出操作保持稳定层级；加载/空/错误/403 由 `ProjectManagementPageStateView`/`PageStateShell` 表达。 | `HAO-431`、`HAO-434`、`HAO-461` |
| 搜索、筛选、保存视图 | `AdaptiveSearchForm`、`DataTableQueryPanel`、现有 `SavedViewManager` | 筛选可见、可清除、可分享；筛选结果数和当前视图名称用文字表达，不能只改变按钮颜色。 | `HAO-462`、`HAO-504` |
| 表格与任务树/行 | `DataTable`、列设置、虚拟化和响应式优先级 | 状态/优先级/日期/阻塞使用统一徽标；树缩进只表达层级，父/子/选中需有文字、焦点和 `aria-expanded`/行操作语义；窄屏保留主要操作并提供列设置或详情入口。 | `HAO-462`、`HAO-464`、`HAO-436` |
| 任务卡 | 项目域 `TaskCard` 应从当前 `TaskWorkspaceProjection` 的局部实现收敛为可复用复合组件 | 固定信息顺序：编码、标题、状态、进度、负责人/参与人、日期、阻塞/逾期；选中、拖动、禁用、加载和错误都有 token 化状态。 | `HAO-462`、`HAO-468` |
| 看板与泳道 | 共享 `TaskCard` + 看板容器，不复制任务语义 | 列标题显示状态名和数量；逾期仅角标/筛选；空列显示文字空状态；拖动时有插入位置、可投放/不可投放和服务器拒绝后的恢复反馈。 | `HAO-468`、`HAO-470`、`HAO-472` |
| 甘特 | 项目域时间计划组件，复用 token 和任务状态语义 | 父任务汇总条、进度、里程碑、依赖和关键路径各有文本/图例；可视窗口、缩放、日期拖动、边界和冲突提示不能只靠线条颜色。 | `HAO-473`、`HAO-474`、`HAO-475`、`HAO-476` |
| 日历 | 项目域月/周组件，复用日期、状态和详情入口 | 日期格提供日期标题、任务数、溢出计数和键盘详情入口；任务用状态/逾期标签加标题，日期拖动失败提供明确反馈。 | `HAO-473` |
| 进度、里程碑、风险 | 小型项目域展示组件，可由共享 token 组成 | 进度条必须同时显示数值；里程碑显示名称/日期/状态；风险显示文字、来源和图标。深色下轨道与填充均须可辨认。 | `HAO-425`、`HAO-442`、`HAO-459` |
| 表单、详情、危险确认 | `ModalForm`、`ResponsiveModal`、`ResponsiveFormGrid`、`useConfirm` | 字段有 label、必填/帮助/错误文本；保存中禁用重复提交但保留说明；危险操作明确对象、后果、确认词和回滚/不可逆信息。 | `HAO-434`、`HAO-455`、`HAO-461` |
| 页面/操作反馈 | `PageStateShell`、`useMessage`、`useConfirm` | Loading、Empty、Error、403、404、409、网络断开、后台作业和重试必须有标题、原因、恢复操作；禁止 `window.alert/confirm`。 | `HAO-431`、`HAO-434`、`HAO-528` |
| 通知、活动、附件、评论 | 现有项目域面板 + 共享反馈/权限组件 | 已读/未读、上传/失败、评论/提及、提醒/失败均有文字和时间/原因；链接、按钮和图标在深色下可见。 | `HAO-479`、`HAO-484`、`HAO-490`–`HAO-494` |

### 4.1 统一交互状态

所有可交互组件至少评审以下状态；未适用时必须在原型或 Case 说明原因：

| 状态 | 最低视觉/无障碍要求 |
|---|---|
| default / hover / active | 不改变信息层级；hover 不能是桌面端唯一提示。 |
| focus-visible | 使用 `--pm-focus-ring` 的可见焦点轮廓；键盘焦点顺序与视觉顺序一致。 |
| selected | 使用 `--pm-selected-bg` 加选择控件、标题或 `aria-selected`，不能只换底色。 |
| disabled / unauthorized | 说明为什么不可用及可请求的权限；后端仍负责拒绝。 |
| loading | 保留布局并说明正在加载什么；长任务提供进度或可取消语义（适用时）。 |
| empty | 给出当前范围（项目/筛选/日期）与下一步，而不是空白容器。 |
| error / forbidden / conflict | 提供原因、重试/返回/刷新/比较等安全动作；错误由文字和语义角色表达。 |
| dragging / drop target | 显示抓取对象、合法/非法目标和插入位置；松开后的服务端拒绝要恢复并提示。 |

## 5. 响应式、键盘与无障碍约束

### 5.1 响应式

现有断点为 `sm=768`、`md=1024`、`lg=1366`、`xl=1600`。ProjectManagement 以 1024px 与平板横屏为不可降级验收点：

| 宽度/密度 | 规则 |
|---|---|
| `>= 1366`（standard） | 可显示多列工具栏、完整表格、五列看板和并列详情；不因空间充足复制状态或操作。 |
| `1024–1365`（compact） | 主操作、当前筛选、状态/截止日期/阻塞和详情入口必须保留；次级操作进入现有 Toolbar 的更多菜单；看板/甘特/日历可横向滚动或缩减信息，但不能移除关键命令。 |
| `768–1023` | 使用现有 drawer 模态策略；工具栏纵向重排；表格依靠 `responsivePriority`、列设置和详情而非压缩到不可读。 |
| `< 768` | 全屏模态、单列表单和聚焦任务详情；复杂计划视图提供受控滚动/简化阅读，但写操作和反馈不能消失。 |

页面不得自行设定与 `ERP_BREAKPOINTS` 冲突的断点。项目域密度使用现有 `--erp-*`/`getResponsiveTokens`，避免同一页面同时使用紧凑表格与舒适卡片的无规则混搭。

### 5.2 键盘、语义与辅助技术

- 所有图标按钮都有可本地化的可读名称；纯装饰图标标为 `aria-hidden`。
- 任务树使用可展开语义、可见焦点、Enter/Space 激活和键盘可达的行操作；树缩进不是唯一层级信息。
- 原生 HTML 拖放不足以满足键盘操作。拖动功能必须有键盘等效操作（移动前/后/成为子任务等）及后端拒绝后的公告；当前 `TaskWorkspaceProjection` 仅有 `draggable`/DragEvent，因此该项未完成。
- 弹窗需规定初始焦点、焦点陷阱、Esc 只关闭最上层、恢复触发元素焦点；危险确认不能只由 Esc/颜色区分。
- 表单控件有持久 label、错误文本与 `aria-describedby`；`disabled` 不得隐藏失败原因。
- 动态加载、冲突、上传和后台任务结果在适当场景使用 `role='status'` 或 `role='alert'`，但不应对普通状态变化过度播报。
- Chrome、Edge、Firefox 的拖放、日期和文件能力由 `HAO-436` 记录差异并纳入 `HAO-528` 验收。

## 6. 当前差距与追踪

| 已定位差距 | 代码/事实证据 | 处理要求 | 后续 Case |
|---|---|---|---|
| 跟随系统主题不存在 | `ThemeMode` 不含 `system`；主题 store 仅持久化固定值 | 按 3.2 增加系统偏好与监听，并验证局部页面不残留旧主题。 | `HAO-433`、`HAO-431`、`HAO-528` |
| 项目域使用浅色硬编码颜色 | `TaskWorkspaceProjection`、工具栏、提醒、通知、成员/里程碑页面存在 `bg-blue-*`、`text-gray-*`、`border-gray-*` 等 | 收敛为共享/项目语义 token，先覆盖任务工作区、通知、表单与里程碑进度，再执行深色回归。 | `HAO-433`、`HAO-434`、`HAO-462`、`HAO-468`、`HAO-473` |
| 没有统一任务状态/风险/优先级复合组件 | 任务投影当前直接输出英文状态、百分比和文本，缺少语义 badge/图例/风险组件 | 建立小型项目域展示组件，复用 token，保留 HAO-429 术语和非颜色表达。 | `HAO-433`、`HAO-462`、`HAO-468`、`HAO-473` |
| 拖放仅有鼠标/指针路径 | `TaskWorkspaceProjection` 使用 `draggable` 与 DragEvent，未定位键盘等效操作 | 补键盘移动、焦点和实时公告；拖动拒绝要恢复 UI 并给出原因。 | `HAO-436`、`HAO-468`、`HAO-472` |
| 复杂视图的 1024px 规则未完成验收 | 看板、甘特、日历尚处后续主题，现有实现是基础投影 | 以第 5.1 节为高保真原型与 E2E 共同口径，不删主操作。 | `HAO-434`、`HAO-436`、`HAO-468`、`HAO-473` |
| 页面状态未覆盖全部高风险/冲突语义 | `ProjectManagementPageStateView` 已覆盖 loading/empty/error/forbidden；未覆盖 404、409、未保存、风险确认、后台作业 | 扩展共享状态的项目域组合使用与原型，不能用 Toast 替代需要决策的页面状态。 | `HAO-431`、`HAO-434`、`HAO-461` |
| 深色与无障碍回归证据缺失 | 能力矩阵将跨浏览器/无障碍/主题验收列为缺口 | 为本规范的 token、状态、键盘、对比度和五类视图建立浏览器/E2E 验收。 | `HAO-436`、`HAO-528` |

## 7. 设计评审与验收清单

新页面或修改 ProjectManagement 组件前，设计/开发评审必须确认：

1. 已说明所关联的 `PM-PRD-*`、Linear Case 和使用的共享组件/token；没有新建脱离共享层的样式。
2. 状态、优先级、风险、逾期和里程碑均以文字/图标/语义补充颜色。
3. 浅色、深色和（实现后）跟随系统三种偏好下，表面、文字、边界、进度和反馈均可读。
4. Loading、Empty、Error、403、404、409、拖动拒绝、未保存和危险确认已在原型或实现中明确。
5. 1024px、平板横屏与键盘路径不丢失主操作；图标按钮、焦点、弹窗和拖放替代路径符合第 5 节。
6. 在关联 Case 留下实际截图/浏览器验证和 E2E 证据；视觉文档本身不能作为深色、无障碍或跨浏览器已通过的证明。
