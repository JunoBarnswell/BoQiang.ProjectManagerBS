export const RUNTIME_MONITORING_EVENT_NAMES = [
  'designer.command',
  'designer.command.failed',
  'designer.save',
  'designer.publish',
  'designer.migration',
  'runtime.render',
  'runtime.action',
  'runtime.binding.error',
  'dataStudio.connection.test',
  'dataStudio.catalog.refresh',
  'dataStudio.query.execute',
  'dataStudio.schema.deploy',
  'dataStudio.data.write'
] as const;

export type RuntimeMonitoringEventName = (typeof RUNTIME_MONITORING_EVENT_NAMES)[number];
export type RuntimeMonitoringOutcome = 'succeeded' | 'failed' | 'cancelled' | 'timedOut';

export interface RuntimeMonitoringContext {
  appCode?: string;
  artifactHash?: string;
  documentId?: string;
  pageCode?: string;
  revision?: number;
  tenantId?: string;
  traceId?: string;
  userId?: string;
}

export interface RuntimeMonitoringEventContext extends RuntimeMonitoringContext {
  affectedRows?: number;
  auditId?: string;
  backupSha?: string;
  bindingPath?: string;
  catalogId?: string;
  commandId?: string;
  commandType?: string;
  connectionId?: string;
  migrationId?: string;
  queryId?: string;
  requestHash?: string;
  resourceKind?: string;
  schemaName?: string;
  actionId?: string;
  actionType?: string;
}

export interface DesignerCommandContext extends RuntimeMonitoringEventContext {
  commandId: string;
  commandType: string;
}

export interface DesignerSaveContext extends RuntimeMonitoringEventContext {
  documentId: string;
  revision: number;
}

export interface DesignerPublishContext extends DesignerSaveContext {
  artifactHash: string;
}

export interface DesignerMigrationContext extends RuntimeMonitoringEventContext {
  documentId: string;
  migrationId: string;
}

export type RuntimeRenderContext = DesignerPublishContext;

export interface RuntimeActionContext extends RuntimeMonitoringEventContext {
  actionId: string;
  actionType: string;
}

export interface RuntimeBindingErrorContext extends RuntimeMonitoringEventContext {
  bindingPath: string;
}

export interface DataStudioConnectionContext extends RuntimeMonitoringEventContext {
  connectionId: string;
}

export interface DataStudioCatalogContext extends DataStudioConnectionContext {
  catalogId: string;
}

export interface DataStudioQueryContext extends DataStudioConnectionContext {
  queryId: string;
}

export interface DataStudioSchemaContext extends DataStudioConnectionContext {
  schemaName: string;
}

export interface DataStudioWriteContext extends DataStudioConnectionContext {
  affectedRows: number;
  resourceKind: string;
}

interface RuntimeMonitoringEventBase<Name extends RuntimeMonitoringEventName, Context extends RuntimeMonitoringEventContext> {
  cancellationRequested: boolean;
  context: Context;
  durationMs: number;
  errorCode?: string;
  eventId: string;
  eventName: Name;
  occurredAt: string;
  outcome: RuntimeMonitoringOutcome;
  timeoutMs?: number;
}

export type DesignerCommandEvent = RuntimeMonitoringEventBase<'designer.command', DesignerCommandContext>;
export interface DesignerCommandFailedEvent extends RuntimeMonitoringEventBase<'designer.command.failed', DesignerCommandContext> {
  errorCode: string;
}
export type DesignerSaveEvent = RuntimeMonitoringEventBase<'designer.save', DesignerSaveContext>;
export type DesignerPublishEvent = RuntimeMonitoringEventBase<'designer.publish', DesignerPublishContext>;
export type DesignerMigrationEvent = RuntimeMonitoringEventBase<'designer.migration', DesignerMigrationContext>;
export type RuntimeRenderEvent = RuntimeMonitoringEventBase<'runtime.render', RuntimeRenderContext>;
export type RuntimeActionEvent = RuntimeMonitoringEventBase<'runtime.action', RuntimeActionContext>;
export interface RuntimeBindingErrorEvent extends RuntimeMonitoringEventBase<'runtime.binding.error', RuntimeBindingErrorContext> {
  errorCode: string;
}
export type DataStudioConnectionTestEvent = RuntimeMonitoringEventBase<'dataStudio.connection.test', DataStudioConnectionContext>;
export type DataStudioCatalogRefreshEvent = RuntimeMonitoringEventBase<'dataStudio.catalog.refresh', DataStudioCatalogContext>;
export type DataStudioQueryExecuteEvent = RuntimeMonitoringEventBase<'dataStudio.query.execute', DataStudioQueryContext>;
export type DataStudioSchemaDeployEvent = RuntimeMonitoringEventBase<'dataStudio.schema.deploy', DataStudioSchemaContext>;
export type DataStudioDataWriteEvent = RuntimeMonitoringEventBase<'dataStudio.data.write', DataStudioWriteContext>;

export type RuntimeMonitoringEvent =
  | DesignerCommandEvent
  | DesignerCommandFailedEvent
  | DesignerSaveEvent
  | DesignerPublishEvent
  | DesignerMigrationEvent
  | RuntimeRenderEvent
  | RuntimeActionEvent
  | RuntimeBindingErrorEvent
  | DataStudioConnectionTestEvent
  | DataStudioCatalogRefreshEvent
  | DataStudioQueryExecuteEvent
  | DataStudioSchemaDeployEvent
  | DataStudioDataWriteEvent;

export interface RuntimeMonitoringExport {
  context?: RuntimeMonitoringContext;
  events: readonly RuntimeMonitoringEvent[];
  metrics: Readonly<Record<string, number>>;
}

