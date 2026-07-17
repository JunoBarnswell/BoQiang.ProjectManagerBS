import { buildQueryString } from '../../../api/queryString';
import type { GridPageResult } from '../../../api/shared.types';
import { httpClient } from '../../../core/http/httpClient';
import { HttpError } from '../../../core/http/httpError';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { translateCurrentLocale } from '../../../core/i18n/I18nProvider';

export interface AiGridQuery {
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  status?: string;
}

export interface AiConversationDto {
  id: string;
  tenantId: string;
  appCode: string;
  ownerUserId: string;
  title: string;
  status: string;
  isFavorite: boolean;
  summary?: string | null;
  lastRunStatus?: string | null;
  lastMessageAt?: string | null;
  createdTime: string;
  updatedTime?: string | null;
}

export interface AiConversationDetailDto extends AiConversationDto {
  messages: AiMessageDto[];
  snapshots: AiContextSnapshotDto[];
}

export interface AiConversationCreateRequest {
  title?: string | null;
  modelConfigId?: string | null;
  promptTemplateId?: string | null;
  agentProfileIds?: string[];
}

export interface AiConversationUpdateRequest {
  title: string;
  isFavorite: boolean;
}

export interface AiMessageDto {
  id: string;
  conversationId: string;
  runId?: string | null;
  agentProfileId?: string | null;
  role: string;
  seq: number;
  content: string;
  reasoningContent?: string | null;
  metadataJson?: string | null;
  status?: string | null;
  finishReason?: string | null;
  tokenCount: number;
  createdTime: string;
}

export interface AiContextSnapshotDto {
  id: string;
  conversationId: string;
  fromSeq: number;
  toSeq: number;
  summary: string;
  totalTokens: number;
  createdTime: string;
}

export interface AiProviderDto {
  id: string;
  providerCode: string;
  providerName: string;
  protocolType: string;
  baseUrl: string;
  apiKeyMask?: string | null;
  isEnabled: boolean;
  timeoutSeconds: number;
  extraParametersJson?: string | null;
  createdTime: string;
}

export interface AiProviderUpsertRequest {
  providerCode: string;
  providerName: string;
  protocolType: string;
  baseUrl: string;
  apiKey?: string | null;
  isEnabled: boolean;
  timeoutSeconds: number;
  extraParametersJson?: string | null;
}

export interface AiModelConfigDto {
  id: string;
  providerId: string;
  providerName: string;
  modelCode: string;
  displayName: string;
  maxContextTokens: number;
  maxOutputTokens: number;
  defaultTemperature?: number | null;
  defaultTopP?: number | null;
  thinkingEnabledDefault: boolean;
  reasoningEffort?: string | null;
  toolStreamEnabledDefault: boolean;
  maxParallelRuns: number;
  isEnabled: boolean;
  sortOrder: number;
}

export interface AiModelConfigUpsertRequest {
  providerId: string;
  modelCode: string;
  displayName: string;
  maxContextTokens: number;
  maxOutputTokens: number;
  defaultTemperature?: number | null;
  defaultTopP?: number | null;
  thinkingEnabledDefault: boolean;
  reasoningEffort?: string | null;
  toolStreamEnabledDefault: boolean;
  maxParallelRuns: number;
  isEnabled: boolean;
  sortOrder: number;
}

export interface AiPromptTemplateDto {
  id: string;
  templateCode: string;
  templateName: string;
  category: string;
  systemPrompt: string;
  userPromptTemplate?: string | null;
  variablesJson?: string | null;
  isEnabled: boolean;
  sortOrder: number;
}

export interface AiPromptTemplateUpsertRequest {
  templateCode: string;
  templateName: string;
  category: string;
  systemPrompt: string;
  userPromptTemplate?: string | null;
  variablesJson?: string | null;
  isEnabled: boolean;
  sortOrder: number;
}

export interface AiAgentProfileDto {
  id: string;
  agentCode: string;
  agentName: string;
  rolePrompt: string;
  modelConfigId?: string | null;
  promptTemplateId?: string | null;
  allowedFunctionsJson?: string | null;
  isCoordinator: boolean;
  isEnabled: boolean;
  sortOrder: number;
}

export interface AiAgentProfileUpsertRequest {
  agentCode: string;
  agentName: string;
  rolePrompt: string;
  modelConfigId?: string | null;
  promptTemplateId?: string | null;
  allowedFunctionsJson?: string | null;
  isCoordinator: boolean;
  isEnabled: boolean;
  sortOrder: number;
}

export interface AiChatStreamRequest {
  content: string;
  workMode: 'Agent' | 'Ask' | 'Plan';
  taskPlanId?: string | null;
  modelConfigId?: string | null;
  promptTemplateId?: string | null;
  mode: 'Collaborative' | 'Single';
  agentProfileIds: string[];
  coordinatorAgentProfileId?: string | null;
  clientMessageId?: string | null;
  idempotencyKey?: string | null;
  thinkingEnabled?: boolean | null;
  reasoningEffort?: string | null;
  temperature?: number | null;
  topP?: number | null;
  maxTokens?: number | null;
  toolStreamEnabled?: boolean | null;
  requireToolConfirmation: boolean;
  enabledToolCodes: string[];
  enabledToolDomains: string[];
  extraParameters: Record<string, unknown>;
}

