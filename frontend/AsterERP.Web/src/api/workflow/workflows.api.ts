import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString } from '../queryString';
import type { GridPageResult } from '../shared.types';

export interface WorkflowModelListItemDto {
  id: string;
  modelId: string;
  modelKey: string;
  name: string;
  appCode: string;
  categoryCode: string;
  status?: number | null;
  extendStatus?: number | null;
  version?: number | null;
  processDefinitionId?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface WorkflowModelDetailDto extends WorkflowModelListItemDto {
  bpmnXml: string;
  extensionJson?: string | null;
}

export interface WorkflowModelUpsertRequest {
  id?: string | null;
  modelId?: string | null;
  modelKey: string;
  name: string;
  appCode?: string | null;
  categoryCode?: string | null;
  modelType?: number | null;
  formType?: number | null;
  remark?: string | null;
}

export interface WorkflowModelXmlSaveRequest {
  bpmnXml: string;
  extensionJson?: string | null;
}

export interface WorkflowModelValidationDto {
  isValid: boolean;
  errors: string[];
}

export interface WorkflowModelPublishDto {
  modelId: string;
  deploymentId: string;
  processDefinitionId: string;
  version: number;
}

export interface WorkflowProcessDefinitionDto {
  id: string;
  key?: string | null;
  name?: string | null;
  deploymentId?: string | null;
  version: number;
  category?: string | null;
  description?: string | null;
  isSuspended: boolean;
  tenantId?: string | null;
}

export interface WorkflowDeploymentListItemDto {
  id: string;
  name?: string | null;
  category?: string | null;
  key?: string | null;
  tenantId?: string | null;
  deployTime?: string | null;
  resources: string[];
}

export interface WorkflowDeploymentResourceDto {
  deploymentId: string;
  resourceName: string;
  contentType: string;
  content: string;
}

export interface WorkflowBindingDto {
  id: string;
  tenantId: string;
  appCode: string;
  menuCode: string;
  businessType: string;
  processDefinitionKey: string;
  processDefinitionId?: string | null;
  modelId?: string | null;
  modelKey?: string | null;
  formResourceCode?: string | null;
  pageCode?: string | null;
  modelCode?: string | null;
  keyField?: string | null;
  detailRoute?: string | null;
  titleTemplate?: string | null;
  isEnabled: boolean;
  startFormJson?: string | null;
  callbackConfig?: WorkflowCallbackConfigDto | null;
  remark?: string | null;
}

export interface WorkflowBindingUpsertRequest {
  tenantId: string;
  appCode: string;
  menuCode: string;
  businessType: string;
  processDefinitionKey: string;
  processDefinitionId?: string | null;
  modelId?: string | null;
  modelKey?: string | null;
  formResourceCode?: string | null;
  pageCode?: string | null;
  modelCode?: string | null;
  keyField?: string | null;
  detailRoute?: string | null;
  titleTemplate?: string | null;
  isEnabled: boolean;
  startFormJson?: string | null;
  callbackConfig?: WorkflowCallbackConfigDto | null;
  remark?: string | null;
}

export type WorkflowCallbackTrigger =
  | 'process-start'
  | 'node-enter'
  | 'task-complete'
  | 'task-reject'
  | 'task-return'
  | 'process-completed'
  | 'process-withdrawn'
  | 'process-terminated';

export type WorkflowCallbackKeySource =
  | 'businessKey'
  | 'context'
  | 'variable'
  | 'submittedField';

export type WorkflowCallbackValueSource =
  | 'constant'
  | 'context'
  | 'variable'
  | 'submittedField';

export interface WorkflowCallbackConfigDto {
  rules?: WorkflowCallbackRuleDto[] | null;
  version?: 'latest';
}

export interface WorkflowCallbackRuleDto {
  ruleId?: string | null;
  enabled: boolean;
  trigger: WorkflowCallbackTrigger;
  nodeId?: string | null;
  target?: WorkflowCallbackTargetDto | null;
  assignments?: WorkflowCallbackAssignmentDto[] | null;
  sortOrder: number;
}

export interface WorkflowCallbackTargetDto {
  modelCode?: string | null;
  keySource?: WorkflowCallbackKeySource | null;
  keyName?: string | null;
}

export interface WorkflowCallbackAssignmentDto {
  fieldCode: string;
  valueSource: WorkflowCallbackValueSource;
  value?: unknown;
  valueName?: string | null;
}

export interface WorkflowFormFieldDto {
  fieldCode: string;
  fieldName: string;
  dataType: string;
  binding: string;
  visible: boolean;
  queryable: boolean;
  sortable: boolean;
  writable: boolean;
  renderer?: string | null;
  dictType?: string | null;
  order: number;
}

export interface WorkflowFormResourceDto {
  resourceCode: string;
  resourceName: string;
  menuCode: string;
  businessType: string;
  routePath?: string | null;
  pageCode: string;
  modelCode: string;
  keyField: string;
  permissionCode?: string | null;
  fields: WorkflowFormFieldDto[];
}

export interface WorkflowCategoryDto {
  id: string;
  tenantId: string;
  appCode: string;
  categoryCode: string;
  categoryName: string;
  parentCode?: string | null;
  sortOrder: number;
  isEnabled: boolean;
  remark?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface WorkflowCategoryUpsertRequest {
  id?: string | null;
  tenantId?: string | null;
  appCode?: string | null;
  categoryCode: string;
  categoryName: string;
  parentCode?: string | null;
  sortOrder?: number | null;
  isEnabled?: boolean | null;
  remark?: string | null;
}

export interface WorkflowRequestDraftDto {
  id: string;
  tenantId: string;
  appCode: string;
  ownerUserId: string;
  ownerUserName?: string | null;
  formResourceCode: string;
  menuCode: string;
  businessType: string;
  businessKey?: string | null;
  title: string;
  draftJson: string;
  status: string;
  lastSavedAt: string;
  submittedAt?: string | null;
  processInstanceId?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface WorkflowRequestDraftUpsertRequest {
  id?: string | null;
  tenantId?: string | null;
  appCode?: string | null;
  formResourceCode: string;
  menuCode: string;
  businessType: string;
  businessKey?: string | null;
  title: string;
  draftJson: string;
}

export interface WorkflowRequestDraftSubmitRequest {
  comment?: string | null;
  variables?: Record<string, unknown> | null;
}

export interface WorkflowDelegationRuleDto {
  id: string;
  tenantId: string;
  appCode: string;
  ownerUserId: string;
  ownerUserName?: string | null;
  delegateUserId: string;
  delegateUserName?: string | null;
  scopeType: string;
  processDefinitionKey?: string | null;
  startAt: string;
  endAt: string;
  isEnabled: boolean;
  reason?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface WorkflowDelegationRuleUpsertRequest {
  id?: string | null;
  tenantId?: string | null;
  appCode?: string | null;
  delegateUserId: string;
  scopeType?: string | null;
  processDefinitionKey?: string | null;
  startAt: string;
  endAt: string;
  isEnabled?: boolean | null;
  reason?: string | null;
}

export interface WorkflowWorkCalendarDto {
  id: string;
  tenantId: string;
  appCode: string;
  calendarDate: string;
  dayType: string;
  isWorkingDay: boolean;
  calendarName: string;
  remark?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
}

export interface WorkflowWorkCalendarUpsertRequest {
  id?: string | null;
  tenantId?: string | null;
  appCode?: string | null;
  calendarDate: string;
  dayType: string;
  isWorkingDay?: boolean | null;
  calendarName: string;
  remark?: string | null;
}

export interface WorkflowApprovalStatisticsDto {
  totalStarted: number;
  running: number;
  completed: number;
  rejected: number;
  withdrawn: number;
  terminated: number;
  todo: number;
  done: number;
  cc: number;
}

export interface WorkflowBottleneckNodeDto {
  nodeKey: string;
  nodeName: string;
  completedCount: number;
  averageDurationHours: number;
}

export interface WorkflowEfficiencyAnalysisDto {
  averageDurationHours: number;
  overdueTaskCount: number;
  bottleneckNodes: WorkflowBottleneckNodeDto[];
}

export interface WorkflowBusinessDataReportItemDto {
  businessType: string;
  total: number;
  running: number;
  finished: number;
}

export interface WorkflowReportOverviewDto {
  approvalStatistics: WorkflowApprovalStatisticsDto;
  efficiencyAnalysis: WorkflowEfficiencyAnalysisDto;
  businessData: WorkflowBusinessDataReportItemDto[];
}

export interface WorkflowBindingStatusRequest {
  pageCode?: string | null;
  modelCode?: string | null;
  businessKeys?: string[] | null;
}

export interface WorkflowBusinessApprovalStatusDto {
  businessKey: string;
  hasHistory: boolean;
  latestStatus?: string | null;
  processInstanceId?: string | null;
  processDefinitionKey?: string | null;
  startedAt?: string | null;
  finishedAt?: string | null;
}

export interface WorkflowBindingStatusDto {
  pageCode?: string | null;
  modelCode?: string | null;
  binding?: WorkflowBindingDto | null;
  items: WorkflowBusinessApprovalStatusDto[];
}

export interface WorkflowIdentityLinkDto {
  id: string;
  userId?: string | null;
  groupId?: string | null;
  type?: string | null;
  taskId?: string | null;
  processInstanceId?: string | null;
  processDefinitionId?: string | null;
}

export interface WorkflowTaskListItemDto {
  id: string;
  name?: string | null;
  assignee?: string | null;
  assigneeName?: string | null;
  attachmentsCount: number;
  availableActions: string[];
  businessKey?: string | null;
  businessType?: string | null;
  candidateNames: string[];
  commentsCount: number;
  owner?: string | null;
  delegationState?: string | null;
  processInstanceId?: string | null;
  processDefinitionId?: string | null;
  processName?: string | null;
  executionId?: string | null;
  isClaimable: boolean;
  isOverdue: boolean;
  taskDefinitionKey?: string | null;
  priority: number;
  createdAt?: string | null;
  dueAt?: string | null;
  starterUserName?: string | null;
  identityLinks: WorkflowIdentityLinkDto[];
}

export interface WorkflowTaskSummaryDto {
  todo: number;
  done: number;
  mine: number;
  delegated: number;
  timeout: number;
  cc: number;
  history: number;
}

export interface WorkflowTaskActionRequest {
  userId?: string | null;
  targetUserId?: string | null;
  comment?: string | null;
  variables?: Record<string, unknown> | null;
}

export interface WorkflowTaskDetailDto {
  task: WorkflowTaskListItemDto;
  submittedForm: WorkflowSubmittedFormDto;
  comments: WorkflowCommentDto[];
  attachments: WorkflowAttachmentDto[];
  timeline: WorkflowTimelineItemDto[];
  nodePolicy: WorkflowTaskNodePolicyDto;
}

export interface WorkflowTaskNodePolicyDto {
  taskDefinitionKey?: string | null;
  actionPolicies: WorkflowTaskActionPolicyDto[];
  fieldPermissions: WorkflowTaskFieldPermissionDto[];
}

export interface WorkflowTaskActionPolicyDto {
  action: string;
  enabled: boolean;
  commentRequired: boolean;
  attachmentPolicy: string;
}

export interface WorkflowTaskFieldPermissionDto {
  fieldKey: string;
  fieldLabel?: string | null;
  visible: boolean;
  readonly: boolean;
  required: boolean;
  hidden: boolean;
}

export interface WorkflowSubmittedFormDto {
  source: string;
  fields: WorkflowSubmittedFormFieldDto[];
}

export interface WorkflowSubmittedFormFieldDto {
  field: string;
  label: string;
  value?: unknown;
  valueType?: string | null;
}

export interface WorkflowStartInstanceRequest {
  tenantId: string;
  appCode: string;
  menuCode: string;
  businessType: string;
  businessKey: string;
  title?: string | null;
  variables?: Record<string, unknown> | null;
}

export interface WorkflowActivityDto {
  id: string;
  activityId?: string | null;
  activityName?: string | null;
  activityType?: string | null;
  executionId?: string | null;
  processInstanceId?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  durationInMillis?: number | null;
}

export interface WorkflowInstanceDto {
  id: string;
  tenantId: string;
  appCode: string;
  menuCode: string;
  businessType: string;
  businessKey: string;
  processInstanceId: string;
  processDefinitionId?: string | null;
  processDefinitionKey: string;
  status: string;
  startedBy: string;
  startedByName?: string | null;
  startedAt: string;
  finishedAt?: string | null;
  variables: Record<string, unknown>;
  runtimeTasks: WorkflowTaskListItemDto[];
  submittedForm: WorkflowSubmittedFormDto;
  activities: WorkflowActivityDto[];
  timeline: WorkflowTimelineItemDto[];
  comments: WorkflowCommentDto[];
  attachments: WorkflowAttachmentDto[];
  identityLinks: WorkflowIdentityLinkDto[];
  notifications: WorkflowNotificationTaskDto[];
}

export interface WorkflowNotificationChannelDto {
  id: string;
  tenantId: string;
  appCode: string;
  channelCode: string;
  channelName: string;
  channelType: string;
  isEnabled: boolean;
  configJson?: string | null;
  failurePolicy: string;
  createdTime?: string | null;
  updatedTime?: string | null;
}

export interface WorkflowNotificationChannelUpsertRequest {
  id?: string | null;
  tenantId?: string | null;
  appCode?: string | null;
  channelCode: string;
  channelName: string;
  channelType: string;
  isEnabled: boolean;
  configJson?: string | null;
  failurePolicy?: string | null;
}

export interface WorkflowMessageTemplateDto {
  id: string;
  tenantId: string;
  appCode: string;
  templateCode: string;
  templateName: string;
  channelType: string;
  subjectTemplate?: string | null;
  bodyTemplate: string;
  variablesJson?: string | null;
  isEnabled: boolean;
  createdTime?: string | null;
  updatedTime?: string | null;
}

export interface WorkflowMessageTemplateUpsertRequest {
  id?: string | null;
  tenantId?: string | null;
  appCode?: string | null;
  templateCode: string;
  templateName: string;
  channelType: string;
  subjectTemplate?: string | null;
  bodyTemplate: string;
  variablesJson?: string | null;
  isEnabled: boolean;
}

export interface WorkflowNodeNotificationRuleDto {
  id: string;
  tenantId: string;
  appCode: string;
  modelId?: string | null;
  processDefinitionId?: string | null;
  processDefinitionKey?: string | null;
  nodeId: string;
  trigger: string;
  receiverType: string;
  receiverValue?: string | null;
  channelCodes: string[];
  templateCode: string;
  conditionJson?: string | null;
  failurePolicy: string;
  isEnabled: boolean;
  createdTime?: string | null;
  updatedTime?: string | null;
}

export interface WorkflowNodeNotificationRuleUpsertRequest {
  id?: string | null;
  tenantId?: string | null;
  appCode?: string | null;
  modelId?: string | null;
  processDefinitionId?: string | null;
  processDefinitionKey?: string | null;
  nodeId: string;
  trigger: string;
  receiverType: string;
  receiverValue?: string | null;
  channelCodes?: string[] | null;
  templateCode: string;
  conditionJson?: string | null;
  failurePolicy?: string | null;
  isEnabled: boolean;
}

export interface WorkflowNotificationTaskDto {
  id: string;
  tenantId: string;
  appCode: string;
  ruleId?: string | null;
  processInstanceId?: string | null;
  workflowTaskId?: string | null;
  nodeId?: string | null;
  trigger?: string | null;
  channelCode: string;
  templateCode?: string | null;
  receiverUserId?: string | null;
  receiverAddress?: string | null;
  subject?: string | null;
  content: string;
  status: string;
  retryCount: number;
  maxRetryCount: number;
  dueAt?: string | null;
  sentAt?: string | null;
  lastError?: string | null;
  createdTime?: string | null;
}

export interface WorkflowNotificationLogDto {
  id: string;
  notificationTaskId?: string | null;
  ruleId?: string | null;
  processInstanceId?: string | null;
  workflowTaskId?: string | null;
  channelCode?: string | null;
  receiverUserId?: string | null;
  eventName: string;
  result: string;
  message?: string | null;
  errorMessage?: string | null;
  provider?: string | null;
  traceId?: string | null;
  createdTime?: string | null;
}

export interface WorkflowNotificationPreviewRequest {
  receiverType: string;
  receiverValue?: string | null;
  templateCode?: string | null;
  variables?: Record<string, unknown> | null;
}

export interface WorkflowNotificationPreviewDto {
  receiverUserIds: string[];
  receiverNames: string[];
}

export interface WorkflowTimelineItemDto {
  id: string;
  kind: string;
  title: string;
  userId?: string | null;
  userName?: string | null;
  activityId?: string | null;
  taskId?: string | null;
  action?: string | null;
  comment?: string | null;
  createdAt?: string | null;
  finishedAt?: string | null;
  durationInMillis?: number | null;
  metadata: Record<string, unknown>;
}

export interface WorkflowCommentDto {
  id: string;
  taskId?: string | null;
  processInstanceId?: string | null;
  type?: string | null;
  userId?: string | null;
  message?: string | null;
  time?: string | null;
}

export interface WorkflowAttachmentDto {
  id: string;
  taskId?: string | null;
  processInstanceId?: string | null;
  name?: string | null;
  description?: string | null;
  type?: string | null;
  url?: string | null;
  hasContent: boolean;
  downloadUrl?: string | null;
  createdAt?: string | null;
}

export interface WorkflowAttachmentRequest {
  attachmentType?: string | null;
  name?: string | null;
  description?: string | null;
  url?: string | null;
  base64Content?: string | null;
}

export interface WorkflowInstanceListItemDto {
  id: string;
  tenantId: string;
  appCode: string;
  menuCode: string;
  businessType: string;
  businessKey: string;
  processInstanceId: string;
  processDefinitionId?: string | null;
  processDefinitionKey: string;
  status: string;
  startedBy: string;
  startedAt: string;
  finishedAt?: string | null;
}

export interface WorkflowHighlightedDiagramDto {
  processInstanceId: string;
  bpmnXml: string;
  activeActivityIds: string[];
  completedActivityIds: string[];
}

export interface WorkflowHistoricProcessDto {
  id: string;
  processDefinitionId?: string | null;
  businessKey?: string | null;
  startUserId?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  durationInMillis?: number | null;
  deleteReason?: string | null;
  businessType?: string | null;
  processName?: string | null;
  starterUserName?: string | null;
  status?: string | null;
}

export interface WorkflowHistoricTaskDto {
  id: string;
  name?: string | null;
  assignee?: string | null;
  owner?: string | null;
  processInstanceId?: string | null;
  processDefinitionId?: string | null;
  taskDefinitionKey?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  durationInMillis?: number | null;
  deleteReason?: string | null;
  businessType?: string | null;
  businessKey?: string | null;
  processName?: string | null;
  starterUserName?: string | null;
  assigneeName?: string | null;
  commentsCount: number;
  attachmentsCount: number;
}

export interface WorkflowHistoricVariableDto {
  id: string;
  name?: string | null;
  variableType?: string | null;
  value?: unknown;
  processInstanceId?: string | null;
  taskId?: string | null;
  createTime?: string | null;
  lastUpdatedTime?: string | null;
}

export interface WorkflowParticipantDto {
  id: string;
  code: string;
  name: string;
  type: string;
  parentId?: string | null;
  groupKey?: string | null;
  description?: string | null;
  employmentSummary?: string | null;
}

export interface WorkflowQuery {
  appCode?: string;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  status?: string;
  tenantId?: string;
}

export function getWorkflowModels(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowModelListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowModelListItemDto>>(`/workflows/models${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowModel(modelId: string, signal?: AbortSignal): Promise<ApiEnvelope<WorkflowModelDetailDto>> {
  return httpClient.get<WorkflowModelDetailDto>(`/workflows/models/${encodeURIComponent(modelId)}`, undefined, signal);
}

export function saveWorkflowModel(request: WorkflowModelUpsertRequest): Promise<ApiEnvelope<WorkflowModelDetailDto>> {
  return httpClient.post<WorkflowModelDetailDto, WorkflowModelUpsertRequest>('/workflows/models', request);
}

export function updateWorkflowModel(modelId: string, request: WorkflowModelUpsertRequest): Promise<ApiEnvelope<WorkflowModelDetailDto>> {
  return httpClient.put<WorkflowModelDetailDto, WorkflowModelUpsertRequest>(`/workflows/models/${encodeURIComponent(modelId)}`, request);
}

export function saveWorkflowModelXml(modelId: string, request: WorkflowModelXmlSaveRequest): Promise<ApiEnvelope<WorkflowModelDetailDto>> {
  return httpClient.put<WorkflowModelDetailDto, WorkflowModelXmlSaveRequest>(`/workflows/models/${encodeURIComponent(modelId)}/xml`, request);
}

export function validateWorkflowModel(modelId: string): Promise<ApiEnvelope<WorkflowModelValidationDto>> {
  return httpClient.post<WorkflowModelValidationDto, Record<string, never>>(`/workflows/models/${encodeURIComponent(modelId)}/validate`, {});
}

export function publishWorkflowModel(modelId: string): Promise<ApiEnvelope<WorkflowModelPublishDto>> {
  return httpClient.post<WorkflowModelPublishDto, Record<string, never>>(`/workflows/models/${encodeURIComponent(modelId)}/publish`, {});
}

export function getWorkflowDeployments(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowDeploymentListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowDeploymentListItemDto>>(`/workflows/deployments${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowDeploymentResource(deploymentId: string, resourceName: string, signal?: AbortSignal): Promise<ApiEnvelope<WorkflowDeploymentResourceDto>> {
  return httpClient.get<WorkflowDeploymentResourceDto>(`/workflows/deployments/${encodeURIComponent(deploymentId)}/resources/${encodeURIComponent(resourceName)}`, undefined, signal);
}

export function getWorkflowProcessDefinitions(key?: string, signal?: AbortSignal): Promise<ApiEnvelope<WorkflowProcessDefinitionDto[]>> {
  return httpClient.get<WorkflowProcessDefinitionDto[]>(`/workflows/deployments/definitions${buildQueryString({ key })}`, undefined, signal);
}

export function getWorkflowBindings(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowBindingDto>>> {
  return httpClient.get<GridPageResult<WorkflowBindingDto>>(`/workflows/bindings${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowFormResources(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowFormResourceDto>>> {
  return httpClient.get<GridPageResult<WorkflowFormResourceDto>>(`/workflows/form-resources${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowCategories(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowCategoryDto>>> {
  return httpClient.get<GridPageResult<WorkflowCategoryDto>>(`/workflows/categories${buildQueryString(query)}`, undefined, signal);
}

export function saveWorkflowCategory(request: WorkflowCategoryUpsertRequest): Promise<ApiEnvelope<WorkflowCategoryDto>> {
  return httpClient.post<WorkflowCategoryDto, WorkflowCategoryUpsertRequest>('/workflows/categories', request);
}

export function deleteWorkflowCategory(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/categories/${encodeURIComponent(id)}`);
}

export function saveWorkflowBinding(request: WorkflowBindingUpsertRequest): Promise<ApiEnvelope<WorkflowBindingDto>> {
  return httpClient.post<WorkflowBindingDto, WorkflowBindingUpsertRequest>('/workflows/bindings', request);
}

export function getWorkflowBindingStatus(request: WorkflowBindingStatusRequest): Promise<ApiEnvelope<WorkflowBindingStatusDto>> {
  return httpClient.post<WorkflowBindingStatusDto, WorkflowBindingStatusRequest>('/workflows/bindings/status', request);
}

export function deleteWorkflowBinding(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/bindings/${encodeURIComponent(id)}`);
}

export function startWorkflowInstance(request: WorkflowStartInstanceRequest): Promise<ApiEnvelope<WorkflowInstanceDto>> {
  return httpClient.post<WorkflowInstanceDto, WorkflowStartInstanceRequest>('/workflows/instances/start', request);
}

export function getWorkflowInstances(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowInstanceListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowInstanceListItemDto>>(`/workflows/instances${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowInstance(processInstanceId: string, signal?: AbortSignal): Promise<ApiEnvelope<WorkflowInstanceDto>> {
  return httpClient.get<WorkflowInstanceDto>(`/workflows/instances/${encodeURIComponent(processInstanceId)}`, undefined, signal);
}

export function getWorkflowInstanceDiagram(processInstanceId: string, signal?: AbortSignal): Promise<ApiEnvelope<WorkflowHighlightedDiagramDto>> {
  return httpClient.get<WorkflowHighlightedDiagramDto>(`/workflows/instances/${encodeURIComponent(processInstanceId)}/diagram`, undefined, signal);
}

export function terminateWorkflowInstance(processInstanceId: string, comment?: string | null): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, WorkflowTaskActionRequest>(`/workflows/instances/${encodeURIComponent(processInstanceId)}/terminate`, { comment });
}

export function getWorkflowDrafts(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowRequestDraftDto>>> {
  return httpClient.get<GridPageResult<WorkflowRequestDraftDto>>(`/workflows/drafts${buildQueryString(query)}`, undefined, signal);
}

export function saveWorkflowDraft(request: WorkflowRequestDraftUpsertRequest): Promise<ApiEnvelope<WorkflowRequestDraftDto>> {
  return httpClient.post<WorkflowRequestDraftDto, WorkflowRequestDraftUpsertRequest>('/workflows/drafts', request);
}

export function submitWorkflowDraft(id: string, request: WorkflowRequestDraftSubmitRequest): Promise<ApiEnvelope<WorkflowInstanceDto>> {
  return httpClient.post<WorkflowInstanceDto, WorkflowRequestDraftSubmitRequest>(`/workflows/drafts/${encodeURIComponent(id)}/submit`, request);
}

export function deleteWorkflowDraft(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/drafts/${encodeURIComponent(id)}`);
}

export function getWorkflowTodoTasks(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowTaskListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowTaskListItemDto>>(`/workflows/tasks/todo${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowTaskSummary(signal?: AbortSignal): Promise<ApiEnvelope<WorkflowTaskSummaryDto>> {
  return httpClient.get<WorkflowTaskSummaryDto>('/workflows/tasks/summary', undefined, signal);
}

export function getWorkflowTaskDetail(taskId: string, signal?: AbortSignal): Promise<ApiEnvelope<WorkflowTaskDetailDto>> {
  return httpClient.get<WorkflowTaskDetailDto>(`/workflows/tasks/${encodeURIComponent(taskId)}/detail`, undefined, signal);
}

export function getWorkflowDoneTasks(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowHistoricTaskDto>>> {
  return httpClient.get<GridPageResult<WorkflowHistoricTaskDto>>(`/workflows/tasks/done${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowMineTaskInstances(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowInstanceListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowInstanceListItemDto>>(`/workflows/tasks/mine${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowDelegatedTasks(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowTaskListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowTaskListItemDto>>(`/workflows/tasks/delegated${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowTimeoutTasks(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowTaskListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowTaskListItemDto>>(`/workflows/tasks/timeout${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowCcTaskInstances(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowInstanceListItemDto>>> {
  return httpClient.get<GridPageResult<WorkflowInstanceListItemDto>>(`/workflows/tasks/cc${buildQueryString(query)}`, undefined, signal);
}

export function claimWorkflowTask(taskId: string, request: WorkflowTaskActionRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, WorkflowTaskActionRequest>(`/workflows/tasks/${encodeURIComponent(taskId)}/claim`, request);
}

export function unclaimWorkflowTask(taskId: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, Record<string, never>>(`/workflows/tasks/${encodeURIComponent(taskId)}/unclaim`, {});
}

export function completeWorkflowTask(taskId: string, request: WorkflowTaskActionRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, WorkflowTaskActionRequest>(`/workflows/tasks/${encodeURIComponent(taskId)}/complete`, request);
}

export function rejectWorkflowTask(taskId: string, request: WorkflowTaskActionRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, WorkflowTaskActionRequest>(`/workflows/tasks/${encodeURIComponent(taskId)}/reject`, request);
}

export function transferWorkflowTask(taskId: string, request: WorkflowTaskActionRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, WorkflowTaskActionRequest>(`/workflows/tasks/${encodeURIComponent(taskId)}/transfer`, request);
}

export function delegateWorkflowTask(taskId: string, request: WorkflowTaskActionRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, WorkflowTaskActionRequest>(`/workflows/tasks/${encodeURIComponent(taskId)}/delegate`, request);
}

export function resolveWorkflowTask(taskId: string, request: WorkflowTaskActionRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, WorkflowTaskActionRequest>(`/workflows/tasks/${encodeURIComponent(taskId)}/resolve`, request);
}

export function addWorkflowComment(taskId: string, message: string, type = 'comment') {
  return httpClient.post(`/workflows/tasks/${encodeURIComponent(taskId)}/comments`, { message, type });
}

export function addWorkflowAttachment(taskId: string, request: WorkflowAttachmentRequest): Promise<ApiEnvelope<WorkflowAttachmentDto>> {
  return httpClient.post<WorkflowAttachmentDto, WorkflowAttachmentRequest>(`/workflows/tasks/${encodeURIComponent(taskId)}/attachments`, request);
}

export function downloadWorkflowAttachment(
  attachment: WorkflowAttachmentDto,
  noDownloadMessage = 'workflow.drawer.noDownload'
): Promise<{ blob: Blob; fileName: string }> {
  if (!attachment.downloadUrl) {
    return Promise.reject(new Error(noDownloadMessage));
  }

  return httpClient.downloadBlob(normalizeDownloadPath(attachment.downloadUrl), 120_000);
}

export function getWorkflowHistoryProcesses(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowHistoricProcessDto>>> {
  return httpClient.get<GridPageResult<WorkflowHistoricProcessDto>>(`/workflows/history/processes${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowHistoryTasks(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowHistoricTaskDto>>> {
  return httpClient.get<GridPageResult<WorkflowHistoricTaskDto>>(`/workflows/history/tasks${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowParticipants(query: { keyword?: string; type?: string }, signal?: AbortSignal): Promise<ApiEnvelope<WorkflowParticipantDto[]>> {
  return httpClient.get<WorkflowParticipantDto[]>(`/workflows/participants${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowNotificationChannels(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowNotificationChannelDto>>> {
  return httpClient.get<GridPageResult<WorkflowNotificationChannelDto>>(`/workflows/notifications/channels${buildQueryString(query)}`, undefined, signal);
}

export function saveWorkflowNotificationChannel(request: WorkflowNotificationChannelUpsertRequest): Promise<ApiEnvelope<WorkflowNotificationChannelDto>> {
  return httpClient.post<WorkflowNotificationChannelDto, WorkflowNotificationChannelUpsertRequest>('/workflows/notifications/channels', request);
}

export function deleteWorkflowNotificationChannel(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/notifications/channels/${encodeURIComponent(id)}`);
}

export function getWorkflowMessageTemplates(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowMessageTemplateDto>>> {
  return httpClient.get<GridPageResult<WorkflowMessageTemplateDto>>(`/workflows/notifications/templates${buildQueryString(query)}`, undefined, signal);
}

export function saveWorkflowMessageTemplate(request: WorkflowMessageTemplateUpsertRequest): Promise<ApiEnvelope<WorkflowMessageTemplateDto>> {
  return httpClient.post<WorkflowMessageTemplateDto, WorkflowMessageTemplateUpsertRequest>('/workflows/notifications/templates', request);
}

export function deleteWorkflowMessageTemplate(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/notifications/templates/${encodeURIComponent(id)}`);
}

export function getWorkflowNotificationRules(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowNodeNotificationRuleDto>>> {
  return httpClient.get<GridPageResult<WorkflowNodeNotificationRuleDto>>(`/workflows/notifications/rules${buildQueryString(query)}`, undefined, signal);
}

export function saveWorkflowNotificationRule(request: WorkflowNodeNotificationRuleUpsertRequest): Promise<ApiEnvelope<WorkflowNodeNotificationRuleDto>> {
  return httpClient.post<WorkflowNodeNotificationRuleDto, WorkflowNodeNotificationRuleUpsertRequest>('/workflows/notifications/rules', request);
}

export function deleteWorkflowNotificationRule(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/notifications/rules/${encodeURIComponent(id)}`);
}

export function getWorkflowNotificationTasks(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowNotificationTaskDto>>> {
  return httpClient.get<GridPageResult<WorkflowNotificationTaskDto>>(`/workflows/notifications/tasks${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowNotificationLogs(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowNotificationLogDto>>> {
  return httpClient.get<GridPageResult<WorkflowNotificationLogDto>>(`/workflows/notifications/logs${buildQueryString(query)}`, undefined, signal);
}

export function getWorkflowDelegations(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowDelegationRuleDto>>> {
  return httpClient.get<GridPageResult<WorkflowDelegationRuleDto>>(`/workflows/delegations${buildQueryString(query)}`, undefined, signal);
}

export function saveWorkflowDelegation(request: WorkflowDelegationRuleUpsertRequest): Promise<ApiEnvelope<WorkflowDelegationRuleDto>> {
  return httpClient.post<WorkflowDelegationRuleDto, WorkflowDelegationRuleUpsertRequest>('/workflows/delegations', request);
}

export function deleteWorkflowDelegation(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/delegations/${encodeURIComponent(id)}`);
}

export function getWorkflowCalendars(query: WorkflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<WorkflowWorkCalendarDto>>> {
  return httpClient.get<GridPageResult<WorkflowWorkCalendarDto>>(`/workflows/calendars${buildQueryString(query)}`, undefined, signal);
}

export function saveWorkflowCalendar(request: WorkflowWorkCalendarUpsertRequest): Promise<ApiEnvelope<WorkflowWorkCalendarDto>> {
  return httpClient.post<WorkflowWorkCalendarDto, WorkflowWorkCalendarUpsertRequest>('/workflows/calendars', request);
}

export function deleteWorkflowCalendar(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/workflows/calendars/${encodeURIComponent(id)}`);
}

export function getWorkflowReportOverview(signal?: AbortSignal): Promise<ApiEnvelope<WorkflowReportOverviewDto>> {
  return httpClient.get<WorkflowReportOverviewDto>('/workflows/reports/overview', undefined, signal);
}

export function previewWorkflowNotification(request: WorkflowNotificationPreviewRequest): Promise<ApiEnvelope<WorkflowNotificationPreviewDto>> {
  return httpClient.post<WorkflowNotificationPreviewDto, WorkflowNotificationPreviewRequest>('/workflows/notifications/preview', request);
}

function normalizeDownloadPath(downloadUrl: string): string {
  if (downloadUrl.startsWith('/api/')) {
    return downloadUrl.slice(4);
  }

  return downloadUrl;
}
