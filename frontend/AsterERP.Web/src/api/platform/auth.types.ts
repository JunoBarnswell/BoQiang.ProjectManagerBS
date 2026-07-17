import type { MenuTreeNodeDto , UserEmploymentDto } from '../system/system.types';

export type WorkspaceLevel = 'application' | 'platform';

export interface CurrentUserDto {
  userId: string;
  userName: string;
  displayName: string;
  tenantId?: string | null;
  tenantName?: string | null;
  appCode?: string | null;
  appName?: string | null;
  deptId?: string | null;
  deptIds?: string[];
  employmentId?: string | null;
  employmentName?: string | null;
  employments?: UserEmploymentDto[];
  positionId?: string | null;
  positionIds?: string[];
  roleIds: string[];
  permissionCodes: string[];
  dataScope: string;
  isAdmin: boolean;
  isPlatformAdmin: boolean;
  isTenantAdmin: boolean;
}

export interface LoginRequest {
  password: string;
  userName: string;
}

export interface InitialAdminPasswordRecoveryRequest {
  password: string;
  recoveryCode: string;
  userName: string;
}

export interface ApplicationLoginRequest {
  password: string;
  userName: string;
}

export interface LoginResponseDto {
  accessToken: string;
  availableWorkspaces: WorkspaceDto[];
  currentWorkspace?: CurrentWorkspaceDto | null;
  user: CurrentUserDto;
}

export interface ApplicationLoginResponseDto {
  accessToken: string;
  branding: BrandingDto;
  currentWorkspace: CurrentWorkspaceDto;
  defaultRoutePath?: string | null;
  menus: MenuTreeNodeDto[];
  permissionCodes: string[];
  user: CurrentUserDto;
}

export interface ApplicationLoginBootstrapDto {
  tenantId: string;
  tenantName: string;
  appCode: string;
  appName: string;
  systemName: string;
  status: string;
  databaseBinding: {
    isBound: boolean;
    isReachable: boolean;
    provider?: 'Sqlite' | 'MySql' | 'PostgreSQL' | 'SqlServer' | null;
    displayName?: string | null;
    databaseName?: string | null;
    updatedAt?: string | null;
    canManage: boolean;
    message?: string | null;
  };
}

export interface SessionResponseDto {
  availableWorkspaces: WorkspaceDto[];
  branding?: BrandingDto | null;
  currentWorkspace?: CurrentWorkspaceDto | null;
  menus: MenuTreeNodeDto[];
  permissionCodes: string[];
  user: CurrentUserDto;
}

export interface WorkspaceDto {
  description?: string | null;
  disabledReason?: string | null;
  workspaceId: string;
  tenantId: string;
  tenantName: string;
  appCode: string;
  appName: string;
  logoFileId?: string | null;
  isDefault: boolean;
  isAvailable: boolean;
  status: string;
  systemCode: string;
  systemId: string;
  systemName: string;
  workspaceLevel: WorkspaceLevel;
  defaultRoutePath?: string | null;
  isDatabaseBound: boolean;
  canManageInitialDatabaseBinding: boolean;
}

export interface CurrentWorkspaceDto {
  workspaceId: string;
  tenantId: string;
  tenantName: string;
  appCode: string;
  appName: string;
  systemCode: string;
  systemId: string;
  systemName: string;
  workspaceLevel: WorkspaceLevel;
  defaultRoutePath?: string | null;
}

export interface BrandingDto {
  systemName: string;
  logoFileId?: string | null;
  faviconFileId?: string | null;
  primaryColor: string;
}

export interface SwitchWorkspaceRequest {
  tenantId: string;
  appCode: string;
}

export interface SwitchWorkspaceResponseDto {
  branding: BrandingDto;
  currentWorkspace: CurrentWorkspaceDto;
  defaultRoutePath?: string | null;
  menus: MenuTreeNodeDto[];
  permissionCodes: string[];
  user: CurrentUserDto;
}

export interface SwitchPlatformRequest {
  target?: string | null;
}
