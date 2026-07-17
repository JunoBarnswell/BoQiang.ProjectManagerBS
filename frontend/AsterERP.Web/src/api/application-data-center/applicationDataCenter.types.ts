import type { FilterQueryRule, SortQueryRule } from '../queryString';

export type { ApplicationDataSourceSslMode } from './applicationDataSourceSslMode';
import type { ApplicationDataSourceSslMode } from './applicationDataSourceSslMode';

export type ApplicationDataCenterModuleKey =
  | 'data-source'
  | 'connection-test'
  | 'data-model'
  | 'api-service'
  | 'microflow'
  | 'entity-field'
  | 'dictionary-code'
  | 'query-dataset'
  | 'integration-task';

export type ApplicationDataCenterObjectStatus = 'Draft' | 'Enabled' | 'Disabled' | 'Published' | 'Archived' | string;

export interface ApplicationDataCenterObjectListQuery {
  filters?: FilterQueryRule[];
  keyword?: string;
  objectType?: string;
  ownerUserId?: string;
  pageIndex?: number;
  pageSize?: number;
  sorts?: SortQueryRule[];
  status?: string;
}

export interface ApplicationDataCenterObjectListItem {
  createdTime: string;
  endpoint?: string | null;
  environment?: string | null;
  id: string;
  lastValidatedAt?: string | null;
  lastValidationMessage?: string | null;
  lastValidationStatus?: string | null;
  moduleKey: ApplicationDataCenterModuleKey | string;
  objectCode: string;
  objectName: string;
  objectType: string;
  ownerName?: string | null;
  ownerUserId?: string | null;
  referenceCount: number;
  remark?: string | null;
  status: ApplicationDataCenterObjectStatus;
  updatedTime?: string | null;
  versionNo: number;
}

export interface ApplicationDataSourceProviderMigrationItem {
  id: string;
  objectCode: string;
  objectName: string;
  retiredProvider: string;
  status: 'MigrationRequired';
  createdTime: string;
  updatedTime?: string | null;
  diagnostic: string;
}

export interface ApplicationDataCenterReferenceItem {
  createdTime: string;
  id: string;
  ownerUserId?: string | null;
  referenceKind: string;
  sourceModule: string;
  sourceObjectCode: string;
  sourceObjectId: string;
  sourceObjectName: string;
  status: string;
  targetModule: string;
  targetObjectId: string;
}

export interface ApplicationDataCenterReferenceSummary {
  integrationTaskCount: number;
  items: ApplicationDataCenterReferenceItem[];
  microflowCount?: number;
  objectId: string;
  objectType: string;
  pageCount: number;
  queryDatasetCount: number;
  total: number;
}

export interface ApplicationDataCenterNextAction {
  actionKey: string;
  description: string;
  permissionCode?: string | null;
  routePath?: string | null;
  title: string;
}

export interface ApplicationDataCenterObjectDetail extends ApplicationDataCenterObjectListItem {
  configJson: string;
  nextActions: ApplicationDataCenterNextAction[];
  publicConfigJson?: string | null;
  referenceSummary: ApplicationDataCenterReferenceSummary;
  secretRef?: string | null;
}

export interface ApplicationDataSourceSqlitePathApproval {
  approvedAt?: string | null;
  approvedBy?: string | null;
  dataSourceId: string;
  expiresAt: string;
  id: string;
  path: string;
  reason: string;
  requestedAt: string;
  requestedBy: string;
  revokedAt?: string | null;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Revoked' | string;
}

export interface ApplicationDataSourceSqlitePathApprovalRequest {
  dataSourceId: string;
  expiresAt: string;
  path: string;
  reason: string;
}

export interface ApplicationDataSourceSqlitePathApprovalDecisionRequest {
  approvalId: string;
}

export interface ApplicationDataCenterObjectUpsertRequest {
  configJson: string;
  confirmedRiskFields?: string[];
  endpoint?: string | null;
  environment?: string | null;
  objectCode: string;
  objectName: string;
  objectType: string;
  ownerUserId?: string | null;
  remark?: string | null;
  secretConfigJson?: string | null;
  diagnosticFingerprint?: string | null;
}

