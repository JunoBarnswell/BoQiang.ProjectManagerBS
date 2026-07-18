import { describe, expect, it } from 'vitest';

import {
  projectCenterPreferenceKey,
  rememberRecentProject,
  toggleProjectFavorite,
  type ProjectCenterPreferences,
} from './projectCenterPreferences';

describe('projectCenterPreferences', () => {
  it('scopes preferences by user, tenant and platform app', () => {
    expect(projectCenterPreferenceKey('user-a', 'tenant-a', 'SYSTEM')).not.toBe(projectCenterPreferenceKey('user-b', 'tenant-a', 'SYSTEM'));
    expect(projectCenterPreferenceKey('user-a', 'tenant-a', 'SYSTEM')).not.toBe(projectCenterPreferenceKey('user-a', 'tenant-b', 'SYSTEM'));
  });

  it('toggles favorites and keeps recent projects unique and bounded', () => {
    const initial: ProjectCenterPreferences = { favoriteProjectIds: ['project-a'], recentProjectIds: ['project-a'] };
    expect(toggleProjectFavorite(initial, 'project-a').favoriteProjectIds).toEqual([]);
    expect(toggleProjectFavorite(initial, 'project-b').favoriteProjectIds).toEqual(['project-b', 'project-a']);
    expect(rememberRecentProject(initial, 'project-a').recentProjectIds).toEqual(['project-a']);
    expect(rememberRecentProject(initial, 'project-b').recentProjectIds).toEqual(['project-b', 'project-a']);
  });
});
