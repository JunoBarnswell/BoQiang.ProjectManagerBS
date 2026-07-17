import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import type { DataTableCondition } from '../../shared/table/tableTypes';
import { buildQueryString } from '../queryString';
import type { QueryViewQueryCondition, QueryViewQueryRequest, QueryViewQuerySort } from '../runtime/query-views.api';
import { queryQueryView } from '../runtime/query-views.api';
import type { BatchDeleteRequest, BatchStatusUpdateRequest, GridPageResult } from '../shared.types';

import type { DepartmentListItemDto, DepartmentTreeNodeDto, DepartmentUpsertRequest, MenuListItemDto, MenuTreeNodeDto, MenuUpsertRequest, PermissionCatalogItemDto, PositionListItemDto, PositionUpsertRequest, RoleListItemDto, RolePermissionUpdateRequest, RoleUpsertRequest, UserEmploymentDto, UserListItemDto, UserResetPasswordRequest, UserRoleUpdateRequest, UserUpsertRequest } from './system.types';

type QueryViewRow = Record<string, unknown>;

function asString(value: unknown, fallback = ''): string {
  return value === undefined || value === null ? fallback : String(value);
}

function asNullableString(value: unknown): string | null {
  const text = asString(value).trim();
  return text ? text : null;
}

function asNumber(value: unknown, fallback = 0): number {
  const numberValue = Number(value);
  return Number.isFinite(numberValue) ? numberValue : fallback;
}

function asBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'number') {
    return value !== 0;
  }

  const text = asString(value).toLowerCase();
  return text === 'true' || text === '1' || text === 'yes';
}

function asStringArray(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.map((item) => String(item)).filter(Boolean);
}

function mapEmployment(value: unknown): UserEmploymentDto | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const row = value as QueryViewRow;
  const deptId = asString(row.deptId);
  const positionId = asString(row.positionId);
  if (!deptId || !positionId) {
    return null;
  }

  return {
    deptId,
    deptName: asNullableString(row.deptName),
    employmentName: asNullableString(row.employmentName),
    id: asNullableString(row.id),
    isPrimary: asBoolean(row.isPrimary),
    positionId,
    positionName: asNullableString(row.positionName),
    sortOrder: asNumber(row.sortOrder, 0),
    status: asString(row.status, 'Enabled')
  };
}

function optionalCondition(field: string, operator: QueryViewQueryCondition['operator'], value: unknown): QueryViewQueryCondition | null {
  if (value === undefined || value === null || value === '') {
    return null;
  }

  return { field, operator, value: value as boolean | number | string };
}

function queryViewRequest(
  pageIndex: number | undefined,
  pageSize: number | undefined,
  conditions: Array<QueryViewQueryCondition | null>,
  filters: DataTableCondition[] = [],
  sorts: QueryViewQueryRequest['sorts'] = []
): QueryViewQueryRequest {
  return {
    conditions: [
      ...conditions.filter((item): item is QueryViewQueryCondition => Boolean(item)),
      ...filters
        .filter((item) => item.field && item.value !== undefined && item.value !== null && item.value !== '')
        .map((item) => ({
          field: item.field,
          operator: item.operator,
          value: item.value as boolean | number | string,
          valueTo: item.valueTo
        }))
    ],
    pageIndex: pageIndex ?? 1,
    pageSize: pageSize ?? 20,
    sorts
  };
}

async function queryViewGrid<TItem>(
  viewCode: string,
  request: QueryViewQueryRequest,
  mapper: (row: QueryViewRow) => TItem,
  signal?: AbortSignal
): Promise<ApiEnvelope<GridPageResult<TItem>>> {
  const response = await queryQueryView(viewCode, request, signal);
  return {
    ...response,
    data: {
      items: response.data.rows.map(mapper),
      total: response.data.total
    }
  };
}

function mapRole(row: QueryViewRow): RoleListItemDto {
  return {
    appCode: asNullableString(row.appCode),
    dataScope: asString(row.dataScope, 'DEPT'),
    id: asString(row.id),
    isEnabled: asBoolean(row.isEnabled),
    permissionCount: asNumber(row.permissionCount),
    remark: asNullableString(row.remark),
    roleCode: asString(row.roleCode),
    roleName: asString(row.roleName),
    tenantId: asNullableString(row.tenantId),
    userCount: asNumber(row.userCount)
  };
}

