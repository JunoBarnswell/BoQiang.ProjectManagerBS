# Flowise Implementation Progress

Date: 2026-06-24

## 2026-07-11 Schedule Runtime Closure

Flowise schedule records now use a native Hangfire recurring-job chain. Chatflow/Agentflow create, update, delete, schedule-status reconciliation, and application startup all apply or remove the recurring job. The job payload freezes `ScheduleRecordId`, `TenantId`, `AppCode`, and `OwnerUserId`; execution establishes that restricted workspace principal, registers the normal ORM data-permission filters, reads the published FlowData, calls `IFlowiseExecutionService`, and lets the existing execution-tracking path write the schedule trigger log and execution result.

Verification evidence:

- Restarted API from `backend/AsterERP.Api` in Development mode and received `GET /api/health` HTTP 200.
- Startup log reported `Synchronized 1 Flowise schedules with Hangfire`.
- Fixed debug Hangfire database contained `recurring-job:flowise-schedule:c6ddd7a28c404de3ac40e66503b1d119` with the frozen tenant/app/owner job arguments and `Asia/Shanghai` timezone.
- Backend build passed with 0 warnings/0 errors and API tests passed 386/386.

This closes schedule registration and execution dispatch, but does not change the overall 100% claim: node-by-node Flowise runtime semantics, full source UI/dialog parity, provider-backed STT/TTS, authenticated permission/workspace/browser matrices, and other rows below remain incomplete.

## Completion Rule

Completion is calculated from the agreed full-source parity acceptance matrix. AsterERP may keep its shell, authentication, RBAC, tenant/app/owner filters, and HTTP client, but Flowise workflow content must use Flowise source protocol and UI semantics. Any generic CRUD page, compatibility projection, BPMN reuse, or simulated runtime is a failed item for the corresponding row.

## Actual Progress

| Module | Target Items | Completed | Verification Evidence | Status |
|---|---:|---:|---|---|
| Protocol boundary | 3 | 3 | Flowise dedicated `FlowiseChatFlowEntity`; no WorkflowModel/BPMN reuse; no split canvas projection | Pass |
| Chatflow/Agentflow backend | 5 | 5 | `FlowiseChatflowContracts`, `FlowiseChatFlowEntity`, `FlowiseChatflowService`, `AiFlowiseChatflowsController`, schema/index/data filters | Pass |
| Canvas persistence protocol | 4 | 4 | `FlowiseCanvasService` accepts/returns only `FlowData` string and reads/writes only `FlowiseChatFlowEntity.FlowData`; canvas node/edge DTOs and entities removed | Pass |
| Compatibility projection removal | 5 | 5 | Removed `FlowiseCanvasEntity`, `FlowiseCanvasNodeEntity`, `FlowiseCanvasEdgeEntity`, split table DDL, split indexes, split filter registrations, and legacy `CanvasJson/FlowDataJson` API aliases | Pass |
| Execution chain | 5 | 4 | Execution parses native `flowData`, records execution/message output, emits Flowise SSE events through `/api/ai/flowise/prediction/stream`, and returns Flowise chat-message protocol fields for agent reasoning, executed data, used tools, artifacts, and source documents; still not full Flowise node-by-node runtime parity | Fail |
| Chatflows/Agentflows frontend page | 6 | 5 | Native page route exists and no longer uses `CrudPage`/`FlowiseResourcePage`; list state now persists like Flowise, Add New opens empty canvas, duplicate/import load `duplicatedFlowData` into empty canvas, save creates a native Flowise Chatflow/Agentflow, export emits sanitized Flowise `flowData`, node preview images are parsed from `flowData.nodes[].data.name` and rendered through Flowise-native `/api/v1/node-icon/{name}`, the source-like Options menu covers Rename, Duplicate, Export, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and Delete through native Flowise fields/API calls, the list now uses dedicated Flowise native `ItemCard`, `FlowListTable`, and `FlowListMenu` component files with source-like local sort persistence, Save As Template now persists a Marketplace template through a template-export endpoint, `@mui/material`, `@mui/icons-material`, and `@emotion/*` are now installed, `FlowListMenu` now uses MUI `Button`, styled `Menu`, `MenuItem`, `Divider`, and MUI icons with Flowise source-style anchor origin, transform origin, menu-list padding, icon sizing, active background, and permission-gated menu items, each list action now maps to distinct Flowise-style frontend/backend permission codes for update, duplicate, export, template export, configuration, allowed domains, and delete, `SaveChatflowDialog`/`TagDialog` now use Flowise source-like MUI dialog/input/chip semantics, `SpeechToTextDialog` now uses Flowise provider-driven form semantics for OpenAI Whisper, Assembly AI, LocalAI STT, Azure Cognitive Services, and Groq Whisper with credential selection, native `speechToText` JSON save shape, and Flowise source provider image assets (`openai.svg`, `assemblyai.png`, `localai.png`, `azure_openai.svg`, `groq.png`) rendered through static imports in source-like circular image containers, `AllowedDomainsDialog` now uses source-like MUI dynamic domain rows with add/remove icon buttons plus an error-message input instead of `NativeDialog + textarea`, `ChatFeedbackDialog` now uses a source-like MUI dialog plus switch control instead of `NativeDialog + checkbox`, `StarterPromptsDialog` now uses source-like MUI dynamic prompt rows with add/remove icon buttons and an info banner instead of `NativeDialog + textarea`, and `ExportAsTemplateDialog` now uses source-like MUI dialog/form/chip semantics instead of `NativeDialog + input/textarea`; remaining dialog internals and browser screenshot parity remain incomplete | Fail |
| Canvas UI source parity | 10 | 9 | Canvas no longer wrapped in `CrudPage`; added real node toolbar actions, editable Sticky Note, Details/Additional Params/Info inspector tabs, edge delete dirty tracking, before-unload dirty guard, AddNodes fuzzy search/category tabs/expand state, richer ConfigInput controls, CanvasHeader actions that open API Code/Configuration/Template/Messages/Leads/Schedule/Webhook/Share/Upsert/Upsert History dialogs, Messages/Leads dialogs backed by real Flowise records, Configuration dialog editing native Chatflow config fields, Share dialog backed by Flowise shared workspace records, Upsert backed by Flowise document-store upsert history records, and ChatPopUp-style floating/expanded chat with history, clear, feedback, lead capture, source docs, persisted chatId, Flowise SSE token streaming, abort/stop control, file/image/audio upload preview and history playback, agent reasoning cards, executed data cards, used tools, artifacts rendering, starter prompts, local input history via ArrowUp/ArrowDown, validation popup, Speech-To-Text-gated MediaRecorder audio upload, and Text-To-Speech-gated browser playback; full source `CanvasHeader`, full ChatMessage event cards, full provider-backed STT/TTS runtime, upload capability negotiation, Redux/MUI parity incomplete | Fail |
| Non-flow menu pages | 16 | 16 | Added native page files for Assistants, Marketplaces, Tools, Credentials, Variables, API Keys, Document Stores, Datasets, Evaluators, Evaluations, SSO Config, Roles, Users, Login Activity, Logs; deleted `FlowiseResourcePage`; removed `FlowiseResourceCollectionPage` from all Flowise Studio menu imports; Tools/Credentials/Variables/API Keys now call dedicated configuration resource APIs rather than `flowiseStudioApi.resources.*` | Pass |
| Strong typed configuration resources | 4 | 4 | Added `FlowiseToolEntity`, `FlowiseCredentialEntity`, `FlowiseVariableEntity`, `FlowiseApiKeyEntity`, dedicated services/interfaces, `AiFlowiseResourcesController`, schema, indexes, data filters, DI registration, frontend `configurationResources.api.ts`; backend and frontend builds pass | Pass |
| Strong typed assistant/marketplace resources | 2 | 2 | Added `FlowiseAssistantEntity`, `FlowiseMarketplaceTemplateEntity`, dedicated services/interfaces, routes, schema, indexes, data filters, DI registration, default marketplace seed moved off `ai_flowise_resources`, and frontend `nativeResources.api.ts` wiring | Pass |
| Strong typed document/evaluation resources | 4 | 4 | Added `FlowiseDocumentStoreEntity`, `FlowiseDatasetEntity`, `FlowiseEvaluatorEntity`, `FlowiseEvaluationEntity`; Document Store root CRUD now lives in `IFlowiseDocumentStoreService`; Dataset/Evaluator/Evaluation root CRUD now lives in `IFlowiseEvaluationService`; corresponding frontend pages use `documentStoresApi`/`evaluationsApi` instead of `createFlowiseNativeCollectionApi` | Pass |
| Strong typed management/log resources | 5 | 5 | Added `FlowiseSsoConfigEntity`, `FlowiseRoleEntity`, `FlowiseUserEntity`, `FlowiseLoginActivityEntity`, `FlowiseAccountSettingEntity`, `IFlowiseManagementService`, `FlowiseManagementService`, explicit management/log routes, schema, indexes, data filters, and frontend `managementApi` wiring for SSO/Roles/Users/Login Activity/Logs; deleted `IFlowiseResourceService`, `FlowiseResourceService`, `FlowiseResourceCatalog`, `FlowiseResourceTypeDescriptor`, `FlowiseResourceEntity`, and removed `flowiseStudioApi.resources.*` / `createFlowiseNativeCollectionApi` | Pass |
| Built-in dialogs | 20 | 19 | Added first real CanvasHeader dialog slice for API Code, Configuration, Export Template, Messages, Leads, Schedule, Webhook, Share, Upsert, and Upsert History entry flows, plus ChatPopUp floating card and expanded dialog; Messages and Leads now read real `ai_flowise_chat_messages`/`ai_flowise_leads` records through `/api/ai/flowise/prediction/messages` and `/api/ai/flowise/prediction/leads`; Configuration now reads/writes native Chatflow fields (`chatbotConfig`, `apiConfig`, `analytic`, `speechToText`, `textToSpeech`, `followUpPrompts`, `mcpServerConfig`, `webhookSecret`) through Flowise chatflows API and exposes sectionized editors for `apiConfig.rateLimit`, `chatbotConfig.allowedOrigins`, `chatbotConfig.allowedOriginsError`, `chatbotConfig.starterPrompts`, `chatbotConfig.followUpPrompts`, `followUpPrompts`, `chatbotConfig.chatFeedback`, `chatbotConfig.leads`, `chatbotConfig.fullFileUpload`, `chatbotConfig.postProcessing`, `apiConfig.overrideConfig`, `speechToText`, `textToSpeech`, and Chatflow built-in MCP Server config/token lifecycle; this covers the Flowise source `Security` aggregate because it is `RateLimit + AllowedDomains + OverrideConfig`; Share now reads/writes `ai_flowise_shared_workspaces`; Upsert now reads/writes `ai_flowise_document_store_upsert_history`; ChatPopUp now reads/writes prediction messages, feedback, lead, clear-chat APIs, `/api/ai/flowise/prediction/stream` SSE token events, abort/stop control, `uploads/fileUploads` attachments, agent reasoning, executed data, used tools, artifacts, source documents, starter prompt buttons, local input history, validation popup, MediaRecorder audio upload, and browser speech playback; `SpeechToTextDialog` now uses provider-form semantics and copied Flowise provider image assets; `AllowedDomainsDialog` now uses source-like MUI dynamic domain rows with add/remove icon buttons and a separate unauthorized-domain error input; `ChatFeedbackDialog` now uses a source-like MUI dialog and switch control for enabling chat feedback; `StarterPromptsDialog` now uses source-like MUI dynamic prompt rows with add/remove icon buttons and the same starter-prompt info banner semantics; `ExportAsTemplateDialog` now uses source-like MUI dialog/form/chip semantics for name, description, category/usecase tags, and source-flow summary; MCP runtime now has a token-authenticated JSON-RPC `/api/v1/mcp/{chatflowId}` entry for `initialize`, `tools/list`, and `tools/call` backed by AsterERP Flowise execution; Tools page now has a dedicated Custom MCP Server resource chain with table/service/API, encrypted auth config, authorize `tools/list` discovery, discovered tools view, and native Tools page panel. Provider-backed STT/TTS runtime, upload capability negotiation, full MCP SDK Streamable HTTP parity, exact Flowise Custom MCP Server dialog UI parity, and remaining dialogs are still incomplete | Fail |
| i18n | 2 | 1 | Current Flowise Studio code has no matched bare English JSX text/placeholder/aria label; full source component set is not migrated yet | Fail |
| Backend build | 1 | 1 | `dotnet build AsterERP.sln --no-restore` passed after single-field `FlowData` canvas protocol, prediction message/lead query API additions, shared workspace API/table additions, document-store Upsert History API/table additions, clear-chat API additions, file upload persistence, Flowise SSE prediction stream additions, and strong typed configuration/assistant/marketplace/document/evaluation resource services/tables; warnings are existing package vulnerability warnings | Pass |
| Frontend typecheck | 1 | 1 | `npm run typecheck` passed after native menu, i18n migration, single-field canvas request contract, AddNodes, ConfigInput, canvas interaction additions, CanvasHeader dialog additions, message/lead dialog queries, Configuration native config editing, Configuration sectionized editors including follow-up prompts/chat feedback/leads/full file upload/post-processing, Share dialog wiring, Upsert dialog/history wiring, ChatPopUp rewrite, attachment playback, SSE stream consumption, dedicated resource API wiring through Document Stores/Evaluations, and the ChatPopUp starter prompt/input history/validation/audio/TTS interaction slice | Pass |
| Frontend build | 1 | 1 | `npm run build` passed after native menu, i18n migration, single-field canvas request contract, AddNodes, ConfigInput, canvas interaction additions, CanvasHeader dialog additions, message/lead dialog queries, Configuration native config editing, Configuration sectionized editors including follow-up prompts/chat feedback/leads/full file upload/post-processing, Share dialog wiring, Upsert dialog/history wiring, ChatPopUp rewrite, attachment playback, SSE stream consumption, dedicated resource API wiring through Document Stores/Evaluations, and the ChatPopUp starter prompt/input history/validation/audio/TTS interaction slice; Vite reported existing large chunk warnings | Pass |
| Backend tests | 1 | 1 | `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` passed 88/88 | Pass |

## Current Completion Rate

Pass rows: 13

Total rows: 18

Actual completion rate: 72.22%

This is not 100% complete. It closes the corrected architectural boundary for Flowise as a dedicated workflow protocol, removes compatibility projection from the canvas main path, removes AsterERP generic CRUD/table usage from Flowise Studio pages, removes the old frontend `FlowiseResourceCollectionPage` menu carrier, cuts Tools/Credentials/Variables/API Keys/Assistants/Marketplaces/Document Stores/Datasets/Evaluators/Evaluations/SSO Config/Roles/Users/Login Activity/Logs/Account from the backend generic resource table into dedicated tables/services/controllers, removes the generic `FlowiseResourceService/FlowiseResourceEntity` main chain, adds real ChatMessage protocol slices for `uploads/fileUploads` attachment upload/history playback and Flowise-style SSE token streaming, and keeps the changed code buildable/testable.

## Latest Actual Progress Implementation Refresh

