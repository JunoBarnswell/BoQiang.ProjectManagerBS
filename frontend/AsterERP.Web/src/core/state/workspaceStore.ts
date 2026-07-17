import { create } from 'zustand';

import { setStoredWorkspace } from '../http/workspaceStorage';
import { queryClient } from '../query/queryClient';

import { useMenuStore } from './menuStore';
import { usePermissionStore } from './permissionStore';
import { useTabStore } from './tabStore';
import type { WorkspaceStoreState } from './types';

export const useWorkspaceStore = create<WorkspaceStoreState>((set) => ({
  availableWorkspaces: [],
  branding: null,
  clearWorkspace: () => {
    queryClient.clear();
    setStoredWorkspace(null);
    set({
      branding: null,
      currentWorkspace: null
    });
    useMenuStore.getState().setMenus([]);
    usePermissionStore.getState().setPermissionCodes([]);
    useTabStore.getState().resetTabs();
  },
  currentWorkspace: null,
  setAvailableWorkspaces: (availableWorkspaces) => {
    set({ availableWorkspaces: Array.isArray(availableWorkspaces) ? availableWorkspaces : [] });
  },
  setWorkspaceState: (workspace) => {
    set({
      branding: workspace.branding ?? null,
      currentWorkspace: workspace.currentWorkspace ?? null
    });
  }
}));

