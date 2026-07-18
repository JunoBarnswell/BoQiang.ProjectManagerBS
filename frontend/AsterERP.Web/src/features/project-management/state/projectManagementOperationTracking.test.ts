// @vitest-environment jsdom

import { afterEach, describe, expect, it } from 'vitest';

import { clearProjectManagementOperationTracking, getProjectManagementOperationTrackingKey, readProjectManagementOperationTracking, writeProjectManagementOperationTracking } from './projectManagementOperationTracking';

describe('projectManagementOperationTracking', () => {
  afterEach(() => window.localStorage.clear());

  it('isolates restored operation tracking by tenant, app, and current user', () => {
    const operatorKey = getProjectManagementOperationTrackingKey('tenant-a', 'mes', 'operator-a');
    const otherUserKey = getProjectManagementOperationTrackingKey('tenant-a', 'MES', 'operator-b');
    writeProjectManagementOperationTracking(operatorKey, 'operation-a');

    expect(readProjectManagementOperationTracking(operatorKey)).toBe('operation-a');
    expect(readProjectManagementOperationTracking(otherUserKey)).toBeNull();
  });

  it('clears stale tracking and rejects incomplete scopes', () => {
    const key = getProjectManagementOperationTrackingKey('tenant-a', 'MES', 'operator-a');
    writeProjectManagementOperationTracking(key, 'operation-a');
    clearProjectManagementOperationTracking(key);

    expect(readProjectManagementOperationTracking(key)).toBeNull();
    expect(getProjectManagementOperationTrackingKey('tenant-a', 'MES', '')).toBeNull();
  });
});
