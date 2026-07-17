import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import {
  getDataStudioMonitoringErrorCode,
  sendDataStudioMonitoringEvent
} from '../application-development-center/applicationMonitoring.api';
import { buildQueryString } from '../queryString';
import type { GridPageResult } from '../shared.types';

import type {
  ApplicationDataCenterActionRequest,
  ApplicationDataCenterActionResult,
  ApplicationDataCenterModuleOverview,
  ApplicationDataCenterObjectDetail,
  ApplicationDataCenterObjectListItem,
  ApplicationDataCenterObjectListQuery,
  ApplicationDataCenterObjectUpsertRequest,
  ApplicationDataSourceProviderMigrationItem,
  ApplicationDataSourceSecretClearRequest,
  ApplicationDataSourceSecretReplaceRequest,
  ApplicationDataCenterOperationResponse,
  ApplicationDataCenterPreviewRequest,
  ApplicationDataCenterPreviewResponse,
  ApplicationDataCenterPublishRequest,
  ApplicationDataCenterReferenceSummary,
  MicroflowExecuteRequest,
  MicroflowExecuteResponse,
  MicroflowPreviewRequest,
  MicroflowPreviewResponse,
  RuntimeMicroflowContractResponse,
  MicroflowSqlScriptRunRequest,
  ApplicationDataSourceCreateTableRequest,
  ApplicationDataSourceSchemaChangePlanRequest,
  ApplicationDataSourceSchemaChangePlanResponse,
  ApplicationDataSourceAlterTableRequest,
  ApplicationDataSourceAlterTablePlanRequest,
  ApplicationDataSourceTableRowDeleteRequest,
  ApplicationDataSourceTableRowMutationResponse,
  ApplicationDataSourceTableRowsExportRequest,
  ApplicationDataSourceTableRowsQueryRequest,
  ApplicationDataSourceTableRowsResponse,
  ApplicationDataSourceTableRowUpsertRequest,
  ApplicationDataCenterTemplate,
  ApplicationDataCenterTypeOption,
  ApplicationDataCenterWorkspace,
  ApplicationMicroflowRevision,
  ApplicationMicroflowValidateRequest,
  ApplicationMicroflowPublishRequest,
  ApplicationMicroflowRestoreRevisionRequest,
  ApplicationDataSourceColumn,
  ApplicationMappingCacheTestRequest,
  ApplicationMappingCacheTestResponse,
  ApplicationDataSourceRuntimeCheck,
  ApplicationDataSourceSqlPreviewRequest,
  ApplicationDataSourceTable,
  ApplicationDataSourceTableDetail,
  ApplicationDataSourceViewItem,
  ApplicationDataSourceViewUpsertRequest,
  ApplicationDataSourceSqlitePathApproval,
  ApplicationDataSourceSqlitePathApprovalDecisionRequest,
  ApplicationDataSourceSqlitePathApprovalRequest,
  ApplicationDataSourceWorkbench,
  ApplicationConnectionDiagnostic,
  ApplicationDataSourceDraftDiagnostic,
  ApplicationMappingCacheItem,
  ApplicationMappingCacheRefreshResponse,
  ApplicationMappingCacheUpsertRequest,
  ApplicationQueryPlanDiagnosticResponse,
  ApplicationQueryPlanRequest,
  ApplicationQueryPlanResponse,
  ApplicationSystemAssignment,
  ApplicationSystemAssignmentUpdateRequest
} from './applicationDataCenter.types';

const basePath = '/application-data-center';

export type ApplicationDataCenterResourcePath =
  | 'data-sources'
  | 'connection-tests'
  | 'models'
  | 'api-services'
  | 'microflows'
  | 'entities-fields'
  | 'dictionaries-codes'
  | 'query-datasets'
  | 'integration-tasks';

