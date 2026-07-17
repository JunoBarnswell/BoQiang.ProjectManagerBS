import { createContext, useContext, useMemo } from 'react';

import { appEnv } from '@/core/config/env';
import { useAuthStore, useWorkspaceStore } from '@/core/state';

import { asterErpImAdapter } from '../adapters/asterErpImAdapter';
import { useImRealtimeConnection } from '../hooks/useImRealtimeConnection';
import type { ImApiAdapter, ImCurrentUser, ImPermissions, ImProviderProps } from '../types/imTypes';

interface ImContextValue {
  adapter: ImApiAdapter;
  currentUser: ImCurrentUser;
  permissions: ImPermissions;
  signalRUrl: string;
}

const ImContext = createContext<ImContextValue | null>(null);

export function ImProvider({ children, config, ...props }: ImProviderProps) {
  const user = useAuthStore((state) => state.user);
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const merged = { ...config, ...props };
  const currentUser = useMemo<ImCurrentUser>(() => {
    if (merged.currentUser) {
      return merged.currentUser;
    }

    return {
      appCode: user?.appCode ?? workspace?.appCode,
      displayName: user?.displayName ?? user?.userName ?? '',
      tenantId: user?.tenantId ?? workspace?.tenantId,
      userId: user?.userId ?? '',
      userName: user?.userName ?? ''
    };
  }, [merged.currentUser, user, workspace]);
  const permissions = useMemo<ImPermissions>(() => ({
    canCreateConversation: merged.permissions?.canCreateConversation ?? true,
    canSearchUsers: merged.permissions?.canSearchUsers ?? true,
    canSendMessage: merged.permissions?.canSendMessage ?? true,
    canView: merged.permissions?.canView ?? true
  }), [merged.permissions]);
  const value = useMemo<ImContextValue>(() => ({
    adapter: merged.adapter ?? asterErpImAdapter,
    currentUser,
    permissions,
    signalRUrl: merged.signalRUrl ?? resolveSignalRUrl()
  }), [currentUser, merged.adapter, merged.signalRUrl, permissions]);

  useImRealtimeConnection(value.adapter, value.signalRUrl, Boolean(currentUser.userId && currentUser.tenantId), currentUser.userId);

  if (!value.adapter) {
    throw new Error('ImProvider requires an ImApiAdapter.');
  }

  return <ImContext.Provider value={value}>{children}</ImContext.Provider>;
}

export function useImContext(): ImContextValue {
  const value = useContext(ImContext);
  if (!value) {
    throw new Error('IM components must be rendered inside ImProvider.');
  }

  return value;
}

function resolveSignalRUrl(): string {
  const apiBase = appEnv.apiBaseUrl.replace(/\/+$/, '');
  const root = apiBase.endsWith('/api') ? apiBase.slice(0, -4) : apiBase;
  return `${root}/hubs/system-notification`;
}
