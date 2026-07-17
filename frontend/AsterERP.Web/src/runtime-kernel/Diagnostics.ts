export type RuntimeDiagnosticSeverity = 'error' | 'warning';

import { sendApplicationMonitoringEvent } from '../api/application-development-center/applicationMonitoring.api';

import {
  validateRuntimeMonitoringEvent,
  type RuntimeMonitoringContext,
  type RuntimeMonitoringEvent,
  type RuntimeMonitoringEventContext,
  type RuntimeMonitoringEventName,
  type RuntimeMonitoringExport,
  type RuntimeMonitoringOutcome
} from './RuntimeMonitoringContract';

export interface RuntimeDiagnostic {
  code: string;
  details?: Readonly<Record<string, unknown>>;
  message: string;
  path?: string;
  severity: RuntimeDiagnosticSeverity;
}

export interface RuntimeArtifactContext {
  artifactHash: string;
  compilerVersion: string;
  documentId: string;
  revision: number;
}

export interface RuntimeMetricsSnapshot {
  actionFailures: number;
  actionSuccesses: number;
  artifactLoadFailures: number;
  artifactLoadSuccesses: number;
  artifactLoadDurationMs: number;
  bindingErrors: number;
  diagnosticErrors: number;
  monitoringDeliveryFailures: number;
  recomputeCancellations: number;
}

export class RuntimeDiagnostics {
  private readonly items: RuntimeDiagnostic[] = [];
  private readonly events: RuntimeMonitoringEvent[] = [];
  private readonly counters: RuntimeMetricsSnapshot = {
    actionFailures: 0,
    actionSuccesses: 0,
    artifactLoadFailures: 0,
    artifactLoadSuccesses: 0,
    artifactLoadDurationMs: 0,
    bindingErrors: 0,
    diagnosticErrors: 0,
    monitoringDeliveryFailures: 0,
    recomputeCancellations: 0
  };

  public constructor(
    public readonly artifact?: RuntimeArtifactContext,
    private readonly monitoringContext?: RuntimeMonitoringContext
  ) {}

  public add(diagnostic: RuntimeDiagnostic): void {
    this.items.push({ ...diagnostic });
    if (diagnostic.severity === 'error') this.counters.diagnosticErrors += 1;
    if (diagnostic.code.toLowerCase().includes('binding')) this.counters.bindingErrors += 1;
  }

  public recordActionFailure(errorCode?: string, durationMs = 0, timeoutMs?: number, outcome: RuntimeMonitoringOutcome = 'failed'): void {
    this.counters.actionFailures += 1;
    this.recordMonitoringEvent('runtime.action', outcome, durationMs, errorCode ?? 'actionFailed', timeoutMs, { actionId: 'runtime.action', actionType: 'execute' });
  }
  public recordActionSuccess(durationMs = 0): void {
    this.counters.actionSuccesses += 1;
    this.recordMonitoringEvent('runtime.action', 'succeeded', durationMs, undefined, undefined, { actionId: 'runtime.action', actionType: 'execute' });
  }
  public recordArtifactLoadFailure(durationMs = 0): void {
    this.counters.artifactLoadFailures += 1;
    this.counters.artifactLoadDurationMs = durationMs;
    this.recordMonitoringEvent('runtime.render', 'failed', durationMs, 'artifactLoadFailed', undefined, this.runtimeContext());
  }
  public recordArtifactLoadSuccess(durationMs = 0): void {
    this.counters.artifactLoadSuccesses += 1;
    this.counters.artifactLoadDurationMs = durationMs;
    this.recordMonitoringEvent('runtime.render', 'succeeded', durationMs, undefined, undefined, this.runtimeContext());
  }
  public recordRecomputeCancellation(durationMs = 0): void {
    this.counters.recomputeCancellations += 1;
    this.recordMonitoringEvent('runtime.binding.error', 'cancelled', durationMs, 'recomputeCancelled', undefined, { bindingPath: 'dependency.recompute' });
  }

