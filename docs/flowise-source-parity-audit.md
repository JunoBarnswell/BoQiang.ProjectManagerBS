# Flowise Source Parity Audit

Date: 2026-06-24

## Locked Rules

- Flowise is a dedicated workflow domain.
- Flowise must not share approval workflow/BPMN protocol.
- Flowise protocol source of truth is Flowise native `ChatFlow.flowData`.
- No compatibility projection is allowed between Flowise `flowData` and custom AsterERP node/edge DTOs.
- AsterERP shell, auth, permissions, tenant/app/owner data filters remain active.

## Current Parity Matrix

| Area | Flowise Source Contract | Current AsterERP State | Status |
|---|---|---|---|
| Chatflow backend protocol | `ChatFlow` fields and `flowData` string | Added `FlowiseChatFlowEntity`, `FlowiseChatflowDto`, native CRUD service/controller | Pass |
| Agentflow backend protocol | Same `ChatFlow`, `type=AGENTFLOW` | Added same native path with `AGENTFLOW` split permissions | Pass |
| Canvas save protocol | Save native `{ nodes, edges, viewport }` flowData | `FlowiseCanvasService` now accepts/returns only `FlowData` string and writes `FlowiseChatFlowEntity.FlowData` | Pass |
| Compatibility projection | Not allowed | Removed canvas node/edge DTOs, split canvas entities, split canvas tables, indexes, data-filter registrations, and legacy `CanvasJson/FlowDataJson` API aliases from the main code path | Pass |
| Execution graph read | Parse native `flowData` | Execution now parses `FlowiseChatFlowEntity.FlowData`, emits Flowise SSE `event/data` prediction stream events, and emits Flowise chat-message protocol fields for executed data, agent reasoning, used tools, artifacts, and source documents | Partial |
| Execution runtime | Flowise runtime node-by-node semantics | Uses AsterERP AI kernel for final chat response and records Flowise runtime metadata; not full Flowise runtime | Fail |
| Chatflows page | Flowise source list/card actions | New native field page for Chatflow/Agentflow; Add New opens empty canvas, duplicate/import load `duplicatedFlowData`, unsaved canvas save creates native Chatflow/Agentflow, export emits sanitized Flowise `{ nodes, edges }`, node preview images are parsed from `flowData.nodes[].data.name` and rendered through Flowise-native `/api/v1/node-icon/{name}`, source-like Options menu actions update native Flowise fields, dedicated `ItemCard`/`FlowListTable`/`FlowListMenu` component files now carry the list/card/menu structure, Save As Template now persists through a template-export endpoint, `@mui/material`, `@mui/icons-material`, and `@emotion/*` are now installed, `FlowListMenu` now uses MUI `Button`, styled `Menu`, `MenuItem`, `Divider`, and source-aligned MUI icons with Flowise source-style anchor origin, transform origin, menu-list padding, icon sizing, active background, and permission-gated menu items, list actions now use distinct Flowise-style action permissions for update, duplicate, export, template export, configuration, allowed domains, and delete on both frontend and backend endpoints, `SaveChatflowDialog`/`TagDialog` now use source-like MUI dialog/input/chip semantics, `SpeechToTextDialog` now uses Flowise provider-driven form semantics with native provider keys, `credentialId`, active-provider JSON save behavior, and Flowise source provider image assets rendered by static imports, `AllowedDomainsDialog` now uses source-like MUI dynamic domain rows with add/remove icon buttons plus an error-message input, `ChatFeedbackDialog` now uses a source-like MUI dialog plus switch control, `StarterPromptsDialog` now uses source-like MUI dynamic prompt rows with add/remove icon buttons plus an info banner, and `ExportAsTemplateDialog` now uses source-like MUI form/chip semantics; remaining source dialog internals and screenshot parity remain incomplete | Partial |
| Generic resource page removal | No generic CRUD for Flowise pages | Deleted `FlowiseResourcePage`, `FlowiseFlowListPage`, `FlowiseResourceEditor`, `FlowiseOverviewStrip`, and frontend `FlowiseResourceCollectionPage`; Flowise Studio no longer imports `CrudPage`, `DataTable`, `useCrudResource`, or `FlowiseResourceCollectionPage`; Tools/Credentials/Variables/API Keys now call dedicated configuration resource APIs | Pass |
| Strong typed configuration resources | Flowise Credentials, Variables, API Keys, Tools have dedicated models and APIs | Added dedicated entities, services, controller endpoints, schema, indexes, data filters, and frontend API wiring for Tools/Credentials/Variables/API Keys. API Key plaintext is only returned on create/rotation; Credential/Variable reveal requires `flowise:secret:reveal` and writes audit logs | Pass |
| Strong typed assistant/marketplace/document/evaluation roots | Flowise non-management resource roots have dedicated models and APIs | Added dedicated entities, services, root CRUD endpoints, schema, indexes, data filters, and frontend API wiring for Assistants, Marketplaces, Document Stores, Datasets, Evaluators, and Evaluations. Document Store detail/upsert and Evaluation result flows now load strong typed roots instead of `FlowiseResourceEntity` | Pass |
| Strong typed management/log roots | Flowise management pages have dedicated models and APIs | Added dedicated entities, service, controller endpoints, schema, indexes, data filters, and frontend API wiring for SSO Config, Roles, Users, Login Activity, Logs, Account Settings, Overview, Workspaces, and Shared Workspaces. Deleted `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, generic `{resourceType}` routes, `flowiseStudioApi.resources.*`, and `createFlowiseNativeCollectionApi` | Pass |
| Canvas UI | Flowise source canvas/header/dialogs | Canvas no longer wrapped in `CrudPage`; node toolbar actions, editable Sticky Note, Details/Additional Params/Info inspector, edge delete dirty tracking, before-unload dirty guard, AddNodes fuzzy search/category tabs/expand state, richer ConfigInput controls, CanvasHeader API Code/Configuration/Template/Messages/Leads/Schedule/Webhook/Share/Upsert/Upsert History dialogs, ChatPopUp floating/expanded chat, real Messages/Leads record queries, Configuration native Chatflow config save, shared workspace save, document-store Upsert History, feedback, lead capture, clear chat, source docs, persisted chatId, Flowise SSE token streaming, abort/stop, uploads/fileUploads attachments, agent reasoning cards, executed data cards, used tools, and artifacts are wired; still not full Flowise source UI | Fail |
| i18n | No bare strings | Current `features/flowise-studio` code has no matched bare English UI strings; full Flowise source component set is not migrated yet | Partial |

## Files Compared

- Flowise source baseline:
  - `C:/Users/kuo13/Downloads/Flowise-main/Flowise-main/packages/server/src/database/entities/ChatFlow.ts`
  - `C:/Users/kuo13/Downloads/Flowise-main/Flowise-main/packages/server/src/Interface.ts`
  - `C:/Users/kuo13/Downloads/Flowise-main/Flowise-main/packages/ui/src/views/chatflows/index.jsx`
  - `C:/Users/kuo13/Downloads/Flowise-main/Flowise-main/packages/ui/src/views/canvas/index.jsx`
- AsterERP changed owners:
  - `backend/AsterERP.Api/Modules/Ai/Flowise/FlowiseChatFlowEntity.cs`
  - `backend/AsterERP.Api/Application/Ai/Flowise/FlowiseChatflowService.cs`
  - `backend/AsterERP.Api/Application/Ai/Flowise/FlowiseCanvasService.cs`
  - `backend/AsterERP.Api/Application/Ai/Flowise/FlowiseExecutionService.cs`
  - `frontend/AsterERP.Web/src/features/flowise-studio/native/views/chatflows/index.tsx`

## Blocking Gaps

- Full Flowise UI source migration is not complete.
- Full Flowise canvas source dialogs and interactions are not complete.
- Full Flowise runtime execution semantics are not complete.
- Backend `FlowiseResourceService/FlowiseResourceEntity` and generic `{resourceType}` routes have been removed from source code. Remaining backend gaps are now source-runtime parity and deeper domain semantics, not generic resource-table persistence.
- Native menu pages still need deeper source-level one-to-one logic beyond the current card/list/dialog structure.
- Full i18n cannot be marked complete until every Flowise source page/component/dialog has been migrated and scanned.

## Latest Progress Refresh

Updated on 2026-06-25 after the Chatflow built-in MCP Server configuration slice, MCP runtime JSON-RPC slice, Tools-page Custom MCP Server resource slice, Custom MCP source-alignment frontend slice, Speech To Text provider-form slice, Speech To Text provider image asset parity slice, Allowed Domains dynamic MUI form slice, Chat Feedback switch dialog slice, Starter Prompts dynamic MUI form slice, and Export As Template MUI form slice. Source parity status remains below 100% because remaining source-dialog gaps outside `native/ui-component/dialog`, Flowise node-by-node runtime, full page/canvas source UI, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and final screenshot/browser matrix are still incomplete.

### Current Implementation Refresh Webhook Listener Runtime

Updated on 2026-06-25 after adding an authenticated native Flowise Webhook Listener registration, stream, trigger, and drawer event path.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Listener registration | `AiFlowiseWebhookListenerController` exposes `/api/v1/webhook-listener/{chatflowId}` and returns a listener id backed by `FlowiseWebhookListenerService`. | Pass |
| Listener stream | `/api/v1/webhook-listener/{chatflowId}/stream/{listenerId}` streams Flowise-style SSE `event`/`data` payloads from an in-memory listener channel with auth and workspace headers preserved by the frontend client. | Pass |
| Trigger path | `/api/v1/webhook/{chatflowId}` validates the configured webhook secret and executes native `flowData` through `IFlowiseExecutionService`, forwarding runtime events to the active listener. | Pass |
| Drawer integration | `WebhookListenerDrawer.tsx` registers/unregisters the listener, displays listener id, and renders incoming event payloads instead of showing only a static endpoint block. | Pass |
| Contract boundary | This slice uses only Flowise `FlowData`; it introduces no WorkflowModel/BPMN reuse, no canvas node/edge projection, no Flowise Node runtime, no generic CRUD/table path, and no bridge business layer. | Pass |
| Remaining source mismatch | Exact Flowise process-flow cards, final response/error rendering sections, schedule history runtime, full node-specific runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Schedule/Webhook Drawers

Updated on 2026-06-25 after extending the source-named Schedule History and Webhook Listener drawers with source-like drawer mechanics. This improves interaction parity while keeping the remaining runtime gaps explicit.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Schedule resize behavior | `ScheduleHistoryDrawer.tsx` now implements source-style left-edge drag resizing with min/default/max bounds and document-level mouse cleanup. | Pass |
| Schedule header structure | The drawer now separates title, status/type summary, disabled refresh affordance, and no-runs content, matching the source layout direction without inventing schedule logs. | Pass |
| Webhook resize and persistent behavior | `WebhookListenerDrawer.tsx` now implements source-style persistent right drawer placement, pointer-event isolation, left-edge drag resizing, and maximize/restore width toggle. | Pass |
| Webhook endpoint block | The drawer now renders the source-style method chip, endpoint row, copy action, cURL collapse, and cURL copy state. | Pass |
| Contract boundary | This slice introduces no hidden API calls, backend runtime, Flowise protocol projection, BPMN, fake logs/events, generic CRUD/table path, or bridge business layer. | Pass |
| Remaining source mismatch | Real schedule status/log APIs, delete/detail table behavior, real webhook listener registration/SSE stream, process-flow cards, response/error runtime sections, exact source styling, authenticated API smoke, workspace/permission checks, and browser screenshot parity remain incomplete. | Fail |

### Previous Implementation Refresh Schedule/Webhook Drawers

Updated on 2026-06-25 after moving the CanvasHeader Schedule and Webhook views from generic inline modal sections into Flowise source-named drawer components. This improves the source directory and component-boundary match for `views/schedule/ScheduleHistoryDrawer.jsx` and `views/webhooklistener/WebhookListenerDrawer.jsx`, but it does not close full drawer/runtime parity because real schedule run history and webhook listener streams are still absent.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Schedule drawer boundary | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/schedule/ScheduleHistoryDrawer.tsx` exists as a typed page-only drawer component with no API calls. | Pass |
| Webhook listener drawer boundary | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/webhooklistener/WebhookListenerDrawer.tsx` exists as a typed page-only drawer component with no API calls. | Pass |
| CanvasHeader routing | `FlowiseCanvasHeaderDialogs.tsx` now routes Schedule/Webhook to the source-named drawers and no longer renders those two states through the generic dialog section. | Pass |
| Start node trigger parsing | Schedule summary reads `startInputType` from `data.inputs` or `data.config`, keeping the drawer and v2 runtime FAB trigger detection aligned. | Pass |
| Contract boundary | The slice introduces no backend runtime, Flowise protocol projection, BPMN, generic CRUD/table path, fake schedule logs, fake webhook events, or bridge business layer. | Pass |
| Remaining source mismatch | Full resizable drawer internals, schedule run table/logs, webhook listener event stream, exact Flowise AppBar/FAB layout, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 Runtime FAB

Updated on 2026-06-25 after adding the source-named Agentflow v2 runtime FAB selector. This improves Flowise source behavior parity for the `isScheduleFlow ? ScheduleHistoryFAB : isWebhookFlow ? WebhookListenerFAB : ChatPopUp` branch in `views/agentflowsv2/Canvas.jsx` without claiming full drawer/runtime parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Source branch detection | `FlowiseCanvasPage` resolves the Start Agentflow node and checks `startInputType` values `scheduleInput` and `webhookTrigger`, with `data.inputs` preferred and `data.config` supported for the current AsterERP node state. | Pass |
| Source-named component | Added `native/views/agentflowsv2/AgentflowV2RuntimeFab.tsx` as a page-only UI component with explicit callbacks and no API calls. | Pass |
| Floating entry behavior | The v2 canvas now displays a runtime FAB that opens Chat Test, Schedule, or Webhook entry flows and hides the v2 validation popup while a runtime panel/dialog is open. | Pass |
| i18n coverage | `flowise.canvas.scheduleHistory` and `flowise.canvas.webhookListener` exist in zh-CN/en-US. | Pass |
| Contract boundary | The change is frontend v2 canvas orchestration only; no backend runtime, Flowise protocol projection, BPMN, generic CRUD/table path, API alias, mock, or bridge business layer was introduced. | Pass |
| Remaining source mismatch | Full `ScheduleHistoryDrawer`, full `WebhookListenerDrawer`, exact Flowise FAB styling/placement inside the original AppBar canvas layout, full validation popup parity, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 Canvas Controls

Updated on 2026-06-25 after aligning v2-only snapping and background controls with Flowise source Tabler glyphs and ReactFlow control-button styling. This improves Flowise source behavior parity for `snapToGrid`, `snapGrid`, centered horizontal controls, snapping/background toggling, and source `IconMagnetFilled` / `IconMagnetOff` / `IconArtboard` / `IconArtboardOff` rendering without claiming exact AppBar/FAB visual parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Snapping behavior | `FlowiseCanvasPage` applies `snapGrid={[25, 25]}` and v2-only `snapToGrid` controlled by `agentV2SnappingEnabled`. | Pass |
| Background behavior | `FlowiseCanvasPage` exposes a v2-only background toggle while preserving default background rendering for non-v2 canvases. | Pass |
| Controls integration | ReactFlow `Controls` contains translated v2-only buttons for snapping and background state, source-like horizontal centering, `react-flow__controls-button react-flow__controls-interactive` classes, and direct Tabler icon imports. | Pass |
| Dependency parity | `frontend/AsterERP.Web/package.json` now includes `@tabler/icons-react` so Flowise-native glyphs are imported from the same icon family used by the source UI. | Pass |
| i18n coverage | `flowise.canvas.toggleSnapping` and `flowise.canvas.toggleBackground` exist in zh-CN/en-US. | Pass |
| Contract boundary | The change is frontend interaction state only; no backend runtime, Flowise protocol projection, BPMN, generic CRUD/table path, API alias, mock, or bridge business layer was introduced. | Pass |
| Remaining source mismatch | Exact Flowise AppBar/FAB layout, validation popup, schedule/webhook controls, full source `Canvas`/`MarketplaceCanvas`, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 Mode Contract

Updated on 2026-06-25 after giving the source-named v2 canvas entries explicit mode ownership. This improves the source route/container boundary without claiming full v2 canvas implementation parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Shared canvas prop | `FlowiseCanvasPage` accepts `forcedMode?: FlowiseCanvasMode` and falls back to URL mode inference when it is omitted. | Pass |
| Agentflow v2 container | `native/views/agentflowsv2/Canvas.tsx` forces `agentflow-v2`. | Pass |
| Marketplace v2 container | `native/views/agentflowsv2/MarketplaceCanvas.tsx` forces `marketplace-v2`. | Pass |
| Contract boundary | Existing Flowise `flowData` behavior, permissions, and route behavior are preserved; no backend runtime, Flowise protocol projection, BPMN, generic CRUD/table path, API alias, mock, or bridge business layer was introduced. | Pass |
| Remaining source mismatch | The entries still delegate to the shared canvas implementation; full Flowise source `Canvas`, `MarketplaceCanvas`, toolbar/FAB behavior, validation popup, schedule/webhook controls, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 Style Entry

Updated on 2026-06-25 after adding `native/views/agentflowsv2/index.css` and importing it from both source-named v2 canvas entries. This improves source stylesheet parity without claiming full visual parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Source style entry | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/index.css` exists and owns v2 canvas edge/control/wrapper class styling. | Pass |
| Canvas import | `Canvas.tsx` imports `./index.css`. | Pass |
| Marketplace import | `MarketplaceCanvas.tsx` imports `./index.css`. | Pass |
| Contract boundary | The change is style-only for v2 canvas entries; it introduces no backend runtime, Flowise protocol projection, BPMN, generic CRUD/table path, API alias, mock, or bridge business layer. | Pass |
| Remaining source mismatch | The style entry does not yet prove pixel parity; full Flowise source `Canvas`, `MarketplaceCanvas`, toolbar/FAB behavior, validation popup, schedule/webhook controls, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 Canvas Entries