function mapDepartment(row: QueryViewRow): DepartmentListItemDto {
  return {
    deptCode: asString(row.deptCode),
    deptName: asString(row.deptName),
    id: asString(row.id),
    leaderNames: Array.isArray(row.leaderNames) ? row.leaderNames.map((item) => String(item)) : [],
    leaderUserIds: Array.isArray(row.leaderUserIds) ? row.leaderUserIds.map((item) => String(item)) : [],
    managerName: asNullableString(row.managerName),
    parentId: asNullableString(row.parentId),
    parentName: asNullableString(row.parentName),
    phoneNumber: asNullableString(row.phoneNumber),
    positionCount: asNumber(row.positionCount),
    remark: asNullableString(row.remark),
    sortOrder: asNumber(row.sortOrder),
    status: asString(row.status, 'Enabled'),
    userCount: asNumber(row.userCount)
  };
}

function mapPosition(row: QueryViewRow): PositionListItemDto {
  return {
    deptId: asString(row.deptId),
    deptName: asString(row.deptName),
    id: asString(row.id),
    positionCode: asString(row.positionCode),
    positionLevel: asNullableString(row.positionLevel),
    positionName: asString(row.positionName),
    remark: asNullableString(row.remark),
    sortOrder: asNumber(row.sortOrder),
    status: asString(row.status, 'Enabled'),
    userCount: asNumber(row.userCount)
  };
}

function mapUser(row: QueryViewRow): UserListItemDto {
  const roleNamesText = asString(row.roleNames);
  const employments = Array.isArray(row.employments) ? row.employments.map(mapEmployment).filter((item): item is UserEmploymentDto => Boolean(item)) : [];
  return {
    dataScope: asString(row.dataScope, 'DEPT'),
    deptId: asNullableString(row.deptId),
    deptName: asNullableString(row.deptName),
    displayName: asString(row.displayName),
    email: asNullableString(row.email),
    employments,
    employmentSummary: asNullableString(row.employmentSummary),
    id: asString(row.id),
    isAdmin: asBoolean(row.isAdmin),
    phoneNumber: asNullableString(row.phoneNumber),
    positionId: asNullableString(row.positionId),
    positionName: asNullableString(row.positionName),
    primaryDeptId: asNullableString(row.primaryDeptId),
    primaryEmploymentId: asNullableString(row.primaryEmploymentId),
    primaryPositionId: asNullableString(row.primaryPositionId),
    remark: asNullableString(row.remark),
    roleIds: asStringArray(row.roleIds),
    roleNames: Array.isArray(row.roleNames) ? asStringArray(row.roleNames) : roleNamesText ? roleNamesText.split('、').filter(Boolean) : [],
    status: asString(row.status, 'Enabled'),
    userName: asString(row.userName)
  };
}