export interface ApplicationDataSourceConnectionConfig {
  charset?: string;
  poolSize?: number;
  sslMode?: ApplicationDataSourceSslMode;
  timeoutSeconds?: number;
}

export interface ApplicationDataSourceConnectionCapability {
  defaultCharset?: string | null;
  defaultPoolSize: number;
  defaultSslMode?: ApplicationDataSourceSslMode | null;
  defaultTimeoutSeconds: number;
  provider: string;
  supportsCharset: boolean;
  supportsConnectionTimeout: boolean;
  supportsPoolSize: boolean;
  supportedSslModes: ApplicationDataSourceSslMode[];
}

export type MicroflowNodeType =
  | 'start'
  | 'end'
  | 'decision'
  | 'loop'
  | 'query'
  | 'retrieve'
  | 'detail'
  | 'compositeDetail'
  | 'create'
  | 'compositeCreate'
  | 'compositeUpdate'
  | 'change'
  | 'delete'
  | 'compositeDelete'
  | 'callApi'
  | 'setVariable'
  | 'globalVariables'
  | 'return';

export interface MicroflowExpressionHelper {
  args: Record<string, unknown>;
  name: string;
}

export type MicroflowValueDataType = 'string' | 'number' | 'boolean' | 'date' | 'datetime' | 'object' | 'array' | 'json' | string;

export type MicroflowValueExpressionKind = 'literal' | 'ref' | 'function' | 'template' | 'object' | 'array';

export interface MicroflowVariableRef {
  dataType: MicroflowValueDataType;
  fieldPath: string[];
  label: string;
  outputKey?: string | null;
  sourceNodeId?: string | null;
  sourceType: 'trigger' | 'nodeOutput' | 'nodeInput' | 'global' | 'context' | 'currentUser' | 'runtime' | 'loopItem' | string;
  variableId: string;
}

export interface MicroflowValueExpression {
  args?: MicroflowValueExpression[];
  dataType: MicroflowValueDataType;
  functionId?: string | null;
  items?: MicroflowValueExpression[];
  kind: MicroflowValueExpressionKind | string;
  properties?: Record<string, MicroflowValueExpression>;
  ref?: MicroflowVariableRef | null;
  value?: unknown;
}

export interface MicroflowDomainField {
  dataType: string;
  displayHelpers?: MicroflowExpressionHelper[];
  fieldCode: string;
  fieldName: string;
  queryHelpers?: MicroflowExpressionHelper[];
  readOnly?: boolean;
  required?: boolean;
  expression?: MicroflowValueExpression | null;
  visible: boolean;
  writable: boolean;
  writeHelpers?: MicroflowExpressionHelper[];
}

export interface MicroflowSqlScriptParameter {
  dataType: MicroflowValueDataType;
  expression?: MicroflowValueExpression | null;
  name: string;
}

export interface MicroflowSqlScriptLocalVariable {
  dataType: MicroflowValueDataType;
  initializer?: MicroflowValueExpression | null;
  name: string;
}

export interface MicroflowSqlScriptResultShape {
  fields: MicroflowDomainField[];
  valueType: 'object' | 'array' | string;
}

export interface MicroflowSqlScript {
  dataSourceId: string;
  localVariables: MicroflowSqlScriptLocalVariable[];
  maxRows?: number;
  parameters: MicroflowSqlScriptParameter[];
  resultShape: MicroflowSqlScriptResultShape;
  script: string;
}

export interface MicroflowDomainObject {
  fields: MicroflowDomainField[];
  idGeneration: 'guid' | 'manual' | 'snowflake';
  keyField: string;
  modelCode: string;
  objectCode: string;
  objectName: string;
}

export interface MicroflowAssociation {
  associationCode: string;
  cardinality: 'oneToMany' | 'manyToMany' | 'oneToOne' | string;
  cascadeDelete?: boolean;
  deleteMode?: string;
  required?: boolean;
  saveMode?: string;
  sourceKeyField?: string;
  sourceObjectCode: string;
  targetForeignKeyField?: string;
  targetObjectCode: string;
}

