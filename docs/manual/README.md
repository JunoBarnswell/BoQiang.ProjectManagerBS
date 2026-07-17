# AsterERP 运维手册维护说明

本目录是 AsterERP 运维快速使用与故障处理手册的维护区。这里可以保留维护人员需要的内部信息，最终给用户的 Word/PDF 只生成运维正文，不生成本 README。

## 维护目标

- 用户手册面向运维人员、业务管理员和 MES 管理员。
- 正文只讲操作、截图、成功标志和故障处理，不讲技术实现。
- 内部覆盖数据继续保存在 `docs/manual/inventory`，用于确认菜单和截图是否遗漏。
- 实际菜单名称中如包含 ABP 基础设施、ApplicationRuntime、ApplicationDraftPreview、权限码、路由、数据库路径等维护信息，只能留在维护文件或清单里，不能进入最终用户手册正文。

## 正文写法

每个页面按固定结构维护：

1. 这个功能做什么。
2. 谁会用。
3. 从哪里进入。
4. 点哪里、填什么、保存后看哪里。
5. 截图编号说明。
6. 成功后页面有什么变化。
7. 遇到问题先查什么、怎么处理、处理不了找谁。

## 截图规范

每张截图保存两份：

- 原始截图：`*-raw.png`
- 标注截图：`*-annotated.png`

正文只能引用标注截图。图下注释必须解释编号含义，例如：

- 1：左侧菜单，用于进入对应功能。
- 2：操作区，用于查询、新增、保存或刷新。
- 3：结果区，用于查看列表、状态、提示和异常信息。

## 生成命令

```powershell
./scripts/manual/export-menu-inventory.ps1
./scripts/manual/annotate-screenshots.ps1
./scripts/manual/verify-manual-coverage.ps1
./scripts/manual/generate-manual.ps1
```

严格截图覆盖检查：

```powershell
./scripts/manual/verify-manual-coverage.ps1 -StrictScreenshots
```

## 交付物

- Word：`output/manual/astererp-platform-mes-user-manual.docx`
- PDF：`output/pdf/astererp-platform-mes-user-manual.pdf`
- PDF 渲染预览：`tmp/pdfs/astererp-platform-mes-user-manual-*.png`
