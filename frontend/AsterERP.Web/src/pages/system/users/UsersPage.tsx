import { useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  batchDeleteUsers,
  batchUpdateUserStatus,
  createUser,
  deleteUser,
  getDepartments,
  getDepartmentTree,
  getPositions,
  getRoles,
  getUser,
  getUsers,
  resetUserPassword,
  updateUser,
} from '../../../api/system/system-management.api';
import type { PositionListItemDto, RoleListItemDto, UserEmploymentDto, UserListItemDto, UserResetPasswordRequest, UserUpsertRequest } from '../../../api/system/system.types';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { LOOKUP_STALE_TIME_MS, STATIC_LOOKUP_STALE_TIME_MS } from '../../../core/query/cacheDurations';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../../core/state/workspaceStore';
import { usePrintLauncher } from '../../../features/print-center/hooks/usePrintLauncher';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useCrudResource } from '../../../shared/components/crud-page/useCrudResource';
import { useMessage } from '../../../shared/feedback/useMessage';
import type { FormFieldConfig, FormOption } from '../../../shared/forms/formTypes';
import { ModalForm } from '../../../shared/forms/ModalForm';
import { SearchForm } from '../../../shared/forms/SearchForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { TreeFilterPanel } from '../../../shared/tree/TreeFilterPanel';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { getScopedQueryKey } from '../../dashboard/dashboardModel';
import { WorkflowBusinessActions } from '../../workflows/WorkflowBusinessActions';

import { UserEmploymentEditor } from './UserEmploymentEditor';

interface UserSearchState {
  deptId: string;
  keyword: string;
  positionId: string;
  roleId: string;
  status: string;
  includeDescendants: boolean;
}

type UserFormState = UserUpsertRequest;

const defaultFormState: UserFormState = {
  dataScope: 'DEPT',
  deptId: '',
  displayName: '',
  email: '',
  employments: [],
  isAdmin: false,
  password: '123456',
  phoneNumber: '',
  positionId: '',
  remark: '',
  roleIds: [],
  status: 'Enabled',
  userName: ''
};

function buildRoleOptions(roles: RoleListItemDto[]): FormOption[] {
  return roles.map((role) => ({ label: `${role.roleName} (${role.roleCode})`, value: role.id }));
}

function buildPositionOptions(positions: PositionListItemDto[]): FormOption[] {
  return positions.map((position) => ({ label: `${position.positionName} (${position.positionCode})`, value: position.id }));
}

function normalizeUserEmployments(user: UserListItemDto | UserUpsertRequest, fallbackDeptId?: string | null, fallbackPositionId?: string | null): UserEmploymentDto[] {
  const existing = Array.isArray(user.employments) ? user.employments : [];
  if (existing.length > 0) {
    return existing.map((item, index) => ({ ...item, sortOrder: item.sortOrder || index + 1 }));
  }

  const deptId = user.deptId || fallbackDeptId || '';
  const positionId = user.positionId || fallbackPositionId || '';
  if (!deptId || !positionId) {
    return [];
  }

  return [{
    deptId,
    deptName: null,
    employmentName: null,
    id: null,
    isPrimary: true,
    positionId,
    positionName: null,
    sortOrder: 1,
    status: user.status || 'Enabled'
  }];
}

function prepareUserSubmitRequest(value: UserFormState): UserFormState {
  const employments = normalizeUserEmployments(value)
    .map((item, index) => ({ ...item, sortOrder: index + 1 }))
    .filter((item) => item.deptId && item.positionId);
  const primary = employments.find((item) => item.isPrimary && item.status === 'Enabled') ?? employments.find((item) => item.status === 'Enabled') ?? employments[0];

  return {
    ...value,
    deptId: primary?.deptId ?? value.deptId ?? null,
    employments,
    positionId: primary?.positionId ?? value.positionId ?? null
  };
}

