export interface MenuTreeNodeDto {
  appCode: string;
  children: MenuTreeNodeDto[];
  componentName?: string | null;
  configJson?: string | null;
  icon?: string | null;
  id: string;
  menuCode: string;
  menuName: string;
  menuType: string;
  pageCode?: string | null;
  artifactId?: string | null;
  permissionCode?: string | null;
  routePath?: string | null;
  scopeType?: string | null;
  sortOrder: number;
  tenantId: string;
  visible: boolean;
}

export interface MenuListItemDto {
  appCode: string;
  componentName?: string | null;
  configJson?: string | null;
  icon?: string | null;
  id: string;
  menuCode: string;
  menuName: string;
  menuType: string;
  pageCode?: string | null;
  artifactId?: string | null;
  parentCode?: string | null;
  parentMenuName?: string | null;
  permissionCode?: string | null;
  remark?: string | null;
  routePath?: string | null;
  scopeType?: string | null;
  sortOrder: number;
  tenantId: string;
  visible: boolean;
}

export interface MenuUpsertRequest {
  appCode?: string | null;
  componentName?: string | null;
  configJson?: string | null;
  icon?: string | null;
  menuCode: string;
  menuName: string;
  menuType: string;
  pageCode?: string | null;
  artifactId?: string | null;
  parentCode?: string | null;
  permissionCode?: string | null;
  remark?: string | null;
  routePath?: string | null;
  scopeType?: string | null;
  sortOrder: number;
  tenantId?: string | null;
  visible: boolean;
}

export interface RoleListItemDto {
  appCode?: string | null;
  dataScope: string;
  id: string;
  isEnabled: boolean;
  permissionCount: number;
  remark?: string | null;
  roleCode: string;
  roleName: string;
  tenantId?: string | null;
  userCount: number;
}

export interface RoleUpsertRequest {
  appCode?: string | null;
  dataScope: string;
  isEnabled: boolean;
  remark?: string | null;
  roleCode: string;
  roleName: string;
  tenantId?: string | null;
}

export interface PermissionCatalogItemDto {
  isEnabled: boolean;
  moduleName: string;
  permissionCode: string;
  permissionName: string;
}

export interface RolePermissionUpdateRequest {
  permissionCodes: string[];
}

export interface UserListItemDto {
  dataScope: string;
  deptId?: string | null;
  deptName?: string | null;
  displayName: string;
  email?: string | null;
  employments?: UserEmploymentDto[];
  employmentSummary?: string | null;
  id: string;
  isAdmin: boolean;
  password?: string;
  phoneNumber?: string | null;
  positionId?: string | null;
  positionName?: string | null;
  primaryDeptId?: string | null;
  primaryEmploymentId?: string | null;
  primaryPositionId?: string | null;
  remark?: string | null;
  roleIds: string[];
  roleNames: string[];
  status: string;
  userName: string;
}

export interface UserEmploymentDto {
  deptId: string;
  deptName?: string | null;
  employmentName?: string | null;
  id?: string | null;
  isPrimary: boolean;
  positionId: string;
  positionName?: string | null;
  sortOrder: number;
  status: string;
}

export interface UserUpsertRequest {
  dataScope?: string;
  deptId?: string | null;
  displayName: string;
  email?: string | null;
  employments?: UserEmploymentDto[];
  isAdmin: boolean;
  password: string;
  phoneNumber?: string | null;
  positionId?: string | null;
  remark?: string | null;
  roleIds: string[];
  status: string;
  userName: string;
}

export interface UserRoleUpdateRequest {
  roleIds: string[];
}

export interface UserResetPasswordRequest {
  password: string;
}

export interface DepartmentListItemDto {
  deptCode: string;
  deptName: string;
  id: string;
  leaderNames: string[];
  leaderUserIds: string[];
  managerName?: string | null;
  parentId?: string | null;
  parentName?: string | null;
  phoneNumber?: string | null;
  positionCount: number;
  remark?: string | null;
  sortOrder: number;
  status: string;
  userCount: number;
}

export interface DepartmentTreeNodeDto {
  children: DepartmentTreeNodeDto[];
  deptCode: string;
  deptName: string;
  id: string;
  leaderNames: string[];
  leaderUserIds: string[];
  parentId?: string | null;
  sortOrder: number;
  status: string;
}

export interface DepartmentUpsertRequest {
  deptCode: string;
  deptName: string;
  leaderUserIds?: string[];
  managerName?: string | null;
  parentId?: string | null;
  phoneNumber?: string | null;
  remark?: string | null;
  sortOrder: number;
  status: string;
}

export interface PositionListItemDto {
  deptId: string;
  deptName: string;
  id: string;
  positionCode: string;
  positionLevel?: string | null;
  positionName: string;
  remark?: string | null;
  sortOrder: number;
  status: string;
  userCount: number;
}

export interface PositionUpsertRequest {
  deptId: string;
  positionCode: string;
  positionLevel?: string | null;
  positionName: string;
  remark?: string | null;
  sortOrder: number;
  status: string;
}

export interface DictTypeListItemDto {
  dictCode: string;
  dictName: string;
  id: string;
  isEnabled: boolean;
  remark?: string | null;
}

export interface DictTypeUpsertRequest {
  dictCode: string;
  dictName: string;
  isEnabled: boolean;
  remark?: string | null;
}

export interface DictItemListItemDto {
  dictTypeId: string;
  id: string;
  isEnabled: boolean;
  itemLabel: string;
  itemValue: string;
  remark?: string | null;
  sortOrder: number;
}

export interface DictItemUpsertRequest {
  isEnabled: boolean;
  itemLabel: string;
  itemValue: string;
  remark?: string | null;
  sortOrder: number;
}