Updated on 2026-06-25 after adding a real Flowise Webhook Listener runtime slice. This turns the previous Webhook drawer-only UI into an authenticated Flowise-protocol listener flow: the drawer registers a listener, opens an SSE stream, renders real incoming runtime events, and closes by unregistering the listener. The trigger path uses `/api/v1/webhook/{chatflowId}` and executes against native `FlowiseChatFlowEntity.FlowData`; it does not use WorkflowModel, BPMN, canvas split tables, compatibility projection, or a Flowise Node service.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Backend listener contract | Added `FlowiseWebhookContracts`, `IFlowiseWebhookListenerService`, `FlowiseWebhookListenerService`, and `AiFlowiseWebhookListenerController` with `/api/v1/webhook-listener/{chatflowId}`, `/stream/{listenerId}`, unregister, and `/api/v1/webhook/{chatflowId}` trigger endpoints. | Pass |
| Flowise protocol boundary | Trigger execution reads native `FlowiseChatFlowEntity.FlowData`, validates configured webhook secret, calls the Flowise execution service, and forwards `agentFlowEvent`, `agentFlowExecutedData`, token, source documents, tools, reasoning, artifacts, metadata, and end events through SSE. | Pass |
| Frontend listener runtime | Added `webhookListener.api.ts` and `webhookListener.types.ts`; `WebhookListenerDrawer` now registers a listener on open, streams events with Bearer/workspace headers, renders listener id and event payloads, and unregisters on close. | Pass |
| i18n | Added `flowise.detail.listenerId` and `flowise.detail.events` in zh-CN/en-US; no new bare UI text is introduced in the drawer path. | Pass |
| Verification for this slice | `dotnet build AsterERP.sln --no-restore` passed with existing package vulnerability warnings; `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings. | Pass |
| Remaining gaps | Real schedule run history API/table integration, schedule log table/delete/detail behavior, process-flow trace cards inside Webhook drawer, exact Flowise Webhook drawer response/error layout, exact Flowise dark-mode palette parity, full node-by-node provider/tool runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after extending the Flowise source-named Schedule History and Webhook Listener drawers with source-style drawer mechanics. This improves the UI/interaction parity for the two drawers by adding left-edge drag resizing, Flowise-like Schedule header summary layout, Webhook persistent drawer positioning, endpoint method chip, cURL example collapse, copy affordance, and maximize/restore width control. It still does not claim full runtime parity because real schedule logs, webhook SSE listener registration/streaming, process-flow trace, and execution response rendering are not implemented in this slice.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Schedule drawer mechanics | `ScheduleHistoryDrawer` now owns source-style width state, left-edge drag resizing with cleanup, temporary right drawer behavior, header status/type summary, disabled refresh affordance, and the no-runs empty state without fabricating logs. | Pass |
| Webhook drawer mechanics | `WebhookListenerDrawer` now owns source-style persistent right drawer positioning, left-edge drag resizing with cleanup, maximize/restore width toggle, endpoint method block, cURL example collapse, and cURL copy state. | Pass |
| i18n | Added `flowise.actions.resize`, `flowise.actions.restore`, and `flowise.canvas.curlExample` in zh-CN/en-US. | Pass |
| Contract boundary | This slice is frontend drawer UI/interaction only. It introduces no backend runtime, no Flowise protocol projection, no WorkflowModel/BPMN reuse, no fake schedule history, no fake webhook listener events, no hidden drawer API calls, no generic CRUD/table path, and no bridge business layer. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; forbidden-symbol scan found no Flowise module matches; hidden-API scan for the schedule/webhook drawer directories found no matches; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Real schedule run history API/table integration, schedule log table/delete/detail behavior, real webhook listener registration and SSE stream, process-flow trace cards inside Webhook drawer, final response/error rendering from runtime stream, exact Flowise dark-mode palette parity, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after replacing the inline CanvasHeader Schedule/Webhook modal sections with Flowise source-named drawer components. This improves source file-boundary parity for `views/schedule/ScheduleHistoryDrawer.jsx` and `views/webhooklistener/WebhookListenerDrawer.jsx` without claiming full runtime parity: the drawers are page-only UI components, contain no hidden API calls, and intentionally do not fabricate schedule logs or webhook listener events before a native Flowise runtime stream exists.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Source-named schedule drawer | Added `native/views/schedule/ScheduleHistoryDrawer.tsx` with typed props for open state, schedule summary, close event, and translation. It renders the source-named right drawer instead of the previous generic inline modal block. | Pass |
| Source-named webhook drawer | Added `native/views/webhooklistener/WebhookListenerDrawer.tsx` with typed props for open state, endpoint, secret label, copy event, close event, and translation. It renders the source-named right drawer instead of the previous generic inline modal block. | Pass |
| CanvasHeader integration | `FlowiseCanvasHeaderDialogs` now routes `activeDialog === 'schedule'` and `activeDialog === 'webhook'` to dedicated drawer components, while API Code, Configuration, Messages, Leads, Share, Upsert, and Upsert History keep the existing dialog path. | Pass |
| Start node schedule detection | `resolveScheduleInfo` now reads `startInputType` from both `data.inputs` and `data.config`, matching the current v2 runtime selector behavior and avoiding a mismatch between FAB selection and drawer content. | Pass |
| i18n | Added `flowise.messages.noScheduleRuns` and `flowise.messages.webhookWaiting` in zh-CN/en-US. | Pass |
| Contract boundary | This slice is frontend UI structure only. It introduces no backend runtime, no WorkflowModel/BPMN reuse, no Flowise protocol projection, no generic CRUD/table path, no API alias, no mock log generation, and no bridge business layer. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; forbidden-symbol scan found no Flowise module matches; hidden-API scan for the two new drawer directories found no matches. | Pass |
| Remaining gaps | Full Flowise source drawer internals, resizable drawer behavior, real schedule run history, real webhook listener stream, exact AppBar/FAB layout, full validation popup parity, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after adding the Flowise Agentflow v2 runtime FAB selector. This moves the v2 canvas closer to the Flowise source `Canvas.jsx` behavior by resolving the Start node `startInputType` and switching the floating runtime entry between Chat Test, Schedule History, and Webhook Listener. The new `native/views/agentflowsv2/AgentflowV2RuntimeFab.tsx` component is a page-only UI component with explicit callbacks and no hidden API access. It does not complete Agentflow v2 canvas parity because the full source drawers, exact AppBar/FAB layout, full validation popup parity, runtime listener streams, node-by-node runtime semantics, authenticated API smoke, and screenshot parity remain incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| v2 runtime detection | `FlowiseCanvasPage` now resolves the Start Agentflow node and reads `data.inputs.startInputType` or `data.config.startInputType`, matching Flowise source `scheduleInput` and `webhookTrigger` branches while defaulting to chat. | Pass |
| v2 runtime FAB | Added `AgentflowV2RuntimeFab` under the Flowise source-named `native/views/agentflowsv2` boundary with Chat Test, Schedule History, and Webhook Listener states using MUI `Fab`, `Badge`, `Tooltip`, and Tabler icons. | Pass |
| v2 runtime orchestration | Chat opens the existing ChatPopUp panel; schedule/webhook close the chat panel and open the existing Flowise CanvasHeader schedule/webhook dialogs; v2 validation popup is shown only when no runtime panel/dialog is open. | Pass |
| i18n | Added `flowise.canvas.scheduleHistory` and `flowise.canvas.webhookListener` to the key map and zh-CN/en-US messages. | Pass |
| Contract boundary | The slice changes only frontend v2 canvas UI orchestration; it preserves Flowise `flowData`, permissions, save/run behavior, route behavior, and introduces no backend runtime, WorkflowModel/BPMN reuse, protocol projection, generic CRUD/table path, API alias, mock, or bridge business logic. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; forbidden-symbol scan found no Flowise module matches; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Flowise source `ScheduleHistoryDrawer` and `WebhookListenerDrawer`, exact AppBar/FAB layout, `EditNodeDialog` portal behavior, full validation popup parity, schedule/webhook runtime streams, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after aligning the Flowise Agentflow v2 canvas snapping/background controls with the source Tabler icon glyphs and ReactFlow control-button classes. This moves the shared canvas closer to the Flowise source `Canvas.jsx` and `MarketplaceCanvas.jsx` behavior by supporting `snapGrid={[25,25]}`, `snapToGrid`, a snapping toggle, a background toggle, the source `IconMagnetFilled` / `IconMagnetOff` / `IconArtboard` / `IconArtboardOff` glyphs, and the same horizontal centered `Controls` layout for `agentflow-v2` and `marketplace-v2` modes only. It does not complete Agentflow v2 canvas parity because the exact Flowise AppBar/Fab composition, validation popup, schedule/webhook controls, full source canvas internals, and screenshot parity remain incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| v2 snapping state | `FlowiseCanvasPage` now keeps `agentV2SnappingEnabled` and applies `snapToGrid={isAgentV2Mode && agentV2SnappingEnabled}` with `snapGrid={[25, 25]}`. | Pass |
| v2 background state | `FlowiseCanvasPage` now keeps `agentV2BackgroundEnabled` and only renders `Background color="#aaa" gap={16}` for v2 mode when enabled. Non-v2 modes continue rendering the background. | Pass |
| v2 controls | `Controls` now exposes v2-only toggle buttons for snapping and background with translated titles/aria labels, Flowise ReactFlow control-button classes, horizontal centered controls style, and source Tabler icon states. | Pass |
| Source icon dependency | Added `@tabler/icons-react` so Flowise-native canvas glyphs can be imported directly instead of approximated through AsterERP `AppIcon`. | Pass |
| i18n | Added `flowise.canvas.toggleSnapping` and `flowise.canvas.toggleBackground` to the key map and zh-CN/en-US messages. | Pass |
| Contract boundary | The slice changes only frontend canvas interaction state; it preserves Flowise `flowData`, permissions, save/run behavior, route behavior, and introduces no backend runtime, WorkflowModel/BPMN reuse, protocol projection, generic CRUD/table path, API alias, mock, or bridge business logic. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; forbidden-symbol scan found no Flowise module matches; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Flowise source `Canvas` and `MarketplaceCanvas` internals, exact AppBar/FAB controls, `EditNodeDialog` portal behavior, validation popup parity, schedule/webhook behavior, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after giving the Flowise source-named Agentflow v2 canvas entries an explicit mode contract. `Canvas.tsx` now forces `agentflow-v2` and `MarketplaceCanvas.tsx` now forces `marketplace-v2` through a typed `FlowiseCanvasPage.forcedMode` prop instead of relying only on URL inference. This makes the v2 entries real mode-owning containers, but it does not complete Agentflow v2 canvas parity because the entries still delegate rendering and orchestration to the shared canvas implementation while full source canvas internals remain incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Shared canvas contract | `FlowiseCanvasPage` now accepts optional `forcedMode?: FlowiseCanvasMode` and preserves URL-based `resolveCanvasMode(location.pathname)` as the default path. | Pass |
| Agentflow v2 mode ownership | `native/views/agentflowsv2/Canvas.tsx` renders `<FlowiseCanvasPage forcedMode="agentflow-v2" />`. | Pass |
| Marketplace v2 mode ownership | `native/views/agentflowsv2/MarketplaceCanvas.tsx` renders `<FlowiseCanvasPage forcedMode="marketplace-v2" />`. | Pass |
| Contract boundary | The slice preserves existing Flowise `flowData` protocol, permissions, save/run behavior, and route behavior; no backend runtime, WorkflowModel/BPMN reuse, protocol projection, generic CRUD/table path, API alias, mock, or bridge business logic was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; forbidden-symbol scan found no Flowise module matches; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Flowise source `Canvas` and `MarketplaceCanvas` internals, toolbar/FAB controls, `EditNodeDialog` portal behavior, validation popup parity, schedule/webhook behavior, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after adding the Flowise source-named Agentflow v2 style entry `index.css` and importing it from the v2 Agentflow and Marketplace canvas entries. This closes the missing source style-entry file for `views/agentflowsv2/index.css`, but it does not complete Agentflow v2 visual parity because the current stylesheet only establishes the source-named class boundary and maps existing AsterERP v2 edge/connection labels; full Flowise toolbar/FAB/background/control styling and screenshot parity remain incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Agentflow v2 style entry | Added `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/index.css` with Flowise source class names for edge buttons, ReactFlow wrappers, handle states, agent edge selectors, dark controls, and current AsterERP v2 edge/connection labels. | Pass |
| Canvas style wiring | `Canvas.tsx` imports `./index.css`, so `/flowise/v2/agentcanvas*` has a source-named style boundary. | Pass |
| Marketplace style wiring | `MarketplaceCanvas.tsx` imports `./index.css`, so `/flowise/v2/marketplace/:resourceId` has the same source-named style boundary. | Pass |
| Contract boundary | The slice changes only v2 visual/style ownership; it introduces no backend runtime, no Flowise protocol projection, no WorkflowModel/BPMN reuse, no generic CRUD/table path, no API alias, no mock, and no bridge business logic. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Flowise source `Canvas` and `MarketplaceCanvas` internals, toolbar/FAB controls, `EditNodeDialog` portal behavior, validation popup parity, schedule/webhook behavior, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after adding Flowise source-named Agentflow v2 canvas page entries and routing `/flowise/v2/agentcanvas*` plus `/flowise/v2/marketplace/:resourceId` through those entries. This closes the source-route boundary mismatch for `views/agentflowsv2/Canvas` and `views/agentflowsv2/MarketplaceCanvas`, but it does not complete Agentflow v2 canvas parity because the entries still delegate to the shared AsterERP `FlowiseCanvasPage` while full source canvas internals, toolbar semantics, validation popups, schedule/webhook FABs, and browser screenshot parity remain incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Agentflow v2 Canvas source path | Added `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/Canvas.tsx` as the source-named route entry for v2 Agentflow canvas. | Pass |
| Marketplace v2 Canvas source path | Added `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/MarketplaceCanvas.tsx` as the source-named route entry for v2 Marketplace canvas. | Pass |
| Route wiring | `workspaceRoutes.full.tsx` now routes `/flowise/v2/agentcanvas`, `/flowise/v2/agentcanvas/:resourceId`, and `/flowise/v2/marketplace/:resourceId` through the source-named v2 entries instead of directly using `FlowiseCanvasPage`. | Pass |
| Contract boundary | The entries preserve the existing Flowise dedicated `flowData` protocol, route permissions, and current canvas behavior; no WorkflowModel/BPMN reuse, protocol projection, generic CRUD/table path, API alias, mock, or bridge business logic was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only; forbidden-symbol scan found no Flowise module matches, with only unrelated existing `WorkflowModelsPage` hits in the shared route file. | Pass |
| Remaining gaps | Full Flowise source `Canvas` and `MarketplaceCanvas` internals, toolbar/FAB controls, `EditNodeDialog` portal behavior, validation popup parity, schedule/webhook behavior, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after extracting node edit/configuration rendering into the Flowise source-named Agentflow v2 `EditNodeDialog` component. This closes another source-file boundary mismatch and reduces `FlowiseCanvasDialogs` responsibility, but it does not complete Agentflow v2 canvas parity because the implementation still lacks Flowise source portal dialog behavior, node label inline edit semantics, `showHideInputParams`/component-node loading parity, dedicated `Canvas`, validation internals, schedule/webhook behavior, browser screenshot parity, and runtime semantics.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| EditNodeDialog source path | Added `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/EditNodeDialog.tsx` as the source-named owner of selected-node configuration tabs and node info rendering. | Pass |
| Dialog shell responsibility | `FlowiseCanvasDialogs.tsx` now delegates selected-node rendering to `EditNodeDialog` and keeps the inspector shell, selected-edge view, and validation panel. | Pass |
| Component contract | `EditNodeDialog` takes typed `node`, `activeTab`, `onTabChange(tab)`, and `onNodeConfigChange(nodeId, name, value)` props; it has no hidden API calls, no backend runtime coupling, and no Flowise protocol writes outside the existing canvas state callback. | Pass |
| Contract boundary | The slice preserves existing `ConfigInput` rendering, additional params badge, read-only JSON fallback, node info display, and dirty propagation; no Flowise protocol projection, BPMN, generic CRUD/table path, API alias, mock, or bridge behavior was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Flowise source portal `Dialog`, node name edit/save/cancel buttons, `showHideInputParams`, component-node configuration loading, Agentflow v2 `Canvas`, validation behavior, schedule/webhook parity, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after moving the node configuration input implementation into the Flowise source-named Agentflow v2 directory. This closes the source-file boundary mismatch for `views/agentflowsv2/ConfigInput`, but it does not complete Agentflow v2 canvas parity because the implementation still lacks the full Flowise source `ConfigInput` accordion/component-node loading behavior, `EditNodeDialog`, dedicated `Canvas`, validation internals, schedule/webhook behavior, browser screenshot parity, and runtime semantics.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| ConfigInput source path | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseConfigInput.tsx` was moved to `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/ConfigInput.tsx`. | Pass |
| Dialog wiring | `FlowiseCanvasDialogs.tsx` now imports `ConfigInput` from the source-named Agentflow v2 directory for Details and Additional Params rendering. | Pass |
| No facade retention | The old `canvas/FlowiseConfigInput.tsx` path is removed rather than kept as a forwarding shim. | Pass |
| Contract boundary | The move preserves `FlowiseNodeInputParam`, `value`, and `onChange(name, value)` contracts, local JSON/password/file input behavior, and existing canvas dirty propagation; no backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge behavior was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Flowise source `ConfigInput` accordion/component-node data loading, Agentflow v2 `Canvas`, `EditNodeDialog`, validation behavior, schedule/webhook parity, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after moving the Sticky Note node implementation into the Flowise source-named Agentflow v2 directory. This closes another source-file boundary mismatch for `views/agentflowsv2/StickyNote`, but it does not complete Agentflow v2 canvas parity because dedicated `Canvas`, source `ConfigInput`, `EditNodeDialog`, validation internals, schedule/webhook behavior, browser screenshot parity, and runtime semantics still need full source-level closure.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Sticky Note source path | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseStickyNote.tsx` was moved to `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/StickyNote.tsx`. | Pass |
| Canvas registration | `FlowiseCanvasPage.tsx` now registers `flowiseStickyNote` from the source-named `StickyNote` component. | Pass |
| No facade retention | The old `canvas/FlowiseStickyNote.tsx` path is removed rather than kept as a forwarding shim. | Pass |
| Contract boundary | The move preserves `NodeProps<FlowiseCanvasNode>`, sticky-note text persistence through `data.onStickyTextChange`, CSS classes, and the existing `flowiseStickyNote` ReactFlow type key; no backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge behavior was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Agentflow v2 `Canvas`, source `ConfigInput`, `EditNodeDialog`, validation behavior, schedule/webhook parity, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after adding the Flowise source-named Agentflow v2 `ConnectionLine` component and wiring it into ReactFlow for `agentflow-v2` and `marketplace-v2` modes. This closes the custom connection-line behavior gap for in-progress Agentflow v2 connections, but it does not complete Agentflow v2 canvas parity because the dedicated `Canvas`, `ConfigInput`, `EditNodeDialog`, validation internals, schedule/webhook behavior, browser screenshot parity, and runtime semantics still need full source-level closure.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Source-named connection line | Added `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/ConnectionLine.tsx` with Flowise-style bezier line, arrow marker, handle-derived labels, and condition/human-input coloring. | Pass |
| ReactFlow v2 wiring | `FlowiseCanvasPage.tsx` now passes `connectionLineComponent={ConnectionLine}` only for `agentflow-v2` and `marketplace-v2` modes. | Pass |
| Label styling | `flowise-canvas.css` now defines `.flowise-agent-connection-label` with the small absolute label styling used by the in-progress connection line. | Pass |
| Contract boundary | The slice uses existing native `flowData` node/edge types and ReactFlow connection state; no backend runtime, Flowise protocol, projection, BPMN, generic CRUD/table path, API alias, mock, or bridge behavior was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Agentflow v2 `Canvas`, source `ConfigInput`, `EditNodeDialog`, validation behavior, schedule/webhook parity, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after moving Agentflow v2 node and edge components into the Flowise source-named `native/views/agentflowsv2` directory. This closes a source-structure mismatch for `AgentFlowNode`, `AgentFlowEdge`, and `IterationNode`, but it does not complete Agentflow v2 canvas parity because `Canvas`, `ConfigInput`, `ConnectionLine`, `EditNodeDialog`, validation internals, schedule/webhook behavior, browser screenshot parity, and runtime semantics still need full source-level closure.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Agentflow v2 source path | `FlowiseAgentFlowNode.tsx`, `FlowiseAgentFlowEdge.tsx`, and `FlowiseIterationNode.tsx` were moved out of `canvas/` into `frontend/AsterERP.Web/src/features/flowise-studio/native/views/agentflowsv2/AgentFlowNode.tsx`, `AgentFlowEdge.tsx`, and `IterationNode.tsx`. | Pass |
| Canvas registration | `FlowiseCanvasPage.tsx` now registers ReactFlow node/edge types from the source-named Agentflow v2 directory. | Pass |
| No facade retention | The old `canvas/FlowiseAgentFlowNode.tsx`, `canvas/FlowiseAgentFlowEdge.tsx`, and `canvas/FlowiseIterationNode.tsx` files are removed rather than kept as forwarding shims. | Pass |
| Contract boundary | The move preserves the existing `NodeProps<FlowiseCanvasNode>`, `EdgeProps<FlowiseCanvasEdge>`, CSS classes, and canvas type names; no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API alias, mock, or bridge behavior was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Agentflow v2 `Canvas`, `ConfigInput`, `ConnectionLine`, `EditNodeDialog`, validation behavior, schedule/webhook parity, full node-by-node runtime, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after extracting the ChatPopUp file-upload trigger into the Flowise source-named `native/ui-component/file/File.tsx` component. This moves the hidden file input and upload button out of the ChatPopUp container while preserving the existing `uploads/fileUploads` request payload and preview behavior. The overall acceptance rows remain unchanged because full Flowise node-by-node runtime parity, full ChatPopUp internals, Agentflow v2 canvas, authenticated smoke, permission smoke, workspace boundary checks, and browser screenshot parity are still incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Source-named file component | Added `frontend/AsterERP.Web/src/features/flowise-studio/native/ui-component/file/File.tsx` with typed UI-only props: `disabled`, `label`, `multiple`, and `onFilesSelected`. | Pass |
| ChatPopUp container cleanup | `FlowiseChatTestPanel.tsx` now renders `FileUpload` and no longer owns the hidden file input ref or input reset logic. | Pass |
| Upload contract preservation | `addFiles(files)` still reads selected files into existing `FlowisePredictionUpload` objects, caps uploads at 10, and keeps the same `uploads/fileUploads` message playback path. | Pass |
| Contract boundary | The component contains no hidden API calls and introduces no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API alias, mock, or bridge behavior. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Flowise node-by-node runtime, exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, Agentflow v2 canvas, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after wiring backend `agentFlowExecutedData` SSE node lifecycle events into the ChatPopUp runtime display. This refresh makes running node execution state visible while a prediction stream is active, but it does not change the overall acceptance rows because full Flowise node-by-node runtime parity, remaining source UI gaps, authenticated smoke, permission smoke, workspace boundary checks, and browser screenshot parity are still incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Streaming executed-data state | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseChatTestPanel.tsx` now keeps `streamingExecutedData` while a prediction stream is running. | Pass |
| SSE event parsing | ChatPopUp now consumes `agentFlowExecutedData` stream events, accepts single-node or array payloads, validates the Flowise executed-node shape, and merges updates by `nodeId` so an `INPROGRESS` node can be replaced by its `FINISHED` update. | Pass |
| Runtime UI feedback | `ChatContent` renders live `AgentExecutedDataList` cards before the final assistant message refetch, so users can see node execution progress during Chat Test instead of only after completion. | Pass |
| Contract boundary | The slice uses the existing Flowise prediction SSE protocol and `FlowiseAgentExecutedNodeDto`; no AsterERP generic CRUD/table path, BPMN reuse, compatibility projection, API alias, mock, or bridge behavior was introduced. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing circular chunk and large chunk warnings; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining gaps | Full Flowise node-by-node runtime, full Canvas/Agentflow v2/ChatPopUp source parity, remaining raw-control migrations, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise RateLimit section MUI slice. This refresh removes raw controls from the Configuration Rate Limit section while preserving the existing `apiConfig.rateLimit` read/normalize/write behavior and incomplete-field validation. The overall acceptance rows remain unchanged because broader runtime, remaining Configuration section editors, MCP Server, TTS/STT, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| RateLimit switch movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeaderDialogs.tsx` now renders the Rate Limit enabled state with MUI `FormControlLabel` and `Checkbox` instead of a raw checkbox input. | Pass |
| RateLimit inputs movement | `limitMax`, `limitDuration`, and `limitMsg` now render with MUI `TextField` controls instead of raw number/text inputs. | Pass |
| Component contract | `readRateLimit`, `updateRateLimit(patch)`, `normalizeRateLimit(next)`, and the invalid-field check are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-dialogs.css` now scopes MUI `FormControlLabel` typography under `.flowise-switch-row`. | Pass |
| Verification for this slice | Targeted scan shows RateLimit raw controls were removed and remaining `FlowiseCanvasHeaderDialogs.tsx` raw-control hits now start at `AllowedDomainsSection`; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed after changing number input min constraints to MUI `slotProps.htmlInput`; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits in `FlowiseCanvasHeaderDialogs.tsx` now start at `AllowedDomainsSection` and continue through Starter Prompts, Follow-up Prompts, Chat Feedback, Leads, File Upload, Post Processing, Override Config, MCP Server, TTS/STT, plus `FlowiseChatTestPanel.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining Configuration section editor/MCP/TTS-STT and ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Configuration JSON/Webhook/Save MUI slice. This refresh removes raw controls from the generic Configuration JSON text area component, webhook secret field, and Configuration save action while preserving the existing string-valued JSON fields and `onSave` callback. The overall acceptance rows remain unchanged because broader runtime, remaining Configuration section editors, MCP Server, TTS/STT, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| JSON editor movement | `JsonTextArea` in `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeaderDialogs.tsx` now uses MUI `TextField multiline` instead of a raw `textarea`, covering advanced JSON, metadata, follow-up, and MCP config string fields. | Pass |
| Webhook secret movement | `webhookSecret` now renders with MUI `TextField` type `password` while preserving the configured placeholder behavior. | Pass |
| Save action movement | The Configuration save action now uses MUI `Button` with the existing `AppIcon` start icon instead of a raw `btn-primary` button. | Pass |
| Component contract | All JSON fields remain string-valued, each `JsonTextArea` still calls `onChange(value)`, and `onSave` is unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Verification for this slice | Targeted scan shows Configuration JSON/webhook/save raw controls were removed and remaining `FlowiseCanvasHeaderDialogs.tsx` raw-control hits now start at `RateLimitSection`; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits in `FlowiseCanvasHeaderDialogs.tsx` now start at `RateLimitSection` and continue through section editors, MCP Server, TTS/STT, plus `FlowiseChatTestPanel.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining Configuration section editor/MCP/TTS-STT and ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Configuration basic-fields MUI slice. This refresh removes raw controls from the top-level Configuration dialog fields for name, category, workspace, API key, deployed, and public while preserving the existing `draft`, `update(patch)`, and save request construction. The overall acceptance rows remain unchanged because broader runtime, remaining Configuration section editors, MCP Server, TTS/STT, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Configuration text field movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeaderDialogs.tsx` now renders `name`, `category`, `workspaceId`, and `apikeyid` with MUI `TextField` instead of raw inputs. | Pass |
| Configuration status movement | `deployed` and `isPublic` now render with MUI `FormControlLabel` and `Checkbox` instead of raw checkbox inputs. | Pass |
| Component contract | `draft`, `update(patch)`, JSON section parsing, and `saveConfiguration()` request construction are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Verification for this slice | Targeted scan shows the Configuration basic fields were removed from raw-control hits and remaining `FlowiseCanvasHeaderDialogs.tsx` raw-control hits now start at the next Configuration URL/action block; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits in `FlowiseCanvasHeaderDialogs.tsx` now start at the next Configuration URL/action block and continue through section editors, MCP Server, TTS/STT, plus `FlowiseChatTestPanel.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining Configuration/MCP/TTS-STT and ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Upsert dialog checkbox MUI slice. This refresh removes the raw `replaceExisting` checkbox from the CanvasHeader Upsert dialog while preserving the existing document-store target summary, latest history summary, RBAC-gated upsert action, and `onRun(replaceExisting)` payload. The overall acceptance rows remain unchanged because broader runtime, remaining Configuration/MCP/TTS-STT dialog bodies, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Upsert checkbox movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeaderDialogs.tsx` now renders the `replaceExisting` option with MUI `FormControlLabel` and `Checkbox` instead of a raw checkbox input. | Pass |
| RBAC action boundary | The existing `PermissionButton` for `flowisePermissions.documentStoresUpsert` remains unchanged so the frontend permission gate and click payload are preserved. | Pass |
| Component contract | `replaceExisting` state and `onRun(replaceExisting)` are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-dialogs.css` now scopes MUI `FormControlLabel` density under `.flowise-dialog-check-inline`. | Pass |
| Verification for this slice | Targeted scan shows the Upsert dialog raw checkbox was removed and remaining `FlowiseCanvasHeaderDialogs.tsx` raw-control hits now start at Configuration basic fields; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits in `FlowiseCanvasHeaderDialogs.tsx` now start at Configuration basic fields and continue through section editors, MCP Server, TTS/STT, plus `FlowiseChatTestPanel.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining Configuration/MCP/TTS-STT and ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Share dialog MUI control slice. This refresh removes raw controls from the CanvasHeader Share dialog while preserving the existing shared-workspace query, local row state, and selected-workspace save payload. The overall acceptance rows remain unchanged because broader runtime, remaining CanvasHeader dialog bodies, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Share title movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeaderDialogs.tsx` now renders the read-only shared flow title with MUI `TextField` instead of a raw disabled input. | Pass |
| Shared workspace selection movement | Workspace share toggles now use MUI `Checkbox` while preserving `rows[].shared` updates by `workspaceId`. | Pass |
| Share submit movement | The Share action now uses MUI `Button` with the existing `AppIcon` start icon instead of a raw `btn-primary` button. | Pass |
| Component contract | `workspaces -> rows`, `updateRow(workspaceId, shared)`, and `onSave(sharedWorkspaceIds)` are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-dialogs.css` now scopes MUI checkbox density in existing Flowise dialog tables. | Pass |
| Verification for this slice | Targeted scan shows the Share dialog raw controls were removed and remaining `FlowiseCanvasHeaderDialogs.tsx` raw-control hits now start at Upsert; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits in `FlowiseCanvasHeaderDialogs.tsx` now start at Upsert and continue through Configuration section editors, MCP Server, TTS/STT, plus `FlowiseChatTestPanel.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining CanvasHeader dialog body and ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise CanvasHeader dialog top-action MUI slice. This refresh removes raw buttons from the shared canvas header dialog close action plus the API Code copy, Webhook copy, and Export Template actions while preserving the same dialog dispatch, copy, and export callbacks. The overall acceptance rows remain unchanged because broader runtime, remaining CanvasHeader dialog bodies, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Header dialog top action movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeaderDialogs.tsx` now uses MUI `IconButton` for the shared dialog close action instead of a raw button. | Pass |
| API/Webhook/Template action movement | API Code copy, Webhook copy, and Export Template now use MUI `Button` with the existing `AppIcon` start icons instead of raw `btn-secondary`/`btn-primary` buttons. | Pass |
| Component contract | `onClose`, `copy(codeSnippet)`, `copy(webhookEndpoint)`, and `exportTemplate()` behavior is unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-dialogs.css` now scopes MUI `IconButton` and `Button` density under `.flowise-canvas-header-dialog`. | Pass |
| Verification for this slice | `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are inside Share, Upsert, Configuration section editors, MCP Server, TTS/STT, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining CanvasHeader dialog body and ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Canvas save dialog MUI control slice. This refresh removes raw controls from the new-flow save dialog embedded in `FlowiseCanvasPage.tsx` while preserving the existing controlled draft, close, and save event contract. The overall acceptance rows remain unchanged because broader runtime, CanvasHeader dialogs, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Save dialog movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasPage.tsx` now uses MUI `IconButton`, `TextField`, and `Button` for the new-flow save dialog instead of raw close, input, cancel, and save controls. | Pass |
| Component contract | `draft`, `open`, `saving`, `onChange(draft)`, `onClose()`, and `onSave()` are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-pages.css` now scopes MUI text-field, button, icon-button, and field-row styling under the existing `.flowise-native-dialog` dialog shell. | Pass |
| Verification for this slice | Targeted scan of `FlowiseCanvasPage.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in CanvasHeader dialogs and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining CanvasHeader dialog and ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise ConfigInput MUI control slice. This refresh removes raw controls from the shared node-parameter input renderer while preserving Flowise param type handling for boolean, options, credential, multiOptions, json/code/array/grid, file, password, number, date, time, and text values. The overall acceptance rows remain unchanged because broader runtime, CanvasHeader dialogs, Canvas save dialog, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Boolean/options movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseConfigInput.tsx` now uses MUI `Switch`, `FormControl`, `InputLabel`, `Select`, and `MenuItem` instead of raw checkbox/select controls. | Pass |
| Multi-value movement | `multiOptions` now uses MUI `FormControl`, `FormGroup`, `FormControlLabel`, and `Checkbox` while preserving selected-value array updates. | Pass |
| JSON/code movement | JSON/code/array/grid values now use MUI `TextField multiline`, preserving JSON parse-on-blur behavior and invalid JSON error feedback. | Pass |
| File/password/text movement | File upload now uses a MUI `Button` plus dynamic file picker, avoiding raw JSX file input; password/text/number/date/time now use MUI `TextField` with reveal adornment for password. | Pass |
| Component contract | `param`, `value`, and `onChange(name, value)` are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-dialogs.css` now scopes MUI input/button density and option-row styling under `.flowise-config-row`. | Pass |
| Verification for this slice | Targeted scan of `FlowiseConfigInput.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in CanvasHeader dialogs, Canvas save dialog, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise NodePalette MUI control slice. This refresh removes raw controls from the AddNodes palette search, category filters, Sticky Note action, category expanders, and node add cards while preserving fuzzy search, expand/collapse state, click-add, and drag-add behavior. The overall acceptance rows remain unchanged because broader runtime, CanvasHeader dialogs, ConfigInput, Canvas save dialog, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Palette search movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseNodePalette.tsx` now uses MUI `TextField` with a search adornment instead of a raw `input`. | Pass |
| Category filter movement | The All Nodes/category filter row now uses MUI `ToggleButtonGroup` and `ToggleButton` instead of raw category `button` tags. | Pass |
| Palette actions movement | Sticky Note, category expand/collapse, and node add cards now use MUI `Button` controls instead of raw `button` tags. | Pass |
| Interaction contract | Fuzzy scoring, `activeCategory`, `expandedCategories`, `onAddNode(item)`, `onAddStickyNote()`, and `application/x-flowise-node` drag data are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-canvas.css` now scopes MUI palette/search/toggle/node-card styling under the existing Flowise palette class names. | Pass |
| Verification for this slice | Targeted scan of `FlowiseNodePalette.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `git diff --check` passed with CRLF warnings only; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in CanvasHeader dialogs, ConfigInput, Canvas save dialog, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise CanvasHeader MUI action-button slice. This refresh removes raw non-permission action buttons from the canvas header while preserving existing dialog, chat, validation, run, save, and RBAC event boundaries. The overall acceptance rows remain unchanged because broader runtime, CanvasHeader dialogs, ConfigInput, NodePalette, Canvas save dialog, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Header action movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasHeader.tsx` now uses MUI `Button` for API Code, Settings, Template, Share, Upsert History, Messages, Leads, Schedule, Webhook, Validation, and Chat Test instead of raw `button` tags. | Pass |
| RBAC boundary | Existing `PermissionButton` paths for Upsert, Run, and Save remain in place, preserving the current frontend permission gate and event contract. | Pass |
| Event contract | `onOpenDialog`, `onOpenChat`, `onOpenValidation`, `onRun`, and `onSave` props are unchanged; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-canvas.css` now scopes compact MUI button styling under `.flowise-canvas-header__actions`. | Pass |
| Verification for this slice | Targeted scan of `FlowiseCanvasHeader.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in CanvasHeader dialogs, ConfigInput, NodePalette, Canvas save dialog, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Sticky Note MUI input slice. This refresh removes the raw Sticky Note textarea from the canvas node implementation while preserving the existing Flowise sticky-note text update contract. The overall acceptance rows remain unchanged because broader runtime, CanvasHeader, CanvasHeader dialogs, ConfigInput, NodePalette, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Sticky Note input movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseStickyNote.tsx` now uses MUI `TextField multiline` instead of a raw `textarea`. | Pass |
| Sticky Note contract | The component still receives `NodeProps<FlowiseCanvasNodeType>` and writes text through `data.onStickyTextChange?.(id, value)`, so save/dirty behavior remains on the existing canvas path. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-canvas.css` now styles `.flowise-sticky-note__input` and its MUI input root/textarea so the yellow note visual remains local to the Flowise canvas. | Pass |
| Verification for this slice | Targeted scan of `FlowiseStickyNote.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in CanvasHeader, CanvasHeader dialogs, ConfigInput, NodePalette, Canvas save dialog, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise canvas inspector dialog MUI slice. This refresh removes raw tab buttons and the raw read-only config textarea from the node inspector while preserving the existing controlled inspector contract. The overall acceptance rows remain unchanged because broader runtime, CanvasHeader dialogs, ConfigInput, NodePalette, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Inspector tab movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasDialogs.tsx` now uses MUI `Tabs` and `Tab` for Details, Additional Params, and Info instead of raw tab `button` tags. | Pass |
| Additional params count | The Additional Params tab now renders the count with MUI `Badge`, preserving the visible count behavior while moving toward the Flowise/MUI source-control structure. | Pass |
| Config JSON fallback | The no-parameter fallback now renders read-only JSON with MUI `TextField multiline` instead of a raw `textarea`. | Pass |
| Component boundary | The component remains controlled by `activeTab` and `onTabChange`; node config writes still flow through `onNodeConfigChange`; no API call, generic resource path, BPMN path, compatibility projection, or bridge behavior was introduced. | Pass |
| Verification for this slice | Targeted scan of `FlowiseCanvasDialogs.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in CanvasHeader, CanvasHeader dialogs, ConfigInput, NodePalette, StickyNote, Canvas save dialog, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise canvas node toolbar MUI slice. This refresh removes raw info/duplicate/delete buttons from the shared canvas node card while preserving the existing delegated node action contract. The overall acceptance rows remain unchanged because broader runtime, canvas dialogs, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Node toolbar movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseCanvasNode.tsx` now uses MUI `IconButton` for info, duplicate, and delete node toolbar actions instead of raw `button` tags. | Pass |
| Action contract | The toolbar still emits the existing `data-flowise-node-action` values consumed by `FlowiseCanvasPage.handleNodeClick`, so info/duplicate/delete behavior remains delegated through the same path. | Pass |
| Style compatibility | `frontend/AsterERP.Web/src/features/flowise-studio/styles/flowise-canvas.css` now includes `.MuiIconButton-root` selectors for the node toolbar styles so the existing Flowise card chrome is retained. | Pass |
| Verification for this slice | Targeted scan of `FlowiseCanvasNode.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in CanvasHeader, CanvasHeader dialogs, ConfigInput, NodePalette, StickyNote, Canvas save dialog, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise canvas edge delete-control MUI slice. This refresh removes the raw edge-delete button from the shared ReactFlow edge component while preserving the existing edge deletion contract. The overall acceptance rows remain unchanged because broader runtime, canvas dialogs, ChatPopUp, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Edge delete control movement | `frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseButtonEdge.tsx` now uses MUI `IconButton` for the edge delete label instead of a raw `button` tag. | Pass |
| Component boundary | The component still renders `BaseEdge`/`EdgeLabelRenderer`, calls `data.onDeleteEdge(id)` when provided, and otherwise removes the edge with ReactFlow `setEdges`; no generic resource API, BPMN path, compatibility projection, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `FlowiseButtonEdge.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are concentrated in other canvas, CanvasHeader dialog, ConfigInput, NodePalette, StickyNote, and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Chatflows/Agentflows native list toolbar and pagination MUI slice. This refresh removes the remaining raw controls from the native Chatflows/Agentflows list page while preserving local view/page-size/sort state and native Flowise import behavior. The overall acceptance rows remain unchanged because broader runtime, canvas, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Chatflows header movement | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/chatflows/index.tsx` now uses MUI `ToggleButtonGroup`, `ToggleButton`, and `Button` with MUI view/refresh icons for card/list and refresh actions instead of raw `button` tags. | Pass |
| Chatflows import movement | The native import action now creates the file picker through the existing import handler on demand instead of rendering a hidden raw JSX `input`; the imported payload still flows through `duplicatedFlowData` and opens the native canvas. | Pass |
| Chatflows toolbar/pagination movement | Search, clear, page size, previous, and next controls now use MUI `TextField`, `Button`, `FormControl`, `InputLabel`, `Select`, `MenuItem`, and `Stack` instead of raw `input`, `button`, and `select` tags. | Pass |
| Component boundary | The page still calls `nativeChatflowsApi`, preserves Flowise native `flowData` import/export behavior, local storage keys, and dedicated Chatflows/Agentflows permissions; no generic resource API, BPMN path, compatibility projection, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `native/views/chatflows/index.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Remaining scoped raw-control hits are now concentrated in canvas and ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining canvas/ChatPopUp raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Custom MCP Server panel toolbar/pagination MUI slice. This refresh removes the raw Custom MCP search, view-mode, and pagination controls while preserving the dedicated Custom MCP Server API and panel behavior. The overall acceptance rows remain unchanged because broader runtime, canvas, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Custom MCP toolbar movement | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/tools/CustomMcpServerPanel.tsx` now uses MUI `TextField`, `ToggleButtonGroup`, and `ToggleButton` for search and card/list view switching instead of raw `input` and `button` tags. | Pass |
| Custom MCP pagination movement | Pagination now uses MUI `Stack`, `Button`, `FormControl`, `InputLabel`, `Select`, and `MenuItem` with the existing `flowise.fields.pageSize` label instead of raw `button` and `select` tags. | Pass |
| Component boundary | The panel still calls `customMcpServersApi` for list/get/create/update/delete/authorize/tools and keeps `PermissionButton` action gates; no generic resource API, BPMN path, compatibility projection, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `CustomMcpServerPanel.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Raw controls still remain in `native/views/chatflows/index.tsx` and multiple canvas/ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Tools native tab-control MUI slice. This refresh removes the raw Tools/MCP tab buttons while preserving the existing Custom Tools and Custom MCP Server panel split. The overall acceptance rows remain unchanged because broader raw-control, runtime, canvas, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Tools tab movement | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/tools/index.tsx` now uses MUI `ToggleButtonGroup` and `ToggleButton` for the Custom Tools / Custom MCP Server tab switch instead of raw `button` tags. | Pass |
| Component boundary | The page still switches between the dedicated Tools collection surface and `CustomMcpServerPanel`; no generic resource API, BPMN path, compatibility projection, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `native/views/tools/index.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Raw controls still remain in `native/views/chatflows/index.tsx`, `CustomMcpServerPanel.tsx`, and multiple canvas/ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the shared Flowise list-table sort-control MUI slice. This refresh removes the raw sort button from the native Flowise table component while preserving the existing column, order, orderBy, and onSort contract. The overall acceptance rows remain unchanged because broader raw-control, runtime, canvas, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Shared table sort movement | `frontend/AsterERP.Web/src/features/flowise-studio/native/ui-component/table/FlowListTable.tsx` now uses MUI `TableSortLabel` for sortable headers instead of a raw `button` tag. | Pass |
| Component boundary | The table keeps the same `FlowListTableColumn`, `order`, `orderBy`, and `onSort` props; no generic AsterERP `DataTable`, resource API, BPMN path, compatibility projection, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `FlowListTable.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Raw controls still remain in `native/views/chatflows/index.tsx`, `native/views/tools/index.tsx`, `CustomMcpServerPanel.tsx`, and multiple canvas/ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Document Store Detail vector-query MUI control slice. This refresh removes the raw vector-query input/action controls while preserving the dedicated document-store API and existing query behavior. The overall acceptance rows remain unchanged because broader raw-control, runtime, canvas, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Document Store Detail query movement | `frontend/AsterERP.Web/src/features/flowise-studio/pages/FlowiseDocumentStoreDetailPage.tsx` now uses MUI `Stack`, `TextField`, and `Button` with the MUI `Search` icon for vector-store query instead of raw `input` and `button` tags. | Pass |
| Component boundary | The page still calls `documentStoresApi.get/files/chunks/vectorConfig/query`; no generic resource API, BPMN path, compatibility projection, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `FlowiseDocumentStoreDetailPage.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Raw controls still remain in `FlowListTable.tsx`, `native/views/chatflows/index.tsx`, `native/views/tools/index.tsx`, `CustomMcpServerPanel.tsx`, and multiple canvas/ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 after the Flowise Account Settings MUI form-control slice. This refresh removes the raw account settings inputs while preserving the dedicated management/account API, `flowise:account:edit` permission gate, and existing save behavior. The overall acceptance rows remain unchanged because broader raw-control, runtime, canvas, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Account Settings form movement | `frontend/AsterERP.Web/src/features/flowise-studio/pages/FlowiseAccountSettingsPage.tsx` now uses MUI `Stack` and `TextField` controls for display name, email, and preferences JSON instead of raw `input` and `textarea` tags. | Pass |
| Component boundary | The page still calls `flowiseStudioApi.account.get/update` and keeps the `flowisePermissions.accountEdit` save gate; no generic resource API, BPMN path, compatibility projection, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `FlowiseAccountSettingsPage.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Raw controls still remain in `FlowiseDocumentStoreDetailPage.tsx`, `FlowListTable.tsx`, `native/views/chatflows/index.tsx`, `native/views/tools/index.tsx`, `CustomMcpServerPanel.tsx`, and multiple canvas/ChatPopUp files. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 00:53 +08:00 after the Flowise Executions page MUI toolbar/pagination slice. This refresh removes raw search/filter/pagination controls from the Executions page while preserving the existing execution list, output, delete permission, and API behavior. The overall acceptance rows remain unchanged because broader raw-control, runtime, canvas, API, and browser gaps remain.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Executions toolbar movement | `frontend/AsterERP.Web/src/features/flowise-studio/pages/FlowiseExecutionsPage.tsx` now uses MUI `TextField`, `FormControl`, `InputLabel`, `Select`, `MenuItem`, and `Button` with `Search` icon instead of raw `input`, `select`, and `button` controls. | Pass |
| Executions pagination movement | Pagination now uses MUI `Stack`, `Button`, `FormControl`, `InputLabel`, `Select`, and `MenuItem`; added `flowise.fields.pageSize` for `zh-CN/en-US`. | Pass |
| Component boundary | The page still calls the existing `flowiseStudioApi.executions` list/delete APIs and `flowisePermissions.executionsManage`; no protocol conversion, generic resource API, BPMN path, or bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `FlowiseExecutionsPage.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Raw controls still remain in `FlowiseDocumentStoreDetailPage.tsx`, `FlowiseAccountSettingsPage.tsx`, `FlowListTable.tsx`, `native/views/chatflows/index.tsx`, `native/views/tools/index.tsx`, and `CustomMcpServerPanel.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 00:44 +08:00 after the native collection surface MUI migration and `NativeDialog` removal. This refresh removes the final business use of `NativeDialog` and deletes the component file. The overall acceptance rows remain unchanged because raw controls and source parity gaps still remain elsewhere.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Native collection surface movement | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/common/FlowiseNativeCollectionSurface.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `TextField`, `FormControl`, `InputLabel`, `Select`, `MenuItem`, `Stack`, `ToggleButtonGroup`, and `ToggleButton` instead of `NativeDialog` plus raw inputs/select/textarea/buttons. | Pass |
| NativeDialog removal | `frontend/AsterERP.Web/src/features/flowise-studio/native/ui-component/dialog/NativeDialog.tsx` was deleted after the scoped scan confirmed no remaining imports/usages. | Pass |
| Component boundary | The collection surface still receives explicit typed APIs through `options.api`; no generic `flowiseStudioApi.resources`, BPMN/projection path, or hidden bridge layer was introduced. | Pass |
| Verification for this slice | `rg -n "NativeDialog" frontend/AsterERP.Web/src/features/flowise-studio` returns no hits; targeted scan of `FlowiseNativeCollectionSurface.tsx`, `CustomMcpServerDialog.tsx`, and `FlowiseWorkspacesPage.tsx` returns no raw `<input>`, `<button>`, `<textarea>`, or `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining raw controls | Raw controls still remain in `FlowiseDocumentStoreDetailPage.tsx`, `FlowiseAccountSettingsPage.tsx`, `FlowiseExecutionsPage.tsx`, `FlowListTable.tsx`, `native/views/chatflows/index.tsx`, `native/views/tools/index.tsx`, and `CustomMcpServerPanel.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 00:35 +08:00 after the Custom MCP Server dialog MUI source-alignment slice. This refresh removes the largest remaining `NativeDialog` business usage and the raw form/action controls inside the Custom MCP dialog. The overall acceptance rows remain unchanged because the final source parity matrix still has runtime, canvas, API smoke, browser, and one remaining collection-surface shell gap.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Custom MCP dialog shell movement | `frontend/AsterERP.Web/src/features/flowise-studio/native/views/tools/CustomMcpServerDialog.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, and `DialogActions` instead of `NativeDialog`. | Pass |
| Custom MCP form movement | The dialog edit form now uses MUI `TextField`, `FormControl`, `InputLabel`, `Select`, `MenuItem`, `Stack`, and color `TextField` instead of raw `input` and `select` tags. Header rows use MUI `TextField`, `IconButton`, and add/delete icons. | Pass |
| Custom MCP tool action movement | Discovered tools section actions now use MUI `Button`, `IconButton`, `TextField`, `ExpandMore`, `ChevronRight`, and `Clear` icons instead of raw buttons/search input. | Pass |
| Component boundary | The dialog remains a UI component with the existing `onSave`, `onAuthorize`, and `onDelete` callbacks; no API calls, protocol conversion, BPMN path, or compatibility projection was introduced. | Pass |
| Verification for this slice | Targeted scan of `CustomMcpServerDialog.tsx` and `FlowiseWorkspacesPage.tsx` returns no `NativeDialog`, raw `<input>`, raw `<button>`, raw `<textarea>`, or raw `<select>` hits; global `NativeDialog` scan now only finds `FlowiseNativeCollectionSurface.tsx` plus the component definition; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining `NativeDialog` usage | Current scoped scan still finds `NativeDialog` in `FlowiseNativeCollectionSurface.tsx` and the `NativeDialog.tsx` component itself. | Fail |
| Remaining raw controls | Raw controls still remain in `FlowiseNativeCollectionSurface.tsx`, `native/views/chatflows/index.tsx`, `FlowListTable.tsx`, `CustomMcpServerPanel.tsx`, and `native/views/tools/index.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, remaining native collection-surface cleanup, Chatflows/table/tools raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 00:26 +08:00 after the Flowise Workspaces MUI dialog/toolbar slice. This refresh records one concrete source-parity cleanup: the standalone Workspaces page no longer uses `NativeDialog` or raw form controls. The overall acceptance rows remain unchanged because broader page/canvas/runtime/API/browser gaps are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Workspaces page dialog movement | `frontend/AsterERP.Web/src/features/flowise-studio/pages/FlowiseWorkspacesPage.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `TextField`, `FormControl`, `InputLabel`, `Select`, `MenuItem`, `Stack`, and MUI `Button` instead of `NativeDialog` plus raw inputs/select/textarea/buttons. | Pass |
| Workspaces toolbar movement | The Workspaces search/status/view-mode toolbar now uses MUI `TextField`, `FormControl`, `Select`, `MenuItem`, and `ToggleButtonGroup`/`ToggleButton` instead of raw `input`, `select`, and `button` controls. | Pass |
| Component boundary | The page still calls the existing `flowiseStudioApi.workspaces` APIs and `flowisePermissions.workspacesManage`; no generic resource API, BPMN/projection path, or hidden bridge layer was introduced. | Pass |
| Verification for this slice | Targeted scan of `FlowiseWorkspacesPage.tsx` returns no `NativeDialog`, raw `<input>`, raw `<button>`, raw `<textarea>`, or raw `<select>` hits; `npm run typecheck` passed; `npm run build` passed. Build still reports the existing circular chunk warning and large chunk warnings. | Pass |
| Remaining `NativeDialog` usage | Current scoped scan still finds `NativeDialog` in `FlowiseNativeCollectionSurface.tsx`, `CustomMcpServerDialog.tsx`, and the `NativeDialog.tsx` component itself. | Fail |
| Remaining raw controls | Raw controls still remain in `FlowiseNativeCollectionSurface.tsx`, `native/views/chatflows/index.tsx`, `FlowListTable.tsx`, `CustomMcpServerPanel.tsx`, `native/views/tools/index.tsx`, and `CustomMcpServerDialog.tsx`. | Fail |
| Remaining gaps | Full Flowise node-by-node runtime, exact Custom MCP Server dialog UI parity, remaining source page raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Documentation Refresh

Updated on 2026-06-25 00:18 +08:00 after a documentation-only rescan. No implementation files were changed in this refresh, so the row score and completion rate remain unchanged. The scan confirms the Flowise dedicated-protocol boundary still holds in the Flowise module paths, but it also confirms remaining UI/source-parity gaps that prevent reporting 100%.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Dedicated Flowise protocol boundary | Scoped scan of Flowise backend/frontend paths found no Flowise use of `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `ai_flowise_canvas*`, `FlowiseResourceService`, `FlowiseResourceEntity`, `createFlowiseNativeCollectionApi`, or `flowiseStudioApi.resources`. The repo-wide scan still finds unrelated approval/workflow `WorkflowModelsController`, which is outside the Flowise scoped paths. | Pass |
| Generic AsterERP CRUD/table ban | Scoped scan found no `CrudPage`, `DataTable`, `useCrudResource`, `FlowiseResourcePage`, or `FlowiseResourceCollectionPage` inside the Flowise Studio source paths. | Pass |
| Remaining `NativeDialog` usage | `NativeDialog` still remains in `frontend/AsterERP.Web/src/features/flowise-studio/native/views/common/FlowiseNativeCollectionSurface.tsx`, `frontend/AsterERP.Web/src/features/flowise-studio/native/views/tools/CustomMcpServerDialog.tsx`, and `frontend/AsterERP.Web/src/features/flowise-studio/pages/FlowiseWorkspacesPage.tsx`; `native/ui-component/dialog/NativeDialog.tsx` therefore cannot be removed yet. | Fail |
| Remaining non-MUI/raw controls | Targeted scan still finds raw `<input>`, `<button>`, `<textarea>`, and `<select>` controls in `FlowiseNativeCollectionSurface.tsx`, `native/views/chatflows/index.tsx`, `native/ui-component/table/FlowListTable.tsx`, `native/views/tools/CustomMcpServerPanel.tsx`, `native/views/tools/index.tsx`, and `native/views/tools/CustomMcpServerDialog.tsx`. These are not yet source-level Flowise MUI/component parity. | Fail |
| Verification for this refresh | This refresh only updated progress/audit documentation from current source scans. No build, test, API smoke, or browser screenshot matrix was rerun. The latest code verification remains the previously recorded `npm run typecheck` and `npm run build` pass after the Export As Template slice. | Blocked for final acceptance |
| Remaining gaps | Full Flowise node-by-node runtime, exact Custom MCP Server dialog UI parity, remaining source page raw-control migrations, full Canvas/Agentflow v2/ChatPopUp source parity, authenticated API smoke, permission-deny smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Latest Actual Progress Implementation Refresh