export function getApplicationDataCenterModules(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDataCenterModuleOverview[]>> {
  return httpClient.get<ApplicationDataCenterModuleOverview[]>(`${basePath}/modules`, undefined, signal);
}

export function getApplicationDataCenterWorkspace(
  params?: { dataSourceId?: string | null; moduleKey?: string | null },
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataCenterWorkspace>> {
  return httpClient.get<ApplicationDataCenterWorkspace>(
    `${basePath}/workspace${buildQueryString({ dataSourceId: params?.dataSourceId ?? '', moduleKey: params?.moduleKey ?? '' })}`,
    undefined,
    signal
  );
}

export function getApplicationDataCenterTypeOptions(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDataCenterTypeOption[]>> {
  return httpClient.get<ApplicationDataCenterTypeOption[]>(`${basePath}/type-options`, undefined, signal);
}

export function getApplicationDataCenterTemplates(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDataCenterTemplate[]>> {
  return httpClient.get<ApplicationDataCenterTemplate[]>(`${basePath}/templates`, undefined, signal);
}

export function listApplicationDataCenterObjects(
  resourcePath: ApplicationDataCenterResourcePath,
  query: ApplicationDataCenterObjectListQuery,
  signal?: AbortSignal
): Promise<ApiEnvelope<GridPageResult<ApplicationDataCenterObjectListItem>>> {
  return httpClient.get<GridPageResult<ApplicationDataCenterObjectListItem>>(
    `${basePath}/${resourcePath}${buildQueryString(query)}`,
    undefined,
    signal
  );
}

export function getApplicationDataSourceMigrationInventory(
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceProviderMigrationItem[]>> {
  return httpClient.get<ApplicationDataSourceProviderMigrationItem[]>(
    `${basePath}/data-sources/migration-required`,
    undefined,
    signal
  );
}

export function getApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataCenterObjectDetail>> {
  return httpClient.get<ApplicationDataCenterObjectDetail>(`${basePath}/${resourcePath}/${encodeURIComponent(id)}`, undefined, signal);
}

export function createApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  request: ApplicationDataCenterObjectUpsertRequest
): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, ApplicationDataCenterObjectUpsertRequest>(
    `${basePath}/${resourcePath}`,
    request
  );
}

export function updateApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string,
  request: ApplicationDataCenterObjectUpsertRequest
): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.put<ApplicationDataCenterOperationResponse, ApplicationDataCenterObjectUpsertRequest>(
    `${basePath}/${resourcePath}/${encodeURIComponent(id)}`,
    request
  );
}

export function deleteApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string
): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`${basePath}/${resourcePath}/${encodeURIComponent(id)}`);
}

export function enableApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string
): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, Record<string, never>>(
    `${basePath}/${resourcePath}/${encodeURIComponent(id)}/enable`,
    {}
  );
}

export function disableApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string
): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, Record<string, never>>(
    `${basePath}/${resourcePath}/${encodeURIComponent(id)}/disable`,
    {}
  );
}

export function testApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string,
  request: ApplicationDataCenterActionRequest = {}
): Promise<ApiEnvelope<ApplicationDataCenterActionResult>> {
  const startedAt = performance.now();
  const requestPromise = httpClient.post<ApplicationDataCenterActionResult, ApplicationDataCenterActionRequest>(
    `${basePath}/${resourcePath}/${encodeURIComponent(id)}/test`,
    request
  );
  if (resourcePath === 'data-sources') {
    void requestPromise.then(() => sendDataStudioMonitoringEvent(
      'dataStudio.connection.test',
      { connectionId: id },
      'succeeded',
      performance.now() - startedAt
    )).catch((error: unknown) => {
      void sendDataStudioMonitoringEvent(
        'dataStudio.connection.test',
        { connectionId: id },
        'failed',
        performance.now() - startedAt,
        getDataStudioMonitoringErrorCode(error)
      ).catch(() => undefined);
    });
  }
  return requestPromise;
}

