import { beforeEach, describe, expect, it, vi } from 'vitest';

const { sendApplicationMonitoringEventMock } = vi.hoisted(() => ({ sendApplicationMonitoringEventMock: vi.fn() }));
vi.mock('../api/application-development-center/applicationMonitoring.api', () => ({
  sendApplicationMonitoringEvent: sendApplicationMonitoringEventMock
}));

import { RuntimeDiagnostics } from './Diagnostics';
import { LATEST_RUNTIME_COMPILER_REVISION } from './RuntimeArtifactIntegrity';
import { validateRuntimeMonitoringEvent } from './RuntimeMonitoringContract';

describe('Runtime monitoring contract', () => {
  beforeEach(() => {
    sendApplicationMonitoringEventMock.mockReset();
    sendApplicationMonitoringEventMock.mockResolvedValue({ data: { accepted: true } });
  });

  it('exports redacted, structured runtime events and counters', () => {
    const diagnostics = new RuntimeDiagnostics(
      { artifactHash: 'sha256:artifact', compilerVersion: LATEST_RUNTIME_COMPILER_REVISION, documentId: 'doc-1', revision: 3 },
      { appCode: 'MES', pageCode: 'page-a', tenantId: 'tenant-a', traceId: 'trace-1', userId: 'user-a' }
    );

    diagnostics.recordArtifactLoadSuccess(12);
    diagnostics.recordActionFailure('ACTION_TIMEOUT', 30, 30, 'timedOut');
    diagnostics.recordRecomputeCancellation(5);

    const exported = diagnostics.exportMonitoring();
    expect(exported.metrics.artifactLoadSuccesses).toBe(1);
    expect(exported.metrics.actionFailures).toBe(1);
    expect(exported.metrics.recomputeCancellations).toBe(1);
    expect(exported.events).toHaveLength(3);
    expect(exported.events.every((event) => event.context.artifactHash === 'sha256:artifact')).toBe(true);
    expect(exported.events.find((event) => event.outcome === 'timedOut')?.cancellationRequested).toBe(false);
    expect(exported.events.find((event) => event.outcome === 'cancelled')?.cancellationRequested).toBe(true);
    expect(JSON.stringify(exported)).not.toContain('secret');
  });

  it('rejects unknown events, missing context, and inconsistent outcome fields', () => {
    const base = {
      cancellationRequested: false,
      context: {
        actionId: 'a-1',
        actionType: 'execute',
        appCode: 'MES',
        artifactHash: 'sha256:artifact',
        documentId: 'doc-1',
        pageCode: 'page-a',
        revision: 3,
        tenantId: 'tenant-a',
        traceId: 'trace-1',
        userId: 'user-a'
      },
      durationMs: 1,
      eventId: 'event-1',
      eventName: 'runtime.action',
      occurredAt: '2026-07-12T00:00:00.000Z',
      outcome: 'succeeded'
    };

    expect(validateRuntimeMonitoringEvent({ ...base, eventName: 'runtime.unknown' }).valid).toBe(false);
    expect(validateRuntimeMonitoringEvent({ ...base, context: {} }).errors).toContain('missing context field: actionId');
    expect(validateRuntimeMonitoringEvent({ ...base, outcome: 'cancelled' }).errors).toContain('cancelled outcome requires cancellationRequested=true');
    expect(validateRuntimeMonitoringEvent({ ...base, outcome: 'timedOut', errorCode: 'timeout' }).errors).toContain('timedOut outcome requires timeoutMs>0');
  });

  it('records monitoring delivery failures as observable local diagnostics', async () => {
    sendApplicationMonitoringEventMock.mockRejectedValueOnce(new Error('audit endpoint unavailable'));
    const diagnostics = new RuntimeDiagnostics(undefined, {
      appCode: 'MES', pageCode: 'page-a', tenantId: 'tenant-a', traceId: 'trace-1', userId: 'user-a'
    });

    diagnostics.recordMonitoringEvent('designer.command', 'succeeded', 1, undefined, undefined, {
      commandId: 'command-1', commandType: 'runtime.test'
    });
    await Promise.resolve();

    expect(diagnostics.metrics.monitoringDeliveryFailures).toBe(1);
    expect(diagnostics.all).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'monitoringDeliveryFailed', severity: 'warning' })
    ]));
    expect(diagnostics.monitoringEvents).toHaveLength(1);
  });
});
