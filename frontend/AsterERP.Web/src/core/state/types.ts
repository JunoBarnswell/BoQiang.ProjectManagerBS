import type { ApplicationLoginRequest, ApplicationLoginResponseDto, BrandingDto, CurrentUserDto, CurrentWorkspaceDto, LoginRequest, LoginResponseDto, SwitchPlatformRequest, SwitchWorkspaceRequest, SwitchWorkspaceResponseDto, WorkspaceDto } from '../../api/platform/auth.types';
import type { ApplicationBackendEntryRequest, ApplicationBackendEntryResponseDto } from '../../api/platform/platform.types';
import type { MenuTreeNodeDto } from '../../api/system/system.types';
import type { ThemeMode } from '../config/env';
import type { UiFontFamilyKey, UiPreferences, UiScalePercent } from '../ui-preferences/uiPreferenceOptions';

export interface OpenTab {
  cacheKey?: string;
  closable?: boolean;
  id: string;
  isDefault?: boolean;
  label: string;
  parentPath?: string;
  path: string;
  refreshToken?: number;
  title?: string;
}

export type PageCache = Record<string, unknown>;

export interface AuthStoreState {
  isAuthenticated: boolean;
  isLoading: boolean;
  applicationLogin: (tenantId: string, appCode: string, request: ApplicationLoginRequest) => Promise<ApplicationLoginResponseDto>;
  enterApplicationBackend: (appCode: string, request: ApplicationBackendEntryRequest) => Promise<ApplicationBackendEntryResponseDto>;
  login: (request: LoginRequest) => Promise<LoginResponseDto>;
  logout: () => void;
  refreshSession: (options?: { preserveTabs?: boolean }) => Promise<void>;
  setUser: (user: CurrentUserDto | null) => void;
  switchPlatform: (request?: SwitchPlatformRequest) => Promise<SwitchWorkspaceResponseDto>;
  switchWorkspace: (request: SwitchWorkspaceRequest) => Promise<SwitchWorkspaceResponseDto>;
  user: CurrentUserDto | null;
}

export interface WorkspaceStoreState {
  availableWorkspaces: WorkspaceDto[];
  branding: BrandingDto | null;
  clearWorkspace: () => void;
  currentWorkspace: CurrentWorkspaceDto | null;
  setAvailableWorkspaces: (availableWorkspaces: WorkspaceDto[]) => void;
  setWorkspaceState: (workspace: {
    branding?: BrandingDto | null;
    currentWorkspace?: CurrentWorkspaceDto | null;
  }) => void;
}

export interface MenuStoreState {
  menus: MenuTreeNodeDto[];
  setMenus: (menus: MenuTreeNodeDto[]) => void;
}

export interface PermissionStoreState {
  hasPermission: (permissionCode?: string | string[]) => boolean;
  permissionCodes: string[];
  setPermissionCodes: (permissionCodes: string[]) => void;
}

export interface ThemeStoreState {
  setTheme: (theme: ThemeMode) => void;
  theme: ThemeMode;
}

export interface UiPreferenceStoreState {
  fontFamilyKey: UiFontFamilyKey;
  preferences: UiPreferences;
  resetPreferences: () => void;
  scalePercent: UiScalePercent;
  setFontFamilyKey: (fontFamilyKey: UiFontFamilyKey) => void;
  setPreferences: (preferences: UiPreferences) => void;
  setScalePercent: (scalePercent: UiScalePercent) => void;
}

export interface TabStoreState {
  addTab: (tab: OpenTab) => void;
  activateWorkspaceGroup: (workspaceKey: string, tabs?: OpenTab[]) => void;
  clearPageCache: (cacheKey: string) => void;
  clearTabCache: (tabPath: string) => void;
  closeTab: (path: string) => void;
  ensureRouteTab: (tab: Omit<OpenTab, 'refreshToken'>) => void;
  getCloseFallback: (path: string, homePath: string) => string;
  getPageCache: <T>(cacheKey: string) => T | undefined;
  openTabs: OpenTab[];
  pageCache: PageCache;
  refreshTab: (path: string) => void;
  resetTabs: (tabs?: OpenTab[]) => void;
  setOpenTabs: (tabs: OpenTab[]) => void;
  setPageCache: (cacheKey: string, value: unknown) => void;
    activeWorkspaceKey: string;
    workspaceActivationVersion: number;
    workspaceTabGroups: Record<string, OpenTab[]>;
}