export function diagnoseApplicationDataSource(
  id: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationConnectionDiagnostic>> {
  return httpClient.post<ApplicationConnectionDiagnostic, Record<string, never>>(
    `${basePath}/data-sources/${encodeURIComponent(id)}/diagnose`,
    {},
    { signal }
  );
}

export function diagnoseApplicationDataSourceDraft(
  request: ApplicationDataCenterObjectUpsertRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceDraftDiagnostic>> {
  return httpClient.post<ApplicationDataSourceDraftDiagnostic, ApplicationDataCenterObjectUpsertRequest>(
    `${basePath}/data-sources/draft/diagnose`,
    request,
    { signal }
  );
}

export function previewApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string,
  request: ApplicationDataCenterPreviewRequest = {}
): Promise<ApiEnvelope<ApplicationDataCenterPreviewResponse>> {
  return httpClient.post<ApplicationDataCenterPreviewResponse, ApplicationDataCenterPreviewRequest>(
    `${basePath}/${resourcePath}/${encodeURIComponent(id)}/preview`,
    request
  );
}

export function publishApplicationDataCenterObject(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string,
  request: ApplicationDataCenterPublishRequest = {}
): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, ApplicationDataCenterPublishRequest>(
    `${basePath}/${resourcePath}/${encodeURIComponent(id)}/publish`,
    request
  );
}

export function listApplicationMicroflowRevisions(id: string, signal?: AbortSignal): Promise<ApiEnvelope<ApplicationMicroflowRevision[]>> {
  return httpClient.get<ApplicationMicroflowRevision[]>(`${basePath}/microflows/${encodeURIComponent(id)}/versions`, undefined, signal);
}

export function validateApplicationMicroflowRevision(id: string, request: ApplicationMicroflowValidateRequest): Promise<ApiEnvelope<ApplicationDataCenterActionResult>> {
  return httpClient.post<ApplicationDataCenterActionResult, ApplicationMicroflowValidateRequest>(`${basePath}/microflows/${encodeURIComponent(id)}/validate`, request);
}

export function publishApplicationMicroflowRevision(id: string, request: ApplicationMicroflowPublishRequest): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, ApplicationMicroflowPublishRequest>(`${basePath}/microflows/${encodeURIComponent(id)}/publish`, request);
}

export function restoreApplicationMicroflowRevision(id: string, request: ApplicationMicroflowRestoreRevisionRequest): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, ApplicationMicroflowRestoreRevisionRequest>(`${basePath}/microflows/${encodeURIComponent(id)}/versions/restore`, request);
}