Updated on 2026-06-25 after adding source-named Agentflow v2 canvas route entries under `native/views/agentflowsv2`. This improves route/source directory parity without claiming full Flowise source canvas internals.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Agentflow Canvas entry | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/Canvas.tsx` exists and is exported as `FlowiseAgentflowV2CanvasPage`. | Pass |
| Marketplace Canvas entry | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/MarketplaceCanvas.tsx` exists and is exported as `FlowiseMarketplaceV2CanvasPage`. | Pass |
| Route integration | `workspaceRoutes.full.tsx` routes `/flowise/v2/agentcanvas*` and `/flowise/v2/marketplace/:resourceId` through the source-named v2 entries. | Pass |
| Contract boundary | Existing route permissions and Flowise `flowData` canvas behavior are preserved; no backend runtime, Flowise protocol projection, BPMN, generic CRUD/table path, API alias, mock, or bridge business layer was introduced. | Pass |
| Remaining source mismatch | The new entries still delegate to the shared canvas implementation; full Flowise source `Canvas`, `MarketplaceCanvas`, toolbar/FAB behavior, validation popup, schedule/webhook controls, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 EditNodeDialog

Updated on 2026-06-25 after extracting selected-node edit/configuration rendering into `native/views/agentflowsv2/EditNodeDialog.tsx`. This improves Flowise source directory parity without claiming full source `EditNodeDialog` behavior parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Source-named component | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/EditNodeDialog.tsx` exists and owns selected-node configuration tabs, additional params, and node info rendering. | Pass |
| Canvas dialog integration | `FlowiseCanvasDialogs.tsx` delegates selected-node rendering to `EditNodeDialog` instead of embedding the node edit UI inline. | Pass |
| Component boundary | `EditNodeDialog` exposes typed props for `node`, `activeTab`, `onTabChange`, and `onNodeConfigChange`; it does not perform API calls or write Flowise protocol directly. | Pass |
| Contract preservation | Existing `ConfigInput`, additional params badge, read-only JSON fallback, node info display, and dirty propagation are preserved; no backend runtime, Flowise protocol projection, BPMN, generic CRUD/table path, API alias, mock, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Full Flowise portal `Dialog`, inline node-name editing, `showHideInputParams`, component-node configuration loading, full Agentflow v2 `Canvas`, validation, schedule/webhook behavior, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 ConfigInput

Updated on 2026-06-25 after moving the node configuration input component into `native/views/agentflowsv2/ConfigInput.tsx`. This improves Flowise source directory parity without claiming full source `ConfigInput` behavior parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Source-named component | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/ConfigInput.tsx` exists and owns the node parameter input UI. | Pass |
| Dialog wiring | `FlowiseCanvasDialogs.tsx` imports `ConfigInput` from the source-named Agentflow v2 directory for Details and Additional Params. | Pass |
| No forwarding shim | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseConfigInput.tsx` was removed from the canvas directory. | Pass |
| Contract boundary | Existing typed param/value/change contracts and local input behavior are preserved; no backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Full Flowise `ConfigInput` accordion/component-node data loading, full Agentflow v2 `Canvas`, `EditNodeDialog`, validation, schedule/webhook behavior, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 StickyNote

Updated on 2026-06-25 after moving the Sticky Note node into `native/views/agentflowsv2/StickyNote.tsx`. This improves Flowise source directory parity without claiming full Agentflow v2 canvas parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Source-named component | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/StickyNote.tsx` exists and owns the sticky note node UI. | Pass |
| Canvas wiring | `FlowiseCanvasPage.tsx` imports `StickyNote` from the source-named Agentflow v2 directory for the `flowiseStickyNote` node type. | Pass |
| No forwarding shim | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseStickyNote.tsx` was removed from the canvas directory. | Pass |
| Contract boundary | Existing sticky note text update callbacks, CSS classes, and ReactFlow node type key are preserved; no backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Full Flowise Agentflow v2 `Canvas`, source `ConfigInput`, `EditNodeDialog`, validation, schedule/webhook behavior, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 ConnectionLine

Updated on 2026-06-25 after adding the source-named Agentflow v2 `ConnectionLine` component and wiring it into AsterERP's ReactFlow canvas for Agentflow v2 modes. This improves Flowise source behavior parity for active connections without claiming full Agentflow v2 canvas parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Source-named component | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/ConnectionLine.tsx` exists and mirrors Flowise source behavior for bezier path, arrow marker, condition labels, and human-input proceed/reject labels. | Pass |
| Canvas wiring | `FlowiseCanvasPage.tsx` passes the custom connection line only for `agentflow-v2` and `marketplace-v2`, preserving Chatflow and Agentflow v1 behavior. | Pass |
| Styling | `flowise-canvas.css` defines `.flowise-agent-connection-label` for absolute small-label rendering. | Pass |
| Contract boundary | No backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Full Flowise Agentflow v2 `Canvas`, `ConfigInput`, `EditNodeDialog`, validation, schedule/webhook behavior, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Agentflow v2 Source Paths

Updated on 2026-06-25 after moving Agentflow v2 ReactFlow components into `native/views/agentflowsv2`. This improves Flowise source directory parity without claiming full Agentflow v2 canvas parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Source-named components | `AgentFlowNode.tsx`, `AgentFlowEdge.tsx`, and `IterationNode.tsx` now live under `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2`. | Pass |
| Canvas wiring | `FlowiseCanvasPage.tsx` imports Agentflow v2 ReactFlow registrations from the source-named directory. | Pass |
| No forwarding shim | The previous `canvas/FlowiseAgentFlowNode.tsx`, `canvas/FlowiseAgentFlowEdge.tsx`, and `canvas/FlowiseIterationNode.tsx` files are removed from the canvas directory. | Pass |
| Contract boundary | Existing node/edge props, class names, and ReactFlow type keys are preserved; no backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Full Flowise Agentflow v2 `Canvas`, `ConfigInput`, `ConnectionLine`, `EditNodeDialog`, validation, schedule/webhook behavior, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh ChatPopUp File Upload Component

Updated on 2026-06-25 after moving the ChatPopUp upload trigger into the Flowise source-named `native/ui-component/file/File.tsx` boundary. This improves source file structure parity, not final source/runtime parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| File upload component boundary | `File.tsx` owns the hidden file input, reset behavior, and MUI upload button with explicit typed props. | Pass |
| ChatPopUp container boundary | `FlowiseChatTestPanel.tsx` delegates upload selection to `FileUpload` and keeps only file-to-Flowise-upload conversion plus message/error handling. | Pass |
| Upload protocol | Existing `FlowisePredictionUpload` conversion, upload cap, request payload, and history playback path are unchanged. | Pass |
| Contract boundary | No backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Exact Flowise ChatMessage internals, provider-backed STT/TTS runtime, Agentflow v2 canvas, node-by-node runtime semantics, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh Streaming Node Execution Display

Updated on 2026-06-25 after consuming backend `agentFlowExecutedData` SSE events in ChatPopUp. This improves runtime visibility for Flowise Chat Test, but it is still not full Flowise runtime/source parity.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Streaming event support | `FlowiseChatTestPanel.tsx` now handles `agentFlowExecutedData` stream events in addition to token/error/chatId events. | Pass |
| Event shape safety | `normalizeExecutedDataEvent` and `isExecutedNodeEvent` validate Flowise executed-node payload fields before rendering. | Pass |
| Live runtime rendering | `ChatContent` renders live `AgentExecutedDataList` cards from `streamingExecutedData` while the prediction stream is active. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API alias, mock, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Full Flowise node-by-node runtime semantics, complete event-card parity, full Canvas/Agentflow v2 UI parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

### Current Implementation Refresh RateLimit Section

Updated on 2026-06-25 after migrating the Flowise Configuration Rate Limit section from raw controls to MUI controls. This is a local source-parity cleanup for one Configuration section, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| RateLimit status control | `FlowiseCanvasHeaderDialogs.tsx` now renders the Rate Limit status with MUI `FormControlLabel` and `Checkbox`. | Pass |
| RateLimit input controls | `limitMax`, `limitDuration`, and `limitMsg` now render with MUI `TextField` controls. | Pass |
| Event contract | `updateRateLimit(patch)` still writes through `normalizeRateLimit(next)` into `apiConfig.rateLimit`; incomplete-field validation is unchanged. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Raw controls now start at `AllowedDomainsSection` and continue through later section editors, MCP Server, TTS/STT, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Configuration JSON Webhook Save

Updated on 2026-06-25 after migrating the generic Configuration JSON editor, webhook secret input, and save action from raw controls to MUI controls. This is a local source-parity cleanup for the Configuration dialog shell, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| JSON text area component | `JsonTextArea` now renders with MUI `TextField multiline`, preserving string-valued JSON inputs and `onChange(value)` callbacks. | Pass |
| Webhook secret control | `webhookSecret` now renders with MUI `TextField` type `password`, preserving the masked placeholder when a secret is configured. | Pass |
| Save action | Configuration save now renders with MUI `Button` plus `AppIcon`, preserving the existing `onSave` callback. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Raw controls now start at `RateLimitSection` and continue through section editors, MCP Server, TTS/STT, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Configuration Basic Fields

Updated on 2026-06-25 after migrating the top-level Flowise Configuration dialog fields from raw inputs to MUI controls. This is a local source-parity cleanup for the Configuration dialog shell, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Basic text fields | `FlowiseCanvasHeaderDialogs.tsx` now renders `name`, `category`, `workspaceId`, and `apikeyid` with MUI `TextField` controls. | Pass |
| Boolean status fields | `deployed` and `isPublic` now render with MUI `FormControlLabel` and `Checkbox` controls. | Pass |
| Event contract | Each field still calls `update({ field: value })`; the save request construction and native Flowise config JSON sections remain unchanged. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Raw controls remain in the next Configuration URL/action block, section editors, MCP Server, TTS/STT, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Upsert Dialog Checkbox

Updated on 2026-06-25 after migrating the Flowise CanvasHeader Upsert dialog `replaceExisting` checkbox from a raw input to MUI controls. This is a local source-parity cleanup for the Upsert dialog body, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Upsert option control | `FlowiseCanvasHeaderDialogs.tsx` now renders `replaceExisting` with MUI `FormControlLabel` and `Checkbox` instead of a raw checkbox input. | Pass |
| Permission boundary | The Upsert run action remains the existing RBAC `PermissionButton`, preserving `flowisePermissions.documentStoresUpsert`. | Pass |
| Event contract | `replaceExisting` state and `onRun(replaceExisting)` remain unchanged. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Raw controls now start at Configuration basic fields and continue through section editors, MCP Server, TTS/STT, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Share Dialog Controls

Updated on 2026-06-25 after migrating the Flowise CanvasHeader Share dialog from raw tags to MUI controls. This is a local source-parity cleanup for the Share dialog body, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Share title control | `FlowiseCanvasHeaderDialogs.tsx` now renders the shared flow name with MUI `TextField` instead of a disabled raw input. | Pass |
| Workspace toggle controls | Workspace rows now use MUI `Checkbox` for the shared flag while preserving `workspaceId` keyed updates. | Pass |
| Share action | The save/share action now renders with MUI `Button` plus `AppIcon` instead of a raw `btn-primary` button. | Pass |
| Event contract | `workspaces`, local `rows`, and `onSave(rows.filter(...).map(workspaceId))` remain unchanged. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Raw controls remain in Upsert, Configuration section editors, MCP Server, TTS/STT, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh CanvasHeader Dialog Top Actions

Updated on 2026-06-25 after migrating the shared Flowise CanvasHeader dialog close/copy/export actions from raw tags to MUI controls. This is a local source-parity cleanup for the header-dialog action strip, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Dialog close action | `FlowiseCanvasHeaderDialogs.tsx` now renders the shared dialog close action with MUI `IconButton` and the existing `AppIcon` close glyph. | Pass |
| API/Webhook/Template actions | API Code copy, Webhook copy, and Export Template now render with MUI `Button` plus `AppIcon` start icons instead of raw JSX buttons. | Pass |
| Event contract | `onClose`, copy state, copy handlers, and template export dispatch remain in the same component and preserve the existing callback payloads. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Remaining source mismatch | Raw controls remain in Share, Upsert, Configuration section editors, MCP Server, TTS/STT, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Canvas Save Dialog Controls

Updated on 2026-06-25 after migrating the Flowise new-flow save dialog inside `FlowiseCanvasPage.tsx` from raw tags to MUI controls. This is a local source-parity cleanup for the canvas save entry, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Save dialog controls | `FlowiseCanvasPage.tsx` now renders the save dialog close action, name/category fields, cancel action, and save action with MUI `IconButton`, `TextField`, and `Button` controls instead of raw JSX controls. | Pass |
| Event contract | The component remains controlled by `draft`, `open`, `saving`, `onChange`, `onClose`, and `onSave`; no hidden persistence, new API call, or page-level state migration was added. | Pass |
| Contract boundary | No AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Verification | Targeted scan of `FlowiseCanvasPage.tsx` found no raw form/action tags. Full typecheck/build evidence is recorded in the progress document for this slice. | Pass |
| Remaining source mismatch | Raw controls remain in CanvasHeader dialogs and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh ConfigInput Controls

Updated on 2026-06-25 after migrating the shared Flowise node-parameter renderer from raw tags to MUI controls. This is a source-parity cleanup for the canvas node config inputs, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Basic param controls | `FlowiseConfigInput.tsx` now renders boolean, options, credential, multiOptions, json/code/array/grid, password, number, date, time, and text param types through MUI controls instead of raw JSX controls. | Pass |
| File param control | File params now use a MUI `Button` and a dynamic browser file picker, preserving `readFileForFlowData` output without rendering a raw JSX file input. | Pass |
| Error and value semantics | JSON parse-on-blur, invalid JSON feedback, password reveal state, selected multi-option updates, and `onChange(param.name, value)` payloads remain in the same component. | Pass |
| Contract boundary | `param`, `value`, and `onChange` are unchanged; no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Verification | Targeted scan of `FlowiseConfigInput.tsx` found no raw form/action tags; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings. | Pass |
| Remaining source mismatch | Raw controls remain in CanvasHeader dialogs, Canvas save dialog, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh NodePalette Controls

Updated on 2026-06-25 after migrating the Flowise AddNodes palette controls from raw tags to MUI controls. This is a source-parity cleanup for the canvas node palette, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Search control | `FlowiseNodePalette.tsx` now renders search with MUI `TextField` and an explicit search adornment class instead of a raw `input`. | Pass |
| Category controls | All Nodes and per-category filters now use MUI `ToggleButtonGroup` / `ToggleButton` instead of raw buttons. | Pass |
| AddNodes actions | Sticky Note, category expand/collapse, and individual node add cards now render with MUI `Button`; click-add and drag-add behavior remain in the same component. | Pass |
| Contract boundary | Fuzzy search, category state, `expandedCategories`, `onAddNode`, `onAddStickyNote`, and `application/x-flowise-node` drag payload are unchanged; no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Verification | Targeted scan of `FlowiseNodePalette.tsx` found no raw form/action tags; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings. | Pass |
| Remaining source mismatch | Raw controls remain in CanvasHeader dialogs, ConfigInput, Canvas save dialog, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh CanvasHeader Actions

