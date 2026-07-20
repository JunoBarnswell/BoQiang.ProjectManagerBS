import { describe, expect, it } from 'vitest';

import { computeScheduleUrgencyMetrics } from './projectManagementScheduleUrgency';

describe('computeScheduleUrgencyMetrics', () => {
  it('returns neutral border when due date is missing', () => {
    expect(computeScheduleUrgencyMetrics(undefined, undefined, 'Todo')).toMatchObject({
      tone: 'none',
      urgencyColor: '#94a3b8',
    });
  });

  it('uses green border for completed tasks', () => {
    expect(computeScheduleUrgencyMetrics('2026-07-01', '2026-07-20', 'Done', Date.parse('2026-07-10'))).toMatchObject({
      tone: 'completed',
      urgencyColor: '#22c55e',
    });
  });

  it('shifts border color toward red as deadline approaches', () => {
    const healthy = computeScheduleUrgencyMetrics('2026-07-01', '2026-07-20', 'InProgress', Date.parse('2026-07-05'));
    const warning = computeScheduleUrgencyMetrics('2026-07-01', '2026-07-20', 'InProgress', Date.parse('2026-07-18'));
    const critical = computeScheduleUrgencyMetrics('2026-07-01', '2026-07-20', 'InProgress', Date.parse('2026-07-19T20:00:00Z'));

    expect(healthy.tone).toBe('healthy');
    expect(warning.tone).toBe('warning');
    expect(critical.tone).toBe('critical');
    expect(healthy.remainingRatio).toBeGreaterThan(warning.remainingRatio);
    expect(healthy.urgencyColor.startsWith('rgb(')).toBe(true);
  });

  it('uses solid red border when overdue', () => {
    expect(computeScheduleUrgencyMetrics('2026-07-01', '2026-07-10', 'InProgress', Date.parse('2026-07-12'))).toMatchObject({
      tone: 'overdue',
      urgencyColor: '#dc2626',
    });
  });
});