export interface RuntimeMonitoringValidationResult {
  errors: readonly string[];
  valid: boolean;
}

const EVENT_CONTEXT_FIELDS = new Set([
  'affectedRows', 'appCode', 'artifactHash', 'auditId', 'backupSha', 'bindingPath', 'catalogId', 'commandId',
  'commandType', 'connectionId', 'documentId', 'migrationId', 'pageCode', 'queryId', 'requestHash', 'resourceKind',
  'revision', 'schemaName', 'tenantId', 'traceId', 'userId', 'actionId', 'actionType'
]);

const REQUIRED_CONTEXT_FIELDS: Readonly<Record<RuntimeMonitoringEventName, readonly string[]>> = {
  'designer.command': ['commandId', 'commandType'],
  'designer.command.failed': ['commandId', 'commandType'],
  'designer.save': ['documentId', 'revision'],
  'designer.publish': ['artifactHash', 'documentId', 'revision'],
  'designer.migration': ['documentId', 'migrationId'],
  'runtime.render': ['artifactHash', 'documentId', 'revision', 'tenantId', 'appCode', 'pageCode', 'userId', 'traceId'],
  'runtime.action': ['actionId', 'actionType', 'artifactHash', 'documentId', 'revision', 'tenantId', 'appCode', 'pageCode', 'userId', 'traceId'],
  'runtime.binding.error': ['bindingPath', 'artifactHash', 'documentId', 'revision', 'tenantId', 'appCode', 'pageCode', 'userId', 'traceId'],
  'dataStudio.connection.test': ['connectionId'],
  'dataStudio.catalog.refresh': ['catalogId', 'connectionId'],
  'dataStudio.query.execute': ['connectionId', 'queryId'],
  'dataStudio.schema.deploy': ['connectionId', 'schemaName'],
  'dataStudio.data.write': ['affectedRows', 'connectionId', 'resourceKind']
};

export function validateRuntimeMonitoringEvent(input: unknown): RuntimeMonitoringValidationResult {
  const errors: string[] = [];
  if (!isRecord(input)) return { errors: ['event must be an object'], valid: false };

  const eventName = input.eventName;
  if (!isRuntimeMonitoringEventName(eventName)) errors.push(`unknown eventName: ${String(eventName)}`);

  for (const field of ['eventId', 'occurredAt', 'outcome', 'durationMs', 'cancellationRequested', 'context']) {
    if (!(field in input)) errors.push(`missing field: ${field}`);
  }
  const allowedFields = new Set(['cancellationRequested', 'context', 'durationMs', 'errorCode', 'eventId', 'eventName', 'occurredAt', 'outcome', 'timeoutMs']);
  for (const field of Object.keys(input)) if (!allowedFields.has(field)) errors.push(`unknown field: ${field}`);

  if (typeof input.eventId !== 'string' || input.eventId.length === 0) errors.push('eventId must be a non-empty string');
  if (typeof input.occurredAt !== 'string' || Number.isNaN(Date.parse(input.occurredAt))) errors.push('occurredAt must be an ISO date-time');
  if (!isOutcome(input.outcome)) errors.push(`invalid outcome: ${String(input.outcome)}`);
  if (typeof input.durationMs !== 'number' || !Number.isFinite(input.durationMs) || input.durationMs < 0) errors.push('durationMs must be a non-negative number');
  if (typeof input.cancellationRequested !== 'boolean') errors.push('cancellationRequested must be boolean');
  if (input.outcome === 'cancelled' && input.cancellationRequested !== true) errors.push('cancelled outcome requires cancellationRequested=true');
  if (input.outcome !== 'cancelled' && input.cancellationRequested === true) errors.push('only cancelled outcome may set cancellationRequested=true');
  if (input.outcome === 'timedOut' && (typeof input.timeoutMs !== 'number' || input.timeoutMs <= 0)) errors.push('timedOut outcome requires timeoutMs>0');
  if (input.outcome !== 'timedOut' && input.timeoutMs !== undefined) errors.push('timeoutMs is only valid for timedOut outcome');
  if (input.outcome !== 'succeeded' && typeof input.errorCode !== 'string') errors.push('failed, cancelled, and timedOut outcomes require errorCode');
  if (input.outcome === 'succeeded' && input.errorCode !== undefined) errors.push('succeeded outcome must not contain errorCode');

  if (!isRecord(input.context)) {
    errors.push('context must be an object');
  } else {
    for (const field of Object.keys(input.context)) if (!EVENT_CONTEXT_FIELDS.has(field)) errors.push(`unknown context field: ${field}`);
    if (isRuntimeMonitoringEventName(eventName)) {
      for (const field of REQUIRED_CONTEXT_FIELDS[eventName]) {
        if (!(field in input.context) || input.context[field] === '' || input.context[field] === undefined || input.context[field] === null) {
          errors.push(`missing context field: ${field}`);
        }
      }
    }
  }

  return { errors, valid: errors.length === 0 };
}

export function isRuntimeMonitoringEvent(input: unknown): input is RuntimeMonitoringEvent {
  return validateRuntimeMonitoringEvent(input).valid;
}

export function isRuntimeMonitoringEventName(value: unknown): value is RuntimeMonitoringEventName {
  return typeof value === 'string' && (RUNTIME_MONITORING_EVENT_NAMES as readonly string[]).includes(value);
}

function isOutcome(value: unknown): value is RuntimeMonitoringOutcome {
  return value === 'succeeded' || value === 'failed' || value === 'cancelled' || value === 'timedOut';
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
