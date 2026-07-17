# Phase 0：前端旧设计器边界基线

## 审计结论

本次审计以当前分支工作树、前端源码引用和现有测试为准。旧目录
`frontend/AsterERP.Web/src/pages/application-console/development-center/full-designer/`
在整改前包含 147 个文件；其中 1 个文件只保留为迁移输入并重命名到
`low-code-studio/migration/LegacyDocumentInput.ts`，其余 146 个文件已物理删除。

旧目录没有剩余文件，也没有前端生产或测试调用方。旧设计器的 UI、registry、renderer、
inspector、parser、compiler 分支和绑定/表达式测试不再作为最新实现的输入。

## 保留的迁移输入

`LegacyDocumentInput.ts` 只描述历史持久化 JSON 的输入字段。它不声明编辑器会话、选择、
视口、历史、组件 registry、renderer、inspector 或 runtime artifact。唯一调用方是
`migration/CurrentDocumentMigration.ts`。

迁移链路为：

`LegacyDocumentInput -> CurrentDocumentMigration -> DesignerDocument + DesignerEditorSession + RuntimeArtifact`

迁移是一次性的，不双写，不在运行时读取旧文档，不保留长期兼容分支。

## 当前唯一契约

- 文档：`low-code-studio/document/DesignerDocument.ts`
- 编辑会话：`low-code-studio/document/DesignerEditorSession.ts`
- 命令：`low-code-studio/commands/DesignerCommandBus.ts`
- Manifest：`low-code-studio/components/ComponentManifest.ts`
- 编辑器 registry：`low-code-studio/components/ComponentRegistry.ts`
- Runtime registry：`runtime-kernel/RuntimeComponentRegistry.ts`
- Runtime 入口：`runtime-kernel/RuntimeKernel.ts`
- Runtime parity：`shared/runtime/designer-document/runtimeRendererParity.test.tsx`

## 验收 Case

| Case | 验证 | 判定 |
| --- | --- | --- |
| F0-01 | `rg --files .../full-designer` 无输出 | 旧目录物理为空才 Pass |
| F0-02 | 前端源码 `rg` 无 `full-designer`、`elementRegistry` 或旧 renderer 引用 | 任一生产/测试引用均 Fail |
| F0-03 | migration 测试验证选中状态/视口被提取、资源引用稳定 | 只产生最新文档、会话和 artifact 才 Pass |
| F0-04 | parity 测试验证 latest Manifest registry 与 Runtime registry | 未知或未注册类型必须 Fail |
| F0-05 | typecheck、全量测试、lint | 任一命令失败不得提交 |

## 已执行证据

- `npm run typecheck`：Pass
- `npm test -- --run`：69 个测试文件、266 个测试 Pass
- `npm run lint`：Pass
- `full-designer` 文件扫描：空
- `LegacyDocumentInput` 调用扫描：仅 migration 目录

后端、Data Studio、PageStudioHost 和 runtime-kernel 不属于本次前端旧边界施工范围，未修改。

## Phase 0/1 migration and integrity closure boundary

The one-time migration now returns `runtimeArtifactDraft`, not `RuntimeArtifact`.
The draft is explicitly marked `kind: "runtime-artifact-draft"` and contains no
`artifactHash`, `signature`, or compiler metadata. Only the latest compiler may
turn the migrated `DesignerDocument` into a signed runtime artifact.

`CurrentDocumentMigration` rejects invalid migration options, unreachable or
broken parent/children graphs, and component types absent from the latest
Manifest registry before returning any draft. The malformed-source cases are
covered by `CurrentDocumentMigration.test.ts`.

The cross-stack canonical document hash fixture is
`fixtures/designer-document-hash-fixture.json`. It requires the `sha256:`
prefix and excludes only the top-level `documentHash`; nested values with the
same property name remain part of the hash. Frontend and backend tests consume
the same fixture.

The stale `wwwroot/assets/elementRegistry-rD7WR26B.js` bundle and its frontend
manifest registration were removed from the working release boundary. The
remaining release gate is a clean rebuilt `wwwroot` scan after the approved
frontend publish process.
