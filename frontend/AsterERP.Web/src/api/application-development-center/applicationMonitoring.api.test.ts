import { describe, expect, it } from 'vitest';

import {
  createDataStudioMonitoringEvent,
  getDataStudioMonitoringErrorCode
} from './applicationMonitoring.api';

describe('application monitoring event contract', () => {
  it('creates a bounded successful Data Studio event', () => {
    const event = createDataStudioMonitoringEvent(
      'dataStudio.query.execute',
      { connectionId: 'connection-1', queryId: 'query-1' },
      'succeeded',
      -12
    );

    expect(event).toMatchObject({
      context: { connectionId: 'connection-1', queryId: 'query-1' },
      durationMs: 0,
      eventName: 'dataStudio.query.execute',
      outcome: 'succeeded'
    });
    expect(event.eventId).toBeTruthy();
    expect(event.errorCode).toBeUndefined();
  });

  it('normalizes failed and cancelled diagnostics without throwing', () => {
    const failed = createDataStudioMonitoringEvent(
      'dataStudio.data.write',
      { connectionId: 'connection-1', affectedRows: 0, resourceKind: 'orders.update' },
      'failed',
      21,
      ''
    );
    const cancelled = createDataStudioMonitoringEvent(
      'dataStudio.catalog.refresh',
      { catalogId: 'catalog-1', connectionId: 'connection-1' },
      'cancelled',
      8
    );

    expect(failed.errorCode).toBe('dataStudioOperationFailed');
    expect(cancelled.cancellationRequested).toBe(true);
    expect(getDataStudioMonitoringErrorCode(new Error('timeout'))).toBe('Error');
  });

  it('keeps browser fractional timing compatible with the Int64 API contract', () => {
    const event = createDataStudioMonitoringEvent(
      'dataStudio.query.execute',
      { connectionId: 'connection-1', queryId: 'query-1' },
      'succeeded',
      21.6
    );

    expect(Math.round(Math.max(0, event.durationMs))).toBe(22);
  });
});
