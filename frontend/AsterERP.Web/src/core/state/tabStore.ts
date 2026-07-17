import { create } from 'zustand';

import type { CurrentWorkspaceDto } from '../../api/platform/auth.types';

import type { OpenTab, TabStoreState } from './types';

const defaultTab: OpenTab = {
  cacheKey: '/home',
  closable: false,
  id: '/home',
  isDefault: true,
  label: 'nav.home',
  path: '/home',
  refreshToken: 0,
  title: 'nav.home'
};

const defaultWorkspaceKey = 'platform::';

export function buildWorkspaceTabGroupKey(workspace: CurrentWorkspaceDto | null | undefined): string {
  if (!workspace) {
    return defaultWorkspaceKey;
  }

  return `${workspace.workspaceLevel}:${workspace.tenantId}:${workspace.appCode}`;
}

export function buildDefaultWorkspaceTab(workspace: CurrentWorkspaceDto | null | undefined): OpenTab {
  const path = workspace?.defaultRoutePath || (workspace?.workspaceLevel === 'platform' ? '/platform/applications' : '/home');
  return {
    cacheKey: path,
    closable: false,
    id: path,
    isDefault: true,
    label: workspace?.workspaceLevel === 'application' ? `${workspace.appCode} 工作台` : 'nav.home',
    path,
    refreshToken: 0,
    title: workspace?.workspaceLevel === 'application' ? `${workspace.appCode} 工作台` : 'nav.home'
  };
}

function normalizeOpenTabs(tabs: OpenTab[]) {
  const next = tabs
    .filter((item) => typeof item.path === 'string' && item.path.startsWith('/'))
    .map((item) => ({
      cacheKey: item.cacheKey ?? item.path,
      closable: item.closable ?? !item.isDefault,
      id: item.id || item.path,
      isDefault: Boolean(item.isDefault),
      label: item.label,
      parentPath: item.parentPath,
      path: item.path.trim(),
      refreshToken: item.refreshToken ?? 0,
      title: item.title ?? item.label
    }));

  return next.length > 0 ? next : [];
}

function clearTabCacheEntries(pageCache: Record<string, unknown>, tabPath: string) {
  if (!tabPath || tabPath === '/') {
    return {};
  }

  return Object.fromEntries(
    Object.entries(pageCache).filter(([key]) => !(key === tabPath || key.startsWith(`${tabPath}::`)))
  );
}

export const useTabStore = create<TabStoreState>((set, get) => ({
  activeWorkspaceKey: defaultWorkspaceKey,
  addTab: (tab) => {
    const normalizedTab = normalizeOpenTabs([tab])[0];
    if (!normalizedTab) {
      return;
    }

    const existing = get().openTabs;
    const existingIndex = existing.findIndex((item) => item.path === normalizedTab.path);

    if (existingIndex >= 0) {
      const currentTab = existing[existingIndex];
      if (
        currentTab &&
        currentTab.cacheKey === normalizedTab.cacheKey &&
        currentTab.closable === normalizedTab.closable &&
        currentTab.id === normalizedTab.id &&
        currentTab.isDefault === normalizedTab.isDefault &&
        currentTab.label === normalizedTab.label &&
        currentTab.parentPath === normalizedTab.parentPath &&
        currentTab.path === normalizedTab.path &&
        currentTab.refreshToken === normalizedTab.refreshToken &&
        currentTab.title === normalizedTab.title
      ) {
        return;
      }

      const nextTabs = [...existing];
      nextTabs[existingIndex] = {
        ...nextTabs[existingIndex],
        ...normalizedTab
      };
      set({ openTabs: nextTabs });
      return;
    }

    const nextTabs = [...existing, normalizedTab].filter((item) => item.path);
    set({ openTabs: nextTabs });
  },
  activateWorkspaceGroup: (workspaceKey, tabs = []) => {
    const normalizedTabs = normalizeOpenTabs(Array.isArray(tabs) ? tabs : []);
    set((state) => {
      const currentGroupTabs = normalizeOpenTabs(state.openTabs);
      const nextGroups = {
        ...state.workspaceTabGroups,
        [state.activeWorkspaceKey]: currentGroupTabs.length > 0 ? currentGroupTabs : [defaultTab]
      };
      const targetTabs = normalizedTabs.length > 0
        ? normalizedTabs
        : normalizeOpenTabs(nextGroups[workspaceKey] ?? []);
      return {
        activeWorkspaceKey: workspaceKey,
        openTabs: targetTabs.length > 0 ? targetTabs : [defaultTab],
        pageCache: {},
        workspaceTabGroups: nextGroups
      };
    });
  },
  clearPageCache: (cacheKey) => {
    set((state) => {
      const nextCache = { ...state.pageCache };
      delete nextCache[cacheKey];
      return { pageCache: nextCache };
    });
  },
  clearTabCache: (tabPath) => {
    set((state) => ({
      pageCache: clearTabCacheEntries(state.pageCache, tabPath)
    }));
  },
  closeTab: (path) => {
    const filteredTabs = normalizeOpenTabs(get().openTabs.filter((item) => item.path !== path));
    const fallbackTabs = filteredTabs.length > 0 ? filteredTabs : [defaultTab];
    set((state) => ({
      openTabs: fallbackTabs,
      pageCache: clearTabCacheEntries(state.pageCache, path)
    }));
  },
  getPageCache: (cacheKey) => {
    return get().pageCache[cacheKey] as never;
  },
  openTabs: [defaultTab],
  pageCache: {},
  refreshTab: (path) => {
    set((state) => ({
      openTabs: state.openTabs.map((tab) =>
        tab.path === path
          ? {
              ...tab,
              refreshToken: (tab.refreshToken ?? 0) + 1
            }
          : tab
      ),
      pageCache: clearTabCacheEntries(state.pageCache, path)
    }));
  },
  resetTabs: (tabs = []) => {
    const normalized = normalizeOpenTabs(Array.isArray(tabs) ? tabs : []);
    set((state) => ({
      openTabs: normalized.length > 0 ? normalized : [defaultTab],
      pageCache: {},
      workspaceTabGroups: {
        ...state.workspaceTabGroups,
        [state.activeWorkspaceKey]: normalized.length > 0 ? normalized : [defaultTab]
      }
    }));
  },
  setPageCache: (cacheKey, value) => {
    set((state) => ({
      pageCache: {
        ...state.pageCache,
        [cacheKey]: value
      }
    }));
  },
  setOpenTabs: (tabs) => {
    const normalized = normalizeOpenTabs(Array.isArray(tabs) ? tabs : []);
    set((state) => ({
      openTabs: normalized.length > 0 ? normalized : [defaultTab],
      workspaceTabGroups: {
        ...state.workspaceTabGroups,
        [state.activeWorkspaceKey]: normalized.length > 0 ? normalized : [defaultTab]
      }
    }));
  },
  workspaceTabGroups: {
    [defaultWorkspaceKey]: [defaultTab]
  }
}));
