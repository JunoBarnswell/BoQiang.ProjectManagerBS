import type { MenuTreeNodeDto } from '../system/system.types';

import type { BrandingDto, CurrentUserDto, CurrentWorkspaceDto, SwitchWorkspaceResponseDto } from './auth.types';

export interface TenantListItemDto {
  id: string;
  tenantCode: string;
  tenantName: string;
  shortName?: string | null;
  status: string;
  expiredAt?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  configJson?: string | null;
  remark?: string | null;
}

export interface TenantUpsertRequest {
  tenantCode: string;
  tenantName: string;
  shortName?: string | null;
  status: string;
  expiredAt?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  configJson?: string | null;
  remark?: string | null;
}

export interface ApplicationListItemDto {
  id: string;
  appCode: string;
  appName: string;
  appType: string;
  icon?: string | null;
  defaultRoutePath?: string | null;
  adminDefaultRoutePath?: string | null;
  runtimeDefaultRoutePath?: string | null;
  status: string;
  version?: string | null;
  remark?: string | null;
}

export interface ApplicationUpsertRequest {
  appCode: string;
  appName: string;
  appType: string;
  icon?: string | null;
  defaultRoutePath?: string | null;
  adminDefaultRoutePath?: string | null;
  runtimeDefaultRoutePath?: string | null;
  status: string;
  version?: string | null;
  remark?: string | null;
}

export interface TenantAppListItemDto {
  id: string;
  tenantId: string;
  tenantName: string;
  appCode: string;
  appName: string;
  status: string;
  systemName?: string | null;
  logoFileId?: string | null;
  faviconFileId?: string | null;
  primaryColor?: string | null;
  expiredAt?: string | null;
  configJson?: string | null;
  remark?: string | null;
}

export interface TenantAppCatalogItemDto {
  appCode: string;
  appName: string;
  appType: string;
  icon?: string | null;
  defaultRoutePath?: string | null;
  version?: string | null;
  installed: boolean;
  tenantAppId?: string | null;
  tenantAppStatus?: string | null;
  systemName?: string | null;
  primaryColor?: string | null;
  expiredAt?: string | null;
}

export interface TenantAppInstallRequest {
  systemName?: string | null;
  logoFileId?: string | null;
  faviconFileId?: string | null;
  primaryColor?: string | null;
  expiredAt?: string | null;
  configJson?: string | null;
  remark?: string | null;
}

export interface TenantAppUpsertRequest {
  tenantId: string;
  appCode: string;
  status: string;
  systemName?: string | null;
  logoFileId?: string | null;
  faviconFileId?: string | null;
  primaryColor?: string | null;
  expiredAt?: string | null;
  configJson?: string | null;
  remark?: string | null;
}

export interface UserTenantMembershipDto {
  id: string;
  userId: string;
  userName: string;
  displayName: string;
  tenantId: string;
  tenantName: string;
  deptId?: string | null;
  deptName?: string | null;
  positionId?: string | null;
  positionName?: string | null;
  isTenantAdmin: boolean;
  isDefault: boolean;
  status: string;
  remark?: string | null;
}

export interface UserTenantMembershipUpsertRequest {
  userId: string;
  tenantId: string;
  deptId?: string | null;
  positionId?: string | null;
  isTenantAdmin: boolean;
  isDefault: boolean;
  status: string;
  remark?: string | null;
}

export interface UserAppRoleDto {
  id: string;
  userId: string;
  userName: string;
  displayName: string;
  tenantId: string;
  tenantName: string;
  appCode: string;
  appName: string;
  roleId: string;
  roleName: string;
  isDefault: boolean;
  remark?: string | null;
}

export interface UserAppRoleUpsertRequest {
  userId: string;
  tenantId: string;
  appCode: string;
  roleId: string;
  isDefault: boolean;
  remark?: string | null;
}

export interface ApplicationBackendEntryRequest {
  tenantId: string;
  source?: string | null;
}

export interface ApplicationBackendEntryResponseDto extends Pick<SwitchWorkspaceResponseDto, 'defaultRoutePath'> {
  branding: BrandingDto;
  currentWorkspace: CurrentWorkspaceDto;
  menus: MenuTreeNodeDto[];
  permissionCodes: string[];
  user: CurrentUserDto;
}