export interface MicroflowVariable {
  defaultValue?: unknown;
  fields?: MicroflowDomainField[];
  schemaObjectCode?: string | null;
  sourceNodeId?: string | null;
  valueType: string;
  variableCode: string;
  variableName: string;
}

export interface MicroflowNode {
  config: Record<string, unknown>;
  id: string;
  name: string;
  type: MicroflowNodeType | string;
  x: number;
  y: number;
}

export interface MicroflowEdge {
  condition?: string | null;
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
}

export interface MicroflowApiEndpoint {
  endpointCode: string;
  endpointName: string;
  httpMethod: string;
  permissionCode?: string | null;
  requiresAuthentication: boolean;
  routePath: string;
  startNodeId?: string | null;
}

export interface MicroflowDefinition {
  apiEndpoints: MicroflowApiEndpoint[];
  associations: MicroflowAssociation[];
  dataMappings: Array<Record<string, unknown>>;
  domainObjects: MicroflowDomainObject[];
  edges: MicroflowEdge[];
  inputs: MicroflowVariable[];
  nodes: MicroflowNode[];
  outputs: MicroflowVariable[];
  permissions: Record<string, unknown>;
  schemaVersion: number;
  testCases: Array<Record<string, unknown>>;
  variables: MicroflowVariable[];
}

export interface MicroflowExecuteRequest {
  action?: string | null;
  bindingId?: string | null;
  correlationId?: string | null;
  modelCode?: string | null;
  pageCode?: string | null;
  previewPageId?: string | null;
  startNodeId?: string | null;
  timeoutMs?: number | null;
  variables?: Record<string, unknown> | null;
}

export interface MicroflowExecuteResponse {
  flowCode: string;
  result?: unknown;
  trace: string[];
  variables: Record<string, unknown>;
}

export interface RuntimeMicroflowContractResponse {
  flowCode: string;
  flowName: string;
  inputs: MicroflowVariable[];
  outputs: MicroflowVariable[];
  versionNo: number;
}

export type MicroflowPreviewMode = 'draft' | 'published';

export interface MicroflowPreviewRequest {
  draftConfigJson?: string | null;
  executeRequest?: MicroflowExecuteRequest | null;
  maxRows?: number | null;
  mode?: MicroflowPreviewMode | null;
  preferredResultPath?: string | null;
}

export interface MicroflowSqlScriptRunRequest {
  definition: MicroflowDefinition;
  executeRequest?: MicroflowExecuteRequest | null;
  nodeId: string;
  pageIndex?: number | null;
  pageSize?: number | null;
  sqlScript: MicroflowSqlScript;
  valueType?: string | null;
}

export interface MicroflowPreviewDataset {
  fields: ApplicationDataCenterPreviewField[];
  key: string;
  rows: Array<Record<string, unknown>>;
  sourcePath: string;
  title: string;
  totalRows: number;
  truncated: boolean;
}

export interface MicroflowPreviewTraceItem {
  nodeId: string;
  nodeName: string;
  nodeType: string;
  order: number;
}

export interface MicroflowPreviewVariableSummary {
  datasetKey?: string | null;
  displayValue: string;
  name: string;
  valueType: string;
}

export interface MicroflowPreviewResponse {
  datasets: MicroflowPreviewDataset[];
  flowCode: string;
  message: string;
  mode: MicroflowPreviewMode | string;
  primaryDatasetKey?: string | null;
  rawResult?: unknown;
  trace: MicroflowPreviewTraceItem[];
  variables: MicroflowPreviewVariableSummary[];
}

export interface ApplicationDataCenterOperationResponse {
  nextActions: ApplicationDataCenterNextAction[];
  object: ApplicationDataCenterObjectDetail;
  referenceSummary: ApplicationDataCenterReferenceSummary;
}

export interface ApplicationDataCenterActionRequest {
  confirmedRiskFields?: string[];
  parametersJson?: string | null;
}