export interface AiRunDto {
  id: string;
  conversationId: string;
  userMessageId?: string | null;
  assistantMessageId?: string | null;
  modelConfigId?: string | null;
  mode: string;
  status: string;
  promptTokens: number;
  completionTokens: number;
  reasoningTokens: number;
  totalTokens: number;
  errorCode?: string | null;
  errorMessage?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  participants: AiRunParticipantDto[];
}

export interface AiRunParticipantDto {
  id: string;
  runId: string;
  agentProfileId: string;
  agentName: string;
  status: string;
  draftMessageId?: string | null;
  errorMessage?: string | null;
}

export interface AiStreamEventDto {
  event: AiStreamEventName;
  runId: string;
  conversationId: string;
  traceId: string;
  seq: number;
  timestamp: string;
  data?: unknown;
}

export type AiStreamEventName =
  | 'content_completed'
  | 'content_delta'
  | 'content_started'
  | 'context_built'
  | 'done'
  | 'error'
  | 'reasoning_completed'
  | 'reasoning_delta'
  | 'reasoning_started'
  | 'run_started'
  | 'usage'
  | 'workflow_binding_suggested'
  | 'workflow_bpmn_generated'
  | 'workflow_canvas_generated'
  | 'workflow_draft_created'
  | 'workflow_draft_updated'
  | 'workflow_form_permission_suggested'
  | 'workflow_instance_diagnosed'
  | 'workflow_notification_previewed'
  | 'workflow_publish_precheck_completed'
  | 'workflow_simulation_completed'
  | 'workflow_simulation_started'
  | 'workflow_validation_completed'
  | 'workflow_validation_started';

export interface AiTaskPlanDto {
  id: string;
  conversationId: string;
  runId?: string | null;
  title: string;
  goal: string;
  status: string;
  mode: string;
  versionNo: number;
  revision: number;
  executionStrategy: string;
  risksJson?: string | null;
  assumptionsJson?: string | null;
  metadataJson?: string | null;
  approvedBy?: string | null;
  approvedRevision?: number | null;
  approvedAt?: string | null;
  completedAt?: string | null;
  createdTime: string;
  updatedTime?: string | null;
  progress: AiTaskPlanProgressDto;
  items: AiTaskPlanItemDto[];
  events: AiTaskPlanEventDto[];
}

export interface AiTaskPlanItemDto {
  id: string;
  planId: string;
  parentItemId?: string | null;
  title: string;
  description: string;
  status: string;
  priority: 'P0' | 'P1' | 'P2' | string;
  ownerType: 'Agent' | 'Tool' | 'User' | string;
  taskType: 'Code' | 'Design' | 'Manual' | 'Review' | 'Test' | 'Tool' | string;
  sortOrder: number;
  depth: number;
  dependsOnJson?: string | null;
  acceptanceCriteriaJson?: string | null;
  toolCode?: string | null;
  executionHint?: string | null;
  result?: string | null;
  resultSummary?: string | null;
  evidenceJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  blockedReason?: string | null;
  skipReason?: string | null;
  retryCount: number;
  maxRetryCount: number;
  startedAt?: string | null;
  completedAt?: string | null;
  updatedTime?: string | null;
}

export interface AiTaskPlanProgressDto {
  totalCount: number;
  completedCount: number;
  failedCount: number;
  blockedCount: number;
  waitingUserCount: number;
  percent: number;
}

export interface AiTaskPlanEventDto {
  id: string;
  planId: string;
  itemId?: string | null;
  runId?: string | null;
  seq: number;
  eventName: string;
  fromStatus?: string | null;
  toStatus?: string | null;
  summary?: string | null;
  payloadJson?: string | null;
  traceId?: string | null;
  operatorUserId?: string | null;
  createdTime: string;
}

export interface AiTaskPlanItemOutputDto {
  id: string;
  planId: string;
  itemId: string;
  runId?: string | null;
  outputType: string;
  resultSummary: string;
  content?: string | null;
  evidenceJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  createdTime: string;
}

export interface AiKernelFunctionDefinitionDto {
  pluginName: string;
  functionName: string;
  toolCode: string;
  toolName: string;
  toolDomain: string;
  toolVersion: string;
  description: string;
  riskLevel: string;
  isEnabled: boolean;
  requiresConfirmation: boolean;
  permissionCode: string;
  workflowPermissionCode?: string | null;
  requiredPermissionCodes: string[];
  sensitiveArgumentNames: string[];
  allowedWorkModes: string[];
  requiredArgumentNames: string[];
  inputSchemaJson: string;
  outputSchemaJson: string;
}

export interface AiToolInvokeRequest {
  conversationId?: string | null;
  runId?: string | null;
  modelConfigId?: string | null;
  planId?: string | null;
  planItemId?: string | null;
  workMode?: string | null;
  argumentsJson?: string | null;
  arguments: Record<string, unknown>;
  confirmedRiskAccepted?: boolean;
}

export interface AiToolDryRunResponse {
  toolCode: string;
  isValid: boolean;
  riskLevel: string;
  permissionCode: string;
  workflowPermissionCode?: string | null;
  requiresConfirmation: boolean;
  issues: string[];
  normalizedArgumentsJson: string;
}

export interface AiToolInvocationDto {
  id: string;
  conversationId?: string | null;
  runId?: string | null;
  modelConfigId?: string | null;
  planId?: string | null;
  itemId?: string | null;
  toolCode: string;
  toolName: string;
  traceId?: string | null;
  argumentsJson?: string | null;
  resultSummary?: string | null;
  status: string;
  durationMs: number;
  errorMessage?: string | null;
  createdTime: string;
  updatedTime?: string | null;
}

