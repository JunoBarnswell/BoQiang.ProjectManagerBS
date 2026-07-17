import { useQueryClient } from '@tanstack/react-query';
import { useMemo, type SetStateAction } from 'react';
import { useNavigate } from 'react-router-dom';

import {
  batchDeleteRoles,
  batchUpdateRoleStatus,
  createRole,
  deleteRole,
  getRole,
  getRolePermissionCodes,
  getRolePermissionTree,
  getRoles,
  updateRole,
  updateRolePermissions
} from '../../../api/system/system-management.api';
import type { RoleListItemDto, RolePermissionUpdateRequest, RoleUpsertRequest } from '../../../api/system/system.types';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { STATIC_LOOKUP_STALE_TIME_MS } from '../../../core/query/cacheDurations';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../../core/state';
import { usePrintLauncher } from '../../../features/print-center/hooks/usePrintLauncher';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { ModalForm } from '../../../shared/forms/ModalForm';
import { SearchForm } from '../../../shared/forms/SearchForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { WorkflowBusinessActions } from '../../workflows/WorkflowBusinessActions';

interface RoleSearchState {
  appCode: string;
  keyword: string;
  keywordDraft: string;
  status: string;
  tenantId: string;
}

interface RoleFormState extends RoleUpsertRequest {
  permissionCodes: string[];
}

interface RolesPageState {
  editingId: string | null;
  formState: RoleFormState;
  isModalOpen: boolean;
  pageIndex: number;
  pageSize: number;
  searchState: RoleSearchState;
  selectedRowKeys: string[];
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
}

const defaultFormState: RoleFormState = {
  appCode: '',
  dataScope: 'DEPT',
  isEnabled: true,
  permissionCodes: [],
  remark: '',
  roleCode: '',
  roleName: '',
  tenantId: ''
};

const defaultPageSize = 10;
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

