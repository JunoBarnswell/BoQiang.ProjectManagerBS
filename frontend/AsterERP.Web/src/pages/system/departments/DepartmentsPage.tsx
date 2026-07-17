import { useQueryClient } from '@tanstack/react-query';
import { useMemo, type SetStateAction } from 'react';

import {
  batchDeleteDepartments,
  batchUpdateDepartmentStatus,
  createDepartment,
  deleteDepartment,
  getDepartment,
  getDepartmentTree,
  getDepartments,
  getUsers,
  updateDepartment
} from '../../../api/system/system-management.api';
import type { DepartmentListItemDto, DepartmentUpsertRequest, UserListItemDto } from '../../../api/system/system.types';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { STATIC_LOOKUP_STALE_TIME_MS } from '../../../core/query/cacheDurations';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../../core/state/workspaceStore';
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
import { TreeFilterPanel } from '../../../shared/tree/TreeFilterPanel';
import { flattenTreeToOptions } from '../../../shared/tree/treeUtils';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { getScopedQueryKey } from '../../dashboard/dashboardModel';

interface SearchState {
  keyword: string;
  keywordDraft: string;
  status: string;
}

type DepartmentFormState = DepartmentUpsertRequest;

interface DepartmentsPageState {
  editingId: string | null;
  formState: DepartmentFormState;
  isModalOpen: boolean;
  pageIndex: number;
  pageSize: number;
  searchState: SearchState;
  selectedTreeDeptId: string;
  selectedRowKeys: string[];
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
  treeSearchKeyword: string;
}

const defaultFormState: DepartmentFormState = {
  deptCode: '',
  deptName: '',
  leaderUserIds: [],
  managerName: '',
  parentId: '',
  phoneNumber: '',
  remark: '',
  sortOrder: 1,
  status: 'Enabled'
};

const defaultPageSize = 10;
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

function buildUserOptions(users: UserListItemDto[]) {
  return users.map((user) => ({
    label: `${user.displayName || user.userName} (${user.userName})`,
    value: user.id
  }));
}

function normalizeLeaderUserIds(leaderUserIds?: string[] | null) {
  return (leaderUserIds ?? [])
    .filter((item) => item && item.trim())
    .map((item) => item.trim())
    .filter((item, index, array) => array.findIndex((candidate) => candidate.toLowerCase() === item.toLowerCase()) === index);
}

