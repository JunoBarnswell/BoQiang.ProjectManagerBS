import { describe, expect, it } from 'vitest';

import { RuntimeArtifactIntegrity } from '../../../../../../runtime-kernel/RuntimeArtifactIntegrity';
import { validateRuntimeMonitoringEvent } from '../../../../../../runtime-kernel/RuntimeMonitoringContract';

describe('HAO-112 Runtime product acceptance boundary', () => {
  it('uses the latest event contract for runtime diagnostics', () => {
    expect(validateRuntimeMonitoringEvent({
      cancellationRequested: false,
      context: {
        actionId: 'action-1',
        actionType: 'setVariable',
        appCode: 'MES',
        artifactHash: 'sha256:' + 'a'.repeat(64),
        documentId: 'page-1',
        pageCode: 'home',
        revision: 1,
        tenantId: 'tenant-a',
        traceId: 'trace-1',
        userId: 'user-a'
      },
      durationMs: 2,
      eventId: 'event-1',
      eventName: 'runtime.action',
      occurredAt: '2026-07-12T00:00:00.000Z',
      outcome: 'succeeded'
    }).valid).toBe(true);
  });

  it('keeps artifact integrity hash canonical across property order', async () => {
    const first = await RuntimeArtifactIntegrity.computeHash({ revision: 1, documentId: 'page-1', manifestTypes: ['text'] });
    const second = await RuntimeArtifactIntegrity.computeHash({ manifestTypes: ['text'], documentId: 'page-1', revision: 1 });
    expect(first).toBe(second);
  });

  it('rejects unknown event names instead of silently accepting diagnostics', () => {
    expect(validateRuntimeMonitoringEvent({
      cancellationRequested: false,
      context: { actionId: 'action-1', actionType: 'setVariable' },
      durationMs: 0,
      eventId: 'event-1',
      eventName: 'runtime.legacy',
      occurredAt: '2026-07-12T00:00:00.000Z',
      outcome: 'succeeded'
    }).valid).toBe(false);
  });
});
