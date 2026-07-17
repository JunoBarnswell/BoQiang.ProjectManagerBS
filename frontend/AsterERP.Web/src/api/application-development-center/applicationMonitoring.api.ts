import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import type {
  DataStudioCatalogContext,
  DataStudioConnectionContext,
  DataStudioQueryContext,
  DataStudioSchemaContext,
  DataStudioWriteContext,
  RuntimeMonitoringEvent,
  RuntimeMonitoringOutcome
} from '../../runtime-kernel/RuntimeMonitoringContract';

export interface ApplicationMonitoringEventResponse {
  accepted: boolean;
  alreadyAccepted: boolean;
  eventId: string;
  eventType: string;
  traceId: string;
}

export type DataStudioMonitoringEventName =
  | 'dataStudio.connection.test'
  | 'dataStudio.catalog.refresh'
  | 'dataStudio.query.execute'
  | 'dataStudio.schema.deploy'
  | 'dataStudio.data.write';

export type DataStudioMonitoringEventContext =
  | DataStudioConnectionContext
  | DataStudioCatalogContext
  | DataStudioQueryContext
  | DataStudioSchemaContext
  | DataStudioWriteContext;

export function createDataStudioMonitoringEvent(
  eventName: DataStudioMonitoringEventName,
  context: DataStudioMonitoringEventContext,
  outcome: RuntimeMonitoringOutcome,
  durationMs: number,
  errorCode?: string
): RuntimeMonitoringEvent {
  const normalizedErrorCode = outcome === 'succeeded' ? undefined : errorCode?.trim() || 'dataStudioOperationFailed';
  return {
    cancellationRequested: outcome === 'cancelled',
    context,
    durationMs: Math.max(0, durationMs),
    ...(normalizedErrorCode ? { errorCode: normalizedErrorCode } : {}),
    eventId: createMonitoringEventId(),
    eventName,
    occurredAt: new Date().toISOString(),
    outcome
  } as RuntimeMonitoringEvent;
}

export function sendDataStudioMonitoringEvent(
  eventName: DataStudioMonitoringEventName,
  context: DataStudioMonitoringEventContext,
  outcome: RuntimeMonitoringOutcome,
  durationMs: number,
  errorCode?: string
): Promise<ApiEnvelope<ApplicationMonitoringEventResponse>> {
  return sendApplicationMonitoringEvent(createDataStudioMonitoringEvent(eventName, context, outcome, durationMs, errorCode));
}

export function getDataStudioMonitoringErrorCode(error: unknown): string {
  return error instanceof Error && error.name.trim() ? error.name : 'dataStudioOperationFailed';
}

export function sendApplicationMonitoringEvent(
  event: RuntimeMonitoringEvent,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationMonitoringEventResponse>> {
  return httpClient.post<ApplicationMonitoringEventResponse, Record<string, unknown>>(
    '/application-development-center/monitoring/events',
    {
      artifactHash: event.context.artifactHash,
      // The API contract uses Int64 milliseconds; performance.now() produces
      // fractional milliseconds in the browser.
      durationMs: Math.round(Math.max(0, event.durationMs)),
      eventId: event.eventId,
      eventType: event.eventName,
      pageId: event.context.documentId,
      payload: {
        cancellationRequested: event.cancellationRequested,
        context: event.context,
        errorCode: event.errorCode,
        occurredAt: event.occurredAt,
        outcome: event.outcome,
        timeoutMs: event.timeoutMs
      },
      revisionId: event.context.revision === undefined ? undefined : String(event.context.revision),
      source: 'frontend.runtime',
      success: event.outcome === 'succeeded'
    },
    undefined,
    signal
  );
}

function createMonitoringEventId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}