export function getMenus(query: {
  appCode?: string;
  filters?: DataTableCondition[];
  includeDescendants?: boolean;
  keyword?: string;
  menuType?: string;
  pageIndex?: number;
  pageSize?: number;
  parentId?: string;
  sorts?: QueryViewQuerySort[];
  status?: string;
  tenantId?: string;
}, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<MenuListItemDto>>> {
  return httpClient.get<GridPageResult<MenuListItemDto>>(
    `/system/menus${buildQueryString({
      appCode: query.appCode,
      filters: query.filters,
      includeDescendants: query.includeDescendants,
      keyword: query.keyword,
      menuType: query.menuType,
      pageIndex: query.pageIndex,
      pageSize: query.pageSize,
      parentId: query.parentId,
      sorts: query.sorts,
      status: query.status,
      tenantId: query.tenantId
    })}`,
    undefined,
    signal
  );
}

export function getMenuTree(query: { appCode?: string; tenantId?: string } = {}, signal?: AbortSignal): Promise<ApiEnvelope<MenuTreeNodeDto[]>> {
  return httpClient.get<MenuTreeNodeDto[]>(`/system/menus/tree${buildQueryString(query)}`, undefined, signal);
}

export function createMenu(request: MenuUpsertRequest): Promise<ApiEnvelope<MenuListItemDto>> {
  return httpClient.post<MenuListItemDto, MenuUpsertRequest>('/system/menus', request);
}

export function getMenu(id: string): Promise<ApiEnvelope<MenuListItemDto>> {
  return httpClient.get<MenuListItemDto>(`/system/menus/${id}`);
}

export function updateMenu(id: string, request: MenuUpsertRequest): Promise<ApiEnvelope<MenuListItemDto>> {
  return httpClient.put<MenuListItemDto, MenuUpsertRequest>(`/system/menus/${id}`, request);
}

export function deleteMenu(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/system/menus/${id}`);
}

export function batchDeleteMenus(ids: string[]): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchDeleteRequest>('/system/menus/batch-delete', { ids });
}

export function batchUpdateMenuStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/system/menus/batch-status', { ids, status });
}

export function getRoles(query: { appCode?: string; filters?: DataTableCondition[]; keyword?: string; pageIndex?: number; pageSize?: number; sorts?: QueryViewQuerySort[]; status?: string; tenantId?: string }, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<RoleListItemDto>>> {
  return queryViewGrid(
    'system_role_default',
    queryViewRequest(query.pageIndex, query.pageSize, [
      optionalCondition('tenantId', 'eq', query.tenantId),
      optionalCondition('appCode', 'eq', query.appCode),
      optionalCondition('__keyword', 'contains', query.keyword),
      optionalCondition('isEnabled', 'eq', query.status === 'Enabled' ? true : query.status === 'Disabled' ? false : undefined)
    ], query.filters ?? [], query.sorts ?? []),
    mapRole,
    signal
  );
}

export function getPermissionCatalog(): Promise<ApiEnvelope<PermissionCatalogItemDto[]>> {
  return httpClient.get<PermissionCatalogItemDto[]>('/system/roles/permissions');
}

export function getRolePermissionCodes(roleId: string): Promise<ApiEnvelope<string[]>> {
  return httpClient.get<string[]>(`/system/roles/${roleId}/permissions`);
}

export function getRolePermissionTree(query: { appCode?: string; tenantId?: string } = {}, signal?: AbortSignal): Promise<ApiEnvelope<MenuTreeNodeDto[]>> {
  return httpClient.get<MenuTreeNodeDto[]>(`/system/roles/permission-tree${buildQueryString(query)}`, undefined, signal);
}

export function createRole(request: RoleUpsertRequest): Promise<ApiEnvelope<RoleListItemDto>> {
  return httpClient.post<RoleListItemDto, RoleUpsertRequest>('/system/roles', request);
}

export function getRole(id: string): Promise<ApiEnvelope<RoleListItemDto>> {
  return httpClient.get<RoleListItemDto>(`/system/roles/${id}`);
}

export function updateRole(id: string, request: RoleUpsertRequest): Promise<ApiEnvelope<RoleListItemDto>> {
  return httpClient.put<RoleListItemDto, RoleUpsertRequest>(`/system/roles/${id}`, request);
}

export function deleteRole(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/system/roles/${id}`);
}

export function batchDeleteRoles(ids: string[]): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchDeleteRequest>('/system/roles/batch-delete', { ids });
}

export function batchUpdateRoleStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/system/roles/batch-status', { ids, status });
}

export function updateRolePermissions(id: string, request: RolePermissionUpdateRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.put<boolean, RolePermissionUpdateRequest>(`/system/roles/${id}/permissions`, request);
}

