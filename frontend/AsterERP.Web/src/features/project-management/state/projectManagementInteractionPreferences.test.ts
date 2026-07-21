// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import {
  projectManagementInteractionPreferenceKey,
  readProjectManagementInteractionPreferences,
  rememberRecentRequirementType,
  restoreProjectManagementRecentPosition,
  type ProjectManagementInteractionPreferences,
  writeProjectManagementInteractionPreferences,
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

  it('remembers the five most recent requirement types without duplicates', () => {
    const preferences = ['A', 'B', 'C', 'D', 'E', 'F'].reduce(
      (current, value) => rememberRecentRequirementType(current, value),
      {} as ProjectManagementInteractionPreferences,
    );

    expect(preferences.recentRequirementTypes).toEqual(['F', 'E', 'D', 'C', 'B']);
    expect(rememberRecentRequirementType(preferences, 'D').recentRequirementTypes).toEqual(['D', 'F', 'E', 'C', 'B']);
  });

  it('ignores malformed persisted recent requirement types', () => {
    const key = 'project-management:test-preferences';
    localStorage.setItem(key, JSON.stringify({ recentRequirementTypes: [' Feature ', '', 1, 'Feature', null] }));

    expect(readProjectManagementInteractionPreferences(key).recentRequirementTypes).toEqual(['Feature']);
    writeProjectManagementInteractionPreferences(key, { recentRequirementTypes: ['A', 'A', 'B'] });
    expect(readProjectManagementInteractionPreferences(key).recentRequirementTypes).toEqual(['A', 'B']);
  });
});
