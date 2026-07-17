export interface GridPageResult<T> {
  items: T[];
  total: number;
}

export interface FlowiseStudioQuery {
  category?: string;
  keyword?: string;
  pageIndex: number;
  pageSize: number;
  resourceType?: string;
  status?: string;
  workspaceId?: string;
}

export interface FlowiseWorkspaceDto {
  createdTime: string;
  description?: string | null;
  id: string;
  status: string;
  workspaceKey: string;
  workspaceName: string;
}

export interface FlowiseWorkspaceUpsertRequest {
  description?: string | null;
  status?: string | null;
  workspaceKey: string;
  workspaceName: string;
}

export interface FlowiseSharedWorkspaceDto {
  shared: boolean;
  workspaceId: string;
  workspaceName: string;
}

export interface FlowiseShareWorkspacesRequest {
  itemType: string;
  workspaceIds: string[];
}

export interface FlowiseOverviewDto {
  agentflowCount: number;
  chatflowCount: number;
  documentStoreCount: number;
  evaluationCount: number;
  executionCount: number;
  latestExecution?: FlowiseExecutionDto | null;
  workspaceCount: number;
}

export interface FlowiseResourceTypeDto {
  displayName: string;
  editPermission: string;
  resourceType: string;
  routeSegment: string;
  supportsCanvas: boolean;
  supportsRun: boolean;
  supportsSecret: boolean;
  viewPermission: string;
}

export interface FlowiseResourceDto {
  category?: string | null;
  createdTime: string;
  definitionJson: string;
  description?: string | null;
  displayName: string;
  id: string;
  metadataJson: string;
  oneTimeSecret?: string | null;
  resourceKey: string;
  resourceType: string;
  secretMask?: string | null;
  status: string;
  updatedTime?: string | null;
  workspaceId?: string | null;
  workspaceName?: string | null;
}

export interface FlowiseResourceUpsertRequest {
  category?: string | null;
  definitionJson?: string | null;
  description?: string | null;
  displayName: string;
  metadataJson?: string | null;
  resourceKey: string;
  secretValue?: string | null;
  status?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseImportRequest {
  mode?: string | null;
  resourceType: string;
  resources: FlowiseResourceUpsertRequest[];
}

export interface FlowiseImportResultDto {
  createdCount: number;
  skippedCount: number;
  updatedCount: number;
}

export interface FlowiseExportDto {
  exportedAt: string;
  resources: FlowiseResourceDto[];
  resourceType: string;
}

export interface FlowiseAccountSettingsDto {
  displayName: string;
  email?: string | null;
  preferencesJson: string;
}

export interface FlowiseExecutionDto {
  completedAt?: string | null;
  createdTime: string;
  durationMs: number;
  errorCode?: string | null;
  errorMessage?: string | null;
  flowType: string;
  id: string;
  inputJson: string;
  outputJson: string;
  resourceId: string;
  resourceName: string;
  sourceDocumentsJson?: string | null;
  startedAt?: string | null;
  status: string;
  traceId: string;
}

export interface FlowiseExecutionStartRequest {
  idempotencyKey?: string | null;
  inputJson?: string | null;
  question?: string | null;
  resourceId: string;
}