Updated on 2026-06-25 after migrating the Flowise CanvasHeader non-permission action buttons from raw tags to MUI buttons. This is a source-parity cleanup for the canvas header action strip, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Header action controls | `FlowiseCanvasHeader.tsx` now renders API Code, Settings, Template, Share, Upsert History, Messages, Leads, Schedule, Webhook, Validation, and Chat Test with MUI `Button` plus `startIcon` instead of raw `button` tags. | Pass |
| RBAC boundary | Permission-gated Upsert, Run, and Save still use the existing `PermissionButton` paths, so permission visibility behavior remains unchanged in this slice. | Pass |
| Contract boundary | Header props and handlers remain unchanged: `onOpenDialog`, `onOpenChat`, `onOpenValidation`, `onRun`, and `onSave`; no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Visual boundary | `flowise-canvas.css` scopes compact MUI header-button styling under `.flowise-canvas-header__actions`, keeping layout changes local to the Flowise canvas header. | Pass |
| Verification | Targeted scan of `FlowiseCanvasHeader.tsx` found no raw form/action tags; `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings. | Pass |
| Remaining source mismatch | Raw controls remain in CanvasHeader dialogs, ConfigInput, NodePalette, Canvas save dialog, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Sticky Note Input

Updated on 2026-06-25 after migrating the Flowise Sticky Note text input from a raw textarea to a MUI multiline field. This is a source-parity cleanup for the Flowise canvas Sticky Note node, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Sticky Note input | `FlowiseStickyNote.tsx` now renders the note body with MUI `TextField multiline` instead of a raw `textarea`. | Pass |
| Contract boundary | The component still uses `NodeProps<FlowiseCanvasNodeType>` and the existing `data.onStickyTextChange?.(id, value)` callback; no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Visual boundary | `flowise-canvas.css` scopes the MUI input styling under `.flowise-sticky-note__input`, preserving the local yellow note chrome. | Pass |
| Verification | Targeted scan of `FlowiseStickyNote.tsx` found no raw form/action tags; `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings. | Pass |
| Remaining source mismatch | Raw controls remain in CanvasHeader, CanvasHeader dialogs, ConfigInput, NodePalette, Canvas save dialog, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Canvas Inspector Dialog

Updated on 2026-06-25 after migrating the canvas node inspector tabs and config JSON fallback from raw controls to MUI controls. This is a source-parity cleanup for the Flowise canvas inspector area, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Inspector tabs | `FlowiseCanvasDialogs.tsx` now renders Details, Additional Params, and Info with MUI `Tabs`/`Tab` instead of raw `button` tags. | Pass |
| Additional params badge | The Additional Params count now uses MUI `Badge`, preserving visible count behavior without introducing custom tab state. | Pass |
| Config JSON fallback | The read-only node config fallback now uses MUI `TextField multiline` instead of a raw `textarea`. | Pass |
| Contract boundary | `activeTab`, `onTabChange`, and `onNodeConfigChange` contracts are unchanged; no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API call, or bridge layer was introduced. | Pass |
| Verification | Targeted scan of `FlowiseCanvasDialogs.tsx` found no raw form/action tags; `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings. | Pass |
| Remaining source mismatch | Raw controls remain in CanvasHeader, CanvasHeader dialogs, ConfigInput, NodePalette, StickyNote, Canvas save dialog, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh 00:53

Updated on 2026-06-25 00:53 +08:00 after migrating the Executions page toolbar and pagination controls from raw tags to MUI controls. This is source-parity cleanup for the Executions menu page, not final parity completion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Executions toolbar | `FlowiseExecutionsPage.tsx` no longer uses raw search/status/action tags; it now uses MUI text, select, and button controls with the Search icon. | Pass |
| Executions pagination | Page navigation and page-size selection now use MUI `Stack`, `Button`, `FormControl`, `InputLabel`, `Select`, and `MenuItem`. | Pass |
| i18n | Added `flowise.fields.pageSize` in `zh-CN/en-US`; no hardcoded page-size label was introduced. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan of `FlowiseExecutionsPage.tsx` found no raw form/action tags. | Pass |
| Remaining source mismatch | Raw controls remain in Document Store detail, Account Settings, Chatflows, FlowListTable sorting, Tools tab, and Custom MCP panel. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh FlowListTable

Updated on 2026-06-25 after migrating the shared Flowise native list-table sort control from a raw tag to MUI `TableSortLabel`. This removes a reusable native table component mismatch, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Native table sorting | `FlowListTable.tsx` now renders sortable headers with MUI `TableSortLabel`; the raw sort `button` tag was removed. | Pass |
| Contract boundary | The table still uses the same local `FlowListTableColumn`, `order`, `orderBy`, and `onSort` contract and does not import AsterERP `DataTable` or generic CRUD resources. | Pass |
| Verification | Targeted scan of `FlowListTable.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Raw controls remain in Chatflows, Tools tab, Custom MCP panel, and multiple canvas/ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Tools Tabs

Updated on 2026-06-25 after migrating the Tools page tab switch from raw buttons to MUI toggle controls. This removes one native Tools page mismatch, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Tools tab control | `native/views/tools/index.tsx` now renders Custom Tools / Custom MCP Server switching with MUI `ToggleButtonGroup` and `ToggleButton`; raw tab `button` tags were removed. | Pass |
| Contract boundary | The page still switches between the typed Tools surface and `CustomMcpServerPanel`; it does not add AsterERP `DataTable`, generic CRUD resources, WorkflowModel/BPMN reuse, projection, or bridge behavior. | Pass |
| Verification | Targeted scan of `native/views/tools/index.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Raw controls remain in Chatflows, Custom MCP panel, and multiple canvas/ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Custom MCP Panel

Updated on 2026-06-25 after migrating the Custom MCP Server panel search, view switch, and pagination controls from raw tags to MUI controls. This removes the remaining non-dialog Custom MCP panel raw controls, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Custom MCP toolbar | `CustomMcpServerPanel.tsx` now renders search with MUI `TextField` and card/list mode with MUI `ToggleButtonGroup`/`ToggleButton`; raw toolbar `input` and `button` tags were removed. | Pass |
| Custom MCP pagination | The panel now renders previous/next and page-size controls with MUI `Button`, `FormControl`, `InputLabel`, `Select`, and `MenuItem`; raw pagination `button` and `select` tags were removed. | Pass |
| Contract boundary | The panel still uses dedicated `customMcpServersApi` calls and permission-gated actions; it does not add AsterERP `DataTable`, generic CRUD resources, WorkflowModel/BPMN reuse, projection, or bridge behavior. | Pass |
| Verification | Targeted scan of `CustomMcpServerPanel.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Raw controls remain in Chatflows and multiple canvas/ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Chatflows List Controls

Updated on 2026-06-25 after migrating the native Chatflows/Agentflows list page header, search, import picker, page-size, and pagination controls from raw tags to MUI controls. This removes the remaining non-canvas native list-page raw controls, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Header controls | `native/views/chatflows/index.tsx` now renders card/list switching with MUI `ToggleButtonGroup`/`ToggleButton` and refresh with MUI `Button`; raw header action `button` tags were removed. | Pass |
| Import picker | The import action now creates the file input on demand through the existing import handler rather than rendering a hidden raw JSX `input`; import still writes native `duplicatedFlowData` and navigates to the Flowise canvas. | Pass |
| Toolbar and pagination | Search, clear, page-size, previous, and next controls now use MUI `TextField`, `Button`, `FormControl`, `InputLabel`, `Select`, `MenuItem`, and `Stack`; raw toolbar/pagination tags were removed. | Pass |
| Contract boundary | The page still uses native Chatflows/Agentflows APIs, local storage view/page-size/sort keys, native Flowise `flowData`, and dedicated permissions; it does not add AsterERP `DataTable`, generic CRUD resources, WorkflowModel/BPMN reuse, projection, or bridge behavior. | Pass |
| Verification | Targeted scan of `native/views/chatflows/index.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Remaining scoped raw controls are concentrated in canvas and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Canvas Edge Control

Updated on 2026-06-25 after migrating the shared Flowise ReactFlow edge delete control from a raw button to MUI `IconButton`. This removes one canvas-level raw control, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Edge delete control | `FlowiseButtonEdge.tsx` now renders the edge-delete label control with MUI `IconButton`; the raw edge `button` tag was removed. | Pass |
| Contract boundary | The edge still uses `BaseEdge`, `EdgeLabelRenderer`, `data.onDeleteEdge(id)` when available, and ReactFlow `setEdges` fallback deletion. | Pass |
| Verification | Targeted scan of `FlowiseButtonEdge.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Remaining scoped raw controls are concentrated in other canvas, CanvasHeader dialog, ConfigInput, NodePalette, StickyNote, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Canvas Node Toolbar

Updated on 2026-06-25 after migrating the shared Flowise canvas node toolbar actions from raw buttons to MUI `IconButton`. This removes one canvas node-card raw-control group, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Node toolbar controls | `FlowiseCanvasNode.tsx` now renders info, duplicate, and delete toolbar actions with MUI `IconButton`; the raw node action `button` tags were removed. | Pass |
| Action contract | `data-flowise-node-action` is preserved for all three actions, so `FlowiseCanvasPage` delegated click handling still receives the same action values. | Pass |
| Style compatibility | `flowise-canvas.css` now includes `.MuiIconButton-root` in the node toolbar styling selectors, retaining the existing node-card hover toolbar appearance. | Pass |
| Verification | Targeted scan of `FlowiseCanvasNode.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Remaining scoped raw controls are concentrated in CanvasHeader, CanvasHeader dialogs, ConfigInput, NodePalette, StickyNote, Canvas save dialog, and ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Document Store Detail

Updated on 2026-06-25 after migrating the Document Store Detail vector-query controls from raw tags to MUI controls. This closes one document-store detail page cleanup item, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Vector query controls | `FlowiseDocumentStoreDetailPage.tsx` now renders vector-store query through MUI `TextField` and `Button` inside MUI `Stack`; raw `input` and `button` tags were removed from the page. | Pass |
| Protocol/API boundary | The page still uses the dedicated `documentStoresApi` detail/files/chunks/vector-config/query calls; no generic resource API, WorkflowModel/BPMN reuse, projection, or bridge path was added. | Pass |
| Verification | Targeted scan of `FlowiseDocumentStoreDetailPage.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Raw controls remain in Chatflows, FlowListTable sorting, Tools tab, Custom MCP panel, and multiple canvas/ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Current Implementation Refresh Account Settings

Updated on 2026-06-25 after migrating the Account Settings form from raw tags to MUI controls. This closes one management-page source-parity cleanup item, but it does not close the full Flowise source parity matrix.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Account Settings form | `FlowiseAccountSettingsPage.tsx` now renders display name, email, and preferences JSON through MUI `TextField` controls inside MUI `Stack`; raw `input` and `textarea` tags were removed from the page. | Pass |
| Protocol/API boundary | The page still uses the dedicated account API and `flowisePermissions.accountEdit`; no generic resource API, WorkflowModel/BPMN reuse, projection, or bridge path was added. | Pass |
| Verification | Targeted scan of `FlowiseAccountSettingsPage.tsx` found no raw form/action tags; `npm run typecheck` and `npm run build` passed. | Pass |
| Remaining source mismatch | Raw controls remain in Document Store detail, Chatflows, FlowListTable sorting, Tools tab, Custom MCP panel, and multiple canvas/ChatPopUp files. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Previous Implementation Refresh 00:44

Updated on 2026-06-25 00:44 +08:00 after migrating the native collection surface and deleting the final `NativeDialog` component. This closes the custom dialog-shell residue completely, but source parity remains below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Native collection surface | `FlowiseNativeCollectionSurface.tsx` no longer imports or renders `NativeDialog`; toolbar and create/edit form now use MUI text/select/toggle/dialog controls. | Pass |
| NativeDialog removal | `NativeDialog.tsx` was deleted and a scoped `rg -n "NativeDialog" frontend/AsterERP.Web/src/features/flowise-studio` scan returns no hits. | Pass |
| Protocol/API boundary | The surface still receives explicit APIs through `FlowiseNativeCollectionOptions.api`; it does not reintroduce generic resource APIs, WorkflowModel/BPMN reuse, projection, or bridge behavior. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan of the three most recent migration files found no raw form/action tags. | Pass |
| Remaining source mismatch | Raw controls remain in Document Store detail, Account Settings, Executions, Chatflows, FlowListTable sorting, Tools tab, and Custom MCP panel. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Previous Implementation Refresh 00:35

Updated on 2026-06-25 00:35 +08:00 after migrating the Custom MCP Server dialog away from the remaining `NativeDialog` shell and raw controls. This closes the largest remaining dialog-shell mismatch, but it is still not a 100% source-parity claim.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Custom MCP dialog shell | `CustomMcpServerDialog.tsx` no longer imports or renders `NativeDialog`; the dialog now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, and `DialogActions`. | Pass |
| Custom MCP edit form | The edit form now uses MUI `TextField`, `FormControl`, `InputLabel`, `Select`, `MenuItem`, and `Stack`; header editing uses MUI `TextField`, `IconButton`, and add/delete icons. | Pass |
| Custom MCP tool controls | Discovered-tool expand/collapse/search controls now use MUI `Button`, `IconButton`, `TextField`, and MUI icons instead of raw form/action tags. | Pass |
| Protocol/API boundary | The dialog still delegates all persistence and actions through `onSave`, `onAuthorize`, and `onDelete`; no backend protocol change, WorkflowModel/BPMN reuse, projection, or generic-resource path was introduced. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan of `CustomMcpServerDialog.tsx` and `FlowiseWorkspacesPage.tsx` found no `NativeDialog` or raw form/action tags. The scoped `NativeDialog` scan now only finds `FlowiseNativeCollectionSurface.tsx` and the `NativeDialog.tsx` component. | Pass |
| Remaining source mismatch | `NativeDialog` remains in `FlowiseNativeCollectionSurface.tsx`; raw controls remain in Chatflows, native collection surface, native table sorting, Custom MCP panel, and Tools tab controls. Runtime/canvas/API/browser parity remain incomplete. | Fail |

### Previous Implementation Refresh 00:26

Updated on 2026-06-25 00:26 +08:00 after migrating the standalone Workspaces page away from the remaining `NativeDialog` shell and raw controls. This is a source-parity cleanup, not a completed parity claim.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Workspaces page shell | `FlowiseWorkspacesPage.tsx` no longer imports or renders `NativeDialog`; create/edit now uses MUI `Dialog` structure. | Pass |
| Workspaces controls | The Workspaces dialog and toolbar no longer contain raw `<input>`, `<button>`, `<textarea>`, or `<select>` tags; they use MUI text, select, toggle, and button controls. | Pass |
| Protocol/API boundary | The page still uses the existing Workspaces API and permission code only; no WorkflowModel/BPMN/projection/generic-resource path was introduced. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan of `FlowiseWorkspacesPage.tsx` found no `NativeDialog` or raw form/action tags. Build still reports existing circular chunk and large chunk warnings. | Pass |
| Remaining source mismatch | `NativeDialog` remains in `FlowiseNativeCollectionSurface.tsx`, `CustomMcpServerDialog.tsx`, and the component file itself. Raw controls remain in Chatflows, Custom MCP, native collection surface, native table sorting, and Tools tab controls. | Fail |

### Previous Documentation Refresh 00:18

Updated on 2026-06-25 00:18 +08:00 from a documentation-only source scan. This refresh does not claim new implementation progress; it records the current remaining source-parity blockers before the next implementation slice.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`. | Fail |
| Flowise protocol isolation | Scoped Flowise paths still have no matched Flowise usage of `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, split canvas tables, generic `FlowiseResourceService/FlowiseResourceEntity`, or `flowiseStudioApi.resources`. | Pass |
| NativeDialog residue | Remaining source hits are `FlowiseNativeCollectionSurface.tsx`, `CustomMcpServerDialog.tsx`, `FlowiseWorkspacesPage.tsx`, and the `NativeDialog.tsx` component itself. These must be migrated before dialog/page shell parity can be marked complete. | Fail |
| Raw control residue | Remaining source hits for raw form/action controls are concentrated in `FlowiseNativeCollectionSurface.tsx`, `chatflows/index.tsx`, `FlowListTable.tsx`, `CustomMcpServerPanel.tsx`, `tools/index.tsx`, and `CustomMcpServerDialog.tsx`. These still need source-level component parity cleanup. | Fail |
| Final verification state | Build/test/API/browser evidence was not rerun for this documentation-only refresh. Final parity still requires backend build/test, frontend typecheck/build, authenticated API smoke, permission and workspace-boundary checks, and browser screenshot comparison. | Blocked |
| Next implementation priority | Migrate remaining `NativeDialog` usages and raw-control pages first, then close Custom MCP exact dialog parity, Canvas/Agentflow v2/ChatPopUp parity, and node-by-node runtime semantics. | Fail |

### Current Implementation Refresh 00:09