export function diagnoseApplicationQueryPlan(
  request: ApplicationQueryPlanRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationQueryPlanDiagnosticResponse>> {
  return httpClient.post<ApplicationQueryPlanDiagnosticResponse, ApplicationQueryPlanRequest>(
    `${basePath}/query-datasets/query-plan/diagnose`,
    request,
    { signal }
  );
}

export function previewApplicationQueryPlan(
  request: ApplicationQueryPlanRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationQueryPlanResponse>> {
  return httpClient.post<ApplicationQueryPlanResponse, ApplicationQueryPlanRequest>(
    `${basePath}/query-datasets/query-plan/preview`,
    request,
    { signal }
  );
}

export function executeApplicationQueryPlan(
  request: ApplicationQueryPlanRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationQueryPlanResponse>> {
  const startedAt = performance.now();
  const requestPromise = httpClient.post<ApplicationQueryPlanResponse, ApplicationQueryPlanRequest>(
    `${basePath}/query-datasets/query-plan/execute`,
    request,
    { signal }
  );
  void requestPromise.then((response) => sendDataStudioMonitoringEvent(
    'dataStudio.query.execute',
    { connectionId: request.dataSourceId, queryId: response.data.auditId, requestHash: request.auditId ?? undefined },
    'succeeded',
    performance.now() - startedAt
  )).catch((error: unknown) => {
    void sendDataStudioMonitoringEvent(
      'dataStudio.query.execute',
      { connectionId: request.dataSourceId, queryId: request.auditId ?? request.dataSourceId },
      signal?.aborted ? 'cancelled' : 'failed',
      performance.now() - startedAt,
      getDataStudioMonitoringErrorCode(error)
    ).catch(() => undefined);
  });
  return requestPromise;
}

export function listApplicationDataSourceSqlitePathApprovals(
  dataSourceId: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceSqlitePathApproval[]>> {
  return httpClient.get<ApplicationDataSourceSqlitePathApproval[]>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/sqlite-path-approvals`,
    undefined,
    signal
  );
}

export function requestApplicationDataSourceSqlitePathApproval(
  dataSourceId: string,
  request: ApplicationDataSourceSqlitePathApprovalRequest
): Promise<ApiEnvelope<ApplicationDataSourceSqlitePathApproval>> {
  return httpClient.post<ApplicationDataSourceSqlitePathApproval, ApplicationDataSourceSqlitePathApprovalRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/sqlite-path-approvals`,
    { ...request, dataSourceId }
  );
}

export function approveApplicationDataSourceSqlitePathApproval(
  dataSourceId: string,
  approvalId: string
): Promise<ApiEnvelope<ApplicationDataSourceSqlitePathApproval>> {
  return decideApplicationDataSourceSqlitePathApproval(dataSourceId, approvalId, 'approve');
}

export function rejectApplicationDataSourceSqlitePathApproval(
  dataSourceId: string,
  approvalId: string
): Promise<ApiEnvelope<ApplicationDataSourceSqlitePathApproval>> {
  return decideApplicationDataSourceSqlitePathApproval(dataSourceId, approvalId, 'reject');
}

export function revokeApplicationDataSourceSqlitePathApproval(
  dataSourceId: string,
  approvalId: string
): Promise<ApiEnvelope<ApplicationDataSourceSqlitePathApproval>> {
  return decideApplicationDataSourceSqlitePathApproval(dataSourceId, approvalId, 'revoke');
}

function decideApplicationDataSourceSqlitePathApproval(
  dataSourceId: string,
  approvalId: string,
  action: 'approve' | 'reject' | 'revoke'
): Promise<ApiEnvelope<ApplicationDataSourceSqlitePathApproval>> {
  const request: ApplicationDataSourceSqlitePathApprovalDecisionRequest = { approvalId };
  return httpClient.post<ApplicationDataSourceSqlitePathApproval, ApplicationDataSourceSqlitePathApprovalDecisionRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/sqlite-path-approvals/${encodeURIComponent(approvalId)}/${action}`,
    request
  );
}

export function getApplicationDataCenterReferences(
  resourcePath: ApplicationDataCenterResourcePath,
  id: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataCenterReferenceSummary>> {
  return httpClient.get<ApplicationDataCenterReferenceSummary>(
    `${basePath}/${resourcePath}/${encodeURIComponent(id)}/references`,
    undefined,
    signal
  );
}

export function executeRuntimeMicroflow(
  flowCode: string,
  request: MicroflowExecuteRequest,
  options?: { signal?: AbortSignal; timeoutMs?: number }
): Promise<ApiEnvelope<MicroflowExecuteResponse>> {
  return httpClient.post<MicroflowExecuteResponse, MicroflowExecuteRequest>(
    `/runtime/microflows/${encodeURIComponent(flowCode)}/execute`,
    request,
    { signal: options?.signal, timeoutMs: request.timeoutMs ?? options?.timeoutMs }
  );
}

export function getRuntimeMicroflowContract(
  flowCode: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<RuntimeMicroflowContractResponse>> {
  return httpClient.get<RuntimeMicroflowContractResponse>(
    `/runtime/microflows/${encodeURIComponent(flowCode)}/contract`,
    undefined,
    signal
  );
}

export function getRuntimeMicroflowContracts(
  flowCodes: string[],
  signal?: AbortSignal
): Promise<ApiEnvelope<RuntimeMicroflowContractResponse[]>> {
  const normalizedCodes = flowCodes.map((code) => code.trim()).filter(Boolean);
  return httpClient.get<RuntimeMicroflowContractResponse[]>(
    `/runtime/microflows/contracts${buildQueryString({ flowCodes: normalizedCodes.join(',') })}`,
    undefined,
    signal
  );
}

export function executeApplicationDataCenterMicroflow(
  id: string,
  request: MicroflowExecuteRequest
): Promise<ApiEnvelope<MicroflowExecuteResponse>> {
  return httpClient.post<MicroflowExecuteResponse, MicroflowExecuteRequest>(
    `${basePath}/microflows/${encodeURIComponent(id)}/execute-test`,
    request
  );
}

export function previewApplicationDataCenterMicroflow(
  id: string,
  request: MicroflowPreviewRequest
): Promise<ApiEnvelope<MicroflowPreviewResponse>> {
  return httpClient.post<MicroflowPreviewResponse, MicroflowPreviewRequest>(
    `${basePath}/microflows/${encodeURIComponent(id)}/preview-run`,
    request
  );
}

export function runApplicationDataCenterMicroflowSqlScript(
  id: string,
  request: MicroflowSqlScriptRunRequest
): Promise<ApiEnvelope<ApplicationDataCenterPreviewResponse>> {
  return httpClient.post<ApplicationDataCenterPreviewResponse, MicroflowSqlScriptRunRequest>(
    `${basePath}/microflows/${encodeURIComponent(id)}/sql-script/run`,
    request
  );
}

export function getApplicationDataSourceTables(
  dataSourceId: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceTable[]>> {
  return httpClient.get<ApplicationDataSourceTable[]>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/metadata/tables`,
    undefined,
    signal
  );
}

export function getApplicationDataSourceColumns(
  dataSourceId: string,
  tableName: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceColumn[]>> {
  return httpClient.get<ApplicationDataSourceColumn[]>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/metadata/tables/${encodeURIComponent(tableName)}/columns`,
    undefined,
    signal
  );
}

export function getApplicationDataSourceWorkbench(
  dataSourceId: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceWorkbench>> {
  return httpClient.get<ApplicationDataSourceWorkbench>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/workbench`,
    undefined,
    signal
  );
}

export function getApplicationDataSourceRuntimeChecks(
  dataSourceId: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceRuntimeCheck>> {
  return httpClient.get<ApplicationDataSourceRuntimeCheck>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/runtime-checks`,
    undefined,
    signal
  );
}

export function listApplicationDataSourceWorkbenchTables(
  dataSourceId: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceTable[]>> {
  const startedAt = performance.now();
  const requestPromise = httpClient.get<ApplicationDataSourceTable[]>(`${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables`, undefined, signal);
  void requestPromise.then(() => sendDataStudioMonitoringEvent(
    'dataStudio.catalog.refresh',
    { catalogId: dataSourceId, connectionId: dataSourceId },
    'succeeded',
    performance.now() - startedAt
  )).catch((error: unknown) => {
    void sendDataStudioMonitoringEvent(
      'dataStudio.catalog.refresh',
      { catalogId: dataSourceId, connectionId: dataSourceId },
      signal?.aborted ? 'cancelled' : 'failed',
      performance.now() - startedAt,
      getDataStudioMonitoringErrorCode(error)
    ).catch(() => undefined);
  });
  return requestPromise;
}

export function getApplicationDataSourceWorkbenchTable(
  dataSourceId: string,
  tableName: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceTableDetail>> {
  return httpClient.get<ApplicationDataSourceTableDetail>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/${encodeURIComponent(tableName)}`,
    undefined,
    signal
  );
}

export function replaceApplicationDataSourceSecret(
  id: string,
  request: ApplicationDataSourceSecretReplaceRequest
): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, ApplicationDataSourceSecretReplaceRequest>(
    `${basePath}/data-sources/${encodeURIComponent(id)}/secret/replace`,
    request
  );
}

export function clearApplicationDataSourceSecret(
  id: string,
  request: ApplicationDataSourceSecretClearRequest
): Promise<ApiEnvelope<ApplicationDataCenterOperationResponse>> {
  return httpClient.post<ApplicationDataCenterOperationResponse, ApplicationDataSourceSecretClearRequest>(
    `${basePath}/data-sources/${encodeURIComponent(id)}/secret/clear`,
    request
  );
}

export function refreshApplicationDataSourceCatalogNode(
  dataSourceId: string,
  request: { schemaName?: string | null; tableName: string },
  signal?: AbortSignal
): Promise<ApiEnvelope<unknown>> {
  return httpClient.post<unknown, { schemaName?: string | null; tableName: string }>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/catalog/refresh-node`,
    request,
    { signal }
  );
}

export function createApplicationDataSourceWorkbenchTable(
  dataSourceId: string,
  request: ApplicationDataSourceCreateTableRequest
): Promise<ApiEnvelope<ApplicationDataSourceSchemaChangePlanResponse>> {
  return httpClient.post<ApplicationDataSourceSchemaChangePlanResponse, ApplicationDataSourceCreateTableRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables`,
    request
  );
}

export function deployApplicationDataSourceSchemaChangePlan(
  dataSourceId: string,
  request: ApplicationDataSourceSchemaChangePlanRequest
): Promise<ApiEnvelope<ApplicationDataSourceTableDetail>> {
  const startedAt = performance.now();
  const requestPromise = httpClient.post<ApplicationDataSourceTableDetail, ApplicationDataSourceSchemaChangePlanRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/deploy`,
    request
  );
  void requestPromise.then(() => sendDataStudioMonitoringEvent(
    'dataStudio.schema.deploy',
    { connectionId: dataSourceId, schemaName: request.table.tableName, requestHash: request.planHash },
    'succeeded',
    performance.now() - startedAt
  )).catch((error: unknown) => {
    void sendDataStudioMonitoringEvent(
      'dataStudio.schema.deploy',
      { connectionId: dataSourceId, schemaName: request.table.tableName, requestHash: request.planHash },
      'failed',
      performance.now() - startedAt,
      getDataStudioMonitoringErrorCode(error)
    ).catch(() => undefined);
  });
  return requestPromise;
}

export function previewApplicationDataSourceWorkbenchTable(
  dataSourceId: string,
  tableName: string,
  request: ApplicationDataCenterPreviewRequest
): Promise<ApiEnvelope<ApplicationDataCenterPreviewResponse>> {
  return httpClient.post<ApplicationDataCenterPreviewResponse, ApplicationDataCenterPreviewRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/${encodeURIComponent(tableName)}/preview`,
    request
  );
}

