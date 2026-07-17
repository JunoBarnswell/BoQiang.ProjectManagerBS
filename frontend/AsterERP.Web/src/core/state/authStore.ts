import type { QueryKey } from '@tanstack/react-query';
import { create } from 'zustand';

import { applicationLogin as applicationLoginRequest, getSession, login as loginRequest, switchPlatformWorkspace, switchWorkspace as switchWorkspaceRequest } from '../../api/platform/auth.api';
import type { ApplicationLoginResponseDto, LoginResponseDto, SessionResponseDto, SwitchWorkspaceResponseDto } from '../../api/platform/auth.types';
import { enterApplicationBackend as enterApplicationBackendRequest } from '../../api/platform/platform-management.api';
import { activatePlatformAccessToken, clearAccessToken, getAccessToken, hasPlatformAccessToken, setAccessToken, setApplicationAccessToken } from '../http/tokenStorage';
import { setStoredWorkspace } from '../http/workspaceStorage';
import { queryClient } from '../query/queryClient';
import { queryKeys } from '../query/queryKeys';

import { useMenuStore } from './menuStore';
import { usePermissionStore } from './permissionStore';
import { buildDefaultWorkspaceTab, buildWorkspaceTabGroupKey, useTabStore } from './tabStore';
import type { AuthStoreState } from './types';
import { useWorkspaceStore } from './workspaceStore';

const rawWorkspaceQueryRoots = new Set([
  'application-data-center',
  'application-development-center',
  'workflows'
]);
const asterWorkspaceQueryScopes = new Set([
  'application-console',
  'runtime',
  'page'
]);

function resetWorkspaceScopedQueryCache(): void {
  queryClient.removeQueries({
    predicate: (query) => isWorkspaceScopedQueryKey(query.queryKey)
  });
}

function isWorkspaceScopedQueryKey(queryKey: QueryKey): boolean {
  const [root, scope] = queryKey;
  if (typeof root === 'string' && rawWorkspaceQueryRoots.has(root)) {
    return true;
  }

  return root === queryKeys.all[0] && typeof scope === 'string' && asterWorkspaceQueryScopes.has(scope);
}

function applyWorkspacePayload(session: SessionResponseDto | SwitchWorkspaceResponseDto, options: { preserveTabs?: boolean } = {}): void {
  setStoredWorkspace(session.currentWorkspace ?? null);
  useWorkspaceStore.getState().setWorkspaceState({
    branding: session.branding ?? null,
    currentWorkspace: session.currentWorkspace ?? null
  });
  usePermissionStore.getState().setPermissionCodes(session.permissionCodes);
  useMenuStore.getState().setMenus(session.menus);
  if (!options.preserveTabs) {
    useTabStore.getState().activateWorkspaceGroup(
      buildWorkspaceTabGroupKey(session.currentWorkspace),
      [buildDefaultWorkspaceTab(session.currentWorkspace)]
    );
  }
}

function applyLoginPayload(session: LoginResponseDto): void {
  setAccessToken(session.accessToken);
  setStoredWorkspace(session.currentWorkspace ?? null);
  useWorkspaceStore.getState().setAvailableWorkspaces(session.availableWorkspaces);
  useWorkspaceStore.getState().setWorkspaceState({
    branding: null,
    currentWorkspace: session.currentWorkspace ?? null
  });
  usePermissionStore.getState().setPermissionCodes([]);
  useMenuStore.getState().setMenus([]);
  useTabStore.getState().activateWorkspaceGroup(
    buildWorkspaceTabGroupKey(session.currentWorkspace),
    session.currentWorkspace ? [buildDefaultWorkspaceTab(session.currentWorkspace)] : []
  );
}

function applyApplicationLoginPayload(session: ApplicationLoginResponseDto): void {
  setApplicationAccessToken(session.accessToken);
  setStoredWorkspace(session.currentWorkspace);
  useWorkspaceStore.getState().setAvailableWorkspaces([]);
  useWorkspaceStore.getState().setWorkspaceState({
    branding: session.branding,
    currentWorkspace: session.currentWorkspace
  });
  usePermissionStore.getState().setPermissionCodes(session.permissionCodes);
  useMenuStore.getState().setMenus(session.menus);
  useTabStore.getState().activateWorkspaceGroup(
    buildWorkspaceTabGroupKey(session.currentWorkspace),
    [buildDefaultWorkspaceTab(session.currentWorkspace)]
  );
}

export const useAuthStore = create<AuthStoreState>((set) => ({
  applicationLogin: async (tenantId, appCode, request) => {
    const response = await applicationLoginRequest(tenantId, appCode, request);
    resetWorkspaceScopedQueryCache();
    applyApplicationLoginPayload(response.data);
    set({
      isAuthenticated: true,
      isLoading: false,
      user: response.data.user
    });
    return response.data;
  },
  isAuthenticated: false,
  enterApplicationBackend: async (appCode, request) => {
    const response = await enterApplicationBackendRequest(appCode, request);
    resetWorkspaceScopedQueryCache();
    applyWorkspacePayload(response.data);
    set({
      isAuthenticated: true,
      isLoading: false,
      user: response.data.user
    });
    return response.data;
  },
  isLoading: Boolean(getAccessToken()),
  login: async (request) => {
    const response = await loginRequest(request);
    queryClient.clear();
    applyLoginPayload(response.data);
    set({
      isAuthenticated: true,
      isLoading: false,
      user: response.data.user
    });
    return response.data;
  },
  logout: () => {
    queryClient.clear();
    clearAccessToken();
    setStoredWorkspace(null);
    useWorkspaceStore.getState().setAvailableWorkspaces([]);
    useWorkspaceStore.getState().setWorkspaceState({
      branding: null,
      currentWorkspace: null
    });
    usePermissionStore.getState().setPermissionCodes([]);
    useMenuStore.getState().setMenus([]);
    useTabStore.getState().resetTabs();
    set({
      isAuthenticated: false,
      isLoading: false,
      user: null
    });
  },
  refreshSession: async (options) => {
    const token = getAccessToken();
    if (!token) {
      useAuthStore.getState().logout();
      return;
    }

    set({ isLoading: true });
    try {
      const response = await getSession();
      useWorkspaceStore.getState().setAvailableWorkspaces(response.data.availableWorkspaces);
      applyWorkspacePayload(response.data, options);
      set({
        isAuthenticated: true,
        isLoading: false,
        user: response.data.user
      });
    } catch {
      useAuthStore.getState().logout();
    }
  },
  setUser: (user) => {
    set({
      isAuthenticated: Boolean(user),
      isLoading: false,
      user
    });
  },
  switchWorkspace: async (request) => {
    const response = await switchWorkspaceRequest(request);
    resetWorkspaceScopedQueryCache();
    applyWorkspacePayload(response.data);
    set({
      isAuthenticated: true,
      isLoading: false,
      user: response.data.user
    });
    return response.data;
  },
  switchPlatform: async (request = { target: 'application-center' }) => {
    if (!hasPlatformAccessToken() || !activatePlatformAccessToken()) {
      useAuthStore.getState().logout();
      throw new Error('请先登录平台账号');
    }

    setStoredWorkspace(null);
    const response = await switchPlatformWorkspace(request);
    resetWorkspaceScopedQueryCache();
    applyWorkspacePayload(response.data);
    set({
      isAuthenticated: true,
      isLoading: false,
      user: response.data.user
    });
    return response.data;
  },
  user: null
}));