Updated on 2026-06-25 00:09 +08:00 after the Export As Template source-like MUI form slice. This refresh closes the last `NativeDialog` usage inside `native/ui-component/dialog` business dialogs, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Dialog shell movement | `ExportAsTemplateDialog.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, and `DialogActions` instead of `NativeDialog`. | Pass |
| Form control movement | The dialog now uses MUI `OutlinedInput` for name, key, and multiline description; category/usecase editing uses MUI `Chip` semantics; source flow summary uses MUI `Typography` and `Box` layout. | Pass |
| Component boundary | The dialog remains pure UI: save still returns `ExportAsTemplatePayload` to the page callback and does not contain hidden API calls. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan confirms `ExportAsTemplateDialog.tsx` no longer contains `NativeDialog`, raw inputs, raw textarea, or `<button>`; visible-string scan returns no hits; `git diff --check` returns CRLF warnings only. Build still reports existing large-chunk/circular-chunk warnings. | Pass |
| Remaining source mismatch | Source dialogs outside `native/ui-component/dialog`, Canvas/runtime parity, authenticated API smoke, permission-deny smoke, workspace boundary checks, and screenshot parity remain open. | Fail |

### Previous Implementation Refresh 00:03

Updated on 2026-06-25 00:03 +08:00 after the Starter Prompts source-like dynamic form slice. This refresh closes the prior `NativeDialog + textarea` mismatch for the Starter Prompts list action, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Dialog shell movement | `StarterPromptsDialog.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, and `DialogActions` instead of `NativeDialog`. | Pass |
| Dynamic prompt rows | The dialog now renders one MUI `OutlinedInput` per prompt, keeps at least one row visible, supports add with a plus icon, and supports removing extra rows with a delete icon, matching the source `StarterPrompts` row interaction pattern. | Pass |
| Source info banner | The dialog now includes a green tips banner matching the source `StarterPrompts` intent, with i18n text explaining when starter prompts appear. | Pass |
| Component boundary | The dialog remains pure UI: save still returns a prompt array to the page callback and does not contain hidden API calls. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan confirms `StarterPromptsDialog.tsx` no longer contains `NativeDialog`, raw textarea, or `<button>`; visible-string scan returns no hits; `git diff --check` returns CRLF warnings only. Build still reports existing large-chunk/circular-chunk warnings. | Pass |
| Remaining source mismatch | Other source dialogs, Canvas/runtime parity, authenticated API smoke, permission-deny smoke, workspace boundary checks, and screenshot parity remain open. | Fail |

### Previous Implementation Refresh 23:59

Updated on 2026-06-24 23:59 +08:00 after the Chat Feedback source-like switch dialog slice. This refresh closes the prior `NativeDialog + checkbox` mismatch for the Chat Feedback list action, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Dialog shell movement | `ChatFeedbackDialog.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, and `DialogActions` instead of `NativeDialog`. | Pass |
| Switch movement | The dialog now uses MUI `FormControlLabel` and `Switch` with `flowise.messages.enableChatFeedback`, replacing the previous raw checkbox input. | Pass |
| Component boundary | The dialog remains pure UI: save still returns the enabled boolean to the page callback and does not contain hidden API calls. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan confirms `ChatFeedbackDialog.tsx` no longer contains `NativeDialog`, raw checkbox, or `<button>`; visible-string scan returns no hits; `git diff --check` returns CRLF warnings only. Build still reports existing large-chunk/circular-chunk warnings. | Pass |
| Remaining source mismatch | Other source dialogs, Canvas/runtime parity, authenticated API smoke, permission-deny smoke, workspace boundary checks, and screenshot parity remain open. | Fail |

### Previous Implementation Refresh 23:54

Updated on 2026-06-24 23:54 +08:00 after the Allowed Domains source-like dynamic form slice. This refresh closes the prior `NativeDialog + textarea` mismatch for the Allowed Domains list action, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Dialog shell movement | `AllowedDomainsDialog.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, and `DialogActions` instead of `NativeDialog`. | Pass |
| Dynamic domain rows | The dialog now renders one MUI `OutlinedInput` per domain, keeps at least one row visible, supports add with a plus icon, and supports removing extra rows with a delete icon, matching the source `AllowedDomains` row interaction pattern. | Pass |
| Error input movement | Unauthorized-domain error text now uses a separate MUI `OutlinedInput` with tooltip/help copy, replacing the previous plain `<input>`. | Pass |
| Component boundary | The dialog remains pure UI: save still returns `{ domains, errorMessage }` to the page callback and does not contain hidden API calls. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted scan confirms `AllowedDomainsDialog.tsx` no longer contains `NativeDialog` or `textarea`; visible-string scan returns no hits; `git diff --check` returns CRLF warnings only. Build still reports existing large-chunk/circular-chunk warnings. | Pass |
| Remaining source mismatch | Other source dialogs, Canvas/runtime parity, authenticated API smoke, permission-deny smoke, workspace boundary checks, and screenshot parity remain open. | Fail |

### Previous Implementation Refresh 23:47

Updated on 2026-06-24 23:47 +08:00 after the Speech To Text provider image asset parity slice. This refresh closes the prior avatar-initial image mismatch for the Speech To Text provider list, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Provider image source assets | Copied Flowise source images `openai.svg`, `assemblyai.png`, `localai.png`, `azure_openai.svg`, and `groq.png` into `frontend/AsterERP.Web/src/features/flowise-studio/native/assets/images`. | Pass |
| Provider image rendering | `SpeechToTextDialog.tsx` imports those files directly and renders each provider with `component="img"` inside a source-like 50x50 white circular image container. The previous MUI avatar initials placeholder is no longer used. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed. Targeted STT scan confirms static provider icon imports and `component="img"` rendering; the only remaining `Avatar` text is `ListItemAvatar`. Flowise Studio visible-string scan still returns no hits. Build emitted copied PNG provider assets and still reports existing large-chunk/circular-chunk warnings. | Pass |
| Remaining source mismatch | Other source dialogs, Canvas/runtime parity, authenticated API smoke, permission-deny smoke, workspace boundary checks, and screenshot parity remain open. | Fail |

### Previous Implementation Refresh 23:41

Updated on 2026-06-24 23:41 +08:00 after the Speech To Text provider-form dialog slice. This refresh closes the prior Speech To Text JSON-textarea mismatch, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Dialog shell movement | `SpeechToTextDialog.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, provider `Select`, provider identity row, provider fields, validation helper text, and save button instead of `NativeDialog` plus textarea. | Pass |
| Provider semantic movement | The dialog now supports Flowise source provider keys `openAIWhisper`, `assemblyAiTranscribe`, `localAISTT`, `azureCognitive`, and `groqWhisper`, writes `credentialId`, and saves one active provider while marking others inactive. | Pass |
| Credential flow | `native/views/chatflows/index.tsx` loads Flowise Credentials once through `flowiseConfigurationResourcesApi.credentials.list` and passes credentials to `FlowListMenu` and `SpeechToTextDialog`; the reusable dialog remains API-free. | Pass |
| i18n and scans | Added zh/en STT provider, field, description, and validation keys. `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, visible-string scan, targeted STT symbol scan, and frontend touched-file `git diff --check` passed. | Pass |
| Remaining source mismatch | This historical row was superseded by the 23:47 provider image asset parity slice. Other source dialogs, Canvas/runtime parity, authenticated API smoke, permission-deny smoke, workspace boundary checks, and screenshot parity remain open. | Fail |

### Previous Implementation Refresh 23:33

Updated on 2026-06-24 23:33 +08:00 after the FlowListMenu action-permission backend closure and Save/Tag MUI dialog slice. This refresh removes a backend/frontend permission mismatch and moves two list dialogs from custom `NativeDialog` toward Flowise source MUI internals, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Save dialog source movement | `SaveChatflowDialog.tsx` now uses the Flowise source MUI dialog structure: `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `OutlinedInput`, and MUI buttons, including `chatflow-name`, placeholder, disabled confirm on empty input, and Enter-to-confirm behavior. | Pass |
| Category dialog source movement | `TagDialog.tsx` now uses MUI dialog, `TextField`, `Chip`, and `Typography`; it supports Enter-to-add tags, chip deletion, submit-time merge, and i18n-backed source help text. | Pass |
| Backend action permission closure | `AiFlowiseChatflowsController` now exposes action-specific configuration/domains routes and delete attributes for Chatflows/Agentflows. `FlowiseChatflowService` has matching service-layer guards and only mutates configuration or domains fields for those paths. | Pass |
| Template export permission closure | `AiFlowiseResourcesController` now exposes `POST /api/ai/flowise/marketplaces/from-flow-template` guarded by `FlowiseTemplatesFlowExport`, backed by `FlowiseMarketplaceService.CreateFromFlowTemplateAsync`; the frontend template save path uses this endpoint. | Pass |
| Verification | `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` 88/88, `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, and `git diff --check` passed. Existing package vulnerability warnings, Vite large-chunk warnings, and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning remain recorded risks. | Pass |
| Remaining source mismatch | This historical row was superseded by the 23:41 Speech To Text provider-form slice. Provider image assets, other dialog internals, Canvas/runtime parity, authenticated API smoke, permission-deny smoke, workspace boundary checks, and browser screenshot parity remain open. | Fail |

### Previous Implementation Refresh 23:18

Updated on 2026-06-24 23:18 +08:00 after the FlowListMenu distinct action permission slice. This refresh closes the previous one-permission Options menu mismatch while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Backend action permission map | `PermissionCodes.Flowise.cs` now includes distinct Chatflow/Agentflow action permissions for duplicate, export, configuration, allowed domains, and delete, plus a template flow-export permission. | Pass |
| Menu seed parity movement | `AiCenterAppModule` now upserts action button menus under `flowise:chatflows` and `flowise:agentflows` so Rename/Edit, Duplicate, Export, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and Delete no longer share one broad permission. | Pass |
| Frontend action permission map | `permissionCodes.ts`, `native/views/chatflows/index.tsx`, and `FlowListMenu.tsx` now pass a typed `FlowListMenuPermissions` object. Each MUI menu item checks its own permission through `FlowListPermissionMenuItem`; the whole Options trigger is hidden when no action permission is available. | Pass |
| Verification | `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` 88/88, `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, targeted `permissions=` prop scan, and touched-file `git diff --check` passed. Existing NuGet vulnerability warnings, Vite large-chunk warnings, and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning remain recorded risks. | Pass |
| Remaining source mismatch | Exact source dialog internals and browser screenshot parity remain open for Chatflows/Agentflows, and broader Canvas/runtime/dialog/API-smoke gaps still prevent 100%. | Fail |

### Previous Implementation Refresh 23:13

Updated on 2026-06-24 23:13 +08:00 after the FlowListMenu MUI source-structure migration slice. This refresh closes the earlier hand-rolled menu implementation gap for FlowListMenu, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Dependency parity movement | `frontend/AsterERP.Web/package.json` and `package-lock.json` now include `@mui/material`, `@mui/icons-material`, `@emotion/react`, and `@emotion/styled`, matching the Flowise UI dependency family needed by `FlowListMenu.jsx`. | Pass |
| MUI source structure | `FlowListMenu.tsx` now uses MUI `Button`, styled `Menu`, `MenuItem`, and `Divider`; the previous custom absolute menu, manual outside-click listener, manual Escape listener, manual focus movement, and `AppIcon` menu icons are no longer the main path. | Pass |
| StyledMenu source details | The styled MUI menu follows the Flowise source shape for zero elevation, bottom-right/top-right anchor/transform origins, 6px radius, 180px min width, `theme.spacing(1)` top margin, source box shadow, `MuiMenu-list` padding, 18px icon size, secondary icon color, and active background through `alpha`. | Pass |
| Permission menu item | Added `FlowListPermissionMenuItem`, a MUI `MenuItem` wrapper that uses AsterERP `usePermission` to hide unauthorized menu actions while preserving the no-hidden-API-call component boundary. | Pass |
| Verification | `npm install @mui/material @mui/icons-material @emotion/react @emotion/styled` completed; `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, and dependency/component `git diff --check` passed. Build still reports large chunk warnings and now reports a `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning, which remains a build-risk note. | Pass |
| Remaining source mismatch | Flowise source uses distinct action permission ids inside `PermissionMenuItem`; AsterERP still passes one permission code to the menu. Exact dialog internals and browser screenshot parity remain open, along with broader canvas/runtime gaps. | Fail |

### Previous Implementation Refresh 23:06

Updated on 2026-06-24 23:06 +08:00 after the FlowListMenu keyboard roving-focus slice. This refresh closes part of the current list-menu keyboard interaction gap, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Roving focus behavior | `FlowListMenu.tsx` now handles `ArrowDown`, `ArrowUp`, `Home`, and `End` while the menu is focused, using the current `role="menuitem"` buttons as the focus target list. | Pass |
| Wrap behavior | Arrow navigation wraps at both ends of the menu item list, matching common MUI MenuList keyboard movement better than static focus. | Pass |
| Responsibility boundary | The implementation remains UI-only in `FlowListMenu`; no API calls or persistence logic moved into the reusable menu component. | Pass |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, and component `git diff --check` passed for this slice; build still reports existing Vite large chunk warnings. | Pass |
| Remaining source mismatch | Literal Flowise MUI `StyledMenu`/`PermissionMenuItem`, exact MUI focus manager behavior, browser screenshot parity, exact source dialog internals, and broader page/canvas/runtime gaps remain open. | Fail |

### Previous Implementation Refresh 23:03

Updated on 2026-06-24 23:03 +08:00 after the FlowListMenu source-like interaction slice. This refresh closes part of the current list-menu interaction gap, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Menu close behavior | `FlowListMenu.tsx` now closes on outside pointer down and `Escape`, which aligns better with the Flowise/MUI menu interaction model than the previous click-toggle-only behavior. | Pass |
| Menu focus behavior | Opening the menu now focuses the first `role="menuitem"` button, providing a keyboard-accessible first action target. | Pass |
| Trigger/menu accessibility | The Options trigger now exposes `aria-haspopup="menu"`, `aria-expanded`, and `aria-controls`; the menu id is generated with React `useId`; the container keeps `role="menu"` and items keep `role="menuitem"`. | Pass |
| Responsibility boundary | This remains a UI-only component improvement. `FlowListMenu` still receives typed callbacks and does not hide API calls; persistence remains in `native/views/chatflows/index.tsx`. | Pass |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, and component `git diff --check` passed for this slice; build still reports existing Vite large chunk warnings. | Pass |
| Remaining source mismatch | This does not yet implement literal Flowise MUI `StyledMenu`/`PermissionMenuItem`, full MUI focus roving behavior, browser screenshot parity, or exact source dialog internals. | Fail |

### Previous Implementation Refresh 23:00

Updated on 2026-06-24 23:00 +08:00 after the FlowListMenu source-like visual menu slice. This refresh closes part of the current list-menu visual gap, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Menu trigger parity movement | `FlowListMenu.tsx` now renders the Options trigger with a visible caret icon and separate label span, matching the source menu affordance more closely than the prior plain action button. | Pass |
| Menu item parity movement | `FlowListMenu.tsx` now centralizes each action in `FlowListMenuItem` with source-like icon+label structure, danger tone, and menu/menuitem ARIA roles for the same native action list: Rename, Duplicate, Export, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and Delete. | Pass |
| Visual styling parity movement | `flowise-pages.css` now applies source-like menu measurements and styling: 180px min width, 4px inner padding, 6px radius, layered MUI-like shadow, 12px icon gap, 18px icon slots, hover/focus background, and danger coloring. | Pass |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, and `git diff --check` passed for this slice; build still reports existing Vite large chunk warnings and diff check only reports CRLF warnings. | Pass |
| Remaining source mismatch | This is still not a literal migration of Flowise MUI `StyledMenu`/`PermissionMenuItem`; exact browser screenshot parity, exact MUI component behavior, and full source dialog internals remain open, along with broader canvas/runtime gaps. | Fail |

### Previous Implementation Refresh 22:55

Updated on 2026-06-24 22:55 +08:00 after the Save As Template Marketplace persistence slice. This refresh closes the current template persistence gap in the Chatflows/Agentflows list menu path, while keeping source parity below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Template save behavior | `ExportAsTemplateDialog.tsx` now collects source-equivalent template metadata and emits a typed payload; `FlowListMenu.tsx` passes it to the page; `native/views/chatflows/index.tsx` persists the template through `flowiseNativeResourcesApi.marketplaces.create` instead of downloading a JSON file. | Pass |
| Marketplace storage | The template is stored through the existing strong typed `FlowiseMarketplaceService` / `FlowiseMarketplaceTemplateEntity` path with sanitized native Flowise `flowData` in `definitionJson` and source flow metadata in `metadataJson`. | Pass |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, and `git diff --check` passed for this slice; build still reports existing Vite large chunk warnings and diff check only reports CRLF warnings. | Pass |
| Remaining source mismatch | Exact Flowise MUI menu/table/card styling, exact dialog internals beyond the current component split, browser screenshot matrix, and broader canvas/runtime gaps remain open. | Fail |

### Previous Implementation Refresh 22:51

Updated on 2026-06-24 22:51 +08:00 after the FlowListMenu source-dialog split slice. This refresh moves the Chatflows/Agentflows list menu closer to Flowise source file structure, but it does not make source parity complete.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Dedicated source dialog files | Added `SaveChatflowDialog.tsx`, `TagDialog.tsx`, `StarterPromptsDialog.tsx`, `ChatFeedbackDialog.tsx`, `AllowedDomainsDialog.tsx`, `SpeechToTextDialog.tsx`, and `ExportAsTemplateDialog.tsx` under `native/ui-component/dialog`, matching the Flowise source dialog names used by `FlowListMenu.jsx`. | Partial |
| FlowListMenu structure | Removed the inline `FlowListOptionsDialog` implementation and moved each list action dialog into its own typed component. The menu now coordinates open state and typed callbacks only; backend persistence remains in the native Chatflow API path. | Pass |
| i18n and visible-text cleanup | Added `zh-CN/en-US` keys for category/domain/prompt hints and replaced `NativeDialog` literal `x` close text with `×`, preserving the current no-bare-English scan baseline for Flowise Studio. | Pass |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, targeted legacy inline-dialog symbol scan, and `git diff --check` passed for this slice; build still reports existing Vite large chunk warnings and diff check only reports CRLF warnings. | Pass |
| Remaining source mismatch | The dialogs now exist as source-named components but are still not exact MUI/dialog internals from Flowise source; full marketplace template persistence and browser screenshot parity remain open, along with broader canvas/runtime gaps. This historical row was superseded by the 22:55 implementation slice for template persistence. | Fail |

### Previous Implementation Refresh 22:42

Updated on 2026-06-24 22:42 +08:00 after the Flowise node-icon endpoint/rendering slice. This refresh closes the previously listed node-icon endpoint/rendering gap but does not make source parity complete.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row still `5/6` and `Fail`. | Fail |
| Native node-icon endpoint | Added `AiFlowiseNodeIconsController` for `/api/v1/node-icon/{name}` and `AiFlowiseNodesController.GetIconAsync` for `/api/ai/flowise/nodes/icon/{name}`; both return SVG file responses from `IFlowiseNodeCatalogService.GetNodeIconAsync`. | Pass |
| Shared Flowise node catalog | Added `FlowiseCanvasNodeCatalog` and moved the static node directory out of `FlowiseCanvasService`, so icon rendering and canvas node catalog share a single Flowise-owned source without approval workflow/BPMN/projection. | Pass |
| Source list icon behavior | `native/views/chatflows/index.tsx` now generates image URLs from `flowData.nodes[].data.name`, matching the Flowise source list-page image extraction shape; `FlowNodePreviewList` renders image stacks and localized overflow text. | Pass |
| Verification | `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` 88/88, `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, and Flowise Studio visible-string scan passed after this slice. | Pass |
| Remaining source mismatch | Exact MUI menu/table/card styling, exact dialog component migration, template marketplace persistence, browser screenshot matrix, and broader canvas/runtime gaps remain open. | Fail |