Updated on 2026-06-25 00:09 +08:00 after the Export As Template source-like MUI form slice. This refresh closes the last `NativeDialog` usage inside `native/ui-component/dialog` business dialogs, while keeping the row score and overall Pass-row completion rate unchanged because remaining dialog internals, Canvas/runtime parity, and browser/API smoke are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| ExportAsTemplateDialog source movement | `ExportAsTemplateDialog.tsx` no longer uses `NativeDialog`, raw inputs, raw textarea, or native buttons. It now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `OutlinedInput`, `Chip`, `Typography`, and MUI buttons, matching the Flowise source `ExportAsTemplateDialog` structure more closely. | Pass |
| Template category behavior | Category editing now uses chip semantics with Enter/blur-to-add and chip deletion, while still returning the existing `category` string payload expected by AsterERP's strong typed marketplace template endpoint. | Pass |
| Component boundary | The dialog remains pure UI: save still returns `ExportAsTemplatePayload` through `onConfirm` and does not contain hidden API calls. | Pass |
| Verification for this slice | `npm run typecheck` passed after replacing unsupported `Typography fontWeight` props with `sx`; `npm run build` passed; targeted scan confirms `ExportAsTemplateDialog.tsx` no longer contains `NativeDialog`, raw inputs, raw textarea, or `<button>`; Flowise Studio visible-string scan returns no hits; `git diff --check` passed for touched frontend files with CRLF warnings only. Build still reports existing large-chunk warnings and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning. | Pass |
| Remaining gaps | Other Flowise dialog internals outside `native/ui-component/dialog`, full Canvas/runtime parity, authenticated API smoke, permission-deny API smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-25 00:03 +08:00 after the Starter Prompts source-like dynamic form slice. This refresh closes the previous `NativeDialog + textarea` implementation gap for the Starter Prompts list action, while keeping the row score and overall Pass-row completion rate unchanged because remaining dialog internals, Canvas/runtime parity, and browser/API smoke are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| StarterPromptsDialog source movement | `StarterPromptsDialog.tsx` no longer uses `NativeDialog`, raw textarea, or native buttons. It now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `List`, `OutlinedInput`, `InputAdornment`, and add/delete `IconButton` controls, matching the Flowise source `StarterPromptsDialog + StarterPrompts` structure more closely. | Pass |
| Prompt row behavior | Existing prompts normalize to at least one editable row. Users can add rows with a plus icon, remove extra rows with a delete icon, and save trimmed prompts through the existing `onConfirm(prompts)` callback without moving API calls into the dialog. | Pass |
| Info banner | Added a source-like green info banner with a tips icon and i18n text explaining starter prompts only show when there are no chat messages. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed; targeted scan confirms `StarterPromptsDialog.tsx` no longer contains `NativeDialog`, raw textarea, or `<button>`; Flowise Studio visible-string scan returns no hits; `git diff --check` passed for touched frontend files with CRLF warnings only. Build still reports existing large-chunk warnings and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning. | Pass |
| Remaining gaps | Other Flowise dialog internals, full Canvas/runtime parity, authenticated API smoke, permission-deny API smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:59 +08:00 after the Chat Feedback source-like switch dialog slice. This refresh closes the previous `NativeDialog + checkbox` implementation gap for the Chat Feedback list action, while keeping the row score and overall Pass-row completion rate unchanged because remaining dialog internals, Canvas/runtime parity, and browser/API smoke are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| ChatFeedbackDialog source movement | `ChatFeedbackDialog.tsx` no longer uses `NativeDialog`, raw checkbox input, or native buttons. It now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `FormControlLabel`, `Switch`, and MUI buttons, matching the Flowise source `ChatFeedbackDialog + ChatFeedback` structure more closely. | Pass |
| Component boundary | The dialog remains pure UI: save still returns the enabled boolean through `onConfirm` and does not contain hidden API calls, matching the existing AsterERP page/container service boundary. | Pass |
| i18n | Added `zh-CN/en-US` key `flowise.messages.enableChatFeedback`; Flowise Studio visible-string scan returns no hits. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed; targeted scan confirms `ChatFeedbackDialog.tsx` no longer contains `NativeDialog`, raw checkbox, or `<button>`; `git diff --check` passed for touched frontend files with CRLF warnings only. Build still reports existing large-chunk warnings and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning. | Pass |
| Remaining gaps | Other Flowise dialog internals, full Canvas/runtime parity, authenticated API smoke, permission-deny API smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:54 +08:00 after the Allowed Domains source-like dynamic form slice. This refresh closes the previous `NativeDialog + textarea` implementation gap for the Allowed Domains list action, while keeping the row score and overall Pass-row completion rate unchanged because remaining dialog internals, Canvas/runtime parity, and browser/API smoke are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| AllowedDomainsDialog source movement | `AllowedDomainsDialog.tsx` no longer uses `NativeDialog` or a raw multiline textarea. It now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `Stack`, `Typography`, `OutlinedInput`, `InputAdornment`, and add/delete `IconButton` controls to match Flowise source dynamic domain-row behavior. | Pass |
| Domain row behavior | Existing domains normalize to at least one editable row. Users can add rows with a plus icon, remove extra rows with a delete icon, and save trimmed domains split by newline/comma/semicolon through the existing `onConfirm` callback without moving API calls into the dialog. | Pass |
| Error message behavior | The unauthorized-domain error message is now a separate MUI `OutlinedInput`, matching the source `AllowedDomains` component structure instead of the previous generic `<input>` inside `NativeDialog`. | Pass |
| i18n | Added `zh-CN/en-US` keys for the domain placeholder, unauthorized-domain placeholder, allowed-domains tooltip, and error-message tooltip; Flowise Studio visible-string scan returns no hits. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed; targeted scan confirms `AllowedDomainsDialog.tsx` no longer contains `NativeDialog` or `textarea`; `git diff --check` passed for touched frontend files with CRLF warnings only. Build still reports existing large-chunk warnings and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning. | Pass |
| Remaining gaps | Other Flowise dialog internals, full Canvas/runtime parity, authenticated API smoke, permission-deny API smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:47 +08:00 after the Speech To Text provider image asset parity slice. This refresh closes the previous avatar-initial placeholder gap for one FlowListMenu dialog, while keeping the row score and overall Pass-row completion rate unchanged because remaining dialog internals, Canvas/runtime parity, and browser/API smoke are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| SpeechToTextDialog source movement | `SpeechToTextDialog.tsx` no longer uses `NativeDialog` or a raw JSON textarea. It now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `Select`, `MenuItem`, `ListItem`, `ListItemAvatar`, `TextField`, helper text, and save button, matching the Flowise source dialog/extended component structure more closely. | Pass |
| Provider image assets | Copied Flowise source assets `openai.svg`, `assemblyai.png`, `localai.png`, `azure_openai.svg`, and `groq.png` into `frontend/AsterERP.Web/src/features/flowise-studio/native/assets/images`; `SpeechToTextDialog.tsx` imports them statically and renders provider identity through `component="img"` inside a 50x50 white circular image container instead of MUI avatar initials. | Pass |
| Provider registry | Added provider definitions for `openAIWhisper`, `assemblyAiTranscribe`, `localAISTT`, `azureCognitive`, and `groqWhisper`, including the same provider keys used by Flowise source and the same native `credentialId`/`status` JSON pattern. | Pass |
| Provider inputs | The form now renders provider-specific credential, language, prompt, temperature, base URL, model, profanity filter mode, and audio channels inputs. Saving marks one provider active and turns the other provider statuses off, matching the Flowise source behavior. | Pass |
| Credential boundary | The dialog remains a pure UI component with typed props. Credentials are loaded in `native/views/chatflows/index.tsx` through the existing Flowise Credentials API and passed down to `FlowListMenu`/`SpeechToTextDialog`; the dialog does not hide API calls. | Pass |
| i18n | Added `zh-CN/en-US` keys for STT providers, provider inputs, descriptions, and credential-required validation; Flowise Studio visible-string scan still returns no hits. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed; targeted STT scan confirms static provider icon imports, `component="img"`, and no `Avatar` component import; Flowise Studio visible-string scan still returns no hits; frontend build emitted the copied PNG provider assets. Build still reports existing large-chunk warnings and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning. | Pass |
| Remaining gaps | Other Flowise dialog internals, full Canvas/runtime parity, authenticated API smoke, permission-deny API smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:41 +08:00 after the Speech To Text provider-form dialog slice. This refresh closes the previous JSON-textarea implementation gap for one FlowListMenu dialog, while keeping the row score and overall Pass-row completion rate unchanged because provider image assets, remaining dialog internals, Canvas/runtime parity, and browser/API smoke were still open at that point.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| SpeechToTextDialog source movement | `SpeechToTextDialog.tsx` no longer uses `NativeDialog` or a raw JSON textarea. It now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `Select`, `MenuItem`, `ListItem`, `Avatar`, `TextField`, helper text, and save button, matching the Flowise source dialog/extended component structure more closely. | Pass |
| Provider registry | Added provider definitions for `openAIWhisper`, `assemblyAiTranscribe`, `localAISTT`, `azureCognitive`, and `groqWhisper`, including the same provider keys used by Flowise source and the same native `credentialId`/`status` JSON pattern. | Pass |
| Provider inputs | The form now renders provider-specific credential, language, prompt, temperature, base URL, model, profanity filter mode, and audio channels inputs. Saving marks one provider active and turns the other provider statuses off, matching the Flowise source behavior. | Pass |
| Credential boundary | The dialog remains a pure UI component with typed props. Credentials are loaded in `native/views/chatflows/index.tsx` through the existing Flowise Credentials API and passed down to `FlowListMenu`/`SpeechToTextDialog`; the dialog does not hide API calls. | Pass |
| i18n | Added `zh-CN/en-US` keys for STT providers, provider inputs, descriptions, and credential-required validation; Flowise Studio visible-string scan still returns no hits. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; targeted STT symbol scan confirms provider keys, `credentialId`, credential query, and validation key usage; `git diff --check` passed for the touched frontend files with CRLF warnings only. Build still reports existing large-chunk warnings and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning. | Pass |
| Remaining gaps | This historical row was superseded by the 23:47 provider image asset parity slice. Other Flowise dialog internals, full Canvas/runtime parity, authenticated API smoke, permission-deny API smoke, workspace-boundary checks, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:33 +08:00 after the FlowListMenu action-permission backend closure and Save/Tag MUI dialog slice. This refresh closes two concrete risks: the frontend menu no longer points to backend endpoints with mismatched permissions for delete/config/domains/template export, and two source-named list dialogs no longer use the custom `NativeDialog` shell. It does not change the row score or overall Pass-row completion rate because Speech To Text provider semantics, remaining dialog internals, runtime parity, and browser screenshot/API smoke are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| SaveChatflowDialog source movement | `SaveChatflowDialog.tsx` now uses MUI `Dialog`, `DialogTitle`, `DialogContent`, `DialogActions`, `OutlinedInput`, and `Button`, with full-width `xs` dialog, source-style `chatflow-name` input id, `My New Chatflow` placeholder, empty-name disabled confirm, and Enter-to-confirm behavior. | Pass |
| TagDialog source movement | `TagDialog.tsx` now uses MUI `Dialog`, `Box`, `TextField`, `Chip`, `Typography`, and `Button`, supports Enter-to-add tags, chip deletion, submit-time merge of the current input, and Flowise-style category help text through i18n. | Pass |
| Backend action permission closure | Added `chatflows/{id}/configuration`, `chatflows/{id}/domains`, `agentflows/{id}/configuration`, and `agentflows/{id}/domains` endpoints with action-specific permission attributes. Delete endpoints now require `FlowiseChatflowsDelete` / `FlowiseAgentflowsDelete`. Service-layer guards enforce the same action boundaries and only write allowed fields for configuration/domains patches. | Pass |
| Template export permission closure | Added `marketplaces/from-flow-template` endpoint and `CreateFromFlowTemplateAsync`, guarded by `FlowiseTemplatesFlowExport`; the list page now saves templates through this endpoint instead of requiring general Marketplace edit permission. | Pass |
| Frontend action API wiring | `nativeChatflowsApi` now has `updateConfiguration` and `updateDomains`; `nativeResources.api.ts` now has `createFromFlowTemplate`; `FlowListMenu` passes a typed action to `onSaveFlow`; `native/views/chatflows/index.tsx` routes update/config/domains/template operations to the matching endpoints. | Pass |
| Verification for this slice | `dotnet build AsterERP.sln --no-restore` passed; `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` passed 88/88; `npm run typecheck` passed; `npm run build` passed; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; `git diff --check` passed with CRLF warnings only. Existing NuGet vulnerability warnings, Vite large-chunk warnings, and the `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning remain recorded risks. | Pass |
| Remaining gaps | This historical row was superseded by the 23:41 Speech To Text provider-form slice. Provider image assets, other Flowise dialog internals, full Canvas/runtime parity, authenticated API smoke, permission-deny API smoke, workspace-boundary checks, and browser screenshot parity are still incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:18 +08:00 after the FlowListMenu distinct action permission slice. This refresh closes the previous one-permission Options menu gap for Chatflows/Agentflows list actions; it does not change the row score or overall Pass-row completion rate because exact dialog internals and browser screenshot parity are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Backend permission codes | Added distinct Flowise action permission constants for Chatflow and Agentflow duplicate, export, configuration, allowed domains, and delete, plus template flow export. | Pass |
| Menu seed action permissions | `AiCenterAppModule` now upserts Flowise flow action button permissions for Rename/Edit, Duplicate, Export, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and Delete under both `flowise:chatflows` and `flowise:agentflows`. | Pass |
| Frontend permission contract | `permissionCodes.ts` now exposes the matching frontend constants, and `FlowListMenu.tsx` now accepts a typed `FlowListMenuPermissions` map instead of one shared permission string. | Pass |
| Menu item authorization | `FlowListMenu` hides the whole Options menu when the current user has no available action permission, and each MUI `MenuItem` is gated by its specific action permission while keeping API orchestration in the page container. | Pass |
| Verification for this slice | `dotnet build AsterERP.sln --no-restore` passed; `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` passed 88/88; `npm run typecheck` passed; `npm run build` passed; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; targeted FlowListMenu prop scan found only `permissions=` usage; `git diff --check` passed for the touched backend/frontend files. Build still reports existing NuGet vulnerability warnings, Vite large-chunk warnings, and a `workflow-bpmn -> vendor -> workflow-bpmn` circular chunk warning. | Pass |
| Remaining Chatflows/Agentflows gaps | Exact Flowise dialog component internals and browser screenshot parity are still incomplete. Broader Canvas, built-in dialogs, runtime execution, and final API/browser smoke gaps also remain open. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:13 +08:00 after the FlowListMenu MUI source-structure migration slice. This refresh records a real source-scheme migration inside the same Chatflows/Agentflows row; it does not change the row score or overall Pass-row completion rate because exact permission-id mapping, exact dialog internals, and browser screenshot parity are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Flowise UI dependencies | Added `@mui/material`, `@mui/icons-material`, `@emotion/react`, and `@emotion/styled` to `frontend/AsterERP.Web/package.json` and lockfile so Flowise native UI components can use the same UI library family as the source. | Pass |
| MUI menu structure | `FlowListMenu.tsx` now uses MUI `Button`, styled `Menu`, `MenuItem`, and `Divider` instead of the previous hand-rolled absolute-position menu. | Pass |
| Source icon map | The menu now uses MUI source-aligned icons: `Edit`, `FileCopy`, `Downloading`, `BookmarksOutlined`, `PictureInPictureAlt`, `ThumbsUpDownOutlined`, `VpnLockOutlined`, `MicNoneOutlined`, `Category`, `Delete`, and `KeyboardArrowDown`. | Pass |
| StyledMenu parity | Added source-shaped `StyledMenu` with zero elevation, bottom-right/top-right anchoring, `borderRadius: 6`, `marginTop: theme.spacing(1)`, `minWidth: 180`, Flowise source box-shadow, `MuiMenu-list` padding, 18px SVG icons, secondary icon color, `theme.spacing(1.5)` icon margin, and active background via `alpha(theme.palette.primary.main, theme.palette.action.selectedOpacity)`. | Pass |
| Permission item boundary | Added `FlowListPermissionMenuItem` using the existing AsterERP `usePermission` hook; it hides unauthorized menu items without moving business API calls into the menu component. | Pass |
| Verification for this slice | `npm install @mui/material @mui/icons-material @emotion/react @emotion/styled` completed; `npm run typecheck` passed; `npm run build` passed; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; `git diff --check` passed for the dependency and component files. Build reports existing large-chunk warnings plus a circular chunk warning involving `workflow-bpmn -> vendor -> workflow-bpmn`, which is a remaining build-risk note, not a failed build. | Pass |
| Remaining Chatflows/Agentflows gaps | Flowise source has distinct permission IDs per menu action (`chatflows:update`, `chatflows:duplicate`, `templates:flowexport`, etc.); AsterERP still passes one permission code into this component. Exact source dialog internals and browser screenshot parity are still incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:06 +08:00 after the FlowListMenu keyboard roving-focus slice. This refresh records another concrete UI behavior improvement inside the same Chatflows/Agentflows row; it does not change the row score or overall Pass-row completion rate because literal MUI `StyledMenu`/`PermissionMenuItem` implementation parity and browser screenshot parity are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Keyboard movement | `FlowListMenu.tsx` now handles `ArrowDown`, `ArrowUp`, `Home`, and `End` on the menu container and moves focus among `role="menuitem"` buttons. | Pass |
| Wrap behavior | Arrow navigation wraps from the last item to the first and from the first item to the last, matching common MenuList behavior. | Pass |
| Component boundary | The new behavior is UI-only and uses the existing menu ref; it does not add API calls, resource calls, generic table code, or persistence logic inside the reusable menu. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; `git diff --check` passed for the touched component. | Pass |
| Remaining Chatflows/Agentflows gaps | Literal Flowise MUI `StyledMenu`/`PermissionMenuItem` implementation parity, browser screenshot parity, exact source dialog internals, and broader page/canvas/runtime gaps remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:03 +08:00 after the FlowListMenu source-like interaction slice. This refresh records UI behavior movement inside the same Chatflows/Agentflows row; it does not change the row score or overall Pass-row completion rate because literal MUI `StyledMenu`/`PermissionMenuItem` implementation parity and browser screenshot parity are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Menu close semantics | `FlowListMenu.tsx` now closes on outside pointer down and closes on `Escape`, matching expected MUI menu behavior better than the previous click-only toggle. | Pass |
| Menu focus semantics | Opening the menu now focuses the first `role="menuitem"` button, giving keyboard users an immediate actionable target and moving closer to Flowise/MUI menu interaction behavior. | Pass |
| Menu ARIA state | The Options trigger now exposes `aria-haspopup="menu"`, `aria-expanded`, and `aria-controls`; the menu has a stable React `useId` id and keeps `role="menu"` / `role="menuitem"` semantics. | Pass |
| Action state cleanup | Direct actions such as Duplicate, Export, and Delete now close the menu before invoking the typed callback; dialog-opening actions already close through `openDialog`. | Pass |
| Component responsibility | The menu still has no hidden API calls. UI-only state stays inside `FlowListMenu`; persistence remains in the page/container callbacks. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; `git diff --check` passed for the touched component. | Pass |
| Remaining Chatflows/Agentflows gaps | This is still not a literal MUI `StyledMenu`/`PermissionMenuItem` source migration. Exact keyboard roving-index behavior, screenshot parity, and full source dialog internals remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 23:00 +08:00 after the FlowListMenu source-like visual menu slice. This refresh records real frontend UI implementation movement inside the same Chatflows/Agentflows row; it does not change the row score or overall Pass-row completion rate because literal MUI `StyledMenu`/`PermissionMenuItem` parity and browser screenshot parity are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Source-like menu trigger | `FlowListMenu.tsx` now renders the Options trigger with a visible caret icon and text span, closer to Flowise's source menu affordance than the previous plain button. | Pass |
| Source-like menu item structure | `FlowListMenu.tsx` now uses a dedicated `FlowListMenuItem` helper with icon, label, tone, and `role="menuitem"` for Rename, Duplicate, Export, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and Delete; the menu container uses `role="menu"`. | Pass |
| Source-like visual styling | `flowise-pages.css` now gives the menu 180px min width, 4px list padding, 6px radius, MUI-like layered shadow, 12px icon/label gap, 18px icon slots, hover/focus background, and danger-state coloring. | Pass |
| Architecture constraints | No MUI dependency was introduced in this slice; no generic CRUD/table/resource API path was added; the menu still delegates persistence to typed native callbacks instead of hiding API calls inside a reusable item component. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining Chatflows/Agentflows gaps | The visual implementation is closer to Flowise source but is not yet a literal migration of Flowise MUI `StyledMenu`/`PermissionMenuItem`; exact screenshot parity and full source dialog internals remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 22:55 +08:00 after the Save As Template Marketplace persistence slice. This refresh records real frontend/API-chain implementation movement inside the same Chatflows/Agentflows row; it does not change the row score or overall Pass-row completion rate because exact MUI visual parity, exact source dialog internals, and browser screenshot parity are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Template persistence chain | `ExportAsTemplateDialog.tsx` now collects template key, display name, description, and category instead of just exporting a file; `FlowListMenu.tsx` passes a typed `ExportAsTemplatePayload`; `native/views/chatflows/index.tsx` calls `flowiseNativeResourcesApi.marketplaces.create` to persist the sanitized Flowise `flowData` into the strong typed Marketplace template service/table. | Pass |
| Template metadata | The persisted Marketplace template includes `definitionJson` from sanitized native Flowise `{ nodes, edges }`, source flow id/name/type in `metadataJson`, category, status `enabled`, and source workspace id. | Pass |
| i18n and UX feedback | Added `templateSaved` and `templateSaveFailed` keys in `zh-CN/en-US`; template save uses the existing mutation success/error feedback path and disables menu actions while the save is pending. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; Flowise Studio visible-string scan returned no hits; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining Chatflows/Agentflows gaps | Exact Flowise MUI `StyledMenu`/`PermissionMenuItem` visuals, exact source dialog internals beyond the current typed component split, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 22:51 +08:00 after the FlowListMenu source-dialog split slice. This refresh records real frontend implementation movement inside the same Chatflows/Agentflows row; it does not change the row score or overall Pass-row completion rate because exact MUI visual parity, template persistence, and browser screenshot parity are still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Source dialog file structure | Added source-named dedicated dialog files under `native/ui-component/dialog`: `SaveChatflowDialog.tsx`, `TagDialog.tsx`, `StarterPromptsDialog.tsx`, `ChatFeedbackDialog.tsx`, `AllowedDomainsDialog.tsx`, `SpeechToTextDialog.tsx`, and `ExportAsTemplateDialog.tsx`. | Pass |
| FlowListMenu responsibility split | `FlowListMenu.tsx` no longer contains the inline `FlowListOptionsDialog` or inline dialog draft builder. It now owns menu/dialog open state and emits typed save/export/delete callbacks while the page remains responsible for API orchestration. | Pass |
| Native field preservation | Rename, category, starter prompts, chat feedback, allowed domains, speech-to-text JSON, and template export continue to update native Flowise fields through the existing `onSaveFlow` and export callbacks; no generic CRUD/table path was introduced. | Pass |
| i18n/style cleanup | Added `categorySeparatorHint`, `onePromptPerLine`, and `oneDomainPerLine` keys in `zh-CN/en-US`; changed `NativeDialog` close text from literal `x` to `×`; added dialog hint and template summary styles. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan returned no hits for WorkflowModel/BPMN/projection/generic resource/generic CRUD-table symbols; targeted legacy inline-dialog symbol scan returned no hits; `git diff --check` passed with CRLF warnings only. | Pass |
| Remaining Chatflows/Agentflows gaps | Exact Flowise MUI `StyledMenu`/`PermissionMenuItem` visuals, exact source dialog internals beyond the current typed component split, full marketplace-backed template persistence, and browser screenshot parity remain incomplete. This historical row was superseded by the 22:55 implementation slice for template persistence. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 22:42 +08:00 after the Flowise node-icon endpoint/rendering slice. This refresh records real backend/frontend implementation movement inside the same Chatflows/Agentflows row; it does not change the row score or overall Pass-row completion rate because exact source visual/dialog/browser parity is still open.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Flowise native node-icon endpoint | Added `/api/v1/node-icon/{name}` plus `/api/ai/flowise/nodes/icon/{name}` backed by the Flowise node catalog service. The endpoint returns SVG file responses, matching the Flowise source image URL shape used by `views/chatflows/index.jsx`. | Pass |
| Node catalog ownership | Moved the static Flowise node directory into `FlowiseCanvasNodeCatalog` so the icon endpoint and canvas catalog share one source without introducing WorkflowModel/BPMN/projection. Canvas node catalog API still keeps its permission check; image file rendering can use the Flowise source `<img src>` path. | Pass |
| Frontend node previews | Chatflows/Agentflows list now builds `imageSrc` from `flowData.nodes[].data.name` as `${apiBaseUrl}/v1/node-icon/{name}` and renders image stacks with card/list limits and localized `+ {count} More` text. | Pass |
| Verification for this slice | `dotnet build AsterERP.sln --no-restore` passed with existing NuGet vulnerability warnings; `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` passed 88/88; `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan and Flowise Studio visible-string scan returned no hits. | Pass |
| Remaining Chatflows/Agentflows gaps | Exact MUI `StyledMenu`/`PermissionMenuItem` visuals, exact source dialogs (`SaveChatflowDialog`, `TagDialog`, `StarterPromptsDialog`, `ChatFeedbackDialog`, `AllowedDomainsDialog`, `SpeechToTextDialog`, `ExportAsTemplateDialog`), full template marketplace persistence, and browser screenshot parity remain incomplete. | Fail |