  public recordMonitoringEvent(
    eventName: RuntimeMonitoringEventName,
    outcome: RuntimeMonitoringOutcome,
    durationMs = 0,
    errorCode?: string,
    timeoutMs?: number,
    context: RuntimeMonitoringEventContext = {}
  ): void {
    const eventId = `${Date.now().toString(36)}-${(this.events.length + 1).toString(36)}`;
    const event = {
      cancellationRequested: outcome === 'cancelled',
      context: { ...this.monitoringContext, ...toMonitoringArtifactContext(this.artifact), ...context },
      durationMs: Math.max(0, durationMs),
      errorCode,
      eventId,
      eventName,
      occurredAt: new Date().toISOString(),
      outcome,
      timeoutMs
    } as RuntimeMonitoringEvent;
    const validation = validateRuntimeMonitoringEvent(event);
    if (!validation.valid) {
      this.add({
        code: 'invalidMonitoringContext',
        details: { errors: validation.errors },
        message: 'Runtime monitoring event context is incomplete or invalid.',
        path: eventName,
        severity: 'error'
      });
      return;
    }
    this.events.push(event);
    void sendApplicationMonitoringEvent(event).catch((error: unknown) => {
      this.counters.monitoringDeliveryFailures += 1;
      this.add({
        code: 'monitoringDeliveryFailed',
        details: { cause: error instanceof Error ? error.message : String(error), eventId: event.eventId, eventName },
        message: 'Runtime monitoring event could not be persisted by the application endpoint.',
        path: eventName,
        severity: 'warning'
      });
    });
  }

  private runtimeContext(): RuntimeMonitoringEventContext {
    return { ...this.monitoringContext, ...toMonitoringArtifactContext(this.artifact) };
  }

  public error(code: string, message: string, path?: string, details?: Readonly<Record<string, unknown>>): void {
    this.add({ code, details, message, path, severity: 'error' });
  }

  public warning(code: string, message: string, path?: string, details?: Readonly<Record<string, unknown>>): void {
    this.add({ code, details, message, path, severity: 'warning' });
  }

  public get all(): readonly RuntimeDiagnostic[] {
    return this.items;
  }

  public get hasErrors(): boolean {
    return this.items.some((item) => item.severity === 'error');
  }

  public get metrics(): RuntimeMetricsSnapshot {
    return { ...this.counters };
  }

  public get monitoringEvents(): readonly RuntimeMonitoringEvent[] {
    return this.events;
  }

  public exportMonitoring(): RuntimeMonitoringExport {
    return {
      context: this.monitoringContext,
      events: this.monitoringEvents,
      metrics: { ...this.metrics }
    };
  }

  public snapshot(): {
    artifact?: RuntimeArtifactContext;
    diagnostics: readonly RuntimeDiagnostic[];
    metrics: RuntimeMetricsSnapshot;
    monitoring: RuntimeMonitoringExport;
  } {
    return { artifact: this.artifact, diagnostics: this.all, metrics: this.metrics, monitoring: this.exportMonitoring() };
  }
}

function toMonitoringArtifactContext(artifact: RuntimeArtifactContext | undefined): RuntimeMonitoringEventContext {
  return artifact
    ? { artifactHash: artifact.artifactHash, documentId: artifact.documentId, revision: artifact.revision }
    : {};
}

export class RuntimeContractError extends Error {
  public constructor(public readonly diagnostics: readonly RuntimeDiagnostic[]) {
    super(diagnostics.map(formatRuntimeDiagnostic).join('; '));
    this.name = 'RuntimeContractError';
  }
}

function formatRuntimeDiagnostic(diagnostic: RuntimeDiagnostic): string {
  const details = diagnostic.details && Object.keys(diagnostic.details).length > 0
    ? ` (${JSON.stringify(diagnostic.details)})`
    : '';
  return `${diagnostic.code}: ${diagnostic.message}${details}`;
}