export interface AiToolInvokeResponse {
  invocation: AiToolInvocationDto;
  resultSummary: string;
  content: string;
  evidenceJson?: string | null;
  outputType: string;
}

export interface AiDataCenterAssistantIntentRequest {
  conversationId?: string | null;
  content: string;
  dataSourceId: string;
  dataSourceName?: string | null;
  modelConfigId?: string | null;
  selectedTable?: string | null;
}

export interface AiDataCenterAssistantToolIntentDto {
  arguments: Record<string, unknown>;
  argumentsJson: string;
  requiresConfirmation: boolean;
  riskLevel: string;
  summary: string;
  toolCode: string;
  toolName: string;
}

export interface AiDataCenterAssistantIntentResponse {
  assistantMessageId?: string | null;
  conversationId: string;
  modelConfigId: string;
  replyText: string;
  runId?: string | null;
  toolIntents: AiDataCenterAssistantToolIntentDto[];
  userMessageId?: string | null;
}

export interface AiSkCapabilityDto {
  capabilityCode: string;
  status: 'Blocked' | 'FrameworkUnavailable' | 'Implemented' | 'NotApplicable' | string;
  frameworkType: string;
  implementationSymbol: string;
  reason: string;
}

export interface AiKnowledgeSourceDto {
  id: string;
  sourceCode: string;
  sourceName: string;
  sourceType: string;
  status: string;
  createdTime: string;
}

export interface AiKnowledgeSourceUpsertRequest {
  sourceCode: string;
  sourceName: string;
  sourceType: string;
  description?: string | null;
}

export interface AiKnowledgeDocumentDto {
  id: string;
  sourceId: string;
  documentName: string;
  contentType: string;
  indexStatus: string;
  chunkCount: number;
  createdTime: string;
}

export interface AiKnowledgeSearchRequest {
  query: string;
  sourceId?: string | null;
  topK: number;
}

export interface AiKnowledgeSearchResponse {
  hits: Array<{
    documentId: string;
    chunkId: string;
    content: string;
    score: number;
  }>;
}

export interface AiWorkflowDraftArtifactDto {
  id: string;
  conversationId: string;
  runId?: string | null;
  planId?: string | null;
  planItemId?: string | null;
  traceId: string;
  workflowKey: string;
  workflowName: string;
  businessType: string;
  status: string;
  draftDslJson: string;
  bpmnXml?: string | null;
  businessCanvasJson?: string | null;
  bindingProposalJson?: string | null;
  formPermissionProposalJson?: string | null;
  actionMappingProposalJson?: string | null;
  notificationPreviewJson?: string | null;
  importedWorkflowModelId?: string | null;
  createdTime: string;
  updatedTime?: string | null;
}

export interface AiWorkflowValidationIssueDto {
  severity: string;
  errorCode: string;
  message: string;
  nodeId?: string | null;
  edgeId?: string | null;
  field?: string | null;
  suggestion?: string | null;
}

export interface AiWorkflowValidationReportDto {
  id: string;
  draftArtifactId: string;
  isValid: boolean;
  errorCount: number;
  warningCount: number;
  issues: AiWorkflowValidationIssueDto[];
  traceId: string;
  createdTime: string;
}

export interface AiWorkflowSimulationStepDto {
  sortOrder: number;
  nodeId: string;
  nodeName: string;
  action: string;
  matchedEdgeId?: string | null;
  condition?: string | null;
  conditionMatched: boolean;
  summary: string;
}

export interface AiWorkflowSimulationReportDto {
  id: string;
  draftArtifactId: string;
  succeeded: boolean;
  variables: Record<string, unknown>;
  steps: AiWorkflowSimulationStepDto[];
  traceId: string;
  createdTime: string;
}

export interface AiWorkflowDiagnosisReportDto {
  id: string;
  diagnosisType: string;
  targetId: string;
  summary: string;
  evidence: string[];
  suggestions: string[];
  traceId: string;
  createdTime: string;
}

export interface AiWorkflowOverviewDto {
  draftArtifacts: AiWorkflowDraftArtifactDto[];
  validationReports: AiWorkflowValidationReportDto[];
  simulationReports: AiWorkflowSimulationReportDto[];
  diagnosisReports: AiWorkflowDiagnosisReportDto[];
  toolInvocations: AiToolInvocationDto[];
}

export interface AiTaskPlanUpsertRequest {
  title: string;
  goal: string;
  status: string;
  mode: string;
  executionStrategy: string;
  risksJson?: string | null;
  assumptionsJson?: string | null;
  metadataJson?: string | null;
  expectedRevision?: number | null;
  items: AiTaskPlanItemUpsertRequest[];
}

export interface AiTaskPlanItemUpsertRequest {
  id?: string | null;
  parentItemId?: string | null;
  title: string;
  description: string;
  status: string;
  priority: string;
  ownerType: string;
  taskType: string;
  sortOrder: number;
  dependsOnJson?: string | null;
  acceptanceCriteriaJson?: string | null;
  toolCode?: string | null;
  executionHint?: string | null;
  maxRetryCount: number;
}