## Current Actual Progress Documentation Refresh

Updated on 2026-06-24 22:44 +08:00 for the current actual-progress documentation request. This refresh is documentation-only and records the current verified state after the node-icon endpoint/rendering slice; it does not change implementation code, row scores, or completion rate.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Latest implementation slice reflected | The latest implemented slice remains the Flowise node-icon endpoint/rendering work: `FlowiseCanvasNodeCatalog`, `FlowiseNodeIcon`, `/api/v1/node-icon/{name}`, `/api/ai/flowise/nodes/icon/{name}`, and Chatflows/Agentflows image stack rendering from `flowData.nodes[].data.name`. | Pass |
| Protocol and generic-chain constraints | The latest recorded scoped scans still show no Flowise use of approval `WorkflowModel`, `BPMN`, compatibility projection fields, split canvas persistence, generic Flowise resource chain, or generic AsterERP `CrudPage`/`DataTable`/`useCrudResource` inside the Flowise implementation scope. | Pass |
| Verification freshness | Latest recorded verification remains: `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` 88/88, `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, and Flowise Studio visible-string scan. No authenticated API smoke or browser screenshot matrix was run for this documentation-only refresh. | Partial |
| Rows still preventing 100% | Execution chain, Chatflows/Agentflows frontend page exact source parity, Canvas UI source parity, Built-in dialogs, i18n after full source migration, and final Browser/API smoke remain incomplete or blocked. | Fail |
| Next required closure | Continue implementation on exact Flowise source dialogs and visual behavior first, then close full Canvas/Agentflow v2, full node-by-node runtime semantics, provider-backed STT/TTS/upload capability behavior, and final authenticated API/browser screenshot matrix. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 22:35 +08:00 after the Chatflows/Agentflows source-component structure slice. This refresh records real frontend implementation movement and increases the Chatflows/Agentflows frontend page row from `4/6` to `5/6`; it still does not change the row status or overall Pass-row completion rate because exact source visual/dialog/browser parity is not closed.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Dedicated FlowListMenu component | Added `native/ui-component/button/FlowListMenu.tsx`; the component owns source-like menu/dialog UI state and emits typed callbacks only, with no hidden backend API calls. The page container remains responsible for save/delete/export orchestration. | Pass |
| Dedicated card/table component usage | `native/views/chatflows/index.tsx` now renders card mode through `native/ui-component/cards/ItemCard.tsx` and list mode through `native/ui-component/table/FlowListTable.tsx`; the old page-local card and row implementations are no longer the main rendering path. | Pass |
| Source list-table behavior movement | `FlowListTable` now supports sortable columns and the page persists sort order/orderBy with Flowise source-style `chatflowcanvas_order`, `chatflowcanvas_orderBy`, `agentcanvas_order`, and `agentcanvas_orderBy` keys. | Partial |
| Source menu cleanup | Removed the non-source raw `flowData` Edit dialog/menu action from the Chatflows/Agentflows list page; remaining list edit actions go through source-like Rename/Category/Starter Prompts/Chat Feedback/Allowed Domains/Speech To Text dialogs. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan found no Flowise use of `WorkflowModel`, `BPMN`, split canvas projection, generic resource chain, or generic AsterERP CRUD/table symbols; Flowise Studio visible-string scan returned no hits; targeted scan found no remaining `FlowListOptionsMenu` or `FlowiseNativeFlowDialog` in the Chatflows/native UI paths. | Pass |
| Remaining Chatflows/Agentflows gaps | Exact MUI `StyledMenu`/`PermissionMenuItem` visuals, exact Flowise node icon endpoint image rendering, exact `SaveChatflowDialog`, `TagDialog`, `StarterPromptsDialog`, `ChatFeedbackDialog`, `AllowedDomainsDialog`, `SpeechToTextDialog`, `ExportAsTemplateDialog`, full template marketplace persistence, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Documentation Refresh

Updated on 2026-06-24 22:27 +08:00 for the current actual-progress documentation request. This refresh is documentation-only and records the latest verified implementation state without changing the completion score.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Latest implementation movement already recorded | The 22:25 FlowListMenu source-action slice remains the latest implementation slice: native Options menu actions, native field persistence, template-shaped export, frontend typecheck/build, scoped forbidden-symbol scan, and Flowise Studio visible-string scan are recorded below. | Pass |
| Can report 100% | No. The remaining failing rows are still Execution chain, Chatflows/Agentflows frontend page, Canvas UI source parity, Built-in dialogs, i18n, and final authenticated API/browser smoke. | Fail |
| Verification freshness | No new build/test/API/browser verification was run for this documentation-only update. The latest recorded frontend verification remains `npm run typecheck`, `npm run build`, scoped forbidden-symbol scan, and visible-string scan from the 22:25 implementation slice. | Partial |
| Next required closure | Continue implementation toward exact Flowise `ItemCard`/`FlowListTable`/`FlowListMenu`, node icon endpoint parity, exact source dialogs, full Canvas/Agentflow v2 source behavior, full Flowise node-by-node runtime, provider-backed STT/TTS/runtime upload semantics, and final smoke/screenshot matrix. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 22:25 +08:00 after the FlowListMenu source-action slice. This refresh records real frontend implementation movement and increases the Chatflows/Agentflows frontend page row from `3/6` to `4/6`; it still does not change the row status or overall Pass-row completion rate because exact source component and visual parity are not closed.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| FlowListMenu action coverage | `native/views/chatflows/index.tsx` now has a source-like Options menu for Rename, Duplicate, Export, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, Edit, and Delete. | Partial |
| Native field persistence | Rename and Update Category save native root fields; Starter Prompts, Chat Feedback, and Allowed Domains update `chatbotConfig`; Speech To Text updates the native `speechToText` JSON field; all saves use `nativeChatflowsApi.update` and refresh the list. | Partial |
| Template action movement | Save As Template now exports a template-shaped JSON payload using sanitized Flowise `flowData`; it is not yet backed by the exact Flowise `ExportAsTemplateDialog` marketplace/template persistence semantics. | Partial |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan found no Flowise use of `WorkflowModel`, `BPMN`, split canvas projection, generic resource chain, or generic AsterERP CRUD/table symbols; Flowise Studio visible-string scan returned no hits. | Pass |
| Remaining Chatflows/Agentflows gaps | Exact Flowise MUI `StyledMenu`/`PermissionMenuItem` visuals, `ItemCard`, `FlowListTable`, real node icon endpoint rendering, exact `SaveChatflowDialog`, `TagDialog`, `StarterPromptsDialog`, `ChatFeedbackDialog`, `AllowedDomainsDialog`, `SpeechToTextDialog`, `ExportAsTemplateDialog`, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 22:19 +08:00 after the Chatflows/Agentflows source-semantics slice. This refresh records real frontend implementation movement and increases the Chatflows/Agentflows frontend page row from `2/6` to `3/6`; it still does not change the row status or overall Pass-row completion rate because exact Flowise source page/menu parity is not closed.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Add New source semantics | `/flowise/canvas`, `/flowise/agentcanvas`, and `/flowise/v2/agentcanvas` now stay on an empty canvas instead of redirecting to lists. Saving an unsaved canvas opens a save dialog, creates a native Flowise Chatflow/Agentflow through `nativeChatflowsApi.create`, and navigates to the newly created canvas id. | Partial |
| Duplicate/import source semantics | List-page duplicate now stores `duplicatedFlowData` in `localStorage` and opens the empty canvas, matching the Flowise source flow shape more closely than the previous immediate database copy. JSON import now accepts Flowise export-shaped `{ nodes, edges }`, existing DTO `flowData`, or wrapped `{ flow.flowData }`, stores it as `duplicatedFlowData`, and opens the empty canvas for review/save. | Partial |
| Export source semantics | List-page export now parses native `flowData` and writes a sanitized Flowise `{ nodes, edges }` payload, removing password/file/folder inputs and recursive `FLOWISE_CREDENTIAL_ID`, instead of exporting an AsterERP DTO wrapper. | Partial |
| Node preview movement | Chatflow/Agentflow cards and list rows now parse `flowData.nodes`, skip sticky notes, dedupe node names, and show node preview chips. This is still not exact Flowise node-icon parity because no AsterERP `/api/v1/node-icon/{name}` equivalent was found in the current source scan. | Partial |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan found no Flowise use of `WorkflowModel`, `BPMN`, split canvas projection, generic resource chain, or generic AsterERP CRUD/table symbols; Flowise Studio visible-string scan returned no hits. | Pass |
| Remaining Chatflows/Agentflows gaps | Exact Flowise `ItemCard`, `FlowListTable`, `FlowListMenu`, node icon endpoint, Rename, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and browser screenshot parity remain incomplete. | Fail |

## Previous Actual Progress Implementation Refresh

Updated on 2026-06-24 22:11 +08:00 after the Chatflows/Agentflows list-page action and persistence-state slice. This refresh records real frontend implementation movement; it still does not change the row score because the Flowise source list page and `FlowListMenu` behavior are not fully migrated.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Chatflows/Agentflows list state | `native/views/chatflows/index.tsx` now persists card/list display mode with `localStorage`, persists page size, performs server-side paged list calls with `pageIndex/pageSize/keyword`, exposes refresh, search clear, page-size selection, previous/next pagination, and fixes local filtering to include name, category, id, and type instead of name only. | Partial |
| Chatflows/Agentflows list actions | Added delete confirmation, duplicate through the existing native create API, JSON export from the selected flow record, JSON import through the existing native create API, success/failure feedback messages, and zh-CN/en-US i18n keys for these new actions. | Partial |
| Source parity limits for this slice | Flowise source `views/chatflows/index.jsx` still differs: Add New navigates directly to empty canvas, Duplicate stores `duplicatedFlowData` and opens a new canvas instead of immediately persisting a copy, Export uses `generateExportFlowData(flowData)` rather than an AsterERP DTO wrapper, node icon previews are parsed from `flowData.nodes`, and deeper `FlowListMenu` actions such as Rename, Save As Template, Starter Prompts, Chat Feedback, Allowed Domains, Speech To Text, Update Category, and exact table/card component behavior remain incomplete. | Fail |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scan found no Flowise use of `WorkflowModel`, `BPMN`, split canvas projection, generic resource chain, or generic AsterERP CRUD/table symbols; Flowise Studio visible-string scan returned no hits. | Pass |
| Remaining global blockers | Full Flowise node-by-node runtime semantics, exact Chatflows/Agentflows source page and `FlowListMenu` parity, full source Canvas/Agentflow v2/Redux/MUI behavior, full source dialog set, full MCP SDK transport parity, upload capability negotiation, provider-backed STT/TTS runtime, i18n after all remaining migrations, and authenticated API/browser screenshot matrix. | Fail |

## Latest Actual Progress Implementation Refresh

Updated on 2026-06-24 22:03 +08:00 after the Custom MCP Server discovered-tools interaction and visual detail slice. This refresh records a real frontend implementation increment; it still does not change the row score because broader Flowise source parity remains incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Custom MCP discovered tools | `CustomMcpServerDialog.tsx` now supports an outer Discovered Tools accordion, multiple expanded tools, Expand all, Collapse all, search clear, filtered/total counts while searching, annotations-title search, current-theme `dark/light` icon selection through `useThemeStore`, risk chips visible in the collapsed tool header, Flowise-style hint tooltip text, compact hint icons, required/optional chips, `integer` support, Flowise `TYPE_CHIP_COLOR` light/dark type-chip colors, parameter-count tooltip, and enum values rendered as outlined individual chips. | Partial |
| i18n for this slice | Added `flowise.customMcp.expandAll` and `flowise.customMcp.collapseAll` to `flowiseI18nKeys`, `flowiseMessages.en-US`, and `flowiseMessages.zh-CN`; replaced the historical bare `x` close button in `native/views/chatflows/index.tsx` with a translated-title `×` button. The current Flowise Studio visible-string scan has no matches. | Pass |
| Verification for this slice | `npm run typecheck` passed; `npm run build` passed with existing large chunk warnings; scoped forbidden-symbol scans found no Flowise use of WorkflowModel/BPMN/projection/generic resource/generic CRUD symbols; the visible-string scan now returns no hits in `frontend/AsterERP.Web/src/features/flowise-studio`. | Pass |
| Remaining Custom MCP gaps | Exact MUI component fidelity, Tabler icon glyph parity, default-label exact text parity, and browser screenshot parity still remain. Expand/collapse all, outer accordion, search clear, annotations-title search, header risk chips, hint tooltip text, `TYPE_CHIP_COLOR`, `integer` type, required/type/enum chips, and theme-aware icon selection are no longer open gaps. | Fail |
| Remaining global blockers | Full Flowise node-by-node runtime semantics, full source Chatflows/Agentflows page actions/layout, full source Canvas/Agentflow v2/Redux/MUI behavior, full source dialog set, full MCP SDK transport parity, upload capability negotiation, provider-backed STT/TTS runtime, i18n after all remaining migrations, and authenticated API/browser screenshot matrix. | Fail |

## Latest Actual Progress Documentation Refresh

Updated on 2026-06-24 21:46 +08:00 for the current progress-documentation request. This refresh changed documentation only; it did not implement new Flowise parity features and therefore does not change the acceptance-row score.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Can report 100% | No. The remaining failing rows still cover execution runtime semantics, Chatflows/Agentflows source page parity, Canvas/UI source parity, built-in dialog parity, full i18n after all source components are migrated, and final authenticated API/browser smoke. | Fail |
| Flowise dedicated protocol boundary | Reran scoped forbidden-symbol scans on `frontend/AsterERP.Web/src/features/flowise-studio`, `backend/AsterERP.Api/Application/Ai/Flowise`, `backend/AsterERP.Api/Modules/Ai/Flowise`, `backend/AsterERP.Contracts/Ai/Flowise`, and `backend/AsterERP.Api/Controllers` filtered to `AiFlowise*.cs`. No `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `ai_flowise_canvas*`, or split canvas projection symbols were found in the Flowise scope. | Pass |
| Generic resource / generic table ban | The same scoped scans found no `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, or `ai_flowise_resources` symbols in the Flowise scope. | Pass |
| Generic AsterERP CRUD/table ban | The same scoped scans found no `CrudPage`, `DataTable`, or `useCrudResource` usage inside `frontend/AsterERP.Web/src/features/flowise-studio`. | Pass |
| i18n visible-string scan | Reran the Flowise Studio bare English JSX/placeholder/aria-label scan. The only hit remains the pre-existing close button text `x` in `frontend/AsterERP.Web/src/features/flowise-studio/native/views/chatflows/index.tsx`. This is not a full i18n pass because the remaining Flowise source component set is not fully migrated. | Partial |
| Verification freshness | This refresh reran documentation-scoped forbidden-symbol and visible-string scans only. The latest recorded full verification remains `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` 88/88, `npm run typecheck`, and `npm run build` from the current implementation slice. | Partial |
| Remaining implementation blockers | Full Flowise node-by-node runtime semantics, full source Chatflows/Agentflows page actions/layout, full source Canvas/Agentflow v2/Redux/MUI behavior, full source dialog set, full MCP SDK transport parity, Custom MCP remaining visual 1:1 gaps, upload capability negotiation, provider-backed STT/TTS runtime, i18n after all remaining migrations, and authenticated API/browser screenshot matrix. | Fail |

## Current Actual Progress Documentation Update

Updated on 2026-06-24 21:43 +08:00 after the Tools Custom MCP Server detail-parity slice. This update records new backend/frontend implementation progress but does not change the acceptance-row score because broader source parity is still incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Current completion rate | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Can report 100% | No. The implementation still has failing or blocked rows for execution runtime semantics, Chatflows/Agentflows source page parity, Canvas/UI source parity, built-in dialog parity, full i18n after remaining migrations, and final authenticated API/browser smoke. | Fail |
| Flowise dedicated protocol boundary | Reran scoped Flowise scans on `frontend/AsterERP.Web/src/features/flowise-studio`, `backend/AsterERP.Api/Application/Ai/Flowise`, `backend/AsterERP.Api/Modules/Ai/Flowise`, `backend/AsterERP.Contracts/Ai/Flowise`, and `backend/AsterERP.Api/Controllers/AiFlowise*.cs`; no `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, `ai_flowise_canvas*`, or split canvas projection symbols were found. Flowise remains documented as a dedicated `FlowiseChatFlowEntity.FlowData` protocol path. | Pass |
| Generic resource / generic table ban | Reran scoped scans and found no `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, or `ai_flowise_resources` symbols in Flowise source paths. | Pass |
| Generic AsterERP CRUD/table ban | Reran scoped scans and found no `CrudPage`, `DataTable`, or `useCrudResource` usage inside `frontend/AsterERP.Web/src/features/flowise-studio`. | Pass |
| i18n visible-string scan | Reran the Flowise Studio bare English JSX/placeholder/aria-label scan. The only hit is the pre-existing close button text `x` in `frontend/AsterERP.Web/src/features/flowise-studio/native/views/chatflows/index.tsx`; no new untranslated English UI text was found by this scan. Full i18n is still not Pass because remaining source components are not fully migrated. | Partial |
| Latest implementation slice reflected | Chatflow built-in MCP Server configuration, first `/api/v1/mcp/{chatflowId}` token-authenticated JSON-RPC runtime slice, Tools-page Custom MCP Server dedicated resource chain, the Tools page Custom MCP source-alignment slice, and the latest detail-parity slice are recorded. The latest slice added backend `flowise:tools:create/update/delete` permission codes and menu seed entries, aligned Controller/Service checks, preserved MCP `annotations` and `icons` from `tools/list`, standardized the redaction token to `************`, added backend redacted header/token merge and partial-mask rejection, and extended the frontend dialog with update-or-create authorize permission, permission-wrapped save buttons, annotation chips, tool icons, enum/default display, and legacy mask normalization. | Partial |
| Verification freshness | Latest verification passed: `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` 88/88, `npm run typecheck`, `npm run build`, scoped forbidden-symbol scans, and Flowise Studio visible-string scan with only the pre-existing `x` close button hit. Build warnings are existing NuGet vulnerability and Vite large chunk warnings. | Partial |
| Remaining implementation blockers | Flowise node-by-node runtime semantics, full source Chatflows/Agentflows page actions/layout, full source Canvas/Agentflow v2/Redux/MUI behavior, full source dialog set, full MCP SDK transport parity, Custom MCP remaining visual 1:1 gaps such as exact MUI spacing/chips/table/card fidelity and expand-all/collapse-all controls, upload capability negotiation, provider-backed STT/TTS runtime, i18n after all remaining migrations, and authenticated API/browser screenshot matrix. | Fail |

## Previous Actual Progress Documentation Refresh

Updated on 2026-06-24 for the earlier progress-doc request. That refresh changed documentation only and did not change the completion rate. The later MCP Server configuration and first MCP runtime JSON-RPC slices add implementation code but still do not change the overall row-based completion rate because full MCP SDK Streamable HTTP parity, Custom MCP Server resource chain, and broader source-parity rows remain incomplete.

| Check | Actual Result | Status |
|---|---|---|
| Completion rate after refresh | `13/18 Pass = 72.22%` | Fail to reach 100% |
| Can report 100% | No. Six acceptance rows are still `Fail` or `Blocked`: Execution chain, Chatflows/Agentflows frontend page, Canvas UI source parity, Built-in dialogs, i18n, Browser/API smoke. | Fail |
| Flowise protocol boundary | Current scoped scans show no Flowise use of approval `WorkflowModel`, BPMN, compatibility projection fields, or split canvas persistence symbols in Flowise module paths. | Pass |
| Generic resource / generic table ban | Current docs record that the generic Flowise resource table/service/page chain has been removed from the source paths and replaced with dedicated Flowise entities/services/APIs. | Pass |
| Generic AsterERP CRUD/table ban | Current docs record no `CrudPage`, `DataTable`, or `useCrudResource` usage inside `features/flowise-studio`. | Pass |
| Verification freshness | Latest documented frontend typecheck/build and forbidden-symbol scans passed after the ChatPopUp and Configuration slices. No new build/test/API/browser verification was run for this documentation-only refresh. | Blocked for final acceptance |
| Remaining implementation blockers | Flowise node-by-node runtime semantics, full source Chatflows/Agentflows page actions/layout, full source canvas/agentflow v2/Redux/MUI behavior, full MCP SDK Streamable HTTP parity, Custom MCP Server resource chain, upload capability negotiation, provider-backed STT/TTS runtime, and final authenticated API/browser smoke matrix. | Fail |

### MCP Source Scan Result

The MCP scan compared Flowise source `packages/ui/src/ui-component/extended/McpServer.jsx`, `packages/ui/src/api/mcpserver.js`, `packages/ui/src/api/custommcpservers.js`, and the Tools Custom MCP Server dialog behavior. It confirms that the current AsterERP progress must still count MCP as incomplete.

| MCP Area | Flowise Source Requirement | Current Progress Impact | Status |
|---|---|---|---|
| Chatflow built-in MCP config | `mcpServerConfig` must contain `{ enabled, token, toolName, description }`; `toolName` is required, max 64, and must match `^[A-Za-z0-9_-]+$`; `description` is required when enabling | Implemented dedicated Configuration section plus backend-generated 32-character hex token storage in camelCase `mcpServerConfig` | Pass |
| Chatflow MCP APIs | Flowise uses `GET/POST/PUT/DELETE /api/v1/mcp-server/:id` and `POST /api/v1/mcp-server/:id/refresh` for load/create/update/disable/rotate | Implemented AsterERP Flowise equivalents under `/api/ai/flowise/mcp-server/{id}` and `/refresh`, with permission checks, backend validation, token generation/rotation, and audit logs | Pass |
| MCP runtime endpoint | Flowise exposes `${origin}/api/v1/mcp/{chatflowId}` and validates `Authorization: Bearer <token>` against enabled `mcpServerConfig` | Implemented token-authenticated `/api/v1/mcp/{chatflowId}` JSON-RPC endpoint for `initialize`, `tools/list`, `tools/call`, invalid-token 401, missing/disabled 404, and stateless DELETE 405; `tools/call` starts the AsterERP Flowise execution chain with Chatflow-owned tenant/app/owner data. Full MCP SDK Streamable HTTP transport parity is still incomplete. | Partial |
| Custom MCP Server resource | Flowise Tools page has a separate Custom MCP Server resource with `name`, `serverUrl`, `iconSrc`, `color`, `authType`, `authConfig.headers`, `status`, discovered `tools`, and `toolCount` | Implemented dedicated `FlowiseCustomMcpServerEntity`, contracts, service, controller, table/index/data filters, encrypted auth config storage, `POST /api/ai/flowise/custom-mcp-servers/{id}/authorize` remote `tools/list` discovery, `GET /tools`, frontend typed API, source-like Tools tab integration, standalone `CustomMcpServerDialog`, ADD/EDIT dialog state machine, create-then-authorize, save-then-reconnect, headers editor, URL validation, discovered tool search, parameter expansion, `tools:create/update/delete` RBAC split, masked header/token preservation with partial-mask rejection, annotations risk chips, icon rendering, and enum/default parameter display. Exact Flowise source Custom MCP Server behavior is closer but still not 1:1 because visual fidelity and expand-all/collapse-all controls remain incomplete. | Partial |

## Latest Actual Progress Rescan

Updated on 2026-06-24 after the current source scan, ChatPopUp interaction implementation slice, Chatflow Configuration sectionized editor slice, Configuration feedback/leads/file-upload/post-processing slice, and Follow-up Prompts configuration slice.

### Rescan Result

| Check | Current Result | Status |
|---|---|---|
| Actual completion rate | Still `13/18 Pass = 72.22%` | Fail to reach 100% |
| Flowise dedicated protocol boundary | Flowise backend/frontend paths have no matched `WorkflowModel`, `BPMN`, `CanvasJson`, `FlowDataJson`, or split canvas projection symbols. A repo-wide scan can still find the unrelated approval/workflow `WorkflowModelsController`, so the valid scan scope is the Flowise module/controller paths only. | Pass |
| Generic resource main chain | Flowise backend/frontend paths have no matched `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceCollectionPage`, `createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources`, or `ai_flowise_resources` source symbols. | Pass |
| Generic AsterERP table/CRUD usage in Flowise Studio | Flowise Studio source has no matched `CrudPage`, `DataTable`, or `useCrudResource` symbols. | Pass |
| Flowise ChatPopUp source parity | Flowise source still has `StarterPromptsCard`, `ChatInputHistory`, `ValidationPopUp`, `audio-recording`, and TTS/STT branches that are not fully implemented in AsterERP. Current AsterERP ChatPopUp has SSE/history/feedback/lead/source-doc/upload slices, but not full source parity. | Fail |
| Flowise Chatflow Configuration source parity | AsterERP Configuration now exposes sectionized editors for Flowise-native `RateLimit`, `AllowedDomains`, `OverrideConfig`, `StarterPrompts`, `FollowUpPrompts`, `ChatFeedback`, `Leads`, `FileUpload`, `PostProcessing`, `SpeechToText`, `TextToSpeech`, and Chatflow built-in MCP Server fields while preserving advanced JSON editing for native payload roundtrip. Flowise source `Security` is covered by the existing `RateLimit + AllowedDomains + OverrideConfig` sections. It is still not full source parity because full MCP SDK Streamable HTTP transport parity, Custom MCP Server resource chain, provider-backed STT/TTS runtime behavior, and upload capability negotiation are not complete. | Fail |
| Runtime execution semantics | Current execution records Flowise-style metadata and streams prediction events, but it is not yet full Flowise node-by-node runtime semantics. | Fail |
| Browser/API smoke matrix | Not rerun in this implementation pass. Current frontend verification is limited to `npm run typecheck` and `npm run build`; API/browser smoke still waits for the remaining source-parity gaps to close. | Blocked |
| ChatPopUp interaction slice | Implemented starter prompt rendering from `chatbotConfig.starterPrompts`, local input history with ArrowUp/ArrowDown navigation, a ChatPopUp validation popup wired to canvas validation, `speechToText`-gated MediaRecorder audio upload, and `textToSpeech`-gated browser speech playback. Verified with `npm run typecheck` and `npm run build`. This is not full provider-backed Flowise STT/TTS runtime. | Pass |
| Chatflow Configuration sectionized editor slice | Implemented editable sections for native `apiConfig.rateLimit`, `chatbotConfig.allowedOrigins`, `chatbotConfig.allowedOriginsError`, `chatbotConfig.starterPrompts`, `apiConfig.overrideConfig`, `speechToText`, and `textToSpeech`; added `zh-CN/en-US` keys and dialog CSS; verified with `npm run typecheck`, `npm run build`, and Flowise forbidden-symbol scans. | Pass |
| Chatflow Configuration feedback/leads/file/post-processing slice | Implemented editable sections for native `chatbotConfig.chatFeedback.status`, `chatbotConfig.leads`, `chatbotConfig.fullFileUpload`, and `chatbotConfig.postProcessing`; added `zh-CN/en-US` keys and section layout CSS; verified with `npm run typecheck`, `npm run build`, and Flowise forbidden-symbol scans. | Pass |
| Chatflow Configuration follow-up prompts slice | Implemented editable section for native `chatbotConfig.followUpPrompts.status` plus top-level `followUpPrompts.selectedProvider` and provider config fields; added `zh-CN/en-US` keys; verified with `npm run typecheck`, `npm run build`, and Flowise forbidden-symbol scans. | Pass |
| Chatflow MCP Server configuration slice | Implemented `FlowiseMcpServerContracts`, `IFlowiseMcpServerService`, `FlowiseMcpServerService`, `AiFlowiseMcpServerController`, DI registration, frontend `chatflowsApi.mcpServer`, `FlowiseMcpServerConfigDto`, Configuration MCP section, token copy/rotate UI, validation messages, and `zh-CN/en-US` keys. Verified with `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build`, `npm run typecheck`, `npm run build`, and scoped forbidden-symbol scans. This does not include `/api/v1/mcp/{chatflowId}` runtime or Custom MCP Server resource chain. | Pass |
| Chatflow MCP runtime JSON-RPC slice | Implemented `FlowiseMcpEndpointContracts`, `IFlowiseMcpEndpointService`, `FlowiseMcpEndpointService`, `AiFlowiseMcpEndpointController`, DI registration, token validation against native `mcpServerConfig`, fixed-time token comparison, `initialize`, `tools/list`, `tools/call`, stateless DELETE 405, and MCP-owned execution/audit path using Chatflow tenant/app/owner fields. This is a real runtime entry, but not full Flowise MCP SDK `StreamableHTTPServerTransport` parity. | Pass |
| Tools Custom MCP Server resource slice | Implemented `FlowiseCustomMcpServerContracts`, `FlowiseCustomMcpServerEntity`, `IFlowiseCustomMcpServerService`, `FlowiseCustomMcpServerService`, `AiFlowiseCustomMcpServersController`, DI/schema/index/data-filter registration, frontend `customMcpServersApi`, `customMcpServer.types.ts`, and `zh-CN/en-US` keys. The latest slices changed Tools to source-like tabs, split `CustomMcpServerDialog.tsx`, added detail fetch before edit, ADD/EDIT dialog modes, create-then-authorize, save-then-reconnect, header key/value editor, URL validation, discovered tool search, parameter expansion, dedicated pagination, non-nested Tools shell, `tools:create/update/delete` permission split, backend redacted auth merge, annotations/icons DTO preservation, annotation chips, icon rendering, enum/default display, and partial-mask validation. Verified with `dotnet build AsterERP.sln --no-restore`, `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build`, `npm run typecheck`, `npm run build`, scoped forbidden-symbol scans, and a Flowise Studio bare-English scan with only pre-existing `x` in chatflows remaining. This is closer to source but still not exact visual/source parity. | Pass |

### Current Fail / Blocked Rows

| Row | Current Gap | Required Closure |
|---|---|---|
| Execution chain | Not full Flowise node-by-node runtime | Implement Flowise source graph execution semantics inside AsterERP Flowise services, without Flowise Node service and without BPMN/projection |
| Chatflows/Agentflows frontend page | Source-level actions/dialogs/layout incomplete | Migrate one-to-one Flowise source page behavior for list/card actions, import/export/share/embed/API code/delete and native dialogs |
| Canvas UI source parity | Full Flowise `CanvasHeader`, `CanvasNode`, `NodeInputHandler`, `AddNodes`, `ChatPopUp`, agentflow v2, Redux/MUI/source styling parity incomplete | Continue direct source-structure migration under `features/flowise-studio`, preserving AsterERP shell/auth/request boundaries |
| Built-in dialogs | Remaining source sections and dialogs are incomplete: full MCP SDK Streamable HTTP parity, exact Custom MCP Server source dialog behavior, provider-backed STT/TTS runtime, upload capability negotiation, and remaining source dialogs | Add source-equivalent dialogs and state flows with real API persistence/errors |
| i18n | Current migrated set is scanned, but full source component set is not migrated yet | Re-scan and add `zh-CN/en-US` keys after every remaining page/dialog/component migration |
| Browser/API smoke | Full authenticated API smoke, permission 403, workspace boundary, and screenshot parity matrix not completed after all changes | Run final verification matrix only after implementation gaps are closed |

## Latest Implementation Snapshot

Updated on 2026-06-24 after the strong-typed resource migration pass.

### Newly Closed Items

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Configuration resource roots | `FlowiseToolEntity`, `FlowiseCredentialEntity`, `FlowiseVariableEntity`, `FlowiseApiKeyEntity`; `FlowiseToolService`, `FlowiseCredentialService`, `FlowiseVariableService`, `FlowiseApiKeyService`; `AiFlowiseResourcesController`; `configurationResources.api.ts` | Tools, Credentials, Variables, and API Keys no longer use `createFlowiseNativeCollectionApi`; secret reveal is routed through dedicated services and audit logging | Pass |
| Assistant and Marketplace roots | `FlowiseAssistantEntity`, `FlowiseMarketplaceTemplateEntity`; `FlowiseAssistantService`, `FlowiseMarketplaceService`; `nativeResources.api.ts` | Assistants and Marketplaces now use dedicated tables/services/API routes; default marketplace seed writes to `FlowiseMarketplaceTemplateEntity` instead of `FlowiseResourceEntity` | Pass |
| Document Store root | `FlowiseDocumentStoreEntity`; `IFlowiseDocumentStoreService.GetPageAsync/CreateAsync/UpdateAsync/DeleteAsync`; `documentStoresApi` | Document Store root CRUD and detail load no longer use `FlowiseResourceEntity`; detail/upsert/history still use Flowise document-store domain tables | Pass |
| Dataset/Evaluator/Evaluation roots | `FlowiseDatasetEntity`, `FlowiseEvaluatorEntity`, `FlowiseEvaluationEntity`; `IFlowiseEvaluationService` root CRUD methods; `evaluationsApi.datasets/evaluators/evaluations` | Dataset rows, evaluator detail, evaluation result, and run-again now resolve their strong typed root records directly | Pass |
| Management and logs | `FlowiseSsoConfigEntity`, `FlowiseRoleEntity`, `FlowiseUserEntity`, `FlowiseLoginActivityEntity`, `FlowiseAccountSettingEntity`; `FlowiseManagementService`; `managementApi` | SSO Config, Roles, Users, Login Activity, Logs, Account, Overview, Workspaces, and Shared Workspaces now run through explicit Flowise management domain endpoints instead of `{resourceType}` routes | Pass |
| ORM data filters | `DataPermissionFilterRegistrar`, `AiCenterAppModule` registrations | New strong typed Flowise tables are registered for tenant/app and owner filters and included in table initialization/index creation; `FlowiseResourceEntity` is no longer registered | Pass |

### Verification Evidence From This Snapshot

| Verification | Result |
|---|---|
| `dotnet build AsterERP.sln --no-restore` | Pass; existing NuGet vulnerability warnings only |
| `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` | Pass; 88/88 |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing large chunk warnings only |
| Directed scan for `FlowiseResourceEntity` in `FlowiseDocumentStoreService` and `FlowiseEvaluationService` | Pass; no remaining root dependency in those services |
| Directed scan for `createFlowiseNativeCollectionApi('document-stores'/'datasets'/'evaluators'/'evaluations')` | Pass; those pages now use dedicated APIs |
| Directed scan for `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `{resourceType}`, `flowiseStudioApi.resources`, and `createFlowiseNativeCollectionApi` in backend/frontend source paths | Pass; no source hits after generic chain removal |