### Current Documentation Refresh 22:44

Updated on 2026-06-24 22:44 +08:00 for the current progress-documentation request. This is a documentation-only refresh and keeps the source-parity conclusion below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | The implementation progress document remains `13/18 Pass = 72.22%`; no row is newly promoted by this documentation-only update. | Fail |
| Latest concrete implementation evidence | The latest implementation evidence is still the node-icon endpoint/rendering slice: shared Flowise node catalog, file-response node-icon endpoints, and Chatflows/Agentflows node preview images derived from native Flowise `flowData`. | Pass |
| Dedicated Flowise protocol boundary | The latest recorded scoped forbidden-symbol scan remains clean for approval `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `ai_flowise_canvas*`, and split canvas projection symbols in Flowise implementation paths. | Pass |
| Generic UI/resource ban | The latest recorded scans remain clean for `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, `CrudPage`, `DataTable`, and `useCrudResource` in the scoped Flowise paths. | Pass |
| Verification freshness | The latest recorded build/test/typecheck/build scans are from the 22:42 implementation slice. This documentation refresh did not rerun authenticated API smoke, permission 403, workspace boundary checks, or browser screenshot parity. | Partial |
| Remaining source gaps | Exact Flowise source dialogs and MUI visual parity, full Canvas/Agentflow v2 source behavior, full Flowise node-by-node runtime, provider-backed STT/TTS/upload capability behavior, i18n after all remaining source migrations, and final API/browser screenshot matrix remain open. | Fail |

### Previous Implementation Refresh 22:35

Updated on 2026-06-24 22:35 +08:00 after the Chatflows/Agentflows source-component structure slice. This refresh records real source-structure movement in the list page while keeping the source parity conclusion below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the progress document remains `13/18 Pass = 72.22%`, with the Chatflows/Agentflows row now `5/6` but still `Fail`. | Fail |
| Dedicated source component files | Added `native/ui-component/button/FlowListMenu.tsx` and routed Chatflows/Agentflows card/table modes through `native/ui-component/cards/ItemCard.tsx` and `native/ui-component/table/FlowListTable.tsx`. | Partial |
| Page-local generic rendering removed | The old page-local flow card and flow row branches were replaced by dedicated native components; the non-source raw `flowData` edit dialog/action was removed from the list page path. | Pass |
| Source table behavior movement | `FlowListTable` supports sortable columns; the page persists local sort state with Flowise source-style `chatflowcanvas_*` and `agentcanvas_*` storage keys. | Partial |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, Flowise Studio visible-string scan, and targeted legacy-symbol scan passed after this slice. | Pass |
| Remaining source mismatch | Exact Flowise MUI menu/table/card styling, node icon endpoint images, exact dialog component migration, template marketplace persistence, browser screenshot matrix, and broader canvas/runtime gaps remain open. | Fail |

### Previous Documentation Refresh 22:27

Updated on 2026-06-24 22:27 +08:00 for the current actual-progress documentation request. This is a documentation-only refresh; it does not change the source-parity conclusion.

| Check | Current Evidence | Status |
|---|---|---|
| Source parity score | Still below 100%; the current progress document remains `13/18 Pass = 72.22%`. | Fail |
| Latest implemented slice | The latest implementation evidence remains the 22:25 FlowListMenu source-action slice, including native Options menu actions, native field persistence, template-shaped export, and frontend verification. | Partial |
| Dedicated protocol boundary | No new implementation changed the Flowise protocol boundary in this documentation-only refresh; Flowise remains documented as a dedicated `FlowiseChatFlowEntity.FlowData` path, not approval workflow/BPMN/projection. | Pass |
| Remaining source gaps | Exact Flowise `ItemCard`, `FlowListTable`, `FlowListMenu`, node icon endpoint rendering, full source dialogs, full Canvas/Agentflow v2 behavior, full node-by-node runtime, and final API/browser screenshot matrix are still open. | Fail |
| Verification freshness | No new scan/build/test/smoke command was run for this documentation-only refresh; the latest recorded verification remains the 22:25 implementation-slice verification. | Partial |

### Previous Implementation Refresh 22:25

Updated on 2026-06-24 22:25 +08:00 after the FlowListMenu source-action slice. This refresh records source-action movement in the Chatflows/Agentflows list page while keeping the source parity conclusion below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Options menu action map | The list page now exposes Rename, Duplicate, Export, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, Edit, and Delete from a dedicated FlowList-style Options menu. | Partial |
| Native persistence | Rename/category/root fields and `chatbotConfig` sections for starter prompts, chat feedback, and allowed domains are saved through `nativeChatflowsApi.update`; Speech To Text saves the native `speechToText` JSON field. | Partial |
| Source mismatch still open | The menu is source-like but not exact Flowise MUI `StyledMenu`, `PermissionMenuItem`, or the original dialog components. Save As Template exports a template payload but does not yet implement exact Flowise template persistence/import semantics. | Fail |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, and Flowise Studio visible-string scan passed after this slice. | Pass |

### Previous Implementation Refresh 22:19

Updated on 2026-06-24 22:19 +08:00 after the Chatflows/Agentflows source-semantics slice. This refresh records source-semantic movement in the list and empty-canvas creation paths while keeping the source parity conclusion below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Add New empty canvas | `FlowiseCanvasPage` no longer redirects when `resourceId` is absent. List Add New navigates to `/flowise/canvas` or `/flowise/agentcanvas`; unsaved canvas Save opens a save dialog, creates the native Flowise record, and redirects to the new id. | Partial |
| Duplicate/import flowData handoff | `native/views/chatflows/index.tsx` now writes `duplicatedFlowData` and opens the empty canvas for duplicate/import, closer to Flowise source than immediate persistence. The canvas consumes and clears `duplicatedFlowData` on first load. | Partial |
| Sanitized Flowise export | Export now uses a source-like `generateExportFlowData` implementation: it exports only `{ nodes, edges }`, resets selected state, drops password/file/folder inputs, and removes `FLOWISE_CREDENTIAL_ID` recursively. | Partial |
| Flow node previews | The page now parses node preview chips from `flowData.nodes`, skips sticky notes, and dedupes node names for cards and list rows. This still lacks exact Flowise image rendering because no AsterERP node-icon endpoint was present in the scanned Flowise module/backend source. | Partial |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, and Flowise Studio visible-string scan passed after this slice. | Pass |
| Remaining source mismatch | Exact source `ItemCard`, `FlowListTable`, `FlowListMenu`, node icon endpoint, Rename, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and screenshot parity are not complete. | Fail |

### Previous Implementation Refresh 22:11

Updated on 2026-06-24 22:11 +08:00 after the Chatflows/Agentflows list-page source-parity slice. This refresh records implemented list-state/action movement and the remaining source gaps identified by the read-only comparison against Flowise `packages/ui/src/views/chatflows/index.jsx` and `ui-component/button/FlowListMenu.jsx`.

| Check | Current Evidence | Status |
|---|---|---|
| List view state parity movement | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/chatflows/index.tsx` now stores card/list mode, page size, current page, and server paged query arguments in the same user-facing flow as the Flowise source page. Search clear, refresh, and bounded pagination are available in both Chatflows and Agentflows. | Partial |
| List actions parity movement | The page now has delete confirm, duplicate, import, export, success/error messages, and translated action text. These actions use existing AsterERP Flowise native CRUD APIs and browser JSON download/upload instead of the old generic resource chain. | Partial |
| Source mismatch still open | Source-level Add New, Duplicate, Export, node icon extraction, `FlowListTable`, `ItemCard`, and `FlowListMenu` are not exact. AsterERP still opens a metadata dialog for Add New, persists duplicate immediately, exports a wrapped DTO payload, and does not yet expose the full source menu actions for Rename, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, or exact source card/table visual behavior. | Fail |
| i18n and forbidden-symbol evidence | Added `flowise.common.pagination`, flow delete/copy/import messages, and copy/import suffix keys in `zh-CN/en-US`. The Flowise Studio visible-string scan returned no hits. Scoped Flowise forbidden-symbol scans returned no `WorkflowModel`, `BPMN`, projection fields, generic resource chain, or generic CRUD/table usage. | Pass |
| Verification | `npm run typecheck` and `npm run build` passed after this slice; Vite large chunk warnings remain existing warnings. No authenticated API smoke or browser screenshot parity matrix was run for this slice. | Partial |

### Current Implementation Refresh 21:53

Updated on 2026-06-24 22:03 +08:00 after the Custom MCP Server discovered-tools interaction and visual detail slice. This refresh records new source-parity movement and keeps the overall conclusion below 100%.

| Check | Current Evidence | Status |
|---|---|---|
| Custom MCP expand/collapse | AsterERP now supports multi-tool expansion plus explicit Expand all and Collapse all actions in `CustomMcpServerDialog.tsx`. This closes the previous single-tool-only expansion gap. | Pass |
| Custom MCP outer accordion and search clear | AsterERP now supports collapsing the whole Discovered Tools section and clearing the tools search query from the search row, closing two previously listed source interaction gaps. | Pass |
| Custom MCP theme icon selection | AsterERP now reads the active AsterERP theme from `useThemeStore` and prefers `dark` MCP icons only when the current theme is dark, otherwise `light`, with fallback to unthemed/light/first icon. This closes the previous fixed-light-icon gap. | Pass |
| Custom MCP search/header details | Search now includes `annotations.title`; the discovered-tools header shows filtered/total counts during search; risk chips are visible in the collapsed tool header; hint chips have Flowise-style explanatory tooltip text and compact icons; required/optional, parameter type, and enum values render as chips; `integer` type is supported; type-chip colors now follow the Flowise `TYPE_CHIP_COLOR` light/dark values. | Partial |
| i18n visible-string cleanup | The historical bare `x` close button in `native/views/chatflows/index.tsx` was replaced with `×` and a translated title. The Flowise Studio visible-string scan now returns no hits. | Pass |
| Remaining Custom MCP source gaps | Exact MUI spacing/card/chip fidelity, Tabler icon glyph parity, default-label exact text parity, and screenshot parity are still incomplete. | Fail |
| Verification | `npm run typecheck`, `npm run build`, scoped forbidden-symbol scans, and visible-string scan passed for this slice. The visible-string scan now has no hits in the Flowise Studio source scope. | Pass |

### Current Documentation Refresh 21:46

Updated on 2026-06-24 21:46 +08:00 for the current progress-documentation request. This refresh updates the audit evidence only and does not change the source-parity conclusion.

| Check | Current Evidence | Status |
|---|---|---|
| Dedicated Flowise protocol | Reran scoped forbidden-symbol scans on the Flowise frontend module, backend Flowise application/module/contracts paths, and `AiFlowise*.cs` controllers. No approval `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `ai_flowise_canvas*`, or split canvas projection symbols were found in the Flowise scope. | Pass |
| Generic resource chain removal | Reran scoped scans and found no `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, or `ai_flowise_resources` symbols in Flowise source paths. | Pass |
| AsterERP generic UI ban | Reran scoped Flowise Studio scan and found no `CrudPage`, `DataTable`, or `useCrudResource` usage. | Pass |
| i18n visible-string scan | Reran the Flowise Studio bare English JSX/placeholder/aria-label scan. The only hit remains the historical close button text `x` in `native/views/chatflows/index.tsx`; no new untranslated visible English text was found by this scan. | Partial |
| Source parity score | The progress document remains at `13/18 Pass = 72.22%`; source parity cannot be marked complete until the runtime, page/canvas, dialog, i18n, API smoke, and browser screenshot gaps are closed. | Fail |
| Final verification | No new build, test, authenticated API smoke, permission 403, workspace-boundary, or browser screenshot run was performed for this documentation-only refresh. The latest recorded build/typecheck/test evidence remains from the current implementation slice. | Blocked |

### Previous Documentation Refresh 21:43

Updated on 2026-06-24 21:43 +08:00 after the Tools Custom MCP Server detail-parity slice. This refresh records implementation progress and preserves the current source-parity conclusion: Flowise remains below 100% source parity.

| Check | Current Evidence | Status |
|---|---|---|
| Dedicated Flowise protocol | Reran scoped Flowise scans on the frontend Flowise Studio module, backend Flowise application/module/contracts paths, and `AiFlowise*.cs` controllers. No approval `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `ai_flowise_canvas*`, or split canvas projection symbols were found in the Flowise scope. | Pass |
| Generic resource chain removal | Reran scoped Flowise scans and found no `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, or `ai_flowise_resources` symbols. | Pass |
| AsterERP generic UI ban | Reran scoped Flowise Studio scan and found no `CrudPage`, `DataTable`, or `useCrudResource` usage. | Pass |
| i18n visible-string scan | Reran the Flowise Studio bare English JSX/placeholder/aria-label scan. The only hit is the historical close button text `x` in `native/views/chatflows/index.tsx`; no additional untranslated visible English text was found by this scan. | Partial |
| MCP source parity | Built-in Chatflow MCP config, first token-authenticated JSON-RPC runtime endpoint, Tools-page Custom MCP Server dedicated resource chain, source-like Custom MCP tab/dialog state flows, action-level Tools RBAC, redacted auth preservation, annotations/icons, risk chips, and enum/default parameter rendering are implemented. Full Flowise MCP SDK `StreamableHTTPServerTransport` behavior and remaining Custom MCP visual 1:1 gaps are still missing. | Partial |
| Runtime parity | Current execution chain emits Flowise-style records/events but still does not implement full Flowise node-by-node runtime semantics. | Fail |
| Page/canvas/dialog parity | Multiple source-aligned slices exist, but full Flowise source page actions, Canvas/Agentflow v2 behavior, Redux/MUI source logic, remaining dialogs, provider-backed STT/TTS, and upload negotiation are not complete. | Fail |
| Final verification | This documentation refresh reran scoped forbidden-symbol scans and a visible-string scan only. Final authenticated API smoke, permission 403, workspace boundary checks, and browser screenshot matrix remain not completed. | Blocked |

| Area | Current Evidence | Current Status |
|---|---|---|
| Source protocol boundary | Flowise source paths remain documented as dedicated `FlowiseChatFlowEntity.FlowData` protocol paths, without approval `WorkflowModel`, BPMN, `CanvasJson/FlowDataJson`, or split canvas projection symbols in the scoped Flowise module scan. | Pass |
| Generic resource removal | The audit still records removal of `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, generic `{resourceType}` routes, `flowiseStudioApi.resources`, and `createFlowiseNativeCollectionApi` from Flowise source paths. | Pass |
| Generic AsterERP UI removal | The audit still records no `CrudPage`, `DataTable`, `useCrudResource`, `FlowiseResourcePage`, or `FlowiseResourceCollectionPage` usage inside Flowise Studio source. | Pass |
| Runtime parity | Execution records Flowise-style metadata and SSE events, but does not yet implement full Flowise node-by-node runtime semantics. | Fail |
| Canvas/page parity | Canvas and menu pages have multiple source-like slices, but full Flowise `CanvasHeader`, `CanvasNode`, `NodeInputHandler`, `AddNodes`, ChatPopUp, agentflow v2, source page actions, Redux/MUI behavior, and remaining dialogs are not complete. | Fail |
| Configuration parity | RateLimit, AllowedDomains, OverrideConfig, StarterPrompts, FollowUpPrompts, ChatFeedback, Leads, FileUpload, PostProcessing, SpeechToText, TextToSpeech, Chatflow built-in MCP Server config/token API, first MCP runtime JSON-RPC slice, and Custom MCP Server resource/dialog detail-parity slices are implemented. Full MCP SDK Streamable HTTP parity, remaining Custom MCP visual details, upload capability negotiation, and provider-backed STT/TTS runtime remain incomplete. | Fail |
| Final verification | Current audit has build/typecheck/test evidence from earlier implementation slices, but final authenticated API smoke, permission 403, workspace boundary, and browser screenshot parity matrix are still not complete. | Blocked |