export interface AiTaskPlanItemPatchRequest {
  title?: string | null;
  description?: string | null;
  status?: string | null;
  priority?: string | null;
  ownerType?: string | null;
  taskType?: string | null;
  sortOrder?: number | null;
  parentItemId?: string | null;
  dependsOnJson?: string | null;
  acceptanceCriteriaJson?: string | null;
  toolCode?: string | null;
  executionHint?: string | null;
  result?: string | null;
  resultSummary?: string | null;
  evidenceJson?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  blockedReason?: string | null;
  skipReason?: string | null;
  userResult?: string | null;
  expectedRevision?: number | null;
  expectedUpdatedTime?: string | null;
}

export interface AiTaskPlanItemActionRequest {
  reason?: string | null;
  userResult?: string | null;
  executionHint?: string | null;
  expectedUpdatedTime?: string | null;
}

export interface AiTaskPlanMoveRequest {
  parentItemId?: string | null;
  sortOrder: number;
  expectedRevision?: number | null;
}

export interface AiTaskPlanGenerateRequest {
  content: string;
  modelConfigId?: string | null;
  promptTemplateId?: string | null;
  clientMessageId?: string | null;
  idempotencyKey?: string | null;
}

export interface AiAgentExecutionResult {
  planId: string;
  runId: string;
  planStatus: string;
  summary: string;
  events: AiTaskPlanEventDto[];
  outputs: AiTaskPlanItemOutputDto[];
}

export interface AiWorkbenchOverviewDto {
  todayConversationCount: number;
  activeConversationCount: number;
  todayRunCount: number;
  todaySuccessRate: number;
  todayTotalTokens: number;
  enabledAgentCount: number;
  enabledModelCount: number;
  enabledToolCount: number;
  recentConversations: AiConversationDto[];
}

export interface AiObservabilitySummaryDto {
  requestCount: number;
  successCount: number;
  failedCount: number;
  promptTokens: number;
  completionTokens: number;
  reasoningTokens: number;
  totalTokens: number;
  costAmount: number;
  runCount: number;
  runningRunCount: number;
  toolExecutionCount: number;
  failedToolExecutionCount: number;
}

export interface AiObservabilityTrendPointDto {
  bucket: string;
  requestCount: number;
  successCount: number;
  failedCount: number;
  totalTokens: number;
  costAmount: number;
}

export interface AiRunListItemDto {
  id: string;
  conversationId?: string | null;
  mode: string;
  status: string;
  totalTokens: number;
  errorCode?: string | null;
  errorMessage?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
}

export interface AiFailureSummaryDto {
  errorCode: string;
  errorMessage: string;
  count: number;
}

export interface AiUsageQuery {
  endedAt?: string | null;
  modelCode?: string | null;
  providerCode?: string | null;
  startedAt?: string | null;
  userId?: string | null;
}

export interface AiRunQuery extends AiUsageQuery, AiGridQuery {
  mode?: string | null;
}

export interface AiToolExecutionQuery extends AiGridQuery {
  endedAt?: string | null;
  runId?: string | null;
  startedAt?: string | null;
  status?: string;
  toolCode?: string | null;
}

export interface AiSecuritySettingsDto {
  requireToolConfirmation: boolean;
  maxParallelAgents: number;
  maxInputCharacters: number;
  maxContextMessages: number;
  allowReasoningDisplay: boolean;
  multiAgentFailurePolicy: string;
}

export interface AiSettingsDto {
  defaultProviderId?: string | null;
  defaultModelConfigId?: string | null;
  defaultAgentProfileId?: string | null;
  defaultPromptTemplateId?: string | null;
  notificationSettingsJson: string;
  logRetentionDays: number;
  cleanupBatchSize: number;
}

export interface AiSettingsExportDto {
  settings: AiSettingsDto;
  promptTemplates: AiPromptTemplateDto[];
  agentProfiles: AiAgentProfileDto[];
  toolDefinitions: AiToolDefinitionDto[];
  exportedAt: string;
}

export interface AiSettingsImportRequest {
  settings?: AiSettingsDto | null;
  promptTemplates: AiPromptTemplateUpsertRequest[];
  agentProfiles: AiAgentProfileUpsertRequest[];
  toolDefinitions: AiToolDefinitionUpsertRequest[];
}

export interface AiSettingsImportResultDto {
  settingsUpdated: number;
  promptTemplatesImported: number;
  agentProfilesImported: number;
  toolDefinitionsImported: number;
}

export interface AiCleanupRequest {
  batchSize?: number | null;
  retentionDays?: number | null;
}

export interface AiCleanupResultDto {
  conversationsArchived: number;
  usageLogsDeleted: number;
  toolExecutionsDeleted: number;
  indexTasksDeleted: number;
}

export interface AiToolDefinitionDto {
  id: string;
  toolCode: string;
  toolName: string;
  toolType: string;
  toolDomain: string;
  riskLevel: string;
  requiresConfirmation: boolean;
  permissionCode: string;
  inputSchemaJson: string;
  outputSchemaJson: string;
  status: string;
  createdTime: string;
}

export interface AiToolDefinitionUpsertRequest {
  toolCode: string;
  toolName: string;
  toolType: string;
  toolDomain: string;
  riskLevel: string;
  requiresConfirmation: boolean;
  permissionCode: string;
  inputSchemaJson: string;
  outputSchemaJson: string;
  status: string;
}

export interface AiToolBindingDto {
  id: string;
  agentProfileId: string;
  toolCode: string;
  autoInvokeAllowed: boolean;
  status: string;
}

export interface AiToolBindingUpsertRequest {
  agentProfileId: string;
  toolCode: string;
  autoInvokeAllowed: boolean;
  status: string;
}