### Generic Chain Removal Confirmed By Scan

The generic resource main chain has been removed from source code:

| Removed Area | Removed Symbols / Files | Replacement |
|---|---|---|
| Generic backend resource controller | `AiFlowiseController` `{resourceType}` routes | Explicit SSO/Roles/Users/Login Activity/Logs/Account/Overview/Workspace endpoints backed by `IFlowiseManagementService` |
| Generic backend service/table | `IFlowiseResourceService`, `FlowiseResourceService`, `FlowiseResourceEntity`, `FlowiseResourceCatalog`, `FlowiseResourceTypeDescriptor` | Dedicated Flowise services and entities for each resource family |
| Generic frontend API helper | `FlowiseNativeCollectionSurface.createFlowiseNativeCollectionApi`, `flowiseStudioApi.resources.*` | Dedicated `configurationResources.api.ts`, `nativeResources.api.ts`, `documentStores.api.ts`, `evaluations.api.ts`, and `management.api.ts` |
| Data filter/table registration | `FlowiseResourceEntity` registrations | New strong typed Flowise tables registered directly |

## Remaining Required Work

- Migrate Flowise source canvas components, dialogs, chat popup, agentflow v2 canvas, and source interactions.
- Specifically close the current ChatPopUp gaps against Flowise source: `StarterPromptsCard`, `ChatInputHistory`, `ValidationPopUp`, `audio-recording`, TTS playback/config behavior, and source event cards that are not yet implemented.
- Complete the remaining Chatflow Configuration and Tools source flows beyond the current sectionized editor/resource slices: upload capability negotiation, full MCP SDK Streamable HTTP parity, exact Flowise Custom MCP Server dialog/component parity, and provider-backed STT/TTS runtime behavior.
- Deepen the new native menu pages from Flowise-style card/list/dialog parity into one-to-one Flowise source page behavior. The backend/frontend generic resource main chain is removed, but the pages still need full Flowise source page structure and dialog behavior.
- Replace simplified execution with Flowise node-by-node runtime semantics implemented in AsterERP backend services without Flowise Node service.
- Complete `zh-CN/en-US` i18n again after each remaining Flowise source component/dialog is migrated.
- Add parity tests and browser screenshot audit for each required menu/page/dialog.
- Rerun all required build, test, API smoke, and browser verification steps after the full migration.
## Latest Control-Parity Slice: Allowed Domains