### MCP Source Parity Detail

The MCP review is based on Flowise source `ui-component/extended/McpServer.jsx`, `api/mcpserver.js`, `api/custommcpservers.js`, and the Tools Custom MCP Server dialog. It separates the built-in Chatflow MCP exposure from the Tools page Custom MCP Server resource.

| Source Feature | Required Source Fields / API | AsterERP Current State | Status |
|---|---|---|---|
| Chatflow MCP config section | UI switch `enabled`, `toolName`, `description`, read-only endpoint URL, read-only token, copy token, rotate token | Implemented in `FlowiseCanvasHeaderDialogs.tsx` with backend-loaded state, token copy, rotate action, disable action, and draft sync to native `mcpServerConfig` | Pass |
| Chatflow MCP config validation | `toolName` required, max 64, only letters/digits/underscore/hyphen; `description` required when enabled | Enforced in `FlowiseMcpServerService` and mirrored in the frontend Configuration section | Pass |
| Chatflow MCP persistence API | `GET/POST/PUT/DELETE /api/v1/mcp-server/:id`, `POST /api/v1/mcp-server/:id/refresh` | Implemented as AsterERP Flowise endpoints under `/api/ai/flowise/mcp-server/{id}` plus `/refresh`, with RBAC/workspace data filters, audit logs, and backend-generated 32-character hex tokens | Pass |
| Chatflow MCP runtime | `/api/v1/mcp/:chatflowId` validates Bearer token from `mcpServerConfig`, registers MCP tool using `toolName`/`description`, and runs the Flowise flow | Implemented `/api/v1/mcp/{chatflowId}` with Bearer token extraction, fixed-time token comparison, `initialize`, `tools/list`, `tools/call`, stateless DELETE 405, and tool execution through AsterERP Flowise execution using the Chatflow tenant/app/owner fields. Full Flowise MCP SDK `StreamableHTTPServerTransport` behavior is still incomplete. | Partial |
| Custom MCP Server resource | Independent Tools page resource with `name`, `serverUrl`, `iconSrc`, `color`, `authType`, `authConfig.headers`, `status`, discovered `tools`, `toolCount`, authorize and tools endpoints | Implemented dedicated entity/table/index/data filters, service, controller, encrypted auth config, remote `tools/list` discovery, `GET /tools`, frontend typed API, source-like Tools `Custom Tools` / `Custom MCP Servers` tab structure, standalone `CustomMcpServerDialog`, ADD default edit mode, EDIT read-only/detail mode, detail fetch before edit, create-then-authorize, save-then-reconnect, headers key/value editor, URL validation, discovered tool search, parameter expansion, `tools:create/update/delete` permission split, masked header/token preservation and partial-mask rejection, annotations risk chips, tool icon rendering, enum/default display, and full build/test verification. Remaining source gaps: exact MUI visual fidelity, theme-aware dark icon selection, expand-all/collapse-all controls, and browser screenshot parity. | Partial |

## Latest Actual Progress Rescan

Updated on 2026-06-24. This rescan did not change implementation files; it refreshed the actual progress evidence and keeps the completion status below 100%.

### Scope Correction

- Valid forbidden-symbol scan scope is the Flowise module itself:
  - `frontend/AsterERP.Web/src/features/flowise-studio`
  - `backend/AsterERP.Api/Application/Ai/Flowise`
  - `backend/AsterERP.Api/Modules/Ai/Flowise`
  - `backend/AsterERP.Api/Controllers/AiFlowise*.cs`
  - `backend/AsterERP.Contracts/Ai/Flowise`
- A broader controller scan can find `backend/AsterERP.Api/Controllers/WorkflowModelsController.cs`, but that is the existing non-Flowise approval/workflow controller and is not evidence that Flowise reused WorkflowModel/BPMN.

### Rescan Evidence

| Evidence | Result | Status |
|---|---|---|
| Flowise protocol forbidden symbols | No matches for `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `ai_flowise_canvas*`, or split canvas projection symbols inside the Flowise module scan scope | Pass |
| Generic Flowise resource chain | No matches for `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, or `ai_flowise_resources` inside the Flowise module scan scope | Pass |
| AsterERP generic UI usage | No matches for `CrudPage`, `DataTable`, or `useCrudResource` inside `features/flowise-studio` | Pass |
| Flowise source ChatPopUp baseline | Source contains `StarterPromptsCard`, `ChatInputHistory`, `ValidationPopUp`, `audio-recording`, and TTS/STT branches that are not fully implemented in AsterERP | Fail |
| Flowise source Configuration baseline | Source contains `ChatflowConfigurationDialog` sections for `RateLimit`, `AllowedDomains`, `OverrideConfig`, `StarterPrompts`, `SpeechToText`, and `TextToSpeech`; current AsterERP still has simplified JSON-field handling for speech sections | Fail |
| ChatPopUp interaction slice | AsterERP now reads `chatbotConfig.starterPrompts`, persists local input history with ArrowUp/ArrowDown navigation, exposes a ChatPopUp validation popup, records audio through browser `MediaRecorder` into Flowise `uploads`, and plays assistant messages through browser speech synthesis when `textToSpeech` config is enabled | Partial |

## Latest Resource Migration Audit

Updated on 2026-06-24.

### Strong Typed Resource Coverage

| Flowise Area | Backend Root | Service/API Root | Frontend API Root | Status |
|---|---|---|---|---|
| Tools | `FlowiseToolEntity` | `FlowiseToolService` via `AiFlowiseResourcesController` | `flowiseConfigurationResourcesApi.tools` | Pass |
| Credentials | `FlowiseCredentialEntity` | `FlowiseCredentialService` with reveal audit | `flowiseConfigurationResourcesApi.credentials` | Pass |
| Variables | `FlowiseVariableEntity` | `FlowiseVariableService` with reveal audit | `flowiseConfigurationResourcesApi.variables` | Pass |
| API Keys | `FlowiseApiKeyEntity` | `FlowiseApiKeyService` with one-time plaintext | `flowiseConfigurationResourcesApi.apiKeys` | Pass |
| Assistants | `FlowiseAssistantEntity` | `FlowiseAssistantService` | `flowiseNativeResourcesApi.assistants` | Pass |
| Marketplaces | `FlowiseMarketplaceTemplateEntity` | `FlowiseMarketplaceService` | `flowiseNativeResourcesApi.marketplaces` | Pass |
| Document Stores | `FlowiseDocumentStoreEntity` | `FlowiseDocumentStoreService` root methods | `documentStoresApi` | Pass |
| Datasets | `FlowiseDatasetEntity` | `FlowiseEvaluationService` dataset root methods | `evaluationsApi.datasets` | Pass |
| Evaluators | `FlowiseEvaluatorEntity` | `FlowiseEvaluationService` evaluator root methods | `evaluationsApi.evaluators` | Pass |
| Evaluations | `FlowiseEvaluationEntity` | `FlowiseEvaluationService` evaluation root methods | `evaluationsApi.evaluations` | Pass |
| SSO Config | `FlowiseSsoConfigEntity` | `FlowiseManagementService` SSO methods | `managementApi.sso` | Pass |
| Roles | `FlowiseRoleEntity` | `FlowiseManagementService` role methods | `managementApi.roles` | Pass |
| Users | `FlowiseUserEntity` | `FlowiseManagementService` user methods | `managementApi.users` | Pass |
| Login Activity | `FlowiseLoginActivityEntity` | `FlowiseManagementService` login activity methods | `managementApi.loginActivityResources` | Pass |
| Logs | `FlowiseAuditLogEntity` | `FlowiseManagementService` log methods | `managementApi.logResources` / `managementApi.logs` | Pass |
| Account Settings | `FlowiseAccountSettingEntity` | `FlowiseManagementService` account methods | `managementApi.account` | Pass |

### Removed Generic Resource References

The generic resource chain has been removed from source code. Historical log files may still contain old stack traces, but application source has no remaining hits.

| File / Symbol | Previous Use | Current State |
|---|---|---|
| `backend/AsterERP.Api/Controllers/AiFlowiseController.cs` generic `{resourceType}` routes | Exposed generic resource CRUD/reveal/import/export | Removed; replaced by explicit management/log routes |
| `backend/AsterERP.Api/Application/Ai/Flowise/IFlowiseResourceService.cs` | Generic resource contract | Deleted |
| `backend/AsterERP.Api/Application/Ai/Flowise/FlowiseResourceService.cs` | Generic resource persistence and mixed management logic | Deleted |
| `backend/AsterERP.Api/Modules/Ai/Flowise/FlowiseResourceEntity.cs` | Generic resource table entity | Deleted |
| `backend/AsterERP.Api/Application/Ai/Flowise/FlowiseResourceCatalog.cs` and `FlowiseResourceTypeDescriptor.cs` | Generic route/resource resolution | Deleted |
| `frontend/AsterERP.Web/src/features/flowise-studio/native/views/common/FlowiseNativeCollectionSurface.tsx` | Exported `createFlowiseNativeCollectionApi` backed by `flowiseStudioApi.resources.*` | Factory removed; surface now accepts explicit APIs only |
| `frontend/AsterERP.Web/src/features/flowise-studio/api/flowiseStudio.api.ts` | Exposed `resources.*` generic methods | Generic resources object removed |

### Current Verification Commands

