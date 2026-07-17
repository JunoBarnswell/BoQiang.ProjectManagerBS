import { createContext, useContext, useEffect, type ReactNode } from 'react';

import type { CurrentUserDto, LoginRequest, LoginResponseDto } from '../../api/platform/auth.types';
import type { MenuTreeNodeDto } from '../../api/system/system.types';
import { useAuthStore, useMenuStore, usePermissionStore } from '../state';

type SessionContextValue = {
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (request: LoginRequest) => Promise<LoginResponseDto>;
  logout: () => void;
  menus: MenuTreeNodeDto[];
  permissionCodes: string[];
  refreshSession: () => Promise<void>;
  setUser: (user: CurrentUserDto | null) => void;
  user: CurrentUserDto | null;
};

const SessionContext = createContext<SessionContextValue | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const isAuthenticated = useAuthStore((current) => current.isAuthenticated);
  const isLoading = useAuthStore((current) => current.isLoading);
  const menus = useMenuStore((current) => current.menus);
  const permissionCodes = usePermissionStore((current) => current.permissionCodes);
  const refreshSession = useAuthStore((current) => current.refreshSession);
  const setUser = useAuthStore((current) => current.setUser);
  const user = useAuthStore((current) => current.user);
  const login = useAuthStore((current) => current.login);
  const logout = useAuthStore((current) => current.logout);

  useEffect(() => {
    if (!isAuthenticated) {
      void refreshSession();
    }
  }, [isAuthenticated, refreshSession]);

  const value = {
    isAuthenticated,
    isLoading,
    login,
    logout,
    menus,
    permissionCodes,
    refreshSession,
    setUser,
    user
  };

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession() {
  const context = useContext(SessionContext);
  if (!context) {
    throw new Error('useSession must be used within SessionProvider');
  }

  return context;
}