export interface ApplicationDataCenterActionResult {
  detailJson?: string | null;
  durationMs: number;
  message: string;
  nextActions: ApplicationDataCenterNextAction[];
  status: string;
  success: boolean;
}

export interface ApplicationMicroflowRevision {
  id: string;
  revisionNo: number;
  status: 'Draft' | 'Published' | string;
  configJson: string;
  validationStatus?: 'Passed' | 'Failed' | string | null;
  validationMessage?: string | null;
  validatedAt?: string | null;
  createdTime: string;
  publishedAt?: string | null;
  isCurrent: boolean;
}

export interface ApplicationMicroflowValidateRequest { revisionId: string; }
export interface ApplicationMicroflowPublishRequest { revisionId: string; confirmedRiskFields?: string[]; }
export interface ApplicationMicroflowRestoreRevisionRequest { revisionId: string; }

export interface ApplicationDataCenterPreviewField {
  children?: ApplicationDataCenterPreviewField[] | null;
  dataType: string;
  datasetKey?: string | null;
  fieldCode: string;
  fieldName: string;
  nullable: boolean;
  order: number;
  primaryKey: boolean;
  sourcePath?: string | null;
  valueKind?: 'scalar' | 'object' | 'array' | 'arrayObject' | string;
}

export interface ApplicationDataCenterPreviewDataset {
  fields: ApplicationDataCenterPreviewField[];
  key: string;
  message?: string | null;
  rows: Array<Record<string, unknown>>;
  sourcePath?: string | null;
  title?: string | null;
  totalRows?: number;
  truncated?: boolean;
}

export interface ApplicationDataCenterPreviewPage {
  hasNext: boolean;
  hasPrevious: boolean;
  pageIndex: number;
  pageSize: number;
  totalRows: number;
}

export interface ApplicationDataCenterSqlScriptAuditSummary {
  affectedRows?: number | null;
  durationMs: number;
  errorMessage?: string | null;
  id?: string | null;
  returnedRows: number;
  statementSummary?: string | null;
  status: string;
  traceId: string;
}

export interface ApplicationDataCenterPreviewResponse {
  audit?: ApplicationDataCenterSqlScriptAuditSummary | null;
  datasets?: ApplicationDataCenterPreviewDataset[] | null;
  fields: ApplicationDataCenterPreviewField[];
  message?: string | null;
  page?: ApplicationDataCenterPreviewPage | null;
  rows: Array<Record<string, unknown>>;
}

export interface ApplicationDataSourceTable {
  resourceId: string;
  schemaName?: string | null;
  tableName: string;
  tableType: string;
}

export interface ApplicationDataSourceColumn {
  columnName: string;
  dataType: string;
  nullable: boolean;
  order: number;
  primaryKey: boolean;
  resourceId: string;
}

export interface ApplicationDataCenterPreviewRequest {
  maxRows?: number;
  parametersJson?: string | null;
}

export interface ApplicationDataCenterPublishRequest {
  confirmedRiskFields?: string[];
  remark?: string | null;
}

export interface ApplicationDataCenterTypeOption {
  description: string;
  moduleKey: ApplicationDataCenterModuleKey | string;
  objectType: string;
  requiredFields: string[];
  testActions: string[];
  title: string;
  type: string;
}

export interface ApplicationDataCenterTemplate {
  configJson: string;
  description: string;
  moduleKey: ApplicationDataCenterModuleKey | string;
  objectType: string;
  templateCode: string;
  templateName: string;
}

export interface ApplicationDataCenterModuleOverview {
  activeCount: number;
  moduleKey: ApplicationDataCenterModuleKey | string;
  pendingActionCount: number;
  publishedCount: number;
  totalCount: number;
}

export interface ApplicationDataCenterWorkspace {
  moduleKey?: string | null;
  selectedDataSourceId?: string | null;
  selectedDataSource?: ApplicationDataCenterObjectDetail | null;
  modules: ApplicationDataCenterModuleOverview[];
  typeOptions: ApplicationDataCenterTypeOption[];
  templates: ApplicationDataCenterTemplate[];
  dataSources: ApplicationDataCenterObjectListItem[];
  recentItems: ApplicationDataCenterObjectListItem[];
}