export function queryApplicationDataSourceTableRows(
  dataSourceId: string,
  tableName: string,
  request: ApplicationDataSourceTableRowsQueryRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDataSourceTableRowsResponse>> {
  return httpClient.post<ApplicationDataSourceTableRowsResponse, ApplicationDataSourceTableRowsQueryRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/${encodeURIComponent(tableName)}/rows/query`,
    request,
    undefined,
    signal
  );
}

export function createApplicationDataSourceAlterTablePlan(
  dataSourceId: string,
  request: ApplicationDataSourceAlterTableRequest
): Promise<ApiEnvelope<ApplicationDataSourceSchemaChangePlanResponse>> {
  return httpClient.post<ApplicationDataSourceSchemaChangePlanResponse, ApplicationDataSourceAlterTableRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/alter-plan`,
    request
  );
}

export function deployApplicationDataSourceAlterTablePlan(
  dataSourceId: string,
  request: ApplicationDataSourceAlterTablePlanRequest
): Promise<ApiEnvelope<ApplicationDataSourceTableDetail>> {
  return httpClient.post<ApplicationDataSourceTableDetail, ApplicationDataSourceAlterTablePlanRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/alter-deploy`,
    request
  );
}

export function streamApplicationDataSourceTableRowsExport(
  dataSourceId: string,
  tableName: string,
  request: ApplicationDataSourceTableRowsExportRequest,
  signal?: AbortSignal
): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.postDownloadBlob(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/${encodeURIComponent(tableName)}/rows/export/stream`,
    request,
    { signal, timeoutMs: 120_000 }
  );
}