export interface AiWorkflowOptionDto {
  workflowModelId: string;
  workflowCode: string;
  workflowName: string;
  status: string;
}

export interface AiWorkflowToolBindingDto {
  id: string;
  workflowModelId: string;
  workflowCode: string;
  workflowName: string;
  toolCode: string;
  riskLevel: string;
  requiresConfirmation: boolean;
  status: string;
}

export interface AiWorkflowToolBindingRequest {
  workflowModelId: string;
  workflowCode: string;
  workflowName: string;
  toolCode: string;
  riskLevel: string;
  requiresConfirmation: boolean;
  status: string;
}

interface AiStreamOptions {
  onEvent: (event: AiStreamEventDto) => void;
  request: AiChatStreamRequest;
  signal?: AbortSignal;
}

export const aiChatApi = {
  workbench: {
    overview: (signal?: AbortSignal) => httpClient.get<AiWorkbenchOverviewDto>('/ai/workbench/overview', undefined, signal)
  },
  conversations: {
    list: (query: AiGridQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiConversationDto>>(`/ai/conversations${buildQueryString(query)}`, undefined, signal),
    create: (request: AiConversationCreateRequest) => httpClient.post<AiConversationDto, AiConversationCreateRequest>('/ai/conversations', request),
    update: (id: string, request: AiConversationUpdateRequest) =>
      httpClient.put<AiConversationDto, AiConversationUpdateRequest>(`/ai/conversations/${id}`, request),
    updateStatus: (id: string, status: string) => httpClient.post<AiConversationDto, { status: string }>(`/ai/conversations/${id}/status`, { status }),
    delete: (id: string) => httpClient.delete<boolean>(`/ai/conversations/${id}`),
    detail: (id: string, signal?: AbortSignal) => httpClient.get<AiConversationDetailDto>(`/ai/conversations/${id}`, undefined, signal),
    messages: (id: string, query: AiGridQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiMessageDto>>(`/ai/conversations/${id}/messages${buildQueryString(query)}`, undefined, signal),
    snapshots: (id: string, signal?: AbortSignal) => httpClient.get<AiContextSnapshotDto[]>(`/ai/conversations/${id}/snapshots`, undefined, signal),
    compress: (id: string, modelConfigId?: string | null) =>
      httpClient.post<AiContextSnapshotDto, null>(
        `/ai/conversations/${id}/compress${buildQueryString({ modelConfigId })}`,
        null
      ),
    feedback: (messageId: string, rating: string, comment?: string | null) =>
      httpClient.post<AiMessageDto, { comment?: string | null; rating: string }>(`/ai/conversations/messages/${messageId}/feedback`, { comment, rating })
  },
  taskPlans: {
    list: (conversationId: string, signal?: AbortSignal) =>
      httpClient.get<AiTaskPlanDto[]>(`/ai/conversations/${conversationId}/task-plans`, undefined, signal),
    detail: (planId: string, includeEvents = false, signal?: AbortSignal) =>
      httpClient.get<AiTaskPlanDto>(`/ai/task-plans/${planId}${buildQueryString({ includeEvents })}`, undefined, signal),
    events: (planId: string, afterSeq?: number | null, pageSize = 100, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiTaskPlanEventDto>>(
        `/ai/task-plans/${planId}/events${buildQueryString({ afterSeq, pageSize })}`,
        undefined,
        signal
      ),
    outputs: (planId: string, itemId?: string | null, pageIndex = 1, pageSize = 50, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiTaskPlanItemOutputDto>>(
        `/ai/task-plans/${planId}/outputs${buildQueryString({ itemId, pageIndex, pageSize })}`,
        undefined,
        signal
      ),
    create: (conversationId: string, request: AiTaskPlanUpsertRequest) =>
      httpClient.post<AiTaskPlanDto, AiTaskPlanUpsertRequest>(`/ai/conversations/${conversationId}/task-plans`, request),
    generate: (conversationId: string, request: AiTaskPlanGenerateRequest) =>
      httpClient.post<AiTaskPlanDto, AiTaskPlanGenerateRequest>(`/ai/conversations/${conversationId}/task-plans/generate`, request),
    update: (planId: string, request: AiTaskPlanUpsertRequest) =>
      httpClient.put<AiTaskPlanDto, AiTaskPlanUpsertRequest>(`/ai/task-plans/${planId}`, request),
    replan: (planId: string, request: AiTaskPlanUpsertRequest) =>
      httpClient.post<AiTaskPlanDto, AiTaskPlanUpsertRequest>(`/ai/task-plans/${planId}/replan`, request),
    duplicate: (planId: string) => httpClient.post<AiTaskPlanDto, Record<string, never>>(`/ai/task-plans/${planId}/duplicate`, {}),
    delete: (planId: string) => httpClient.delete<boolean>(`/ai/task-plans/${planId}`),
    addItem: (planId: string, request: AiTaskPlanItemUpsertRequest) =>
      httpClient.post<AiTaskPlanItemDto, AiTaskPlanItemUpsertRequest>(`/ai/task-plans/${planId}/items`, request),
    patchItem: (itemId: string, request: AiTaskPlanItemPatchRequest) =>
      httpClient.request<AiTaskPlanItemDto, AiTaskPlanItemPatchRequest>({
        body: request,
        method: 'PATCH',
        path: `/ai/task-plan-items/${itemId}`
      }),
    moveItem: (itemId: string, request: AiTaskPlanMoveRequest) =>
      httpClient.post<AiTaskPlanItemDto, AiTaskPlanMoveRequest>(`/ai/task-plan-items/${itemId}/move`, request),
    deleteItem: (itemId: string, expectedRevision?: number | null) =>
      httpClient.delete<boolean>(`/ai/task-plan-items/${itemId}${buildQueryString({ expectedRevision })}`),
    approve: (planId: string) => httpClient.post<AiTaskPlanDto, Record<string, never>>(`/ai/task-plans/${planId}/approve`, {}),
    unapprove: (planId: string) => httpClient.post<AiTaskPlanDto, Record<string, never>>(`/ai/task-plans/${planId}/unapprove`, {}),
    execute: (planId: string, request: AiTaskPlanItemActionRequest = {}) =>
      httpClient.post<AiAgentExecutionResult, AiTaskPlanItemActionRequest>(`/ai/task-plans/${planId}/execute`, request),
    pause: (planId: string) => httpClient.post<AiTaskPlanDto, Record<string, never>>(`/ai/task-plans/${planId}/pause`, {}),
    resume: (planId: string) => httpClient.post<AiTaskPlanDto, Record<string, never>>(`/ai/task-plans/${planId}/resume`, {}),
    cancel: (planId: string) => httpClient.post<AiTaskPlanDto, Record<string, never>>(`/ai/task-plans/${planId}/cancel`, {}),
    markComplete: (itemId: string, request: AiTaskPlanItemActionRequest) =>
      httpClient.post<AiTaskPlanItemDto, AiTaskPlanItemActionRequest>(`/ai/task-plan-items/${itemId}/mark-complete`, request),
    retry: (itemId: string, request: AiTaskPlanItemActionRequest = {}) =>
      httpClient.post<AiTaskPlanItemDto, AiTaskPlanItemActionRequest>(`/ai/task-plan-items/${itemId}/retry`, request),
    skip: (itemId: string, request: AiTaskPlanItemActionRequest) =>
      httpClient.post<AiTaskPlanItemDto, AiTaskPlanItemActionRequest>(`/ai/task-plan-items/${itemId}/skip`, request),
    block: (itemId: string, request: AiTaskPlanItemActionRequest) =>
      httpClient.post<AiTaskPlanItemDto, AiTaskPlanItemActionRequest>(`/ai/task-plan-items/${itemId}/block`, request),
    unblock: (itemId: string, request: AiTaskPlanItemActionRequest = {}) =>
      httpClient.post<AiTaskPlanItemDto, AiTaskPlanItemActionRequest>(`/ai/task-plan-items/${itemId}/unblock`, request)
  },
  providers: {
    list: (query: AiGridQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiProviderDto>>(`/ai/providers${buildQueryString(query)}`, undefined, signal),
    options: (signal?: AbortSignal) => httpClient.get<AiProviderDto[]>('/ai/providers/options', undefined, signal),
    create: (request: AiProviderUpsertRequest) => httpClient.post<AiProviderDto, AiProviderUpsertRequest>('/ai/providers', request),
    update: (id: string, request: AiProviderUpsertRequest) => httpClient.put<AiProviderDto, AiProviderUpsertRequest>(`/ai/providers/${id}`, request),
    delete: (id: string) => httpClient.delete<boolean>(`/ai/providers/${id}`),
    test: (id: string) => httpClient.post<boolean, Record<string, never>>(`/ai/providers/${id}/test`, {})
  },
  models: {
    list: (query: AiGridQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiModelConfigDto>>(`/ai/model-configs${buildQueryString(query)}`, undefined, signal),
    options: (signal?: AbortSignal) => httpClient.get<AiModelConfigDto[]>('/ai/model-configs/options', undefined, signal),
    create: (request: AiModelConfigUpsertRequest) => httpClient.post<AiModelConfigDto, AiModelConfigUpsertRequest>('/ai/model-configs', request),
    update: (id: string, request: AiModelConfigUpsertRequest) =>
      httpClient.put<AiModelConfigDto, AiModelConfigUpsertRequest>(`/ai/model-configs/${id}`, request),
    delete: (id: string) => httpClient.delete<boolean>(`/ai/model-configs/${id}`)
  },
  dataCenterAssistant: {
    intent: (request: AiDataCenterAssistantIntentRequest) =>
      httpClient.post<AiDataCenterAssistantIntentResponse, AiDataCenterAssistantIntentRequest>(
        '/ai/data-center-assistant/intent',
        request,
        { timeoutMs: 180_000 }
      )
  },
  prompts: {
    list: (query: AiGridQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiPromptTemplateDto>>(`/ai/prompt-templates${buildQueryString(query)}`, undefined, signal),
    options: (signal?: AbortSignal) => httpClient.get<AiPromptTemplateDto[]>('/ai/prompt-templates/options', undefined, signal),
    create: (request: AiPromptTemplateUpsertRequest) => httpClient.post<AiPromptTemplateDto, AiPromptTemplateUpsertRequest>('/ai/prompt-templates', request),
    update: (id: string, request: AiPromptTemplateUpsertRequest) =>
      httpClient.put<AiPromptTemplateDto, AiPromptTemplateUpsertRequest>(`/ai/prompt-templates/${id}`, request),
    delete: (id: string) => httpClient.delete<boolean>(`/ai/prompt-templates/${id}`)
  },
  agents: {
    list: (query: AiGridQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiAgentProfileDto>>(`/ai/agents${buildQueryString(query)}`, undefined, signal),
    options: (signal?: AbortSignal) => httpClient.get<AiAgentProfileDto[]>('/ai/agents/options', undefined, signal),
    create: (request: AiAgentProfileUpsertRequest) => httpClient.post<AiAgentProfileDto, AiAgentProfileUpsertRequest>('/ai/agents', request),
    update: (id: string, request: AiAgentProfileUpsertRequest) => httpClient.put<AiAgentProfileDto, AiAgentProfileUpsertRequest>(`/ai/agents/${id}`, request),
    delete: (id: string) => httpClient.delete<boolean>(`/ai/agents/${id}`)
  },
  observability: {
    summary: (query: AiUsageQuery, signal?: AbortSignal) =>
      httpClient.get<AiObservabilitySummaryDto>(`/ai/observability/summary${buildQueryString(query)}`, undefined, signal),
    trends: (query: AiUsageQuery, signal?: AbortSignal) =>
      httpClient.get<AiObservabilityTrendPointDto[]>(`/ai/observability/trends${buildQueryString(query)}`, undefined, signal),
    runs: (query: AiRunQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiRunListItemDto>>(`/ai/observability/runs${buildQueryString(query)}`, undefined, signal),
    runDetail: (runId: string, signal?: AbortSignal) =>
      httpClient.get<AiRunDto>(`/ai/observability/runs/${runId}`, undefined, signal),
    toolExecutions: (query: AiToolExecutionQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiToolInvocationDto>>(`/ai/observability/tool-executions${buildQueryString(query)}`, undefined, signal),
    failures: (query: AiUsageQuery, signal?: AbortSignal) =>
      httpClient.get<AiFailureSummaryDto[]>(`/ai/observability/failures${buildQueryString(query)}`, undefined, signal)
  },
  security: {
    policy: (signal?: AbortSignal) => httpClient.get<AiSecuritySettingsDto>('/ai/security/policy', undefined, signal),
    updatePolicy: (request: AiSecuritySettingsDto) => httpClient.put<AiSecuritySettingsDto, AiSecuritySettingsDto>('/ai/security/policy', request)
  },
  settings: {
    get: (signal?: AbortSignal) => httpClient.get<AiSettingsDto>('/ai/settings', undefined, signal),
    update: (request: AiSettingsDto) => httpClient.put<AiSettingsDto, AiSettingsDto>('/ai/settings', request),
    export: (signal?: AbortSignal) => httpClient.get<AiSettingsExportDto>('/ai/settings/export', undefined, signal),
    import: (request: AiSettingsImportRequest) => httpClient.post<AiSettingsImportResultDto, AiSettingsImportRequest>('/ai/settings/import', request),
    cleanup: (request: AiCleanupRequest) => httpClient.post<AiCleanupResultDto, AiCleanupRequest>('/ai/settings/cleanup', request)
  },
  tools: {
    list: (signal?: AbortSignal) => httpClient.get<AiKernelFunctionDefinitionDto[]>('/ai/tools', undefined, signal),
    detail: (toolCode: string, signal?: AbortSignal) => httpClient.get<AiKernelFunctionDefinitionDto>(`/ai/tools/${encodeURIComponent(toolCode)}`, undefined, signal),
    definitions: (query: AiGridQuery & { riskLevel?: string | null; toolDomain?: string | null; toolType?: string | null }, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiToolDefinitionDto>>(`/ai/tools/definitions${buildQueryString(query)}`, undefined, signal),
    upsertDefinition: (id: string | null, request: AiToolDefinitionUpsertRequest) =>
      id
        ? httpClient.put<AiToolDefinitionDto, AiToolDefinitionUpsertRequest>(`/ai/tools/definitions/${id}`, request)
        : httpClient.post<AiToolDefinitionDto, AiToolDefinitionUpsertRequest>('/ai/tools/definitions', request),
    syncDefinitions: () => httpClient.post<boolean, Record<string, never>>('/ai/tools/definitions/sync', {}),
    bindings: (agentProfileId?: string | null, signal?: AbortSignal) =>
      httpClient.get<AiToolBindingDto[]>(`/ai/tools/bindings${buildQueryString({ agentProfileId })}`, undefined, signal),
    upsertBinding: (request: AiToolBindingUpsertRequest) => httpClient.put<AiToolBindingDto, AiToolBindingUpsertRequest>('/ai/tools/bindings', request),
    availableWorkflows: (signal?: AbortSignal) => httpClient.get<AiWorkflowOptionDto[]>('/ai/workflow-tools/available-workflows', undefined, signal),
    bindWorkflow: (request: AiWorkflowToolBindingRequest) =>
      httpClient.put<AiWorkflowToolBindingDto, AiWorkflowToolBindingRequest>('/ai/workflow-tools/bindings', request),
    dryRun: (toolCode: string, request: AiToolInvokeRequest) =>
      httpClient.post<AiToolDryRunResponse, AiToolInvokeRequest>(`/ai/tools/${encodeURIComponent(toolCode)}/dry-run`, request),
    invoke: (toolCode: string, request: AiToolInvokeRequest) =>
      httpClient.post<AiToolInvokeResponse, AiToolInvokeRequest>(`/ai/tools/${encodeURIComponent(toolCode)}/invoke`, request),
    invocations: (runId: string, signal?: AbortSignal) =>
      httpClient.get<AiToolInvocationDto[]>(`/ai/runs/${runId}/tool-invocations`, undefined, signal)
  },
  capabilities: {
    list: (signal?: AbortSignal) => httpClient.get<AiSkCapabilityDto[]>('/ai/sk-capabilities', undefined, signal)
  },
  knowledge: {
    sources: (query: AiGridQuery, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiKnowledgeSourceDto>>(`/ai/knowledge/sources${buildQueryString(query)}`, undefined, signal),
    createSource: (request: AiKnowledgeSourceUpsertRequest) =>
      httpClient.post<AiKnowledgeSourceDto, AiKnowledgeSourceUpsertRequest>('/ai/knowledge/sources', request),
    documents: (query: AiGridQuery & { sourceId?: string | null }, signal?: AbortSignal) =>
      httpClient.get<GridPageResult<AiKnowledgeDocumentDto>>(`/ai/knowledge/documents${buildQueryString(query)}`, undefined, signal),
    reindex: (sourceId?: string | null) => httpClient.post<boolean, null>(`/ai/knowledge/reindex${buildQueryString({ sourceId })}`, null),
    search: (request: AiKnowledgeSearchRequest) =>
      httpClient.post<AiKnowledgeSearchResponse, AiKnowledgeSearchRequest>('/ai/knowledge/search', request)
  },
  workflow: {
    overview: (conversationId: string, signal?: AbortSignal) =>
      httpClient.get<AiWorkflowOverviewDto>(`/ai/workflow/conversations/${conversationId}/overview`, undefined, signal),
    draft: (id: string, signal?: AbortSignal) => httpClient.get<AiWorkflowDraftArtifactDto>(`/ai/workflow/draft-artifacts/${id}`, undefined, signal),
    validationReport: (id: string, signal?: AbortSignal) =>
      httpClient.get<AiWorkflowValidationReportDto>(`/ai/workflow/validation-reports/${id}`, undefined, signal),
    simulationReport: (id: string, signal?: AbortSignal) =>
      httpClient.get<AiWorkflowSimulationReportDto>(`/ai/workflow/simulation-reports/${id}`, undefined, signal),
    diagnosisReport: (id: string, signal?: AbortSignal) =>
      httpClient.get<AiWorkflowDiagnosisReportDto>(`/ai/workflow/diagnosis-reports/${id}`, undefined, signal),
    validate: (draftArtifactId: string, request: AiToolInvokeRequest) =>
      httpClient.post<AiToolInvokeResponse, AiToolInvokeRequest>(`/ai/workflow/draft-artifacts/${draftArtifactId}/validate`, request),
    simulate: (draftArtifactId: string, request: AiToolInvokeRequest) =>
      httpClient.post<AiToolInvokeResponse, AiToolInvokeRequest>(`/ai/workflow/draft-artifacts/${draftArtifactId}/simulate`, request),
    openInDesigner: (draftArtifactId: string) =>
      httpClient.post<{ designerRoute: string; draftArtifactId: string }, Record<string, never>>(`/ai/workflow/draft-artifacts/${draftArtifactId}/open-in-designer`, {}),
    importDraft: (draftArtifactId: string) =>
      httpClient.post<unknown, Record<string, never>>(`/workflows/models/import-ai-draft/${draftArtifactId}`, {})
  },
  stream: (conversationId: string, options: AiStreamOptions) => streamAiChat(conversationId, options),
  stopRun: (runId: string) => httpClient.post<AiRunDto, Record<string, never>>(`/ai/chat/runs/${runId}/stop`, {})
};

async function streamAiChat(conversationId: string, options: AiStreamOptions): Promise<void> {
  await httpClient.streamSse<AiStreamEventDto, AiChatStreamRequest>({
    body: options.request,
    method: 'POST',
    onEvent: options.onEvent,
    parseEvent: parseAiStreamEvent,
    path: `/ai/chat/conversations/${conversationId}/stream`,
    signal: options.signal,
    traceId: true
  });
}

function parseAiStreamEvent(frame: { data: string; event: string }): AiStreamEventDto | null {
  let parsed: AiStreamEventDto;
  try {
    parsed = JSON.parse(frame.data) as AiStreamEventDto;
  } catch {
    throw new HttpError({
      data: frame.data,
      message: formatMessage(translateCurrentLocale('ai.chat.error.streamFrameParseFailed'), { event: frame.event }),
      status: 500
    });
  }

  return {
    ...parsed,
    event: (parsed.event || frame.event) as AiStreamEventName
  };
}

export function createAiStreamEventError(event: AiStreamEventDto): HttpError {
  const payload = readStreamErrorPayload(event.data);
  const message = payload.message || payload.error || translateCurrentLocale('ai.chat.error.modelCallFailed');
  return new HttpError({
    code: undefined,
    data: event.data,
    message: payload.errorCode ? `${payload.errorCode}: ${message}` : message,
    status: 500,
    traceId: event.traceId || payload.traceId || undefined
  });
}

function readStreamErrorPayload(data: unknown): { error?: string; errorCode?: string; message?: string; traceId?: string } {
  if (!data || typeof data !== 'object') {
    return {};
  }

  return {
    error: typeof (data as { error?: unknown }).error === 'string' ? (data as { error: string }).error : undefined,
    errorCode: typeof (data as { errorCode?: unknown }).errorCode === 'string' ? (data as { errorCode: string }).errorCode : undefined,
    message: typeof (data as { message?: unknown }).message === 'string' ? (data as { message: string }).message : undefined,
    traceId: typeof (data as { traceId?: unknown }).traceId === 'string' ? (data as { traceId: string }).traceId : undefined
  };
}