Updated on 2026-06-25 after the Flowise Configuration Allowed Domains control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Allowed domains controls | `FlowiseCanvasHeaderDialogs.tsx` `AllowedDomainsSection` | Replaced raw add/remove buttons and text inputs with MUI `Button`, `IconButton`, and `TextField` while preserving native `chatbotConfig.allowedOrigins` and `chatbotConfig.allowedOriginsError` update semantics. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `StarterPromptsSection`, confirming the Allowed Domains raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `AllowedDomainsSection` scan | Pass; no raw `input`/`button` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: audio-recording

Updated on 2026-06-25 after moving ChatPopUp MediaRecorder session handling into a Flowise source-named audio-recording module.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Audio recording module | `native/views/chatmessage/audio-recording.ts` | Added typed helpers for recording support detection, starting an audio recording session, stopping the recorder into a `FlowisePredictionUpload`, and cleaning up media tracks. | Pass |
| ChatPopUp recording path | `FlowiseChatTestPanel.tsx` `startAudioRecording` / `stopAudioRecording` / `cleanupAudioRecording` | Removed direct `MediaRecorder`, stream, and chunk refs from the panel. The panel now owns only recording state, user feedback, and upload list updates. | Pass |
| Responsibility boundary | `audio-recording.ts`, `FlowiseChatTestPanel.tsx` | Browser media lifecycle logic is separated from request/stream/chat state; no backend runtime, Flowise protocol, projection, BPMN, or generic CRUD path was introduced. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice closes the previously tracked audio-recording source module parity gap, but full parity still requires exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: Starter Prompts