export function insertApplicationDataSourceTableRow(
  dataSourceId: string,
  tableName: string,
  request: ApplicationDataSourceTableRowUpsertRequest
): Promise<ApiEnvelope<ApplicationDataSourceTableRowMutationResponse>> {
  const startedAt = performance.now();
  const requestPromise = httpClient.post<ApplicationDataSourceTableRowMutationResponse, ApplicationDataSourceTableRowUpsertRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/${encodeURIComponent(tableName)}/rows`,
    request
  );
  void requestPromise.then(() => sendDataStudioMonitoringEvent(
    'dataStudio.data.write',
    { connectionId: dataSourceId, affectedRows: 1, resourceKind: `${tableName}.insert` },
    'succeeded',
    performance.now() - startedAt
  )).catch((error: unknown) => {
    void sendDataStudioMonitoringEvent(
      'dataStudio.data.write',
      { connectionId: dataSourceId, affectedRows: 0, resourceKind: `${tableName}.insert` },
      'failed',
      performance.now() - startedAt,
      getDataStudioMonitoringErrorCode(error)
    ).catch(() => undefined);
  });
  return requestPromise;
}

export function updateApplicationDataSourceTableRow(
  dataSourceId: string,
  tableName: string,
  request: ApplicationDataSourceTableRowUpsertRequest
): Promise<ApiEnvelope<ApplicationDataSourceTableRowMutationResponse>> {
  const startedAt = performance.now();
  const requestPromise = httpClient.put<ApplicationDataSourceTableRowMutationResponse, ApplicationDataSourceTableRowUpsertRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/${encodeURIComponent(tableName)}/rows`,
    request
  );
  void requestPromise.then(() => sendDataStudioMonitoringEvent(
    'dataStudio.data.write',
    { connectionId: dataSourceId, affectedRows: 1, resourceKind: `${tableName}.update` },
    'succeeded',
    performance.now() - startedAt
  )).catch((error: unknown) => {
    void sendDataStudioMonitoringEvent(
      'dataStudio.data.write',
      { connectionId: dataSourceId, affectedRows: 0, resourceKind: `${tableName}.update` },
      'failed',
      performance.now() - startedAt,
      getDataStudioMonitoringErrorCode(error)
    ).catch(() => undefined);
  });
  return requestPromise;
}