export function RolesPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const printLauncher = usePrintLauncher();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const defaultTenantId = currentWorkspace?.tenantId ?? '';
  const defaultAppCode = currentWorkspace?.appCode ?? '';
  const [pageState, setPageState] = useTabPageState<RolesPageState>(
    {
      editingId: null,
      formState: { ...defaultFormState, appCode: defaultAppCode, tenantId: defaultTenantId },
      isModalOpen: false,
      pageIndex: 1,
      pageSize: defaultPageSize,
      searchState: { appCode: defaultAppCode, keyword: '', keywordDraft: '', status: '', tenantId: defaultTenantId },
      selectedRowKeys: [],
      sorts: [],
      tableQuery: defaultTableQuery
    },
    { cacheKey: 'roles-page' }
  );
  const { editingId, formState, isModalOpen, pageIndex, pageSize, searchState, selectedRowKeys, sorts, tableQuery } = pageState;
  const setPageField = <K extends keyof RolesPageState>(key: K, value: SetStateAction<RolesPageState[K]>) => {
    setPageState((current) => ({
      ...current,
      [key]: typeof value === 'function' ? (value as (previous: RolesPageState[K]) => RolesPageState[K])(current[key]) : value
    }));
  };
  const setSearchState = (value: SetStateAction<RoleSearchState>) => setPageField('searchState', value);
  const setSelectedRowKeys = (value: SetStateAction<string[]>) => setPageField('selectedRowKeys', value);
  const setIsModalOpen = (value: boolean) => setPageField('isModalOpen', value);
  const setEditingId = (value: string | null) => setPageField('editingId', value);
  const setFormState = (value: SetStateAction<RoleFormState>) => setPageField('formState', value);
  const setPageIndex = (value: number) => setPageField('pageIndex', value);
  const setPageSize = (value: number) => {
    setPageField('pageSize', value);
    setPageIndex(1);
  };
  const setSorts = (value: DataTableSortRule[]) => {
    setPageState((current) => ({ ...current, pageIndex: 1, sorts: value }));
  };
  const setTableQuery = (value: DataTableQueryState) => {
    setPageState((current) => ({ ...current, pageIndex: 1, tableQuery: value }));
  };

  const rolesQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) =>
      getRoles({
        filters: tableQuery.conditions,
        keyword: searchState.keyword,
        pageIndex,
        pageSize,
        status: searchState.status,
        tenantId: searchState.tenantId,
        appCode: searchState.appCode,
        sorts
      }, signal),
    queryKey: [
      ...queryKeys.systemManagement.roles(pageIndex, pageSize, searchState.keyword, searchState.status, searchState.tenantId, searchState.appCode, sorts),
      tableQuery
    ]
  });

  const permissionTreeQuery = useApiQuery({
    queryFn: () => getRolePermissionTree({ appCode: searchState.appCode, tenantId: searchState.tenantId }),
    queryKey: queryKeys.systemManagement.permissionTree(searchState.tenantId, searchState.appCode),
    staleTimeMs: STATIC_LOOKUP_STALE_TIME_MS
  });

  const createMutation = useApiMutation({
    mutationFn: (request: RoleUpsertRequest) => createRole(request)
  });

  const updateMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: RoleUpsertRequest }) => updateRole(id, request)
  });

  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => deleteRole(id)
  });
  const batchDeleteMutation = useApiMutation({
    mutationFn: (ids: string[]) => batchDeleteRoles(ids)
  });
  const batchStatusMutation = useApiMutation({
    mutationFn: ({ ids, status }: { ids: string[]; status: string }) => batchUpdateRoleStatus(ids, status)
  });

  const updatePermissionMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: RolePermissionUpdateRequest }) => updateRolePermissions(id, request)
  });

  const columns: DataTableColumn<RoleListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemRoles.column.rowIndex'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      { key: 'appCode', title: translate('page.systemRoles.column.appCode'), width: '90px', responsivePriority: 98, sortable: true, filterable: true, filterType: 'text', render: (row) => row.appCode ?? '-' },
      { key: 'roleName', title: translate('page.systemRoles.column.roleName'), width: '180px', responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'roleCode', title: translate('page.systemRoles.column.roleCode'), width: '160px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      {
        key: 'dataScope',
        title: translate('page.systemRoles.column.dataScope'),
        width: '160px',
        responsivePriority: 90,
        sortable: true,
        filterable: true,
        filterType: 'select',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('page.systemRoles.dataScope.all'), value: 'ALL' },
          { label: translate('page.systemRoles.dataScope.deptAndChild'), value: 'DEPT_AND_CHILD' },
          { label: translate('page.systemRoles.dataScope.dept'), value: 'DEPT' },
          { label: translate('page.systemRoles.dataScope.self'), value: 'SELF' },
          { label: translate('page.systemRoles.dataScope.custom'), value: 'CUSTOM' }
        ],
        render: (row) => row.dataScope
      },
      { key: 'permissionCount', title: translate('page.systemRoles.column.permissionCount'), width: '90px', align: 'center', responsivePriority: 80, sortable: false },
      {
        key: 'isEnabled',
        title: translate('page.systemRoles.column.status'),
        width: '90px',
        align: 'center',
        sortable: true,
        filterable: true,
        filterType: 'boolean',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('common.enabled'), value: true },
          { label: translate('common.disabled'), value: false }
        ],
        render: (row) => (row.isEnabled ? translate('common.enabled') : translate('common.disabled'))
      },
      { key: 'remark', title: translate('page.systemRoles.column.remark'), width: '180px', hideBelow: 'xl', responsivePriority: 65, sortable: false, filterable: false, render: (row) => row.remark ?? '-' }
    ],
    [pageIndex, pageSize, translate]
  );

  const formFields: FormFieldConfig<RoleFormState>[] = useMemo(
    () => [
      { label: translate('page.systemRoles.field.roleName'), name: 'roleName', placeholder: translate('page.systemRoles.placeholder.roleName'), required: true, span: 1, type: 'text', section: translate('page.systemRoles.section.basicInfo') },
      { label: translate('page.systemRoles.field.roleCode'), name: 'roleCode', placeholder: translate('page.systemRoles.placeholder.roleCode'), required: true, span: 1, type: 'text', section: translate('page.systemRoles.section.basicInfo') },
      { label: translate('page.systemRoles.field.tenantId'), name: 'tenantId', placeholder: translate('page.systemRoles.placeholder.tenantId'), required: true, span: 1, type: 'text', section: translate('page.systemRoles.section.workspace') },
      { label: translate('page.systemRoles.field.appCode'), name: 'appCode', placeholder: translate('page.systemRoles.placeholder.appCode'), required: true, span: 1, type: 'text', section: translate('page.systemRoles.section.workspace') },
      {
        label: translate('page.systemRoles.field.dataScope'),
        name: 'dataScope',
        options: [
          { label: translate('page.systemRoles.dataScope.all'), value: 'ALL' },
          { label: translate('page.systemRoles.dataScope.deptAndChild'), value: 'DEPT_AND_CHILD' },
          { label: translate('page.systemRoles.dataScope.dept'), value: 'DEPT' },
          { label: translate('page.systemRoles.dataScope.self'), value: 'SELF' },
          { label: translate('page.systemRoles.dataScope.custom'), value: 'CUSTOM' }
        ],
        required: true,
        span: 1,
        type: 'select',
        section: translate('page.systemRoles.section.permissions')
      },
      {
        label: translate('page.systemRoles.field.status'),
        name: 'isEnabled',
        options: [
          { label: translate('common.enabled'), value: 'true' },
          { label: translate('common.disabled'), value: 'false' }
        ],
        required: true,
        span: 1,
        type: 'select',
        section: translate('page.systemRoles.section.permissions')
      },
      {
        label: translate('page.systemRoles.field.permissionCodes'),
        name: 'permissionCodes',
        span: 2,
        type: 'permissionTree',
        permissionTreeNodes: permissionTreeQuery.data?.data ?? [],
        section: translate('page.systemRoles.section.permissions')
      },
      { label: translate('page.systemRoles.field.remark'), name: 'remark', placeholder: translate('page.systemRoles.placeholder.remark'), rows: 3, span: 2, type: 'textarea', section: translate('page.systemRoles.section.remark') }
    ],
    [permissionTreeQuery.data?.data, translate]
  );

  const rows = rolesQuery.data?.data.items ?? [];
  const total = rolesQuery.data?.data.total ?? 0;

  const refreshRoles = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.rolesRoot() });
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.permissionTree(searchState.tenantId, searchState.appCode) });
  };

  const openCreateModal = () => {
    setEditingId(null);
    setFormState({ ...defaultFormState, appCode: searchState.appCode || defaultAppCode, tenantId: searchState.tenantId || defaultTenantId });
    setIsModalOpen(true);
  };

  const openEditModal = async (row: RoleListItemDto) => {
    try {
      const [detailResponse, permissionResponse] = await Promise.all([
        getRole(row.id),
        getRolePermissionCodes(row.id)
      ]);
      const detail = detailResponse.data;
      setEditingId(detail.id);
      setFormState({
        appCode: detail.appCode ?? '',
        dataScope: detail.dataScope,
        isEnabled: detail.isEnabled,
        permissionCodes: permissionResponse.data,
        remark: detail.remark ?? '',
        roleCode: detail.roleCode,
        roleName: detail.roleName,
        tenantId: detail.tenantId ?? ''
      });
      setIsModalOpen(true);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemRoles.error.loadDetailFailed')));
    }
  };

  const handleSave = async () => {
    const request: RoleUpsertRequest = {
      appCode: formState.appCode?.trim().toUpperCase() ?? '',
      dataScope: formState.dataScope,
      isEnabled: Boolean(formState.isEnabled),
      remark: formState.remark?.trim() ?? '',
      roleCode: formState.roleCode.trim(),
      roleName: formState.roleName.trim(),
      tenantId: formState.tenantId?.trim() ?? ''
    };

    if (!request.roleCode || !request.roleName || !request.tenantId || !request.appCode) {
      message.error(translate('page.systemRoles.error.completeInfo'));
      return;
    }

    try {
      const response = editingId
        ? await updateMutation.mutateAsync({ id: editingId, request })
        : await createMutation.mutateAsync(request);
      const roleId = editingId ?? response.data.id;
      await updatePermissionMutation.mutateAsync({
        id: roleId,
        request: { permissionCodes: formState.permissionCodes }
      });

      setIsModalOpen(false);
      await refreshRoles();
      message.success(editingId ? translate('page.systemRoles.success.update') : translate('page.systemRoles.success.create'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemRoles.error.saveFailed')));
    }
  };

  const handleChangeStatus = async (ids: string[], status: 'Enabled' | 'Disabled') => {
    if (ids.length === 0) {
      message.error(translate('page.systemRoles.error.selectForAction'));
      return;
    }

    try {
      await batchStatusMutation.mutateAsync({ ids, status });
      setSelectedRowKeys((current) => current.filter((id) => !ids.includes(id)));
      await refreshRoles();
      message.success(status === 'Enabled' ? translate('page.systemRoles.success.enabled') : translate('page.systemRoles.success.disabled'));
    } catch (error) {
      message.error(getErrorMessage(error, status === 'Enabled' ? translate('page.systemRoles.error.enableFailed') : translate('page.systemRoles.error.disableFailed')));
    }
  };

  const handleDelete = async (row: RoleListItemDto) => {
    confirm({
      title: translate('page.systemRoles.confirm.deleteTitle'),
      content: formatMessage(translate('page.systemRoles.confirm.deleteContent'), { name: row.roleName }),
      onConfirm: async () => {
        try {
          await deleteMutation.mutateAsync(row.id);
          await refreshRoles();
          message.success(translate('page.systemRoles.success.delete'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemRoles.error.deleteFailed')));
        }
      }
    });
  };

  const handleBatchDelete = async () => {
    if (selectedRowKeys.length === 0) {
      message.error(translate('page.systemRoles.error.selectForDelete'));
      return;
    }

    const targetIds = [...selectedRowKeys];
    confirm({
      title: translate('page.systemRoles.confirm.deleteTitle'),
      content: formatMessage(translate('page.systemRoles.confirm.batchDeleteContent'), { count: targetIds.length }),
      onConfirm: async () => {
        try {
          await batchDeleteMutation.mutateAsync(targetIds);
          setSelectedRowKeys([]);
          await refreshRoles();
          message.success(formatMessage(translate('page.systemRoles.success.batchDelete'), { count: targetIds.length }));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemRoles.error.batchDeleteFailed')));
        }
      }
    });
  };

  const actionNode = (
    <div className="flex items-center gap-2">
      {selectedRowKeys.length > 0 && (
        <div className="mr-2 flex items-center gap-1 border-r pr-2 border-gray-300">
          <span className="text-xs text-gray-500 mr-2">{formatMessage(translate('common.selectedCount'), { count: selectedRowKeys.length })}</span>
          <PermissionButton code="system:role:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Enabled')}>
            {translate('common.enabled')}
          </PermissionButton>
          <PermissionButton code="system:role:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Disabled')}>
            {translate('common.disabled')}
          </PermissionButton>
          <PermissionButton code="system:role:delete" className="text-red-600 hover:bg-red-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleBatchDelete()}>
            {translate('common.delete')}
          </PermissionButton>
        </div>
      )}
      <PermissionButton
        code="system:print:use"
        className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors"
        type="button"
        onClick={() => printLauncher.open({
          conditions: tableQuery.conditions,
          menuCode: 'system:role',
          pageIndex,
          pageSize,
          scene: 'list',
          selectedIds: selectedRowKeys,
          sorts
        })}
      >
        <AppIcon name="printer" /> {translate('page.systemRoles.action.printList')}
      </PermissionButton>
      <PermissionButton
        code="system:print:edit"
        className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors"
        type="button"
        onClick={() => navigate('/system/print-center/new?menuCode=system:role&scene=list&returnTo=/system/roles')}
      >
        <AppIcon name="gear-six" /> {translate('page.systemRoles.action.configureListTemplate')}
      </PermissionButton>
      <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void refreshRoles()}>
        <AppIcon name="arrows-clockwise" /> {translate('page.systemRoles.action.refresh')}
      </button>
      <PermissionButton code="system:role:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={openCreateModal}>
        <AppIcon name="plus" /> {translate('page.systemRoles.action.create')}
      </PermissionButton>
    </div>
  );

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('page.systemRoles.search.tenantId'), name: 'tenantId', placeholder: translate('page.systemRoles.search.tenantId'), type: 'text' },
        { label: translate('page.systemRoles.search.appCode'), name: 'appCode', placeholder: translate('page.systemRoles.search.appCode'), type: 'text' },
        { label: translate('page.systemRoles.search.keyword'), name: 'keywordDraft', placeholder: translate('page.systemRoles.search.keywordPlaceholder'), type: 'text' },
        {
          emptyOptionLabel: translate('page.systemRoles.search.allStatus'),
          label: translate('page.systemRoles.search.status'),
          name: 'status',
          options: [
            { label: translate('common.enabled'), value: 'Enabled' },
            { label: translate('common.disabled'), value: 'Disabled' }
          ],
          type: 'select'
        }
      ]}
      onReset={() => {
        setPageIndex(1);
        setTableQuery(defaultTableQuery);
        setSearchState({ appCode: defaultAppCode, keyword: '', keywordDraft: '', status: '', tenantId: defaultTenantId });
      }}
      onSubmit={(value) => {
        setPageIndex(1);
        setSearchState((current) => ({
          ...current,
          ...value,
          appCode: value.appCode.trim().toUpperCase(),
          keyword: value.keywordDraft.trim(),
          tenantId: value.tenantId.trim()
        }));
      }}
      onValueChange={(value) => setSearchState((current) => ({ ...current, ...value }))}
      value={searchState}
    />
  );

  return (
    <CrudPage description={translate('page.systemRoles.description')} title={translate('page.systemRoles.title')} actions={actionNode} searchArea={searchNode}>
      <div className="flex-1 flex gap-3 h-full overflow-hidden">
        <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
          <DataTable
            columnSettingsKey="system-roles"
            columns={columns}
            emptyText={rolesQuery.isError ? translate('page.systemRoles.error.loadFailed') : translate('common.empty')}
            fitScreen
            loading={rolesQuery.isLoading}
            onPageChange={setPageIndex}
            onPageSizeChange={setPageSize}
            onQueryChange={setTableQuery}
            onSortsChange={setSorts}
            pageSizeOptions={[10, 20, 50]}
            pagination={{ current: pageIndex, pageSize, total }}
            rowActions={(row) => (
              <TableActions>
                <PermissionButton code="system:role:edit" className="hover:text-primary-600 transition-colors" title={translate('common.edit')} type="button" onClick={() => void openEditModal(row)}>
                  <AppIcon className="text-base" name="pencil-simple" />
                </PermissionButton>
                <PermissionButton code="system:role:grant" className="hover:text-primary-600 transition-colors" title={translate('page.systemRoles.action.grant')} type="button" onClick={() => void openEditModal(row)}>
                  <AppIcon className="text-base" name="shield-check" />
                </PermissionButton>
                <PermissionButton code="system:print:use" className="hover:text-primary-600 transition-colors" title={translate('page.systemRoles.action.printDetail')} type="button" onClick={() => printLauncher.open({
                  conditions: [],
                  detailId: row.id,
                  menuCode: 'system:role',
                  pageIndex: 1,
                  pageSize: 1,
                  scene: 'detail',
                  selectedIds: [],
                  sorts: []
                })}>
                  <AppIcon className="text-base" name="printer" />
                </PermissionButton>
                <PermissionButton code="system:print:edit" className="hover:text-primary-600 transition-colors" title={translate('page.systemRoles.action.configureDetailTemplate')} type="button" onClick={() => navigate('/system/print-center/new?menuCode=system:role&scene=detail&returnTo=/system/roles')}>
                  <AppIcon className="text-base" name="gear-six" />
                </PermissionButton>
                <WorkflowBusinessActions businessKey={row.id} businessType="system.role" menuCode="system:role" title={formatMessage(translate('page.systemRoles.action.approvalTitle'), { roleName: row.roleName })} />
                <PermissionButton code="system:role:delete" className="hover:text-red-600 transition-colors" title={translate('common.delete')} type="button" onClick={() => handleDelete(row)}>
                  <AppIcon className="text-base" name="trash" />
                </PermissionButton>
              </TableActions>
            )}
            rowKey={(row) => row.id}
            rows={rows}
            selection={{ selectedRowKeys, onChange: setSelectedRowKeys }}
            sorts={sorts}
            tableQuery={tableQuery}
          />
        </div>
      </div>

      <ModalForm
        actions={[
          {
            label: translate('common.cancel'),
            onClick: () => setIsModalOpen(false),
            variant: 'ghost'
          },
          {
            label: translate('common.save'),
            onClick: () => void handleSave(),
            type: 'button',
            variant: 'primary',
            loading: createMutation.isPending || updateMutation.isPending || updatePermissionMutation.isPending
          }
        ]}
        fields={formFields}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onValueChange={(name, value) =>
          setFormState((currentValue) => ({
            ...currentValue,
            [name]:
              name === 'isEnabled'
                ? ((value === 'true') as RoleFormState[keyof RoleFormState])
                : (value as RoleFormState[keyof RoleFormState])
          }))
        }
        title={editingId ? translate('page.systemRoles.modal.editTitle') : translate('page.systemRoles.modal.createTitle')}
        value={formState}
      >
        {translate('page.systemRoles.modal.description')}
      </ModalForm>

      {printLauncher.dialog}
    </CrudPage>
  );
}