export interface ApplicationDataSourceWorkbenchStats {
  connectionRunCount: number;
  integrationTaskCount: number;
  mappingCacheCount: number;
  microflowCount: number;
  tableCount: number;
  viewCount: number;
}

export interface ApplicationDataSourceWorkbench {
  dataSource: ApplicationDataCenterObjectDetail;
  endpoint?: string | null;
  isDatabase: boolean;
  lastValidatedAt?: string | null;
  lastValidationMessage?: string | null;
  lastValidationStatus?: string | null;
  stats: ApplicationDataSourceWorkbenchStats;
}

export interface ApplicationDataSourceCreateTableColumnRequest {
  columnName: string;
  dataType: string;
  defaultValue?: string | null;
  nullable: boolean;
  primaryKey: boolean;
  remark?: string | null;
}

export interface ApplicationDataSourceCreateTableRequest {
  alias?: string | null;
  columns: ApplicationDataSourceCreateTableColumnRequest[];
  remark?: string | null;
  schemaName?: string | null;
  tableName: string;
}

export interface ApplicationQueryPlanColumn {
  fieldResourceId: string;
  alias?: string | null;
  nodeId?: string | null;
  aggregate?: string | null;
  function?: string | null;
}

export interface ApplicationQueryPlanNode {
  id: string;
  resourceId: string;
  alias: string;
  kind?: 'table' | 'view' | string;
}

export interface ApplicationQueryPlanJoin {
  type: 'inner' | 'left' | 'right' | 'full' | string;
  leftNodeId: string;
  leftFieldResourceId: string;
  rightNodeId: string;
  rightFieldResourceId: string;
}

export interface ApplicationQueryPlanGroupBy {
  nodeId: string;
  fieldResourceId: string;
}

export interface ApplicationQueryPlanFilter {
  fieldResourceId: string;
  operator: string;
  parameterResourceId: string;
  nodeId?: string | null;
}

export interface ApplicationQueryPlanSort {
  fieldResourceId: string;
  direction: string;
  nodeId?: string | null;
}

export interface ApplicationQueryPlanPage {
  index: number;
  size: number;
}

export interface ApplicationQueryPlanParameter {
  resourceId: string;
  name: string;
  type: string;
  value: unknown;
}

export interface ApplicationQueryPlanRequest {
  dataSourceId: string;
  nodes: ApplicationQueryPlanNode[];
  joins: ApplicationQueryPlanJoin[];
  columns: ApplicationQueryPlanColumn[];
  filters: ApplicationQueryPlanFilter[];
  groupBy: ApplicationQueryPlanGroupBy[];
  having: ApplicationQueryPlanFilter[];
  sorts: ApplicationQueryPlanSort[];
  page: ApplicationQueryPlanPage;
  parameters: ApplicationQueryPlanParameter[];
  accessMode: 'readOnly' | string;
  riskConfirmed: boolean;
  riskConfirmationId?: string | null;
  auditId?: string | null;
  timeoutSeconds: number;
  rowLimit: number;
}

export interface ApplicationQueryPlanDiagnosticResponse {
  isValid: boolean;
  provider?: string | null;
  sql?: string | null;
  errors: string[];
  warnings: string[];
  auditId?: string | null;
}

export interface ApplicationQueryPlanResponse {
  data: ApplicationDataCenterPreviewResponse;
  plan: ApplicationQueryPlanDiagnosticResponse;
  total: number;
  auditId: string;
}

export interface ApplicationDataSourceSchemaChangePlanRequest {
  confirmed: boolean;
  planHash: string;
  table: ApplicationDataSourceCreateTableRequest;
}

export interface ApplicationDataSourceSchemaChangePlanResponse {
  createdAt: string;
  dataSourceId: string;
  dependencies: string[];
  estimatedAffectedRows?: number | null;
  operation: string;
  planHash: string;
  planId: string;
  provider: string;
  requiresConfirmation: boolean;
  requiresLock: boolean;
  reversible: boolean;
  riskLevel: string;
  risks: string[];
  sqlPreview: string;
  target: string;
}

