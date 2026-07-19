// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import {
  projectManagementInteractionPreferenceKey,
  restoreProjectManagementRecentPosition,
  type ProjectManagementInteractionPreferences,
} from './projectManagementInteractionPreferences';

describe('projectManagementInteractionPreferences', () => {
  it('isolates persisted state by user, tenant and app', () => {
    expect(projectManagementInteractionPreferenceKey({ appCode: 'SYSTEM', tenantId: 'tenant-a', userId: 'user-a' }))
      .not.toBe(projectManagementInteractionPreferenceKey({ appCode: 'SYSTEM', tenantId: 'tenant-a', userId: 'user-b' }));
    expect(projectManagementInteractionPreferenceKey({ appCode: 'SYSTEM', tenantId: 'tenant-a', userId: 'user-a' }))
      .not.toBe(projectManagementInteractionPreferenceKey({ appCode: 'MES', tenantId: 'tenant-a', userId: 'user-a' }));
    expect(projectManagementInteractionPreferenceKey({ appCode: 'SYSTEM', tenantId: 'tenant:a', userId: 'user-b' }))
      .not.toBe(projectManagementInteractionPreferenceKey({ appCode: 'SYSTEM', tenantId: 'tenant', userId: 'a:user-b' }));
  });

  it('only restores a recent position when the caller confirms it is still authorized', () => {
    const preferences: ProjectManagementInteractionPreferences = {
      recentPosition: { pathname: '/project-management/projects/project-a', search: '?tab=tasks', updatedAt: 1 },
    };

    expect(restoreProjectManagementRecentPosition(preferences, () => false)).toBeUndefined();
    expect(restoreProjectManagementRecentPosition(preferences, (position) => position.pathname.includes('project-a')))
      .toEqual(preferences.recentPosition);
  });
});