export function deleteApplicationDataSourceTableRow(
  dataSourceId: string,
  tableName: string,
  request: ApplicationDataSourceTableRowDeleteRequest
): Promise<ApiEnvelope<ApplicationDataSourceTableRowMutationResponse>> {
  const startedAt = performance.now();
  const requestPromise = httpClient.request<ApplicationDataSourceTableRowMutationResponse, ApplicationDataSourceTableRowDeleteRequest>({
    body: request,
    method: 'DELETE',
    path: `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/tables/${encodeURIComponent(tableName)}/rows`
  });
  void requestPromise.then(() => sendDataStudioMonitoringEvent(
    'dataStudio.data.write',
    { connectionId: dataSourceId, affectedRows: 1, resourceKind: `${tableName}.delete` },
    'succeeded',
    performance.now() - startedAt
  )).catch((error: unknown) => {
    void sendDataStudioMonitoringEvent(
      'dataStudio.data.write',
      { connectionId: dataSourceId, affectedRows: 0, resourceKind: `${tableName}.delete` },
      'failed',
      performance.now() - startedAt,
      getDataStudioMonitoringErrorCode(error)
    ).catch(() => undefined);
  });
  return requestPromise;
}

export function listApplicationDataSourceViews(dataSourceId: string, signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDataSourceViewItem[]>> {
  return httpClient.get<ApplicationDataSourceViewItem[]>(`${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/views`, undefined, signal);
}

export function createApplicationDataSourceView(
  dataSourceId: string,
  request: ApplicationDataSourceViewUpsertRequest
): Promise<ApiEnvelope<ApplicationDataSourceViewItem>> {
  return httpClient.post<ApplicationDataSourceViewItem, ApplicationDataSourceViewUpsertRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/views`,
    request
  );
}

export function updateApplicationDataSourceView(
  dataSourceId: string,
  viewId: string,
  request: ApplicationDataSourceViewUpsertRequest
): Promise<ApiEnvelope<ApplicationDataSourceViewItem>> {
  return httpClient.put<ApplicationDataSourceViewItem, ApplicationDataSourceViewUpsertRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/views/${encodeURIComponent(viewId)}`,
    request
  );
}