export interface ApplicationDataSourceTableDetail {
  columns: ApplicationDataSourceColumn[];
  table: ApplicationDataSourceTable;
}

export interface ApplicationDataSourceTableRowFilterRequest {
  fieldCode: string;
  operator: 'contains' | 'equals' | 'notEquals' | 'gt' | 'gte' | 'lt' | 'lte' | string;
  value?: unknown;
}

export interface ApplicationDataSourceTableRowSortRequest {
  direction: 'asc' | 'desc' | string;
  fieldCode: string;
}

export interface ApplicationDataSourceTableRowsQueryRequest {
  filters?: ApplicationDataSourceTableRowFilterRequest[];
  keyword?: string | null;
  pageIndex?: number;
  pageSize?: number;
  sorts?: ApplicationDataSourceTableRowSortRequest[];
}

export interface ApplicationDataSourceTableRowsResponse {
  canInsert: boolean;
  editDisabledReason?: string | null;
  editable: boolean;
  fields: ApplicationDataCenterPreviewField[];
  insertDisabledReason?: string | null;
  pageIndex: number;
  pageSize: number;
  primaryKeys: string[];
  rows: Array<Record<string, unknown>>;
  total: number;
  concurrencyStrategy: 'version' | 'originalValues' | 'none' | string;
  concurrencyColumn?: string | null;
  concurrencyDisabledReason?: string | null;
}

export interface ApplicationDataSourceTableRowUpsertRequest {
  keyValues?: Record<string, unknown>;
  originalValues?: Record<string, unknown>;
  versionValue?: unknown;
  conflictResolution?: 'retry' | 'overwrite' | string;
  auditId?: string | null;
  requestHash?: string;
  expectedAffectedRows?: number;
  confirmed: boolean;
  values: Record<string, unknown>;
}

export interface ApplicationDataSourceTableRowDeleteRequest {
  keyValues: Record<string, unknown>;
  originalValues?: Record<string, unknown>;
  versionValue?: unknown;
  conflictResolution?: 'retry' | 'overwrite' | string;
  auditId?: string | null;
  requestHash?: string;
  expectedAffectedRows?: number;
  confirmed: boolean;
}

export interface ApplicationDataSourceTableRowMutationResponse {
  affectedRows: number;
  auditId?: string | null;
  canOverwrite?: boolean;
  canRetry?: boolean;
  conflict: boolean;
  conflictMessage?: string | null;
  executionStatus?: string | null;
  localValues?: Record<string, unknown>;
  ledgerId?: string | null;
  recoveryRequired: boolean;
  requestHash?: string | null;
  serverValues?: Record<string, unknown>;
  succeeded: boolean;
}

export interface ApplicationDataSourceSecretReplaceRequest {
  secretConfigJson: string;
  reason: string;
}

export interface ApplicationDataSourceSecretClearRequest {
  reason: string;
}

export interface ApplicationDataSourceAlterTableRequest {
  tableName: string;
  schemaName?: string | null;
  columns: ApplicationDataSourceCreateTableColumnRequest[];
}

export interface ApplicationDataSourceAlterTablePlanRequest {
  confirmed: boolean;
  planHash: string;
  table: ApplicationDataSourceAlterTableRequest;
}

export interface ApplicationDataSourceTableRowsExportRequest {
  filters?: ApplicationDataSourceTableRowFilterRequest[];
  keyword?: string | null;
  maxRows?: number;
  sorts?: ApplicationDataSourceTableRowSortRequest[];
}

export interface ApplicationDataSourceSqlPreviewRequest {
  maxRows?: number;
  sql: string;
}

export interface ApplicationDataSourceViewItem {
  alias: string;
  createdTime: string;
  id: string;
  lastValidatedAt?: string | null;
  lastValidationMessage?: string | null;
  lastValidationStatus?: string | null;
  objectCode: string;
  remark?: string | null;
  schemaName?: string | null;
  sql: string;
  status: string;
  updatedTime?: string | null;
  viewName: string;
}