export function DepartmentsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const systemKey = currentWorkspace
    ? currentWorkspace.systemId || currentWorkspace.workspaceId || `${currentWorkspace.tenantId}:${currentWorkspace.appCode}`
    : 'none';
  const [pageState, setPageState] = useTabPageState<DepartmentsPageState>(
    {
      editingId: null,
      formState: defaultFormState,
      isModalOpen: false,
      pageIndex: 1,
      pageSize: defaultPageSize,
      searchState: { keyword: '', keywordDraft: '', status: '' },
      selectedTreeDeptId: '',
      selectedRowKeys: [],
      sorts: [],
      tableQuery: defaultTableQuery,
      treeSearchKeyword: ''
    },
    { cacheKey: 'departments-page' }
  );
  const { editingId, formState, isModalOpen, pageIndex, pageSize, searchState, selectedTreeDeptId, selectedRowKeys, sorts, tableQuery, treeSearchKeyword } = pageState;
  const setPageField = <K extends keyof DepartmentsPageState>(key: K, value: SetStateAction<DepartmentsPageState[K]>) => {
    setPageState((current) => ({
      ...current,
      [key]: typeof value === 'function' ? (value as (previous: DepartmentsPageState[K]) => DepartmentsPageState[K])(current[key]) : value
    }));
  };
  const setSearchState = (value: SetStateAction<SearchState>) => setPageField('searchState', value);
  const setSelectedRowKeys = (value: SetStateAction<string[]>) => setPageField('selectedRowKeys', value);
  const setIsModalOpen = (value: boolean) => setPageField('isModalOpen', value);
  const setEditingId = (value: string | null) => setPageField('editingId', value);
  const setFormState = (value: SetStateAction<DepartmentFormState>) => setPageField('formState', value);
  const setSelectedTreeDeptId = (value: string) => setPageField('selectedTreeDeptId', value);
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
  const setTreeSearchKeyword = (value: string) => setPageField('treeSearchKeyword', value);

  const departmentsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) =>
      getDepartments({
        filters: tableQuery.conditions,
        includeDescendants: Boolean(selectedTreeDeptId),
        keyword: searchState.keyword,
        pageIndex,
        pageSize,
        status: searchState.status,
        parentId: selectedTreeDeptId,
        sorts
      }, signal),
    queryKey: getScopedQueryKey([
      ...queryKeys.systemManagement.departments(pageIndex, pageSize, searchState.keyword, searchState.status, selectedTreeDeptId, Boolean(selectedTreeDeptId), sorts),
      tableQuery
    ], systemKey)
  });

  const departmentTreeQuery = useApiQuery({
    queryFn: ({ signal }) => getDepartmentTree(signal),
    queryKey: getScopedQueryKey(queryKeys.systemManagement.departmentTree(), systemKey),
    staleTimeMs: STATIC_LOOKUP_STALE_TIME_MS
  });

  const usersQuery = useApiQuery({
    queryFn: ({ signal }) => getUsers({ pageIndex: 1, pageSize: 500, status: 'Enabled' }, signal),
    queryKey: getScopedQueryKey(queryKeys.systemManagement.users(1, 500, '', 'Enabled'), systemKey),
    staleTimeMs: STATIC_LOOKUP_STALE_TIME_MS
  });

  const createMutation = useApiMutation({ mutationFn: (request: DepartmentUpsertRequest) => createDepartment(request) });
  const updateMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: DepartmentUpsertRequest }) => updateDepartment(id, request)
  });
  const deleteMutation = useApiMutation({ mutationFn: (id: string) => deleteDepartment(id) });
  const batchDeleteMutation = useApiMutation({ mutationFn: (ids: string[]) => batchDeleteDepartments(ids) });
  const batchStatusMutation = useApiMutation({
    mutationFn: ({ ids, status }: { ids: string[]; status: string }) => batchUpdateDepartmentStatus(ids, status)
  });

  const parentOptions = useMemo(
    () => [
      { label: translate('page.systemDepartments.rootDepartment'), value: '' },
      ...flattenTreeToOptions(
        departmentTreeQuery.data?.data ?? [],
        (node) => `${node.deptName} (${node.deptCode})`,
        (node) => node.id
      )
    ],
    [departmentTreeQuery.data?.data, translate]
  );
  const userOptions = useMemo(
    () => buildUserOptions(usersQuery.data?.data.items ?? []),
    [usersQuery.data?.data.items]
  );

  const columns: DataTableColumn<DepartmentListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemDepartments.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      { key: 'deptName', title: translate('page.systemDepartments.column.deptName'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'deptCode', title: translate('page.systemDepartments.column.deptCode'), width: '140px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      { key: 'parentName', title: translate('page.systemDepartments.column.parentName'), width: '160px', sortable: false, render: (row) => row.parentName ?? translate('page.systemDepartments.rootDepartment') },
      { key: 'managerName', title: translate('page.systemDepartments.column.managerName'), width: '180px', sortable: true, filterable: true, filterType: 'text', render: (row) => row.leaderNames?.length ? row.leaderNames.join('、') : row.managerName ?? '-' },
      { key: 'phoneNumber', title: translate('page.systemDepartments.column.phoneNumber'), width: '150px', hideBelow: 'lg', sortable: true, filterable: true, filterType: 'text', render: (row) => row.phoneNumber ?? '-' },
      {
        key: 'status',
        title: translate('page.systemDepartments.column.status'),
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
    [pageIndex, pageSize, translate]
  );

  const formFields: FormFieldConfig<DepartmentFormState>[] = useMemo(
    () => [
      { label: translate('page.systemDepartments.field.deptName'), name: 'deptName', placeholder: translate('page.systemDepartments.placeholder.deptName'), required: true, span: 1, type: 'text', section: translate('page.systemDepartments.section.basicInfo') },
      { label: translate('page.systemDepartments.field.deptCode'), name: 'deptCode', placeholder: translate('page.systemDepartments.placeholder.deptCode'), required: true, span: 1, type: 'text', section: translate('page.systemDepartments.section.basicInfo') },
      { label: translate('page.systemDepartments.field.parentId'), name: 'parentId', options: parentOptions, span: 2, type: 'select', section: translate('page.systemDepartments.section.organization') },
      { label: translate('page.systemDepartments.field.leaderUserIds'), name: 'leaderUserIds', options: userOptions, span: 2, type: 'multiselect', section: translate('page.systemDepartments.section.organization'), helpText: translate('page.systemDepartments.help.leaderUserIds') },
      { label: translate('page.systemDepartments.field.phoneNumber'), name: 'phoneNumber', placeholder: translate('page.systemDepartments.placeholder.phoneNumber'), span: 1, type: 'text', section: translate('page.systemDepartments.section.organization') },
      {
        label: translate('page.systemDepartments.field.status'),
        name: 'status',
        options: [
          { label: translate('common.enabled'), value: 'Enabled' },
          { label: translate('common.disabled'), value: 'Disabled' }
        ],
        required: true,
        span: 2,
        type: 'select',
        section: translate('page.systemDepartments.section.organization')
      },
      { label: translate('page.systemDepartments.field.remark'), name: 'remark', placeholder: translate('page.systemDepartments.placeholder.remark'), rows: 3, span: 2, type: 'textarea', section: translate('page.systemDepartments.section.remark') }
    ],
    [parentOptions, translate, userOptions]
  );

  const rows = departmentsQuery.data?.data.items ?? [];
  const total = departmentsQuery.data?.data.total ?? 0;

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.departmentsRoot() });
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemManagement.departmentTree() });
  };

  const openCreateModal = (parentId = '') => {
    setEditingId(null);
    setFormState({ ...defaultFormState, parentId });
    setIsModalOpen(true);
  };

  const openEditModal = async (row: DepartmentListItemDto) => {
    try {
      const response = await getDepartment(row.id);
      const detail = response.data;
      setEditingId(detail.id);
      setFormState({
        deptCode: detail.deptCode,
        deptName: detail.deptName,
        leaderUserIds: detail.leaderUserIds ?? [],
        managerName: detail.managerName ?? '',
        parentId: detail.parentId ?? '',
        phoneNumber: detail.phoneNumber ?? '',
        remark: detail.remark ?? '',
        sortOrder: detail.sortOrder,
        status: detail.status
      });
      setIsModalOpen(true);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemDepartments.error.loadDetailFailed')));
    }
  };

  const handleSave = async () => {
    const request: DepartmentUpsertRequest = {
      deptCode: formState.deptCode.trim(),
      deptName: formState.deptName.trim(),
      leaderUserIds: normalizeLeaderUserIds(formState.leaderUserIds),
      managerName: formState.managerName?.trim() ?? '',
      parentId: formState.parentId?.trim() ?? '',
      phoneNumber: formState.phoneNumber?.trim() ?? '',
      remark: formState.remark?.trim() ?? '',
      sortOrder: Number(formState.sortOrder),
      status: formState.status
    };

    if (!request.deptCode || !request.deptName) {
      message.error(translate('page.systemDepartments.error.completeInfo'));
      return;
    }

    if ((request.leaderUserIds?.length ?? 0) > 3) {
      message.error(translate('page.systemDepartments.error.maxLeaders'));
      return;
    }

    try {
      if (editingId) {
        await updateMutation.mutateAsync({ id: editingId, request });
      } else {
        await createMutation.mutateAsync(request);
      }

      setIsModalOpen(false);
      await refresh();
      message.success(editingId ? translate('page.systemDepartments.success.update') : translate('page.systemDepartments.success.create'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemDepartments.error.saveFailed')));
    }
  };

  const handleChangeStatus = async (ids: string[], status: 'Enabled' | 'Disabled') => {
    if (ids.length === 0) {
      message.error(translate('page.systemDepartments.error.selectToOperate'));
      return;
    }

    try {
      await batchStatusMutation.mutateAsync({ ids, status });
      setSelectedRowKeys((current) => current.filter((id) => !ids.includes(id)));
      await refresh();
      message.success(status === 'Enabled' ? translate('page.systemDepartments.success.enabled') : translate('page.systemDepartments.success.disabled'));
    } catch (error) {
      message.error(getErrorMessage(error, status === 'Enabled' ? translate('page.systemDepartments.error.enableFailed') : translate('page.systemDepartments.error.disableFailed')));
    }
  };

  const handleDelete = async (row: DepartmentListItemDto) => {
    confirm({
      title: translate('page.systemDepartments.confirm.deleteTitle'),
      content: formatMessage(translate('page.systemDepartments.confirm.deleteContent'), { name: row.deptName }),
      onConfirm: async () => {
        try {
          await deleteMutation.mutateAsync(row.id);
          await refresh();
          message.success(translate('page.systemDepartments.success.delete'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemDepartments.error.deleteFailed')));
        }
      }
    });
  };

  const handleBatchDelete = async () => {
    if (selectedRowKeys.length === 0) {
      message.error(translate('page.systemDepartments.error.selectToDelete'));
      return;
    }

    const targetIds = [...selectedRowKeys];
    confirm({
      title: translate('page.systemDepartments.confirm.deleteTitle'),
      content: formatMessage(translate('page.systemDepartments.confirm.batchDeleteContent'), { count: targetIds.length }),
      onConfirm: async () => {
        try {
          await batchDeleteMutation.mutateAsync(targetIds);
          setSelectedRowKeys([]);
          await refresh();
          message.success(formatMessage(translate('page.systemDepartments.success.batchDelete'), { count: targetIds.length }));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemDepartments.error.batchDeleteFailed')));
          await refresh();
        }
      }
    });
  };

  const actionNode = (
    <div className="flex items-center gap-2">
      {selectedRowKeys.length > 0 && (
        <div className="mr-2 flex items-center gap-1 border-r pr-2 border-gray-300">
          <span className="text-xs text-gray-500 mr-2">{formatMessage(translate('common.selectedCount'), { count: selectedRowKeys.length })}</span>
          <PermissionButton code="system:dept:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Enabled')}>
            {translate('page.systemDepartments.action.enable')}
          </PermissionButton>
          <PermissionButton code="system:dept:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Disabled')}>
            {translate('page.systemDepartments.action.disable')}
          </PermissionButton>
          <PermissionButton code="system:dept:delete" className="text-red-600 hover:bg-red-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleBatchDelete()}>
            {translate('common.delete')}
          </PermissionButton>
        </div>
      )}
      <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void refresh()}>
        <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
      </button>
      <PermissionButton code="system:dept:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={() => openCreateModal()}>
        <AppIcon name="plus" /> {formatMessage(translate('platform.actions.create'), { itemName: translate('page.systemDepartments.itemName') })}
      </PermissionButton>
    </div>
  );

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('page.systemDepartments.field.keyword'), name: 'keywordDraft', placeholder: translate('page.systemDepartments.placeholder.keyword'), type: 'text' },
        {
          emptyOptionLabel: translate('platform.search.allStatus'),
          label: translate('page.systemDepartments.field.status'),
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
        setSelectedTreeDeptId('');
        setTableQuery(defaultTableQuery);
        setSearchState({ keyword: '', keywordDraft: '', status: '' });
      }}
      onSubmit={(value) => {
        setPageIndex(1);
        setSearchState((current) => ({ ...current, ...value, keyword: value.keywordDraft.trim() }));
      }}
      onValueChange={(value) => setSearchState((current) => ({ ...current, ...value }))}
      value={searchState}
    />
  );

  return (
    <CrudPage title={translate('page.systemDepartments.title')} description={translate('page.systemDepartments.description')} actions={actionNode} searchArea={searchNode}>
      <div className="flex-1 flex gap-3 h-full overflow-hidden">
        <div className="w-56 bg-white border border-gray-200 rounded-lg shadow-sm flex flex-col shrink-0">
          <TreeFilterPanel
            emptyText={translate('page.systemDepartments.tree.empty')}
            error={departmentTreeQuery.isError}
            errorText={translate('page.systemDepartments.tree.error')}
            getKey={(node) => node.id}
            getLabel={(node) => node.deptName}
            getMeta={(node) => node.deptCode}
            getSearchText={(node) => `${node.deptName} ${node.deptCode}`}
            loading={departmentTreeQuery.isLoading}
            nodes={departmentTreeQuery.data?.data ?? []}
            placeholder={translate('page.systemDepartments.tree.placeholder')}
            searchKeyword={treeSearchKeyword}
            selectedKey={selectedTreeDeptId}
            onReset={() => {
              setSelectedTreeDeptId('');
              setPageIndex(1);
            }}
            onSearchKeywordChange={setTreeSearchKeyword}
            onSelect={(key) => {
              setSelectedTreeDeptId(key);
              setPageIndex(1);
            }}
          />
        </div>

        <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
          <DataTable
            columnSettingsKey="system-departments"
            columns={columns}
            emptyText={departmentsQuery.isError ? translate('page.systemDepartments.error.loadFailed') : translate('common.empty')}
            fitScreen
            loading={departmentsQuery.isLoading}
            onPageChange={setPageIndex}
            onPageSizeChange={setPageSize}
            onQueryChange={setTableQuery}
            onSortsChange={setSorts}
            pageSizeOptions={[10, 20, 50]}
            pagination={{ current: pageIndex, pageSize, total }}
            rowActions={(row) => (
              <TableActions>
                <PermissionButton className="hover:text-primary-600 transition-colors" code="system:dept:add" title={translate('page.systemDepartments.action.addChild')} type="button" onClick={() => openCreateModal(row.id)}>
                  <AppIcon className="text-base" name="plus" />
                </PermissionButton>
                <PermissionButton className="hover:text-primary-600 transition-colors" code="system:dept:edit" title={translate('common.edit')} type="button" onClick={() => void openEditModal(row)}>
                  <AppIcon className="text-base" name="pencil-simple" />
                </PermissionButton>
                <PermissionButton className="hover:text-red-600 transition-colors" code="system:dept:delete" title={translate('common.delete')} type="button" onClick={() => handleDelete(row)}>
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
          { label: translate('common.cancel'), onClick: () => setIsModalOpen(false), variant: 'ghost' },
          { label: translate('common.save'), onClick: () => void handleSave(), type: 'button', variant: 'primary', loading: createMutation.isPending || updateMutation.isPending }
        ]}
        fields={formFields}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: value as DepartmentFormState[keyof DepartmentFormState] }))}
        title={editingId ? formatMessage(translate('platform.modal.edit'), { itemName: translate('page.systemDepartments.itemName') }) : formatMessage(translate('platform.modal.create'), { itemName: translate('page.systemDepartments.itemName') })}
        value={formState}
      >
        {translate('page.systemDepartments.modal.description')}
      </ModalForm>
    </CrudPage>
  );
}

