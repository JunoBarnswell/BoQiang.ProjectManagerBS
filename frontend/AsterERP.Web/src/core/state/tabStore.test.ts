import { beforeEach, describe, expect, it } from 'vitest';

import { useTabStore } from './tabStore';

describe('tabStore route lifecycle', () => {
  beforeEach(() => {
    useTabStore.setState({
      openTabs: [
        { cacheKey: '/home', closable: false, id: '/home', isDefault: true, label: '首页', path: '/home', refreshToken: 0, title: '首页' },
        { cacheKey: '/platform/project-management', closable: true, id: '/platform/project-management', label: '项目管理', path: '/platform/project-management', refreshToken: 2, title: '项目管理' },
        { cacheKey: '/system/users', closable: true, id: '/system/users', label: '用户管理', path: '/system/users', refreshToken: 0, title: '用户管理' },
      ],
      pageCache: { '/platform/project-management': { selectedProjectId: 'p-1' } },
      workspaceActivationVersion: 0,
    });
  });

  it('preserves refresh state while ensuring an existing route tab', () => {
    useTabStore.getState().ensureRouteTab({ cacheKey: '/platform/project-management', closable: true, id: '/platform/project-management', label: '项目管理', path: '/platform/project-management', title: '项目管理' });
    expect(useTabStore.getState().openTabs.find((tab) => tab.path === '/platform/project-management')?.refreshToken).toBe(2);
  });

  it('calculates the nearest fallback and clears the closed tab cache', () => {
    const store = useTabStore.getState();
    expect(store.getCloseFallback('/platform/project-management', '/home')).toBe('/home');
    store.closeTab('/platform/project-management');
    expect(useTabStore.getState().openTabs.map((tab) => tab.path)).toEqual(['/home', '/system/users']);
    expect(useTabStore.getState().pageCache['/platform/project-management']).toBeUndefined();
  });

  it('signals that a workspace group has replaced the route tab set', () => {
    useTabStore.getState().activateWorkspaceGroup('application:tenant-a:PM', [
      { cacheKey: '/home', closable: false, id: '/home', isDefault: true, label: '首页', path: '/home', refreshToken: 0, title: '首页' },
    ]);

    expect(useTabStore.getState().workspaceActivationVersion).toBe(1);
  });
});