export interface ApplicationDataSourceViewUpsertRequest {
  alias: string;
  remark?: string | null;
  schemaName?: string | null;
  sql: string;
  viewName: string;
}

export interface ApplicationMappingCacheTestRequest {
  maxRows?: number;
  parameters?: Record<string, unknown> | null;
}

export interface ApplicationMappingCacheTestResponse {
  durationMs: number;
  fields: ApplicationDataCenterPreviewField[];
  message: string;
  rows: Array<Record<string, unknown>>;
  success: boolean;
}

export interface ApplicationMappingCacheItem {
  cacheKey: string;
  cacheName: string;
  createdTime: string;
  id: string;
  lastRefreshedAt?: string | null;
  lastRowCount?: number | null;
  lastValidationMessage?: string | null;
  lastValidationStatus?: string | null;
  remark?: string | null;
  source: ApplicationMappingCacheSource;
  columns: ApplicationMappingCacheColumn[];
  parameters: ApplicationMappingCacheParameter[];
  capability: ApplicationMappingCacheProviderCapability;
  status: string;
  updatedTime?: string | null;
}

export interface ApplicationMappingCacheUpsertRequest {
  cacheKey: string;
  cacheName: string;
  source: ApplicationMappingCacheSource;
  columns: ApplicationMappingCacheColumn[];
  parameters: ApplicationMappingCacheParameter[];
  remark?: string | null;
}

export interface ApplicationMappingCacheSource {
  dataSourceId: string;
  resourceId: string;
  schemaName?: string | null;
  provider: string;
}
export interface ApplicationMappingCacheColumn { sourceResourceId: string; targetName: string; dataType: string; nullable: boolean; ordinal: number; }
export interface ApplicationMappingCacheParameter { resourceId: string; name: string; columnResourceId: string; dataType: string; required: boolean; defaultValue?: unknown; }
export interface ApplicationMappingCacheProviderCapability {
  provider: string;
  supportsStructuredMappingCache: boolean;
  supportsParameters: boolean;
  maxColumns: number;
  maxParameters: number;
  maxRows: number;
  supportLevel?: 'Unsupported' | 'Partial' | 'Supported' | string;
  supportReason?: string | null;
}

export interface ApplicationMappingCacheRefreshResponse {
  message: string;
  refreshedAt: string;
  rowCount: number;
  rows: Array<Record<string, unknown>>;
  success: boolean;
}

export interface ApplicationConnectionCheckRun {
  durationMs: number;
  errorMessage?: string | null;
  finishedAt?: string | null;
  id: string;
  result: string;
  resultJson?: string | null;
  startedAt: string;
  templateCode: string;
}

export interface ApplicationConnectionDiagnosticStage {
  code: string;
  detailJson?: string | null;
  durationMs: number;
  message: string;
  repairSuggestion?: string | null;
  status: 'Passed' | 'Failed' | 'Blocked' | 'NotApplicable' | string;
}

export interface ApplicationConnectionDiagnostic {
  stages: ApplicationConnectionDiagnosticStage[];
  success: boolean;
  taskId: string;
  connectionFingerprint?: string | null;
}

export interface ApplicationDataSourceDraftDiagnostic {
  stages: ApplicationConnectionDiagnosticStage[];
  success: boolean;
  connectionFingerprint?: string | null;
}

export interface ApplicationDataSourceRuntimeCheck {
  connectionRuns: ApplicationConnectionCheckRun[];
  stats: ApplicationDataSourceWorkbenchStats;
}

export interface ApplicationSystemAssignment {
  appCode: string;
  appName: string;
  authorizedObjectIds: string[];
  configJson?: string | null;
  noPermissionDisplay: string;
  runningVersion?: string | null;
  status: string;
  tenantAppId: string;
}

export interface ApplicationSystemAssignmentUpdateRequest {
  appCode: string;
  authorizedObjectIds: string[];
  noPermissionDisplay: string;
  runningVersion?: string | null;
}