export function getDepartments(query: {
  filters?: DataTableCondition[];
  includeDescendants?: boolean;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  parentId?: string;
  sorts?: QueryViewQuerySort[];
  status?: string;
}, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<DepartmentListItemDto>>> {
  return queryViewGrid(
    'system_dept_tree',
    queryViewRequest(query.pageIndex, query.pageSize, [
      optionalCondition('__keyword', 'contains', query.keyword),
      optionalCondition('parentId', 'eq', query.parentId),
      optionalCondition('status', 'eq', query.status)
    ], query.filters ?? [], query.sorts ?? []),
    mapDepartment,
    signal
  );
}

export function getDepartmentTree(signal?: AbortSignal): Promise<ApiEnvelope<DepartmentTreeNodeDto[]>> {
  return httpClient.get<DepartmentTreeNodeDto[]>('/system/departments/tree', undefined, signal);
}

export function createDepartment(request: DepartmentUpsertRequest): Promise<ApiEnvelope<DepartmentListItemDto>> {
  return httpClient.post<DepartmentListItemDto, DepartmentUpsertRequest>('/system/departments', request);
}

export function getDepartment(id: string): Promise<ApiEnvelope<DepartmentListItemDto>> {
  return httpClient.get<DepartmentListItemDto>(`/system/departments/${id}`);
}

export function updateDepartment(id: string, request: DepartmentUpsertRequest): Promise<ApiEnvelope<DepartmentListItemDto>> {
  return httpClient.put<DepartmentListItemDto, DepartmentUpsertRequest>(`/system/departments/${id}`, request);
}

export function deleteDepartment(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/system/departments/${id}`);
}

export function batchDeleteDepartments(ids: string[]): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchDeleteRequest>('/system/departments/batch-delete', { ids });
}

export function batchUpdateDepartmentStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/system/departments/batch-status', { ids, status });
}

export function getPositions(query: {
  deptId?: string;
  filters?: DataTableCondition[];
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  sorts?: QueryViewQuerySort[];
  status?: string;
}, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<PositionListItemDto>>> {
  return queryViewGrid(
    'system_position_default',
    queryViewRequest(query.pageIndex, query.pageSize, [
      optionalCondition('deptId', 'eq', query.deptId),
      optionalCondition('__keyword', 'contains', query.keyword),
      optionalCondition('status', 'eq', query.status)
    ], query.filters ?? [], query.sorts ?? []),
    mapPosition,
    signal
  );
}

export function createPosition(request: PositionUpsertRequest): Promise<ApiEnvelope<PositionListItemDto>> {
  return httpClient.post<PositionListItemDto, PositionUpsertRequest>('/system/positions', request);
}

export function getPosition(id: string): Promise<ApiEnvelope<PositionListItemDto>> {
  return httpClient.get<PositionListItemDto>(`/system/positions/${id}`);
}

export function updatePosition(id: string, request: PositionUpsertRequest): Promise<ApiEnvelope<PositionListItemDto>> {
  return httpClient.put<PositionListItemDto, PositionUpsertRequest>(`/system/positions/${id}`, request);
}

export function deletePosition(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/system/positions/${id}`);
}

export function batchDeletePositions(ids: string[]): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchDeleteRequest>('/system/positions/batch-delete', { ids });
}

export function batchUpdatePositionStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/system/positions/batch-status', { ids, status });
}

export function getUsers(query: {
  deptId?: string;
  filters?: DataTableCondition[];
  includeDescendants?: boolean;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
  positionId?: string;
  roleId?: string;
  sorts?: QueryViewQuerySort[];
  status?: string;
}, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<UserListItemDto>>> {
  return queryViewGrid(
    'system_user_default',
    queryViewRequest(query.pageIndex, query.pageSize, [
      optionalCondition('__keyword', 'contains', query.keyword),
      optionalCondition('deptId', 'eq', query.deptId),
      optionalCondition('positionId', 'eq', query.positionId),
      optionalCondition('status', 'eq', query.status)
    ], query.filters ?? [], query.sorts ?? []),
    mapUser,
    signal
  );
}

export function createUser(request: UserUpsertRequest): Promise<ApiEnvelope<UserListItemDto>> {
  return httpClient.post<UserListItemDto, UserUpsertRequest>('/system/users', request);
}

export function getUser(id: string): Promise<ApiEnvelope<UserListItemDto>> {
  return httpClient.get<UserListItemDto>(`/system/users/${id}`);
}

export function updateUser(id: string, request: UserUpsertRequest): Promise<ApiEnvelope<UserListItemDto>> {
  return httpClient.put<UserListItemDto, UserUpsertRequest>(`/system/users/${id}`, request);
}

export function deleteUser(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/system/users/${id}`);
}

export function batchDeleteUsers(ids: string[]): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchDeleteRequest>('/system/users/batch-delete', { ids });
}

export function batchUpdateUserStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/system/users/batch-status', { ids, status });
}

export function updateUserRoles(id: string, request: UserRoleUpdateRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.put<boolean, UserRoleUpdateRequest>(`/system/users/${id}/roles`, request);
}

export function resetUserPassword(id: string, request: UserResetPasswordRequest): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, UserResetPasswordRequest>(`/system/users/${id}/password`, request);
}