function getEmploymentValidationError(employments: UserEmploymentDto[], translate: (key: string) => string): string | null {
  const enabledRows = employments.filter((item) => item.status === 'Enabled');
  if (enabledRows.length === 0 || enabledRows.some((item) => !item.deptId || !item.positionId)) {
    return translate('page.systemUsers.error.employmentRequired');
  }

  if (enabledRows.filter((item) => item.isPrimary).length !== 1) {
    return translate('page.systemUsers.error.primaryEmploymentRequired');
  }

  const keys = new Set<string>();
  for (const row of enabledRows) {
    const key = `${row.deptId}:${row.positionId}`;
    if (keys.has(key)) {
      return translate('page.systemUsers.error.duplicateEmployment');
    }
    keys.add(key);
  }

  return null;
}

export function UsersPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const message = useMessage();
  const queryClient = useQueryClient();
  const printLauncher = usePrintLauncher();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const systemKey = currentWorkspace
    ? currentWorkspace.systemId || currentWorkspace.workspaceId || `${currentWorkspace.tenantId}:${currentWorkspace.appCode}`
    : 'none';

  const [pageState, setPageState] = useTabPageState(
    {
      isTreeCollapsed: false,
      passwordForm: { password: '' },
      passwordModalOpen: false,
      passwordTargetUser: null as UserListItemDto | null,
      selectedRowKeys: [] as string[]
    },
    { cacheKey: 'users-page-extra' }
  );

  const resource = useCrudResource<UserListItemDto, UserUpsertRequest, UserUpsertRequest, UserSearchState>({
    api: {
      list: getUsers,
      create: createUser,
      update: updateUser,
      delete: deleteUser,
      batchDelete: batchDeleteUsers
    },
    defaultFormState,
    defaultSearchState: { deptId: '', includeDescendants: false, keyword: '', positionId: '', roleId: '', status: '' },
    getId: (item) => item.id,
    itemName: translate('page.systemUsers.itemName'),
    queryKeyPrefix: [...queryKeys.systemManagement.usersRoot(), 'system', systemKey]
  });

  const departmentTreeQuery = useApiQuery({
    queryFn: ({ signal }) => getDepartmentTree(signal),
    queryKey: getScopedQueryKey(queryKeys.systemManagement.departmentTree(), systemKey),
    staleTimeMs: STATIC_LOOKUP_STALE_TIME_MS
  });
  const rolesLookupQuery = useApiQuery({
    queryFn: ({ signal }) => getRoles({ keyword: '', pageIndex: 1, pageSize: 200 }, signal),
    queryKey: getScopedQueryKey(queryKeys.systemManagement.roles(1, 200, ''), systemKey),
    staleTimeMs: LOOKUP_STALE_TIME_MS
  });
  const departmentsLookupQuery = useApiQuery({
    queryFn: ({ signal }) => getDepartments({ pageIndex: 1, pageSize: 500, status: 'Enabled' }, signal),
    queryKey: getScopedQueryKey(queryKeys.systemManagement.departments(1, 500, '', 'Enabled', ''), systemKey),
    staleTimeMs: LOOKUP_STALE_TIME_MS
  });
  const positionsLookupQuery = useApiQuery({
    queryFn: ({ signal }) => getPositions({ pageIndex: 1, pageSize: 500, status: 'Enabled' }, signal),
    queryKey: getScopedQueryKey(queryKeys.systemManagement.positions(1, 500, '', 'Enabled', ''), systemKey),
    staleTimeMs: LOOKUP_STALE_TIME_MS
  });

  const batchStatusMutation = useApiMutation({ mutationFn: ({ ids, status }: { ids: string[]; status: string }) => batchUpdateUserStatus(ids, status) });
  const resetPasswordMutation = useApiMutation({ mutationFn: ({ id, request }: { id: string; request: UserResetPasswordRequest }) => resetUserPassword(id, request) });

  const openEditUser = async (row: UserListItemDto) => {
    try {
      const response = await getUser(row.id);
      resource.handleEdit(response.data, (u) => ({
        ...u,
        dataScope: u.dataScope || 'DEPT',
        employments: normalizeUserEmployments(u),
        password: ''
      }));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemUsers.error.loadDetailFailed')));
    }
  };

  const roleOptions = useMemo(() => buildRoleOptions(rolesLookupQuery.data?.data.items ?? []), [rolesLookupQuery.data?.data.items]);
  const searchPositionOptions = useMemo(
    () => buildPositionOptions((positionsLookupQuery.data?.data.items ?? []).filter((pos) => !resource.search.deptId || pos.deptId === resource.search.deptId)),
    [resource.search.deptId, positionsLookupQuery.data?.data.items]
  );
  const columns: DataTableColumn<UserListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemUsers.column.rowIndex'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (resource.pageIndex - 1) * resource.pageSize + index + 1 },
      {
        key: 'displayName',
        title: translate('page.systemUsers.column.user'),
        width: '220px',
        responsivePriority: 100,
        sortable: true,
        filterable: true,
        filterField: 'displayName',
        filterType: 'text',
        render: (row) => (<><div className="font-medium text-gray-900">{row.displayName}</div><div className="text-xs text-gray-500">{row.userName}</div></>)
      },
      { key: 'deptName', title: translate('page.systemUsers.column.department'), width: '160px', responsivePriority: 80, sortable: false, render: (row) => row.employmentSummary ?? row.deptName ?? row.deptId ?? '-' },
      { key: 'positionName', title: translate('page.systemUsers.column.position'), width: '140px', responsivePriority: 78, sortable: false, render: (row) => row.positionName ?? '-' },
      { key: 'roleNames', title: translate('page.systemUsers.column.role'), width: '180px', responsivePriority: 90, sortable: false, render: (row) => row.roleNames.join(', ') || '-' },
      { key: 'phoneNumber', title: translate('page.systemUsers.column.phoneNumber'), width: '150px', responsivePriority: 75, sortable: true, filterable: true, filterType: 'text', render: (row) => row.phoneNumber ?? '-' },
      {
        key: 'status',
        title: translate('page.systemUsers.column.status'),
        width: '90px',
        align: 'center',
        sortable: true,
        filterable: true,
        filterType: 'select',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('common.enabled'), value: 'Enabled' },
          { label: translate('common.disabled'), value: 'Disabled' }
        ],
        render: (row) => (row.status === 'Enabled' ? translate('common.enabled') : translate('common.disabled'))
      }
    ],
    [resource.pageIndex, resource.pageSize, translate]
  );

  const formFields: FormFieldConfig<UserFormState>[] = useMemo(
    () => [
      { label: translate('page.systemUsers.field.userName'), name: 'userName', placeholder: translate('page.systemUsers.placeholder.userName'), required: true, span: 1, type: 'text', section: translate('page.systemUsers.section.basicInfo') },
      { label: translate('page.systemUsers.field.displayName'), name: 'displayName', placeholder: translate('page.systemUsers.placeholder.displayName'), required: true, span: 1, type: 'text', section: translate('page.systemUsers.section.basicInfo') },
      { label: translate('page.systemUsers.field.phoneNumber'), name: 'phoneNumber', placeholder: translate('page.systemUsers.placeholder.phoneNumber'), span: 1, type: 'text', section: translate('page.systemUsers.section.basicInfo') },
      { label: translate('page.systemUsers.field.password'), name: 'password', placeholder: translate('page.systemUsers.placeholder.password'), span: 1, type: 'text', section: translate('page.systemUsers.section.basicInfo') },
      { label: translate('page.systemUsers.field.status'), name: 'status', options: [{ label: translate('common.enabled'), value: 'Enabled' }, { label: translate('common.disabled'), value: 'Disabled' }], required: true, span: 1, type: 'select', section: translate('page.systemUsers.section.orgAndPermission') },
      { label: translate('page.systemUsers.field.roleIds'), name: 'roleIds', options: roleOptions, span: 2, type: 'checkbox', section: translate('page.systemUsers.section.orgAndPermission') },
      { label: translate('page.systemUsers.field.remark'), name: 'remark', placeholder: translate('page.systemUsers.placeholder.remark'), rows: 3, span: 2, type: 'textarea', section: translate('page.systemUsers.section.remark') }
    ],
    [roleOptions, translate]
  );

  const refreshUsers = async () => {
    await resource.refresh();
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.rolesRoot() });
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.departmentsRoot() });
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.positionsRoot() });
  };

  const handleCreate = () => {
    resource.handleCreate();
    resource.setFormState(prev => ({
      ...prev,
      deptId: resource.search.deptId || prev.deptId,
      employments: normalizeUserEmployments(prev, resource.search.deptId, prev.positionId)
    }));
  };

  const handleSubmitUser = async () => {
    if (!resource.formState.userName || !resource.formState.displayName || (!resource.editingId && !resource.formState.password)) {
      message.error(translate('page.systemUsers.error.completeInfo'));
      return;
    }

    const request = prepareUserSubmitRequest(resource.formState);
    const validationError = getEmploymentValidationError(request.employments ?? [], translate);
    if (validationError) {
      message.error(validationError);
      return;
    }

    if (resource.editingId) {
      await resource.updateMutation.mutateAsync({ id: resource.editingId, request });
      return;
    }

    await resource.createMutation.mutateAsync(request);
  };

  const handleChangeStatus = async (ids: string[], status: 'Enabled' | 'Disabled') => {
    if (ids.length === 0) return;
    try {
      await batchStatusMutation.mutateAsync({ ids, status });
      setPageState(current => ({ ...current, selectedRowKeys: current.selectedRowKeys.filter(id => !ids.includes(id)) }));
      await refreshUsers();
      message.success(status === 'Enabled' ? translate('page.systemUsers.success.enabled') : translate('page.systemUsers.success.disabled'));
    } catch (error) {
      message.error(getErrorMessage(error, status === 'Enabled' ? translate('page.systemUsers.error.enableFailed') : translate('page.systemUsers.error.disableFailed')));
    }
  };

  const handleResetPassword = async () => {
    if (!pageState.passwordTargetUser || !pageState.passwordForm.password.trim()) {
      message.error(translate('page.systemUsers.error.newPasswordRequired'));
      return;
    }
    try {
      await resetPasswordMutation.mutateAsync({ id: pageState.passwordTargetUser.id, request: { password: pageState.passwordForm.password.trim() } });
      setPageState(prev => ({ ...prev, passwordModalOpen: false, passwordTargetUser: null }));
      await refreshUsers();
      message.success(translate('page.systemUsers.success.resetPassword'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemUsers.error.resetPasswordFailed')));
    }
  };

  const actionNode = (
    <div className="flex flex-wrap items-center gap-4">
      <div className="flex items-center gap-2">
        <PermissionButton
          className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors"
          code="system:print:use"
          type="button"
          onClick={() => printLauncher.open({
            conditions: resource.tableQuery.conditions,
            menuCode: 'system:user',
            pageIndex: resource.pageIndex,
            pageSize: resource.pageSize,
            scene: 'list',
            selectedIds: pageState.selectedRowKeys,
            sorts: resource.sorts
          })}
        >
          <AppIcon name="printer" /> {translate('page.systemUsers.action.printList')}
        </PermissionButton>
        <PermissionButton
          className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors"
          code="system:print:edit"
          type="button"
          onClick={() => navigate('/system/print-center/new?menuCode=system:user&scene=list&returnTo=/system/users')}
        >
          <AppIcon name="gear-six" /> {translate('page.systemUsers.action.configureListTemplate')}
        </PermissionButton>
        {pageState.selectedRowKeys.length > 0 && (
          <div className="mr-2 flex items-center gap-1 border-r pr-2 border-gray-300">
            <span className="text-xs text-gray-500 mr-1">{formatMessage(translate('common.selectedCount'), { count: pageState.selectedRowKeys.length })}</span>
            <button className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" onClick={() => void handleChangeStatus(pageState.selectedRowKeys, 'Enabled')}>{translate('common.enabled')}</button>
            <button className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" onClick={() => void handleChangeStatus(pageState.selectedRowKeys, 'Disabled')}>{translate('common.disabled')}</button>
            <button className="text-red-600 hover:bg-red-50 px-2 py-1 rounded text-xs transition-colors" onClick={() => resource.batchDelete(pageState.selectedRowKeys)}>{translate('common.delete')}</button>
          </div>
        )}
        <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void refreshUsers()}>
          <AppIcon name="arrows-clockwise" /> {translate('page.systemUsers.action.refresh')}
        </button>
        <button className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={handleCreate}>
          <AppIcon name="plus" /> {translate('page.systemUsers.action.create')}
        </button>
      </div>
    </div>
  );

  const searchNode = (
    <div className="flex items-start gap-2">
      <button className="px-1 text-gray-500 hover:text-primary-600" onClick={() => setPageState(prev => ({ ...prev, isTreeCollapsed: !prev.isTreeCollapsed }))}>
        <AppIcon name={pageState.isTreeCollapsed ? 'sidebar' : 'sidebar-simple'} />
      </button>
      <div className="min-w-0 flex-1">
        <SearchForm
          fields={[
            { label: translate('page.systemUsers.search.keyword'), name: 'keyword', placeholder: translate('page.systemUsers.search.keywordPlaceholder'), type: 'text' },
            {
              emptyOptionLabel: translate('page.systemUsers.search.allStatus'),
              label: translate('page.systemUsers.search.status'),
              name: 'status',
              options: [
                { label: translate('common.enabled'), value: 'Enabled' },
                { label: translate('common.disabled'), value: 'Disabled' }
              ],
              type: 'select'
            },
            {
              emptyOptionLabel: translate('page.systemUsers.search.allPosition'),
              label: translate('page.systemUsers.search.position'),
              name: 'positionId',
              options: searchPositionOptions,
              type: 'select'
            }
          ]}
          onReset={resource.handleReset}
          onSubmit={(value) => resource.handleSearch(value)}
          onValueChange={(value) => resource.setSearchDraft(value)}
          value={resource.searchDraft}
        />
      </div>
    </div>
  );

  return (
    <CrudPage description={translate('page.systemUsers.description')} title={translate('page.systemUsers.title')} actions={actionNode} searchArea={searchNode}>
      <div className="flex-1 flex gap-3 h-full overflow-hidden">
        {!pageState.isTreeCollapsed && (
          <div className="w-56 bg-white border border-gray-200 rounded-lg shadow-sm flex flex-col shrink-0">
          <TreeFilterPanel
              emptyText={translate('page.systemUsers.tree.empty')}
              error={departmentTreeQuery.isError}
              errorText={translate('page.systemUsers.tree.loadFailed')}
              getKey={(n) => n.id}
              getLabel={(n) => n.deptName}
              getMeta={(n) => n.deptCode}
              getSearchText={(n) => `${n.deptName} ${n.deptCode}`}
              loading={departmentTreeQuery.isLoading}
              nodes={departmentTreeQuery.data?.data ?? []}
              placeholder={translate('page.systemUsers.tree.placeholder')}
              searchKeyword=""
              selectedKey={resource.search.deptId}
              onReset={() => resource.handleSearch({ ...resource.search, deptId: '', includeDescendants: false })}
              onSearchKeywordChange={() => {}}
              onSelect={(key) => resource.handleSearch({ ...resource.search, deptId: key, includeDescendants: true })}
            />
          </div>
        )}
        <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
          <DataTable
            columnSettingsKey="system-users"
            columns={columns}
            emptyText={resource.listQuery.isError ? translate('page.systemUsers.error.loadFailed') : translate('common.empty')}
            loading={resource.listQuery.isLoading}
            fitScreen
            onPageChange={resource.setPageIndex}
            onPageSizeChange={resource.setPageSize}
            onQueryChange={resource.setTableQuery}
            onSortsChange={resource.setSorts}
            pageSizeOptions={[10, 20, 50]}
            pagination={{ current: resource.pageIndex, pageSize: resource.pageSize, total: resource.listQuery.data?.data.total ?? 0 }}
            rowActions={(row) => (
              <TableActions>
                <PermissionButton className="hover:text-primary-600" code="system:user:edit" title={translate('common.edit')} type="button" onClick={() => void openEditUser(row)}>
                  <AppIcon className="text-base" name="pencil-simple" />
                </PermissionButton>
                <PermissionButton className="hover:text-primary-600" code="system:user:reset-password" title={translate('page.systemUsers.action.resetPassword')} type="button" onClick={() => setPageState(prev => ({ ...prev, passwordTargetUser: row, passwordForm: { password: '' }, passwordModalOpen: true }))}>
                  <AppIcon className="text-base" name="key" />
                </PermissionButton>
                <PermissionButton className="hover:text-primary-600" code="system:print:use" title={translate('page.systemUsers.action.printDetail')} type="button" onClick={() => printLauncher.open({
                  conditions: [],
                  detailId: row.id,
                  menuCode: 'system:user',
                  pageIndex: 1,
                  pageSize: 1,
                  scene: 'detail',
                  selectedIds: [],
                  sorts: []
                })}>
                  <AppIcon className="text-base" name="printer" />
                </PermissionButton>
                <PermissionButton className="hover:text-primary-600" code="system:print:edit" title={translate('page.systemUsers.action.configureDetailTemplate')} type="button" onClick={() => navigate('/system/print-center/new?menuCode=system:user&scene=detail&returnTo=/system/users')}>
                  <AppIcon className="text-base" name="gear-six" />
                </PermissionButton>
                <WorkflowBusinessActions businessKey={row.id} businessType="system.user" menuCode="system:user" title={formatMessage(translate('page.systemUsers.action.approvalTitle'), { userName: row.displayName })} />
                <PermissionButton className="hover:text-red-600" code="system:user:delete" title={translate('common.delete')} type="button" onClick={() => resource.handleDelete(row, row.displayName)}>
                  <AppIcon className="text-base" name="trash" />
                </PermissionButton>
              </TableActions>
            )}
            rowKey={(row) => row.id}
            rows={resource.listQuery.data?.data.items ?? []}
            selection={{ selectedRowKeys: pageState.selectedRowKeys, onChange: (keys) => setPageState(prev => ({ ...prev, selectedRowKeys: keys })) }}
            sorts={resource.sorts}
            tableQuery={resource.tableQuery}
          />
        </div>
      </div>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => resource.setModalOpen(false), variant: 'ghost' },
          { label: translate('common.save'), onClick: () => void handleSubmitUser(), type: 'button', variant: 'primary' }
        ]}
        fields={formFields}
        open={resource.modalOpen}
        onClose={() => resource.setModalOpen(false)}
        onValueChange={(name, value) => resource.setFormState(current => ({ ...current, [name]: value }))}
        title={resource.editingId ? translate('page.systemUsers.modal.editTitle') : translate('page.systemUsers.modal.createTitle')}
        value={resource.formState}
      >
        <div className="mb-3">{translate('page.systemUsers.modal.description')}</div>
        <UserEmploymentEditor
          departments={departmentsLookupQuery.data?.data.items ?? []}
          positions={positionsLookupQuery.data?.data.items ?? []}
          translate={translate}
          value={resource.formState.employments ?? []}
          onChange={(employments) => resource.setFormState(current => ({ ...current, employments }))}
        />
      </ModalForm>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => setPageState(prev => ({ ...prev, passwordModalOpen: false })), variant: 'ghost' },
          { label: translate('common.save'), onClick: () => void handleResetPassword(), type: 'button', variant: 'primary' }
        ]}
        fields={[{ label: translate('page.systemUsers.passwordModal.field.password'), name: 'password', placeholder: translate('page.systemUsers.passwordModal.placeholder.password'), required: true, span: 2, type: 'text' }]}
        open={pageState.passwordModalOpen}
        onClose={() => setPageState(prev => ({ ...prev, passwordModalOpen: false }))}
        onValueChange={(name, value) => setPageState(prev => ({ ...prev, passwordForm: { ...prev.passwordForm, [name]: value as string } }))}
        title={pageState.passwordTargetUser ? formatMessage(translate('page.systemUsers.passwordModal.titleWithName'), { userName: pageState.passwordTargetUser.displayName }) : translate('page.systemUsers.passwordModal.title')}
        value={pageState.passwordForm}
      >
        {translate('page.systemUsers.passwordModal.description')}
      </ModalForm>

      {printLauncher.dialog}
    </CrudPage>
  );
}