Updated on 2026-06-25 after the Flowise Configuration Starter Prompts control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Starter prompts controls | `FlowiseCanvasHeaderDialogs.tsx` `StarterPromptsSection` | Replaced raw add/remove buttons and prompt text input with MUI `Button`, `IconButton`, and `TextField` while preserving native `chatbotConfig.starterPrompts` read/write through `readStarterPromptRows` and `writeStarterPromptRows`. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `FollowUpPromptsSection`, confirming the Starter Prompts raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `StarterPromptsSection` scan | Pass; no raw `input`/`button` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: Follow-up Prompts

Updated on 2026-06-25 after the Flowise Configuration Follow-up Prompts control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Follow-up prompts controls | `FlowiseCanvasHeaderDialogs.tsx` `FollowUpPromptsSection` | Replaced raw status checkbox, provider select, credential/base URL/model/temperature inputs, and prompt textarea with MUI `FormControlLabel`, `Checkbox`, `TextField`, and `MenuItem` while preserving native `followUpPrompts.status`, `selectedProvider`, and provider config update semantics. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `ChatFeedbackSection`, confirming the Follow-up Prompts raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `FollowUpPromptsSection` scan | Pass; no raw `input`/`select`/`textarea` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: Chat Feedback

Updated on 2026-06-25 after the Flowise Configuration Chat Feedback control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Chat feedback controls | `FlowiseCanvasHeaderDialogs.tsx` `ChatFeedbackSection` | Replaced the raw status checkbox with MUI `FormControlLabel` and `Checkbox` while preserving native `chatbotConfig.chatFeedback.status` update semantics. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `LeadsSection`, confirming the Chat Feedback raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `ChatFeedbackSection` scan | Pass; no raw `input` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: Leads