| Command / Scan | Status |
|---|---|
| `dotnet build AsterERP.sln --no-restore` | Pass |
| `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` | Pass, 88/88 |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass |
| Scan for `createFlowiseNativeCollectionApi` in Document Stores/Datasets/Evaluators/Evaluations pages | Pass; no longer used |
| Scan for `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, generic `{resourceType}` routes, `flowiseStudioApi.resources`, `createFlowiseNativeCollectionApi`, and `ai_flowise_resources` in backend/frontend source paths | Pass; no source hits |

## No Projection Evidence

- Removed:
  - `backend/AsterERP.Api/Modules/Ai/Flowise/FlowiseCanvasEntity.cs`
  - `backend/AsterERP.Api/Modules/Ai/Flowise/FlowiseCanvasNodeEntity.cs`
  - `backend/AsterERP.Api/Modules/Ai/Flowise/FlowiseCanvasEdgeEntity.cs`
- Removed contract types:
  - `FlowiseCanvasNodeDto`
  - `FlowiseCanvasNodeUpsertRequest`
  - `FlowiseCanvasEdgeDto`
  - `FlowiseCanvasEdgeUpsertRequest`
- Removed schema initialization for:
  - `ai_flowise_canvases`
  - `ai_flowise_canvas_nodes`
  - `ai_flowise_canvas_edges`
- Main canvas read/write path:
  - reads `FlowiseChatFlowEntity.FlowData`
  - writes `FlowiseChatFlowEntity.FlowData`
  - accepts one canvas payload field: `FlowData`
  - returns one canvas payload field: `FlowData`
  - validates Flowise native `{ nodes, edges, viewport }` JSON without persisting a secondary projection
- Current grep evidence in Flowise code paths:
  - no `CanvasJson`
  - no `FlowDataJson`
  - no `canvasJson`
  - no `flowDataJson`

## No Generic CRUD Evidence

- Removed:
  - `frontend/AsterERP.Web/src/features/flowise-studio/pages/FlowiseResourcePage.tsx`
  - `frontend/AsterERP.Web/src/features/flowise-studio/pages/FlowiseFlowListPage.tsx`
  - `frontend/AsterERP.Web/src/features/flowise-studio/components/FlowiseResourceEditor.tsx`
  - `frontend/AsterERP.Web/src/features/flowise-studio/components/FlowiseOverviewStrip.tsx`
  - `frontend/AsterERP.Web/src/features/flowise-studio/pages/flowisePageConfigs.ts`
  - `frontend/AsterERP.Web/src/features/flowise-studio/native/views/common/FlowiseResourceCollectionPage.tsx`
- Added native Flowise UI primitives:
  - `native/ui-component/cards/MainCard.tsx`
  - `native/ui-component/cards/ItemCard.tsx`
  - `native/ui-component/table/FlowListTable.tsx`
  - `native/ui-component/dialog/NativeDialog.tsx`
- Current grep evidence:
  - no `CrudPage`
  - no `DataTable`
  - no `useCrudResource`
  - no `FlowiseResourcePage`
  - no `FlowiseResourceCollectionPage`
  - no `FlowiseFlowListPage`
  - no `FlowiseResourceEditor`
  - no `FlowiseOverviewStrip`

## Current I18n Evidence

- Added missing `flowise.*` keys for:
  - native resource page titles, descriptions, edit/create dialogs, and empty states
  - common native actions such as open, edit, previous, next, close, reveal, delete
  - account fields, assistant detail headings, flow data, chat question placeholder, source documents, and iteration label
- Current grep evidence in `frontend/AsterERP.Web/src/features/flowise-studio`:
  - no matched bare English JSX text
  - no matched bare English placeholder
  - no matched bare English aria label

## Current Canvas Interaction Evidence

- Implemented in the current AsterERP Flowise canvas path:
  - node toolbar `info` opens the inspector info tab
  - node toolbar `duplicate` creates an offset copy with a stable Flowise node id
  - node toolbar `delete` removes the node and all connected edges
  - edge delete button marks the canvas dirty through the page state
  - Sticky Note text edits write to `node.data.config.text`
  - inspector separates normal params from Additional Params
  - AddNodes supports category tabs, fuzzy search relevance, category expand/collapse, draggable nodes, and empty result state
  - ConfigInput supports multiOptions, file input persisted into `flowData`, password reveal, JSON blur validation, and standard date/time/number/text controls
  - CanvasHeader buttons open dedicated dialogs for API Code, Configuration, Export Template, Messages, Leads, Schedule, and Webhook
  - Upsert and Upsert History buttons appear only when current native `flowData.nodes` contains a Document Store or Vector Store upsert target
  - API Code and Webhook dialogs derive endpoints from the current Flowise resource id and support copy actions
  - Schedule dialog reads native `flowData` and reports whether a `startAgentflow` schedule input is configured
  - Export Template dialog downloads the current native `flowData` JSON with template metadata
  - Messages dialog queries `/api/ai/flowise/prediction/messages` and renders real `ai_flowise_chat_messages` records with feedback and source document counts
  - Leads dialog queries `/api/ai/flowise/prediction/leads` and renders real `ai_flowise_leads` contact JSON records
  - Configuration dialog reads the current native Chatflow detail and saves native config fields through `/api/ai/flowise/chatflows/{id}` or `/api/ai/flowise/agentflows/{id}`
  - Configuration dialog now exposes sectionized editors for Flowise-native `apiConfig.rateLimit`, `chatbotConfig.allowedOrigins`, `chatbotConfig.allowedOriginsError`, `chatbotConfig.starterPrompts`, `chatbotConfig.followUpPrompts`, top-level `followUpPrompts`, `chatbotConfig.chatFeedback`, `chatbotConfig.leads`, `chatbotConfig.fullFileUpload`, `chatbotConfig.postProcessing`, `apiConfig.overrideConfig`, `speechToText`, and `textToSpeech`, while retaining Advanced JSON panels for native payload roundtrip and recovery
  - Share dialog queries and saves shared workspaces through `/api/ai/flowise/shared-workspaces/{itemId}` with `itemType` and `workspaceIds`, matching Flowise source dialog semantics
  - Upsert dialog posts native `flowData`, `storeId`, `loaderId`, `chatflowId`, and `replaceExisting` to `/api/ai/flowise/document-stores/{storeId}/upsert`
  - Upsert History dialog queries `/api/ai/flowise/document-stores/{storeId}/upsert-history` and renders processed/added/replaced/skipped counts
  - ChatPopUp-style floating button opens/closes an internal chat card, exposes clear and expand controls, and uses a modal expanded chat view
  - ChatPopUp stores a per-resource `chatId`, reloads message history through `/api/ai/flowise/prediction/messages`, sends predictions through `/api/ai/flowise/prediction`, saves feedback through `/api/ai/flowise/prediction/feedback`, saves leads through `/api/ai/flowise/prediction/lead`, and clears history through `/api/ai/flowise/prediction/messages/clear`
  - ChatPopUp streams predictions through `/api/ai/flowise/prediction/stream`, parses Flowise source-style `data: {"event": "...", "data": ...}` SSE payloads, appends `token` events into a live assistant bubble, and refreshes persisted messages after `end`
  - ChatPopUp now supports abort/stop for an in-flight prediction request through the AsterERP HTTP client abort signal
  - ChatPopUp supports Flowise `uploads` request payloads and `fileUploads` message playback for image, audio, and full-file upload previews
  - ChatMessage rendering now includes Flowise-style Agent Reasoning cards, Executed Data cards, Used Tools details, Artifacts JSON details, and Source Documents details
  - ChatPopUp reads `chatbotConfig.starterPrompts` and renders source-style starter prompt buttons before the first message
  - ChatPopUp persists local input history per resource and supports Flowise-style ArrowUp/ArrowDown question recall
  - ChatPopUp exposes a validation popup wired to current canvas validation results and the canvas validation action
  - ChatPopUp gates microphone recording from the native `speechToText` config and converts the recorded browser audio into a Flowise `uploads` audio payload
  - ChatPopUp gates assistant speech playback from the native `textToSpeech` config and provides play/stop controls on assistant bubbles
  - dirty canvas registers a browser `beforeunload` guard
- Remaining gap:
  - these are source-aligned interactions, not a full direct migration of Flowise `CanvasHeader.jsx`, `CanvasNode.jsx`, `NodeInputHandler.jsx`, `AddNodes.jsx`, and `ChatPopUp.jsx`
  - Flowise source `Security` is covered by the current `RateLimit`, `AllowedDomains`, and `OverrideConfig` sections; full Flowise Chatflow Configuration source internals are still incomplete beyond the current `RateLimit`, `AllowedDomains`, `OverrideConfig`, `StarterPrompts`, `FollowUpPrompts`, `ChatFeedback`, `Leads`, `FileUpload`, `PostProcessing`, `SpeechToText`, `TextToSpeech`, Chatflow built-in MCP Server config/token API, first MCP runtime JSON-RPC slice, and Custom MCP Server resource chain; upload capability negotiation, full MCP SDK Streamable HTTP parity, exact Custom MCP Server source dialog behavior, and full runtime vector upsert semantics are not yet complete
  - `SpeechToText` and `TextToSpeech` are only frontend-gated browser interaction slices today; they are not yet Flowise provider-backed STT/TTS runtime chains
  - Flowise source `ChatInputHistory`, `ValidationPopUp`, `audio-recording`, and `StarterPromptsCard` are partially covered in AsterERP but not direct source component migrations

## Current Header Dialog Evidence

- Added backend query API:
  - `GET /api/ai/flowise/prediction/messages`
  - `GET /api/ai/flowise/prediction/leads`
- Added backend share API:
  - `GET /api/ai/flowise/shared-workspaces/{itemId}`
  - `PUT /api/ai/flowise/shared-workspaces/{itemId}`
- Added backend document-store upsert API:
  - `POST /api/ai/flowise/document-stores/{storeId}/upsert`
  - `GET /api/ai/flowise/document-stores/{storeId}/upsert-history`
- Added backend clear-chat API:
  - `POST /api/ai/flowise/prediction/messages/clear`
- Added backend streaming API:
  - `POST /api/ai/flowise/prediction/stream`
- Added persistence:
  - `backend/AsterERP.Api/Modules/Ai/Flowise/FlowiseSharedWorkspaceEntity.cs`
  - `backend/AsterERP.Api/Modules/Ai/Flowise/FlowiseDocumentStoreUpsertHistoryEntity.cs`
  - `ai_flowise_shared_workspaces`
  - `ai_flowise_document_store_upsert_history`
  - unique index on `TenantId + AppCode + ItemId + ItemType + SharedWorkspaceId`
  - query index on `IsDeleted + TenantId + AppCode + StoreId + CreatedTime`
- Added contracts:
  - `FlowisePredictionListQuery`
  - `FlowiseChatMessageDto.Feedback`
  - `FlowiseSharedWorkspaceDto`
  - `FlowiseShareWorkspacesRequest`
  - `FlowiseDocumentStoreUpsertRequest`
  - `FlowiseDocumentStoreUpsertHistoryDto`
  - `FlowiseChatClearRequest`
  - `FlowiseAgentReasoningDto`
  - `FlowiseAgentExecutedNodeDto`
  - `FlowiseUsedToolDto`
  - `FlowiseFileUploadDto`
- Added service methods:
  - `IFlowisePredictionService.GetMessagesAsync`
  - `IFlowisePredictionService.GetLeadsAsync`
  - `IFlowiseManagementService.GetSharedWorkspacesAsync`
  - `IFlowiseManagementService.SetSharedWorkspacesAsync`
  - `IFlowiseDocumentStoreService.UpsertAsync`
  - `IFlowiseDocumentStoreService.GetUpsertHistoryAsync`
  - `IFlowisePredictionService.ClearChatAsync`
  - `IFlowisePredictionService.StreamAsync`
  - `IFlowiseExecutionService.StreamAsync`
- Added message persistence fields:
  - `AgentReasoningJson`
  - `AgentExecutedDataJson`
  - `UsedToolsJson`
  - `ArtifactsJson`
  - `FileUploadsJson`
- Added:
  - `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeaderDialogs.tsx`
- Modified:
  - `frontend/AsterERP.Web/src/features/flowise-studio/hooks/useFlowiseCanvas.ts`
  - `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeader.tsx`
  - `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasPage.tsx`
  - `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-dialogs.css`
- Implemented dialog entries:
  - API Code
  - Configuration
  - Export Template
  - Messages
  - Leads
  - Schedule
  - Webhook
  - Share
  - Upsert
  - Upsert History
  - ChatPopUp
  - ChatExpandDialog equivalent
  - Flowise SSE token streaming equivalent
  - AgentReasoningCard equivalent
  - AgentExecutedDataCard equivalent
  - File upload preview and playback equivalent
- Implemented Configuration fields:
  - `name`
  - `category`
  - `workspaceId`
  - `apikeyid`
  - `deployed`
  - `isPublic`
  - `chatbotConfig`
  - `apiConfig`
  - `analytic`
  - `speechToText`
  - `textToSpeech`
  - `followUpPrompts`
  - `mcpServerConfig`
  - `webhookSecret`
- Implemented Configuration sectionized editors:
  - `apiConfig.rateLimit.status`
  - `apiConfig.rateLimit.limitMax`
  - `apiConfig.rateLimit.limitDuration`
  - `apiConfig.rateLimit.limitMsg`
  - `chatbotConfig.allowedOrigins`
  - `chatbotConfig.allowedOriginsError`
  - `chatbotConfig.starterPrompts`
  - `chatbotConfig.followUpPrompts.status`
  - `followUpPrompts.status`
  - `followUpPrompts.selectedProvider`
  - `followUpPrompts.<provider>.credentialId`
  - `followUpPrompts.<provider>.baseUrl`
  - `followUpPrompts.<provider>.modelName`
  - `followUpPrompts.<provider>.prompt`
  - `followUpPrompts.<provider>.temperature`
  - `apiConfig.overrideConfig.status`
  - `apiConfig.overrideConfig.nodes`
  - `apiConfig.overrideConfig.variables`
  - `chatbotConfig.chatFeedback.status`
  - `chatbotConfig.leads.status`
  - `chatbotConfig.leads.title`
  - `chatbotConfig.leads.successMessage`
  - `chatbotConfig.leads.name`
  - `chatbotConfig.leads.email`
  - `chatbotConfig.leads.phone`
  - `chatbotConfig.fullFileUpload.status`
  - `chatbotConfig.fullFileUpload.allowedUploadFileTypes`
  - `chatbotConfig.fullFileUpload.pdfFile.usage`
  - `chatbotConfig.postProcessing.enabled`
  - `chatbotConfig.postProcessing.customFunction`
  - selected `speechToText` provider status and basic credential/model/voice/autoplay fields
  - selected `textToSpeech` provider status and basic credential/model/voice/autoplay fields
  - `mcpServerConfig.enabled`
  - `mcpServerConfig.toolName`
  - `mcpServerConfig.description`
  - `mcpServerConfig.token`
  - derived MCP endpoint path `/api/v1/mcp/{chatflowId}`
- Verification:
  - `dotnet build AsterERP.sln --no-restore` passed
  - `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` passed 88/88
  - `npm run typecheck` passed
  - `npm run build` passed
  - no matched `CanvasJson/FlowDataJson`, `ai_flowise_canvas*`, WorkflowModel, BPMN, or compatibility projection symbols in Flowise backend/frontend code paths
  - no matched `CrudPage/DataTable/useCrudResource/FlowiseResourcePage` symbols in `features/flowise-studio`
  - no matched bare English JSX text, placeholder, or aria label in `features/flowise-studio`
- Latest frontend verification after ChatPopUp starter prompt/input history/validation/audio/TTS slice:
  - `cd frontend/AsterERP.Web && npm run typecheck` passed
  - `cd frontend/AsterERP.Web && npm run build` passed; existing large chunk warnings only
- Latest frontend verification after Chatflow Configuration sectionized editor slice:
  - `cd frontend/AsterERP.Web && npm run typecheck` passed
  - `cd frontend/AsterERP.Web && npm run build` passed; existing large chunk warnings only
  - directed Flowise forbidden-symbol scans found no `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, `ai_flowise_resources`, `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `CrudPage`, `DataTable`, or `useCrudResource` in the scoped Flowise frontend/backend paths
- Latest frontend verification after Configuration feedback/leads/file-upload/post-processing slice:
  - `cd frontend/AsterERP.Web && npm run typecheck` passed
  - `cd frontend/AsterERP.Web && npm run build` passed; existing large chunk warnings only
  - directed Flowise forbidden-symbol scans found no generic resource, WorkflowModel/BPMN/projection, or AsterERP CRUD/table symbols in the scoped Flowise paths
- Latest frontend verification after Follow-up Prompts configuration slice:
  - `cd frontend/AsterERP.Web && npm run typecheck` passed
  - `cd frontend/AsterERP.Web && npm run build` passed; existing large chunk warnings only
  - directed Flowise forbidden-symbol scans found no generic resource, WorkflowModel/BPMN/projection, or AsterERP CRUD/table symbols in the scoped Flowise paths
- Remaining gap:
  - these dialogs do not yet equal the full Flowise source dialog set or source runtime data chain
  - Configuration still needs source-equivalent MCP, upload capability negotiation, and provider-backed STT/TTS runtime behavior
## Current Implementation Refresh: Allowed Domains Section

Updated on 2026-06-25 after migrating the Configuration dialog Allowed Domains controls.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `AllowedDomainsDialog` / Configuration allowed domains controls | `AllowedDomainsSection` now uses MUI `Button`, `IconButton`, and `TextField` for add/remove/domain/error-message controls while preserving native `chatbotConfig.allowedOrigins` and `allowedOriginsError` persistence. | Targeted scan confirms raw controls are removed from `AllowedDomainsSection`; the next remaining raw-control block starts at `StarterPromptsSection`. | Improved, not full parity |

This update does not change the overall parity result. The project still cannot claim 100% because remaining source gaps include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: Starter Prompts Section

Updated on 2026-06-25 after migrating the Configuration dialog Starter Prompts controls.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `StarterPromptsDialog` / Configuration starter prompts controls | `StarterPromptsSection` now uses MUI `Button`, `IconButton`, and `TextField` for add/remove/edit prompt rows while preserving native starter prompt JSON roundtrip through `readStarterPromptRows` and `writeStarterPromptRows`. | Targeted scan confirms raw controls are removed from `StarterPromptsSection`; the next remaining raw-control block starts at `FollowUpPromptsSection`. | Improved, not full parity |

This update does not change the overall parity result. Remaining gaps still include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: Follow-up Prompts Section

Updated on 2026-06-25 after migrating the Configuration dialog Follow-up Prompts controls.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `FollowUpPromptsDialog` / Configuration follow-up prompt provider controls | `FollowUpPromptsSection` now uses MUI `FormControlLabel`, `Checkbox`, `TextField`, and `MenuItem` for status, provider, credential/base URL, model, temperature, and prompt controls while preserving native `followUpPrompts` JSON roundtrip and validation semantics. | Targeted scan confirms raw controls are removed from `FollowUpPromptsSection`; the next remaining raw-control block starts at `ChatFeedbackSection`. | Improved, not full parity |

This update does not change the overall parity result. Remaining gaps still include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: Chat Feedback Section

Updated on 2026-06-25 after migrating the Configuration dialog Chat Feedback control.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `ChatFeedbackDialog` / Configuration chat feedback switch | `ChatFeedbackSection` now uses MUI `FormControlLabel` and `Checkbox` for the status switch while preserving native `chatbotConfig.chatFeedback.status` persistence. | Targeted scan confirms raw controls are removed from `ChatFeedbackSection`; the next remaining raw-control block starts at `LeadsSection`. | Improved, not full parity |

This update does not change the overall parity result. Remaining gaps still include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: Leads Section

Updated on 2026-06-25 after migrating the Configuration dialog Leads controls.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `LeadsDialog` / Configuration lead capture controls | `LeadsSection` now uses MUI `FormControlLabel`, `Checkbox`, and multiline `TextField` for status, title, success message, and lead field toggles while preserving native `chatbotConfig.leads` persistence through `normalizeLeadsConfig`. | Targeted scan confirms raw controls are removed from `LeadsSection`; the next remaining raw-control block starts at `FileUploadSection`. | Improved, not full parity |

This update does not change the overall parity result. Remaining gaps still include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: File Upload Section

Updated on 2026-06-25 after migrating the Configuration dialog File Upload controls.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `FileUpload` / Configuration upload controls | `FileUploadSection` now uses MUI `FormControlLabel`, `Checkbox`, `TextField`, and `MenuItem` for status, allowed file types, and PDF processing mode while preserving native `chatbotConfig.fullFileUpload` persistence through `normalizeFileUploadConfig`. | Targeted scan confirms raw controls are removed from `FileUploadSection`; the next remaining raw-control block starts at `PostProcessingSection`. | Improved, not full parity |

This update does not change the overall parity result. Remaining gaps still include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, upload capability negotiation, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: Post Processing Section

Updated on 2026-06-25 after migrating the Configuration dialog Post Processing controls.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `PostProcessing` / Configuration custom post-processing controls | `PostProcessingSection` now uses MUI `FormControlLabel`, `Checkbox`, and multiline `TextField` for enabled state and custom JS function while preserving native `chatbotConfig.postProcessing` persistence through `normalizePostProcessingConfig`. | Targeted scan confirms raw controls are removed from `PostProcessingSection`; the next remaining raw-control block starts at `OverrideConfigSection`. | Improved, not full parity |

This update does not change the overall parity result. Remaining gaps still include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, upload capability negotiation, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: Override Config Section

Updated on 2026-06-25 after migrating the Configuration dialog Override Config status control.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `OverrideConfig` / Configuration override controls | `OverrideConfigSection` now uses MUI `FormControlLabel` and `Checkbox` for the status switch while preserving native `apiConfig.overrideConfig.status`, `nodes`, and `variables` persistence through `normalizeOverrideConfig`. | Targeted scan confirms raw checkbox controls are removed from `OverrideConfigSection`; the next remaining raw-control blocks start at `McpServerSection` and `ProviderConfigSection`. | Improved, not full parity |

This update does not change the overall parity result. Remaining gaps still include exact Flowise dialog parity across the rest of Configuration, agentflow v2 canvas, upload capability negotiation, full ChatPopUp/provider runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: MCP Server and Provider Config Sections

Updated on 2026-06-25 after migrating the remaining raw controls in the Configuration dialog MCP Server and STT/TTS provider sections.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `McpServer` / Configuration MCP controls | `McpServerSection` now uses MUI `FormControlLabel`, `Checkbox`, `TextField`, and `Button` for enabled state, tool name, description, endpoint, token, save, copy, and rotate actions while preserving native `mcpServerConfig` persistence and token callbacks. | Targeted scan confirms raw controls are removed from `McpServerSection`. | Improved, not full parity |
| `SpeechToText` / `TextToSpeech` provider controls | `ProviderConfigSection` now uses MUI `TextField`, `MenuItem`, `FormControlLabel`, and `Checkbox` for provider selection, credential, model, voice, and auto-play controls while preserving provider JSON roundtrip semantics. | Targeted scan confirms raw controls are removed from `ProviderConfigSection`. | Improved, not full parity |
| Header dialog raw controls | `FlowiseCanvasHeaderDialogs.tsx` no longer contains raw `input`, `textarea`, `select`, or `button` tags. | Repository scan shows the remaining `features/flowise-studio` raw controls now start in `FlowiseChatTestPanel.tsx`. | Pass for this file |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatPopUp parity, agentflow v2 canvas, upload capability negotiation, provider-backed STT/TTS runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: ChatPopUp Shell and Composer

Updated on 2026-06-25 after migrating the first visible-control group in the Flowise ChatPopUp panel.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `ChatPopUp` launcher and side tools | Popup launcher, clear, expand, validation, and close controls now use MUI `Fab` and `IconButton` while preserving existing chat open, clear, validation, expand, and close state transitions. | Targeted scan confirms these shell controls no longer render raw `button` tags. | Improved, not full parity |
| `ChatPopUp` expanded header | Expanded chat dialog clear, validation, and close controls now use MUI `Button` and `IconButton` with unchanged callback payloads. | Typecheck and build pass after the migration. | Improved, not full parity |
| `ChatInput` / composer | Question input now uses MUI multiline `TextField` and the upload, recording, stop, and run actions now use MUI `Button`; input history still reads the same textarea via `inputRef`. | Targeted scan confirms the raw composer textarea and visible action buttons were removed. | Improved, not full parity |
| Remaining ChatPopUp mismatch | Feedback buttons, feedback reason input, starter prompt buttons, validation close, file upload remove, lead capture inputs/action, and exact source message-card visual parity remain incomplete. | `rg -n "<input|<textarea|<select|<button" frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseChatTestPanel.tsx` still reports remaining lower-level raw controls. | Partial |

This update does not change the overall parity result. Remaining gaps still include full Flowise ChatPopUp component parity, agentflow v2 canvas, provider-backed STT/TTS runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: ChatPopUp Message Actions and Lead Capture

Updated on 2026-06-25 after migrating the remaining visible lower-level controls in the Flowise ChatPopUp panel.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `ChatMessage` feedback and speech actions | Assistant message feedback up/down and TTS play/stop controls now use MUI `Button`, preserving feedback mutation and speech toggle callbacks. | Targeted scan confirms these controls no longer render raw `button` tags. | Improved, not full parity |
| Feedback reason input | Feedback reason now uses MUI `TextField`, preserving the same `onReasonChange` state update. | Typecheck and build pass. | Improved, not full parity |
| `StarterPromptsCard` prompt actions | Starter prompt chips now use MUI `Button`, preserving prompt submission payloads. | Targeted scan confirms starter prompt raw buttons were removed. | Improved, not full parity |
| Validation popup and upload remove | Validation close and file upload remove actions now use MUI `IconButton`. | Targeted scan confirms those raw buttons were removed. | Improved, not full parity |
| Lead capture | Lead name/email/phone and submit controls now use MUI `TextField` and `Button`, preserving lead draft updates and the email/phone disabled rule. | Targeted scan confirms lead capture raw controls were removed. | Improved, not full parity |
| Remaining raw control exception | `FlowiseChatTestPanel.tsx` only keeps the hidden file picker input needed for native browser file selection. | `rg -n "<input|<textarea|<select|<button" frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseChatTestPanel.tsx` reports only `flowise-chat-file-input`. | Pass with documented exception |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatPopUp message-card visuals, deeper event-card rendering parity, agentflow v2 canvas, provider-backed STT/TTS runtime behavior, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: ChatPopUp Event Cards

Updated on 2026-06-25 after adding Flowise source-named ChatPopUp event-card components and routing the existing message event data through them.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `AgentReasoningCard` | Added `native/views/chatmessage/AgentReasoningCard.tsx` and renders agent reasoning events through it from `FlowiseChatTestPanel.tsx`, preserving used tools, state, artifacts, and source documents via typed render callbacks. | Source-named component exists and `npm run typecheck` passes. | Improved, not full parity |
| `AgentExecutedDataCard` | Added `native/views/chatmessage/AgentExecutedDataCard.tsx` and renders executed-node status/data through it from `FlowiseChatTestPanel.tsx`. | Source-named component exists and `npm run build` passes. | Improved, not full parity |
| `ThinkingCard` | Added `native/views/chatmessage/ThinkingCard.tsx` and uses it for the streaming assistant bubble, replacing the plain streaming `pre` with an expandable thinking-style panel. | Source-named component exists and the ChatPopUp stream path still uses the same `streamingAnswer` state. | Improved, not full parity |
| Styling and boundaries | Added focused classes in `flowise-dialogs.css`; no API client, backend runtime, projection, BPMN, or generic CRUD path was introduced. | Diff scope is limited to ChatPopUp UI files, CSS, and documentation. | Pass |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatPopUp internals, source document dialogs, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, and browser screenshot parity.

## Current Implementation Refresh: Source Document Dialog

Updated on 2026-06-25 after adding the Flowise source-named SourceDocDialog and routing ChatPopUp source document actions through it.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `SourceDocDialog` | Added `native/ui-component/dialog/SourceDocDialog.tsx` with typed props for open state, title, close event, and selected source documents. It renders document content, parsed metadata, score, source id, and JSON payload in a dedicated MUI dialog. | Source-named component exists and is imported by `FlowiseChatTestPanel.tsx`; `npm run typecheck` and `npm run build` pass. | Improved, not full parity |
| `ChatPopUp` source document action | `SourceDocuments` now accepts an `onView` event and opens `SourceDocDialog` from both normal assistant messages and agent reasoning cards. Inline expansion remains available for quick inspection. | `FlowiseChatTestPanel.tsx` routes `onViewSourceDocuments` through `ChatContent`, `ChatBubble`, `AgentReasoningList`, and `SourceDocuments`. | Improved, not full parity |
| Styling and boundaries | Added focused source document dialog classes in `styles/flowise-dialogs.css`; no backend runtime, Flowise protocol, projection, BPMN, or generic CRUD path was introduced. | Diff scope is limited to ChatPopUp UI files, CSS, and documentation. | Pass |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatPopUp internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: ChatExpandDialog

Updated on 2026-06-25 after adding the Flowise source-named ChatExpandDialog component and replacing the inline expanded ChatPopUp overlay.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `views/chatmessage/ChatExpandDialog` | Added `native/views/chatmessage/ChatExpandDialog.tsx` with typed `open`, `title`, `clearText`, optional validation action, close action, and children content contract. The component uses MUI Dialog/Title/Content like the Flowise source boundary and contains no API calls. | Source-named component exists and `FlowiseChatTestPanel.tsx` imports it for the expanded chat path. | Improved, not full parity |
| Expanded `ChatPopUp` display | `FlowiseChatTestPanel.tsx` no longer builds a custom backdrop/section for expanded chat; it passes the existing `ChatContent` into `ChatExpandDialog` and preserves clear, validation, and close callbacks. | `npm run typecheck` and `npm run build` pass after the extraction. | Improved, not full parity |
| Styling and boundaries | `flowise-dialogs.css` now styles MUI Dialog paper/title/content for expanded chat instead of the previous hand-built overlay container. | Diff scope remains frontend ChatPopUp structure, CSS, and documentation; no backend runtime, Flowise protocol, projection, BPMN, or generic CRUD path was introduced. | Pass |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: ChatMessage

Updated on 2026-06-25 after adding the Flowise source-named ChatMessage component and moving the message bubble body out of FlowiseChatTestPanel.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `views/chatmessage/ChatMessage` | Added `native/views/chatmessage/ChatMessage.tsx` with typed message, feedback, speech, translation, and render-prop contracts. It renders the user/assistant bubble, feedback actions, feedback reason input, and TTS action without API calls. | Source-named component exists and `npm run typecheck` passes. | Improved, not full parity |
| ChatPopUp message rendering | `FlowiseChatTestPanel.tsx` now renders `ChatMessage` for each message and removed the internal `ChatBubble` helper. File uploads, agent reasoning, executed data, used tools, artifacts, and source docs are still supplied by existing panel render functions. | `npm run build` passes with existing chunk warnings only. | Improved, not full parity |
| Boundaries | Request state, streaming, mutations, chat id, uploads, feedback mutations, and source document dialog state remain in `FlowiseChatTestPanel`; the new component is UI-only. | Diff scope is limited to frontend ChatPopUp structure and documentation; no backend runtime, Flowise protocol, projection, BPMN, or generic CRUD path was introduced. | Pass |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: StarterPromptsCard

Updated on 2026-06-25 after adding the Flowise source-named StarterPromptsCard component and replacing inline ChatPopUp starter prompt buttons.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `ui-component/cards/StarterPromptsCard` | Added `native/ui-component/cards/StarterPromptsCard.tsx` with typed `starterPrompts`, `isGrid`, `sx`, and `onPromptClick` props. It renders MUI Chips like the Flowise source component boundary and contains no API calls. | Source-named component exists and `npm run typecheck` passes. | Improved, not full parity |
| ChatPopUp starter prompt display | `FlowiseChatTestPanel.tsx` now maps starter prompt strings into `StarterPromptsCard` items and removed the internal `StarterPrompts` helper. | Prompt click still uses the existing `onStarterPrompt(prompt)` submission callback. | Improved, not full parity |
| Styling and boundaries | `flowise-dialogs.css` now styles starter prompt card/chip classes instead of raw buttons; no backend runtime, Flowise protocol, projection, BPMN, or generic CRUD path was introduced. | `npm run build` passes with existing chunk warnings only. | Pass |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: ValidationPopUp

Updated on 2026-06-25 after adding the Flowise source-named ValidationPopUp component and replacing the internal ChatPopUp validation function.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `views/chatmessage/ValidationPopUp` | Added `native/views/chatmessage/ValidationPopUp.tsx` with typed `validation`, `translate`, and `onClose` props. The component renders the validation checklist, empty state, issue severity classes, node/edge references, and close action without API calls. | Source-named component exists and imports `flowiseI18nKeys` rather than hardcoded keys. | Improved, not full parity |
| ChatPopUp validation display | `FlowiseChatTestPanel.tsx` now imports and renders `ValidationPopUp`; the previous internal `ValidationPopup` helper was removed from the panel file. | `npm run typecheck` and `npm run build` pass after the extraction. | Improved, not full parity |
| Boundaries | Styling continues to use existing `flowise-chat-validation-popup` classes; no backend runtime, Flowise protocol, projection, BPMN, or generic CRUD path was introduced. | Diff scope remains frontend ChatPopUp structure and documentation. | Pass |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: ChatInputHistory

Updated on 2026-06-25 after adding the Flowise source-named ChatInputHistory module and removing input-history storage helpers from FlowiseChatTestPanel.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `views/chatmessage/ChatInputHistory` | Added `native/views/chatmessage/ChatInputHistory.ts` with typed read, persist, and next/previous navigation helpers. The implementation keeps AsterERP's resource-scoped storage key while moving history behavior behind a source-named module boundary. | Source-named module exists and `npm run typecheck` passes. | Improved, not full parity |
| ChatPopUp input history | `FlowiseChatTestPanel.tsx` now imports `readChatInputHistory`, `persistChatInputHistory`, and `resolveChatInputHistoryNavigation`, and removed the local `readInputHistory` / `persistInputHistory` functions. | ArrowUp/ArrowDown still uses the same panel callback and `npm run build` passes with existing chunk warnings only. | Improved, not full parity |
| Boundaries | The new module is pure frontend storage/navigation logic and does not call APIs or alter Flowise protocol, BPMN, projection, backend runtime, or generic CRUD paths. | Diff scope is limited to frontend ChatPopUp structure and documentation. | Pass |

This update does not change the overall parity result. Remaining gaps still include exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, audio-recording source module parity, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: audio-recording

Updated on 2026-06-25 after adding the Flowise source-named `audio-recording` module and removing direct MediaRecorder session ownership from `FlowiseChatTestPanel`.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `views/chatmessage/audio-recording` | Added `native/views/chatmessage/audio-recording.ts` with typed recording support, start, stop-to-upload, and cleanup helpers. It keeps browser media lifecycle logic in the Flowise source-named module boundary. | Source-named module exists and is imported by `FlowiseChatTestPanel.tsx`. | Improved, not full parity |
| ChatPopUp audio recording | `FlowiseChatTestPanel.tsx` no longer stores `MediaRecorder`, stream, or chunk refs directly. It delegates recording lifecycle to `startAudioRecording`, `stopAudioRecording`, and `cleanupAudioRecording`, while preserving current upload insertion and error feedback. | Static scan has no remaining `mediaRecorderRef`, `mediaStreamRef`, `audioChunksRef`, `stopRecorderAsUpload`, or `cleanupRecordingStream` references in the panel. | Improved, not full parity |
| Boundaries | The new module is frontend browser media logic only and does not call backend APIs directly or alter Flowise protocol, BPMN, projection, backend runtime, or generic CRUD paths. | Diff scope is limited to frontend ChatPopUp structure and documentation. | Pass |

This update closes the previously tracked audio-recording source module structure gap, but does not change the overall parity result. Remaining gaps still include exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: text-to-speech

Updated on 2026-06-25 after adding the Flowise source-named `text-to-speech` module and removing direct browser speech synthesis ownership from `FlowiseChatTestPanel`.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `views/chatmessage` text-to-speech lifecycle | Added `native/views/chatmessage/text-to-speech.ts` with typed support detection, message playback, playback handle, and stop helpers. It keeps browser speech synthesis lifecycle logic in the Flowise chatmessage source area. | Source-named module exists and is imported by `FlowiseChatTestPanel.tsx`. | Improved, not full parity |
| ChatPopUp TTS playback | `FlowiseChatTestPanel.tsx` no longer constructs `SpeechSynthesisUtterance` or calls `window.speechSynthesis` directly. It delegates playback to `speakTextMessage`, `stopTextToSpeech`, and `supportsTextToSpeech`, while preserving current active message and error feedback behavior. | Static scan has no remaining `stopSpeech` helper or direct `SpeechSynthesisUtterance` construction in the panel. | Improved, not full parity |
| Boundaries | The new module is frontend browser media logic only and does not call backend APIs directly or alter Flowise protocol, BPMN, projection, backend runtime, or generic CRUD paths. | Diff scope is limited to frontend ChatPopUp structure and documentation. | Pass |

This update improves ChatPopUp text-to-speech source module structure, but does not change the overall parity result. Remaining gaps still include provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: AudioWaveform

Updated on 2026-06-25 after adding the Flowise source-named `AudioWaveform` component and using it for ChatPopUp audio upload previews.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `ui-component/extended/AudioWaveform` | Added `native/ui-component/extended/AudioWaveform.tsx` with typed playback props, hidden audio element, canvas waveform drawing, progress updates, click-to-seek, and optional external callbacks. | Source-named component exists and `npm run typecheck` passes. | Improved, not full parity |
| ChatPopUp uploaded audio preview | `FlowiseChatTestPanel.tsx` `FileUploads` renders `AudioWaveform` for `audio/*` uploads instead of raw `<audio controls>`. The existing upload data shape and remove action are unchanged. | Static scan has no remaining raw `<audio controls>` branch in the ChatPopUp upload preview. | Improved, not full parity |
| Styling and boundaries | `flowise-dialogs.css` defines waveform container/button/canvas styles. The component is UI-only and does not alter backend APIs, Flowise protocol, BPMN, projection, runtime execution, or generic CRUD paths. | Diff scope is limited to frontend ChatPopUp audio preview structure, CSS, and documentation. | Pass |

This update improves ChatPopUp audio preview source UI parity, but does not change the overall parity result. Remaining gaps still include provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: audio-recording Cancel and Safari Capture

Updated on 2026-06-25 after extending the AsterERP `audio-recording` module beyond file-boundary parity into Flowise source behavior for cancel and Safari recording.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| `views/chatmessage/audio-recording` cancel lifecycle | Added `cancelAudioRecording(session)` to detach recorder callbacks, stop active recording, clear captured chunks, and stop media tracks. `FlowiseChatTestPanel.tsx` now uses it on unmount. | Static scan shows the panel imports `cancelAudioRecording` and no longer calls raw cleanup directly. | Improved, not full parity |
| Safari timeslice recording | `startAudioRecording` now calls `MediaRecorder.start(1000)` for Safari user agents and `start()` otherwise, matching the Flowise source workaround for Safari audio chunk production. | Static scan shows `recorder.start(1000)` and `isSafariBrowser()` in `audio-recording.ts`. | Improved, not full parity |
| Ownership boundary | `stopAudioRecording` owns media-track cleanup after upload conversion. The panel owns only state reset and user feedback. | `npm run typecheck` passes after the lifecycle change. | Pass |

This update improves audio-recording behavior parity, but does not change the overall parity result. Remaining gaps still include provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

## Current Implementation Refresh: Node Execution Snapshots

Updated on 2026-06-25 after adding dependency-ordered node execution snapshots and streaming node lifecycle events to the native Flowise execution service.

| Source Area | AsterERP Implementation | Evidence | Status |
|---|---|---|---|
| Flowise runtime node order | `FlowiseExecutionService.PlanExecutionOrder` derives a dependency order from native `flowData.nodes` and `flowData.edges`. It does not use WorkflowModel, BPMN, canvas split tables, or compatibility projection. | `dotnet build AsterERP.sln --no-restore` passes after the change. | Improved, not full parity |
| Agent executed data | `BuildNodeExecutionSnapshot` writes per-node status, previous node ids, next node ids, node type, raw node data, and timestamp into Flowise `AgentExecutedData` records. | `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` passes 88/88. | Improved, not full parity |
| Streaming lifecycle events | `ExecuteDefinitionStreamingAsync` now emits `agentFlowExecutedData` events for each node in `INPROGRESS` and `FINISHED` states before final aggregate source/tool/reasoning events. | Static diff shows lifecycle events are emitted from native `flowData` execution only. | Improved, not full parity |

This update moves the execution chain closer to Flowise node-by-node semantics, but does not make the execution row Pass. Full parity still requires provider/tool/node-specific runtime behavior, agentflow v2 runtime branches, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.