export function deleteApplicationDataSourceView(dataSourceId: string, viewId: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/views/${encodeURIComponent(viewId)}`);
}

export function previewApplicationDataSourceSql(
  dataSourceId: string,
  request: ApplicationDataSourceSqlPreviewRequest
): Promise<ApiEnvelope<ApplicationDataCenterPreviewResponse>> {
  return httpClient.post<ApplicationDataCenterPreviewResponse, ApplicationDataSourceSqlPreviewRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/views/preview-sql`,
    request
  );
}

export function listApplicationMappingCaches(dataSourceId: string, signal?: AbortSignal): Promise<ApiEnvelope<ApplicationMappingCacheItem[]>> {
  return httpClient.get<ApplicationMappingCacheItem[]>(`${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/mapping-caches`, undefined, signal);
}

export function createApplicationMappingCache(
  dataSourceId: string,
  request: ApplicationMappingCacheUpsertRequest
): Promise<ApiEnvelope<ApplicationMappingCacheItem>> {
  return httpClient.post<ApplicationMappingCacheItem, ApplicationMappingCacheUpsertRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/mapping-caches`,
    request
  );
}

export function updateApplicationMappingCache(
  dataSourceId: string,
  cacheId: string,
  request: ApplicationMappingCacheUpsertRequest
): Promise<ApiEnvelope<ApplicationMappingCacheItem>> {
  return httpClient.put<ApplicationMappingCacheItem, ApplicationMappingCacheUpsertRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/mapping-caches/${encodeURIComponent(cacheId)}`,
    request
  );
}

export function deleteApplicationMappingCache(dataSourceId: string, cacheId: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/mapping-caches/${encodeURIComponent(cacheId)}`);
}

export function testApplicationMappingCache(
  dataSourceId: string,
  cacheId: string,
  request: ApplicationMappingCacheTestRequest
): Promise<ApiEnvelope<ApplicationMappingCacheTestResponse>> {
  return httpClient.post<ApplicationMappingCacheTestResponse, ApplicationMappingCacheTestRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/mapping-caches/${encodeURIComponent(cacheId)}/test`,
    request
  );
}

export function refreshApplicationMappingCache(dataSourceId: string, cacheId: string, request: ApplicationMappingCacheTestRequest = {}): Promise<ApiEnvelope<ApplicationMappingCacheRefreshResponse>> {
  return httpClient.post<ApplicationMappingCacheRefreshResponse, ApplicationMappingCacheTestRequest>(
    `${basePath}/data-sources/${encodeURIComponent(dataSourceId)}/mapping-caches/${encodeURIComponent(cacheId)}/refresh`,
    request
  );
}

export function listApplicationSystemAssignments(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationSystemAssignment[]>> {
  return httpClient.get<ApplicationSystemAssignment[]>(`${basePath}/application-assignments`, undefined, signal);
}

export function updateApplicationSystemAssignment(
  request: ApplicationSystemAssignmentUpdateRequest
): Promise<ApiEnvelope<ApplicationSystemAssignment>> {
  return httpClient.put<ApplicationSystemAssignment, ApplicationSystemAssignmentUpdateRequest>(`${basePath}/application-assignments`, request);
}