Updated on 2026-06-25 after the Flowise Configuration Leads control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Leads controls | `FlowiseCanvasHeaderDialogs.tsx` `LeadsSection` | Replaced raw status/name/email/phone checkboxes and title/success-message textareas with MUI `FormControlLabel`, `Checkbox`, and multiline `TextField` while preserving native `chatbotConfig.leads` update and `normalizeLeadsConfig` semantics. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `FileUploadSection`, confirming the Leads raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `LeadsSection` scan | Pass; no raw `input`/`textarea` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: File Upload

Updated on 2026-06-25 after the Flowise Configuration File Upload control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| File upload controls | `FlowiseCanvasHeaderDialogs.tsx` `FileUploadSection` | Replaced raw status/file-type checkboxes and PDF processing select with MUI `FormControlLabel`, `Checkbox`, `TextField`, and `MenuItem` while preserving native `chatbotConfig.fullFileUpload` update and `normalizeFileUploadConfig` semantics. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `PostProcessingSection`, confirming the File Upload raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `FileUploadSection` scan | Pass; no raw `input`/`select` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: Post Processing

Updated on 2026-06-25 after the Flowise Configuration Post Processing control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Post processing controls | `FlowiseCanvasHeaderDialogs.tsx` `PostProcessingSection` | Replaced raw enabled checkbox and custom function textarea with MUI `FormControlLabel`, `Checkbox`, and multiline `TextField` while preserving native `chatbotConfig.postProcessing` update and `normalizePostProcessingConfig` semantics. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `OverrideConfigSection`, confirming the Post Processing raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `PostProcessingSection` scan | Pass; no raw `input`/`textarea` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: Override Config

Updated on 2026-06-25 after the Flowise Configuration Override Config control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Override config controls | `FlowiseCanvasHeaderDialogs.tsx` `OverrideConfigSection` | Replaced the raw status checkbox with MUI `FormControlLabel` and `Checkbox` while preserving native `apiConfig.overrideConfig` update and `normalizeOverrideConfig` semantics. | Pass |
| Remaining raw controls | `FlowiseCanvasHeaderDialogs.tsx` scan | Targeted raw-control scan now starts at `McpServerSection` and `ProviderConfigSection`, confirming the Override Config raw controls were removed. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves source-control parity inside the Chatflow Configuration dialog, but it does not close the larger remaining items: full source-level dialogs, agentflow v2 canvas, full ChatPopUp parity, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `OverrideConfigSection` scan | Pass; no raw `input` controls remain in that section |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: MCP Server and Provider Config

Updated on 2026-06-25 after the Flowise Configuration MCP Server and provider control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| MCP server controls | `FlowiseCanvasHeaderDialogs.tsx` `McpServerSection` | Replaced raw enabled checkbox, tool name input, description textarea, read-only endpoint/token inputs, and save/copy/rotate buttons with MUI `FormControlLabel`, `Checkbox`, `TextField`, and `Button` while preserving native `mcpServerConfig` update, disable, save, copy token, and token refresh callbacks. | Pass |
| STT/TTS provider controls | `FlowiseCanvasHeaderDialogs.tsx` `ProviderConfigSection` | Replaced the raw provider select, credential/model/voice inputs, and auto-play checkbox with MUI `TextField`, `MenuItem`, `FormControlLabel`, and `Checkbox` while preserving provider status selection and provider JSON roundtrip semantics. | Pass |
| Remaining raw controls | `features/flowise-studio` scan | `FlowiseCanvasHeaderDialogs.tsx` no longer has raw `input`, `textarea`, `select`, or `button` controls; remaining raw controls now start in `FlowiseChatTestPanel.tsx`. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice closes the remaining raw-control mismatch inside `FlowiseCanvasHeaderDialogs.tsx`, but it does not close the larger remaining items: full source-level ChatPopUp, agentflow v2 canvas, node-by-node runtime execution, authenticated API/browser smoke, and screenshot parity matrix.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `FlowiseCanvasHeaderDialogs.tsx` raw-control scan | Pass; no raw `input`, `textarea`, `select`, or `button` controls remain in this file |
| `rg -n "<input|<textarea|<select|<button" frontend/AsterERP.Web/src/features/flowise-studio` | Pass for this file; remaining matches are in `FlowiseChatTestPanel.tsx` |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: ChatPopUp Shell and Composer

Updated on 2026-06-25 after the first Flowise ChatPopUp visible-control migration.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Chat popup launcher and tools | `FlowiseChatTestPanel.tsx` `FlowiseChatTestPanel` | Replaced visible raw popup FAB/tool/close buttons with MUI `Fab` and `IconButton` while preserving `toggleChat`, `clearChat`, expand, validation popup, and close callbacks. | Pass |
| Expanded chat dialog header | `FlowiseChatTestPanel.tsx` expanded dialog header | Replaced raw clear/validation/close buttons with MUI `Button` and `IconButton` while preserving the same callbacks and validation visibility rules. | Pass |
| Chat composer | `FlowiseChatTestPanel.tsx` `ChatContent` | Replaced the raw question textarea and upload/record/stop/run visible buttons with MUI `TextField` and `Button`; textarea history navigation still uses the existing `inputRef` via `inputRef={inputRef}`. | Pass |
| Remaining raw controls | `FlowiseChatTestPanel.tsx` scan | Remaining raw controls are now lower-level message actions, hidden file input, starter prompt buttons, validation close, upload remove, and lead capture controls. | Partial |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves visible ChatPopUp shell/composer source-control parity, but the full ChatPopUp source parity remains incomplete because feedback actions, starter prompts, validation popup, file upload remove, lead capture, exact Flowise chat message visuals, and runtime parity still need closure.

### Latest Verification

| Verification | Result |
|---|---|
| Targeted `FlowiseChatTestPanel.tsx` raw-control scan | Pass for migrated shell/composer controls; remaining matches are documented above |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Control-Parity Slice: ChatPopUp Message Actions and Lead Capture

Updated on 2026-06-25 after migrating the remaining visible lower-level Flowise ChatPopUp controls.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Message feedback and TTS actions | `FlowiseChatTestPanel.tsx` `ChatBubble` | Replaced raw feedback and text-to-speech buttons with MUI `Button` while preserving feedback rating callbacks and speech toggle behavior. | Pass |
| Feedback reason | `FlowiseChatTestPanel.tsx` `ChatBubble` | Replaced the raw feedback reason input with MUI `TextField` while preserving `onReasonChange`. | Pass |
| Starter prompts | `FlowiseChatTestPanel.tsx` `StarterPrompts` | Replaced raw starter prompt buttons with MUI `Button` while preserving prompt submission callback payloads. | Pass |
| Validation and upload remove controls | `FlowiseChatTestPanel.tsx` `ValidationPopup`, `FileUploads` | Replaced raw validation close and upload remove buttons with MUI `IconButton`. | Pass |
| Lead capture | `FlowiseChatTestPanel.tsx` `LeadCapture` | Replaced raw name/email/phone inputs and submit button with MUI `TextField` and `Button` while preserving lead draft updates and submit disabled rule. | Pass |
| Remaining raw controls | `FlowiseChatTestPanel.tsx` scan | Only the hidden file picker input remains: `className="flowise-chat-file-input" type="file"`, which is required to trigger browser file selection from the MUI upload button. | Pass with documented exception |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice removes all visible raw controls from `FlowiseChatTestPanel.tsx`, but full ChatPopUp parity still needs exact source message-card visuals, deeper Flowise event rendering parity, provider-backed STT/TTS runtime behavior, browser screenshots, and authenticated runtime smoke.

### Latest Verification

| Verification | Result |
|---|---|
| `rg -n "<input|<textarea|<select|<button" frontend/AsterERP.Web/src/features/flowise-studio/canvas/FlowiseChatTestPanel.tsx` | Pass with documented exception; only hidden file input remains |
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: ChatPopUp Event Cards

Updated on 2026-06-25 after adding Flowise source-named ChatPopUp event-card components.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Agent reasoning card | `native/views/chatmessage/AgentReasoningCard.tsx`, `FlowiseChatTestPanel.tsx` | Added a Flowise source-named MUI card component for agent reasoning events and routed `AgentReasoningList` through it while preserving used-tools, state, artifacts, and source-document render callbacks. | Pass |
| Agent executed data card | `native/views/chatmessage/AgentExecutedDataCard.tsx`, `FlowiseChatTestPanel.tsx` | Added a Flowise source-named MUI card component for executed node data and routed `AgentExecutedDataList` through it while preserving metadata JSON rendering. | Pass |
| Thinking card | `native/views/chatmessage/ThinkingCard.tsx`, `FlowiseChatTestPanel.tsx` | Added a Flowise source-named expandable thinking card and uses it for the streaming assistant bubble instead of a plain `pre`. | Pass |
| Styling | `styles/flowise-dialogs.css` | Added card and thinking-panel classes without changing API contracts or runtime calls. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp source file structure and event-card visual semantics, but full parity still requires exact Flowise card internals, source document dialogs, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API/browser smoke, and screenshot parity.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Runtime-Parity Slice: Node Execution Snapshots

Updated on 2026-06-25 after adding dependency-ordered node execution snapshots to the Flowise execution service.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Node execution order | `FlowiseExecutionService` `PlanExecutionOrder` | Flowise `flowData.nodes` and `flowData.edges` are now topologically ordered for runtime metadata; cyclic or unresolved remainder nodes are appended deterministically without BPMN or projection. | Pass |
| Node execution snapshots | `FlowiseExecutionService` `BuildNodeExecutionSnapshot` | `AgentExecutedData` now includes per-node `previousNodeIds`, `nextNodeIds`, node type, node data, status, and execution timestamp in `DataJson`. | Pass |
| Streaming node lifecycle | `FlowiseExecutionService.ExecuteDefinitionStreamingAsync` | Streaming execution now emits `agentFlowExecutedData` for each node with `INPROGRESS` and `FINISHED` states before final aggregate events. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice materially improves the execution-chain row toward Flowise node-by-node runtime semantics, but it is not full Flowise runtime parity because provider/tool/node-specific execution semantics, agentflow v2 runtime branches, authenticated API smoke, browser screenshot parity, and permission/workspace verification remain incomplete.

### Latest Verification

| Verification | Result |
|---|---|
| `dotnet build AsterERP.sln --no-restore` | Pass; existing NuGet vulnerability warnings only |
| `dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --no-build` | Pass; 88/88 |

## Latest Behavior-Parity Slice: audio-recording Cancel and Safari Capture

Updated on 2026-06-25 after aligning the AsterERP audio-recording module with the Flowise source cancel lifecycle and Safari timeslice recording behavior.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Recording cancel lifecycle | `native/views/chatmessage/audio-recording.ts` `cancelAudioRecording` | Added a typed cancel helper that detaches recorder handlers, stops active recording, clears chunks, and stops media tracks. | Pass |
| Safari capture behavior | `native/views/chatmessage/audio-recording.ts` `startAudioRecording` | Starts `MediaRecorder` with a `1000ms` timeslice for Safari user agents, matching the Flowise source workaround for Safari audio chunks. | Pass |
| ChatPopUp cleanup path | `FlowiseChatTestPanel.tsx` | Unmount cleanup now calls `cancelAudioRecording`; normal stop/upload cleanup is owned by `stopAudioRecording`, avoiding duplicated stream cleanup in the panel. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp audio-recording behavior parity, but full parity still requires provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: AudioWaveform

Updated on 2026-06-25 after replacing ChatPopUp audio upload previews with a Flowise source-named waveform component.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Audio waveform component | `native/ui-component/extended/AudioWaveform.tsx` | Added a typed component matching the Flowise source file boundary for audio preview playback, canvas waveform drawing, progress updates, and click-to-seek behavior. It contains no backend/API calls. | Pass |
| ChatPopUp audio upload preview | `FlowiseChatTestPanel.tsx` `FileUploads` | Replaced raw `<audio controls>` upload preview rendering with `AudioWaveform`, preserving the existing upload data and remove action. | Pass |
| Styling | `styles/flowise-dialogs.css` | Added stable waveform container, hidden audio element, play/pause button, and canvas styles for the upload preview path. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp audio preview source UI parity, but full parity still requires provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: Source Document Dialog

Updated on 2026-06-25 after adding the Flowise source-named source document dialog and wiring ChatPopUp source document entries to it.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Source document dialog | `native/ui-component/dialog/SourceDocDialog.tsx` | Added a typed MUI dialog component that renders selected source documents, content, metadata, score, source id, and JSON payload without backend/API changes. | Pass |
| ChatPopUp source document entry | `FlowiseChatTestPanel.tsx` `SourceDocuments` | Source document summaries now open `SourceDocDialog` with the selected document list instead of only expanding inline text. | Pass |
| Agent reasoning source docs | `FlowiseChatTestPanel.tsx` `AgentReasoningList` | Agent reasoning cards pass source document callbacks into the shared dialog path, preserving existing reasoning/artifact rendering. | Pass |
| Styling | `styles/flowise-dialogs.css` | Added source document dialog title, content, section, paragraph, and JSON block styles. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice closes the previously documented ChatPopUp source document dialog gap, but full parity still requires exact Flowise ChatPopUp internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: text-to-speech

Updated on 2026-06-25 after moving ChatPopUp browser speech synthesis handling into a Flowise source-named text-to-speech module.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Text-to-speech module | `native/views/chatmessage/text-to-speech.ts` | Added typed helpers for browser TTS support detection, message playback, playback handle tracking, and global stop. | Pass |
| ChatPopUp TTS path | `FlowiseChatTestPanel.tsx` `speakTextMessage` / `stopTextToSpeech` / `supportsTextToSpeech` | Removed direct `SpeechSynthesisUtterance` construction and `speechSynthesis` calls from the panel. The panel now owns only feature gating, active message state, and user feedback. | Pass |
| Responsibility boundary | `text-to-speech.ts`, `FlowiseChatTestPanel.tsx` | Browser speech lifecycle logic is separated from request/stream/chat state; no backend runtime, Flowise protocol, projection, BPMN, or generic CRUD path was introduced. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp text-to-speech source module structure, but full parity still requires provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: ChatExpandDialog

Updated on 2026-06-25 after replacing the inline expanded chat overlay with a Flowise source-named ChatExpandDialog component.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Expanded chat dialog | `native/views/chatmessage/ChatExpandDialog.tsx` | Added a typed MUI dialog component matching the Flowise source file boundary for expanded ChatPopUp display. It exposes only UI props/events and contains no hidden API calls. | Pass |
| ChatPopUp expanded path | `FlowiseChatTestPanel.tsx` `ChatExpandDialog` | Replaced the inline `flowise-native-dialog-backdrop`/`section` expanded chat overlay with `ChatExpandDialog` while preserving chat content, clear, validation, and close behavior. | Pass |
| Styling | `styles/flowise-dialogs.css` | Reworked expanded chat styles for MUI Dialog paper/title/content instead of the old hand-built section container. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp source file structure and expanded-dialog parity, but full parity still requires exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: ValidationPopUp

Updated on 2026-06-25 after extracting the ChatPopUp validation panel into a Flowise source-named ValidationPopUp component.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Validation popup component | `native/views/chatmessage/ValidationPopUp.tsx` | Added a typed component matching the Flowise source file boundary for validation checklist display. It receives validation data and UI events only, with no hidden API calls. | Pass |
| ChatPopUp validation path | `FlowiseChatTestPanel.tsx` `ValidationPopUp` | Removed the internal `ValidationPopup` function and now renders the source-named component while preserving issue list, close action, and no-issues state. | Pass |
| i18n and styling boundary | `ValidationPopUp.tsx`, `styles/flowise-dialogs.css` | Reused existing `flowiseI18nKeys` and existing validation classes; no backend runtime, protocol, projection, BPMN, or generic CRUD path was introduced. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp source file structure for validation, but full parity still requires exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: StarterPromptsCard

Updated on 2026-06-25 after replacing the inline ChatPopUp starter prompt buttons with a Flowise source-named StarterPromptsCard component.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Starter prompts card | `native/ui-component/cards/StarterPromptsCard.tsx` | Added a typed MUI Chip-based component matching the Flowise source file boundary for starter prompt chips. It exposes prompt items and prompt-click events only, with no hidden API calls. | Pass |
| ChatPopUp starter prompt path | `FlowiseChatTestPanel.tsx` `StarterPromptsCard` | Removed the internal `StarterPrompts` helper and now renders the source-named card when the current chat has no messages. Prompt click still submits the selected prompt through the existing `onStarterPrompt` callback. | Pass |
| Styling | `styles/flowise-dialogs.css` | Reworked starter prompt styling from raw button selectors to `flowise-starter-prompts-card` and chip button classes. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp source file structure for starter prompts, but full parity still requires exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: ChatMessage

Updated on 2026-06-25 after extracting the ChatPopUp message bubble body into a Flowise source-named ChatMessage component.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Chat message component | `native/views/chatmessage/ChatMessage.tsx` | Added a typed message component for user/assistant bubbles, feedback buttons, feedback reason input, and text-to-speech action display. It receives render props for file uploads, agent reasoning, executed data, used tools, artifacts, and source docs, with no hidden API calls. | Pass |
| ChatPopUp message path | `FlowiseChatTestPanel.tsx` `ChatMessage` | Removed the internal `ChatBubble` helper and now renders `ChatMessage` for each message. The existing feedback, feedback reason, TTS, source doc, artifacts, tools, reasoning, and executed data callbacks are preserved. | Pass |
| Responsibility boundary | `ChatMessage.tsx`, `FlowiseChatTestPanel.tsx` | UI-only message layout moved to the source-named component while the panel keeps request state, mutations, stream control, and render composition. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp source file structure for message bubbles, but full parity still requires exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |

## Latest Source-Structure Slice: ChatInputHistory

Updated on 2026-06-25 after moving ChatPopUp input history persistence and navigation helpers into a Flowise source-named ChatInputHistory module.

### What Changed

| Area | Files / Symbols | Evidence | Status |
|---|---|---|---|
| Chat input history module | `native/views/chatmessage/ChatInputHistory.ts` | Added typed helpers for reading resource-scoped input history, persisting deduplicated history, and resolving next/previous navigation cursor/value. | Pass |
| ChatPopUp history path | `FlowiseChatTestPanel.tsx` `readChatInputHistory` / `persistChatInputHistory` / `resolveChatInputHistoryNavigation` | Removed local history storage functions from the panel and now delegates storage/navigation logic to the source-named module while preserving ArrowUp/ArrowDown behavior and resource-scoped localStorage keys. | Pass |
| Responsibility boundary | `ChatInputHistory.ts`, `FlowiseChatTestPanel.tsx` | History storage/navigation is separated from React request/stream state; no backend runtime, protocol, projection, BPMN, or generic CRUD path was introduced. | Pass |

### Current Actual Progress

Actual completion remains `13/18 Pass = 72.22%`. This slice improves ChatPopUp source file structure for input history, but full parity still requires exact Flowise ChatMessage internals, provider-backed STT/TTS runtime behavior, audio-recording source module parity, agentflow v2 canvas, node-by-node execution semantics, authenticated API smoke, browser screenshot parity, and full permission/workspace verification.

### Latest Verification

| Verification | Result |
|---|---|
| `cd frontend/AsterERP.Web && npm run typecheck` | Pass |
| `cd frontend/AsterERP.Web && npm run build` | Pass; existing circular chunk and large chunk warnings only |
