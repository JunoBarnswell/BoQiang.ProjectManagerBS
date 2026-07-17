import { useMemo } from 'react';

import {
  batchDeletePositions,
  batchUpdatePositionStatus,
  createPosition,
  deletePosition,
  getDepartments,
  getPosition,
  getPositions,
  updatePosition
} from '../../../api/system/system-management.api';
import type { DepartmentListItemDto, PositionListItemDto, PositionUpsertRequest } from '../../../api/system/system.types';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { LOOKUP_STALE_TIME_MS } from '../../../core/query/cacheDurations';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../../core/state/workspaceStore';
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
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { getScopedQueryKey } from '../../dashboard/dashboardModel';

interface SearchState {
  deptId: string;
  keyword: string;
  status: string;
}

type PositionFormState = PositionUpsertRequest;

const defaultFormState: PositionFormState = {
  deptId: '',
  positionCode: '',
  positionLevel: '',
  positionName: '',
  remark: '',
  sortOrder: 1,
  status: 'Enabled'
};

function buildDepartmentOptions(departments: DepartmentListItemDto[]): FormOption[] {
  return departments.map((department) => ({
    label: `${department.deptName} (${department.deptCode})`,
    value: department.id
  }));
}

export function PositionsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const systemKey = currentWorkspace
    ? currentWorkspace.systemId || currentWorkspace.workspaceId || `${currentWorkspace.tenantId}:${currentWorkspace.appCode}`
    : 'none';
  const [pageState, setPageState] = useTabPageState<{ selectedRowKeys: string[] }>(
    { selectedRowKeys: [] },
    { cacheKey: 'positions-page' }
  );

  const resource = useCrudResource<PositionListItemDto, PositionFormState, PositionFormState, SearchState>({
    api: {
      list: getPositions,
      create: createPosition,
      update: updatePosition,
      delete: deletePosition,
      batchDelete: batchDeletePositions
    },
    defaultFormState,
    defaultSearchState: { deptId: '', keyword: '', status: '' },
    getId: (item) => item.id,
    itemName: translate('page.systemPositions.itemName'),
    queryKeyPrefix: [...queryKeys.systemManagement.positionsRoot(), 'system', systemKey]
  });

  const departmentsQuery = useApiQuery({
    queryFn: ({ signal }) => getDepartments({ pageIndex: 1, pageSize: 500, status: 'Enabled' }, signal),
    queryKey: getScopedQueryKey(queryKeys.systemManagement.departments(1, 500, '', 'Enabled', ''), systemKey),
    staleTimeMs: LOOKUP_STALE_TIME_MS
  });

  const batchStatusMutation = useApiMutation({
    mutationFn: ({ ids, status }: { ids: string[]; status: string }) => batchUpdatePositionStatus(ids, status)
  });

  const openEditPosition = async (row: PositionListItemDto) => {
    try {
      const response = await getPosition(row.id);
      resource.handleEdit(response.data, (p) => ({ ...p, positionLevel: p.positionLevel ?? '' }));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemPositions.error.loadDetailFailed')));
    }
  };

  const departmentOptions = useMemo(() => buildDepartmentOptions(departmentsQuery.data?.data.items ?? []), [departmentsQuery.data?.data.items]);

  const columns: DataTableColumn<PositionListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemPositions.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (resource.pageIndex - 1) * resource.pageSize + index + 1 },
      { key: 'positionName', title: translate('page.systemPositions.column.positionName'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'positionCode', title: translate('page.systemPositions.column.positionCode'), width: '140px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      { key: 'deptName', title: translate('page.systemPositions.column.deptName'), width: '160px', responsivePriority: 85, sortable: false, render: (row) => row.deptName || '-' },
      { key: 'sortOrder', title: translate('page.systemPositions.column.sortOrder'), width: '90px', align: 'center', sortable: true, filterable: true, filterType: 'number' },
      {
        key: 'status',
        title: translate('page.systemPositions.column.status'),
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
      },
      { key: 'remark', title: translate('page.systemPositions.column.remark'), width: '180px', sortable: false, filterable: false, render: (row) => row.remark ?? '-' }
    ],
    [resource.pageIndex, resource.pageSize, translate]
  );

  const formFields: FormFieldConfig<PositionFormState>[] = useMemo(
    () => [
      { label: translate('page.systemPositions.field.positionName'), name: 'positionName', placeholder: translate('page.systemPositions.placeholder.positionName'), required: true, span: 1, type: 'text', section: translate('page.systemPositions.section.basicInfo') },
      { label: translate('page.systemPositions.field.positionCode'), name: 'positionCode', placeholder: translate('page.systemPositions.placeholder.positionCode'), required: true, span: 1, type: 'text', section: translate('page.systemPositions.section.basicInfo') },
      { label: translate('page.systemPositions.field.sortOrder'), name: 'sortOrder', required: true, span: 1, type: 'number', section: translate('page.systemPositions.section.basicInfo') },
      { label: translate('page.systemPositions.field.deptId'), name: 'deptId', options: departmentOptions, required: true, span: 1, type: 'select', section: translate('page.systemPositions.section.organization') },
      {
        label: translate('page.systemPositions.field.status'),
        name: 'status',
        options: [
          { label: translate('common.enabled'), value: 'Enabled' },
          { label: translate('common.disabled'), value: 'Disabled' }
        ],
        required: true,
        span: 2,
        type: 'select',
        section: translate('page.systemPositions.section.organization')
      },
      { label: translate('page.systemPositions.field.remark'), name: 'remark', placeholder: translate('page.systemPositions.placeholder.remark'), rows: 3, span: 2, type: 'textarea', section: translate('page.systemPositions.section.remark') }
    ],
    [departmentOptions, translate]
  );

  const handleChangeStatus = async (ids: string[], status: 'Enabled' | 'Disabled') => {
    if (ids.length === 0) {
      message.error(translate('page.systemPositions.error.selectToOperate'));
      return;
    }
    try {
      await batchStatusMutation.mutateAsync({ ids, status });
      setPageState((current) => ({ ...current, selectedRowKeys: current.selectedRowKeys.filter((id) => !ids.includes(id)) }));
      await resource.refresh();
      message.success(status === 'Enabled' ? translate('page.systemPositions.success.enabled') : translate('page.systemPositions.success.disabled'));
    } catch (error) {
      message.error(getErrorMessage(error, status === 'Enabled' ? translate('page.systemPositions.error.enableFailed') : translate('page.systemPositions.error.disableFailed')));
    }
  };

  const actionNode = (
    <div className="flex items-center gap-2">
      {pageState.selectedRowKeys.length > 0 && (
        <div className="mr-2 flex items-center gap-1 border-r pr-2 border-gray-300">
          <span className="text-xs text-gray-500 mr-2">{formatMessage(translate('common.selectedCount'), { count: pageState.selectedRowKeys.length })}</span>
          <PermissionButton code="system:position:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(pageState.selectedRowKeys, 'Enabled')}>
            {translate('page.systemPositions.action.enable')}
          </PermissionButton>
          <PermissionButton code="system:position:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(pageState.selectedRowKeys, 'Disabled')}>
            {translate('page.systemPositions.action.disable')}
          </PermissionButton>
          <PermissionButton code="system:position:delete" className="text-red-600 hover:bg-red-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => { resource.batchDelete(pageState.selectedRowKeys); setPageState(current => ({ ...current, selectedRowKeys: [] })); }}>
            {translate('common.delete')}
          </PermissionButton>
        </div>
      )}
      <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void resource.refresh()}>
        <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
      </button>
      <PermissionButton code="system:position:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={resource.handleCreate}>
        <AppIcon name="plus" /> {formatMessage(translate('platform.actions.create'), { itemName: translate('page.systemPositions.itemName') })}
      </PermissionButton>
    </div>
  );

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('page.systemPositions.field.keyword'), name: 'keyword', placeholder: translate('page.systemPositions.placeholder.keyword'), type: 'text' },
        { emptyOptionLabel: translate('common.allDepartments'), label: translate('page.systemPositions.field.deptId'), name: 'deptId', options: departmentOptions, type: 'select' },
        {
          emptyOptionLabel: translate('platform.search.allStatus'),
          label: translate('page.systemPositions.field.status'),
          name: 'status',
          options: [
            { label: translate('common.enabled'), value: 'Enabled' },
            { label: translate('common.disabled'), value: 'Disabled' }
          ],
          type: 'select'
        }
      ]}
      onReset={resource.handleReset}
      onSubmit={(value) => resource.handleSearch(value)}
      onValueChange={(value) => resource.setSearchDraft(value)}
      value={resource.searchDraft}
    />
  );

  return (
    <CrudPage title={translate('page.systemPositions.title')} description={translate('page.systemPositions.description')} actions={actionNode} searchArea={searchNode}>
      <div className="flex-1 flex gap-3 h-full overflow-hidden">
        <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
          <DataTable
            columnSettingsKey="system-positions"
            columns={columns}
            emptyText={resource.listQuery.isError ? translate('page.systemPositions.error.loadFailed') : translate('common.empty')}
            fitScreen
            loading={resource.listQuery.isLoading}
            onPageChange={resource.setPageIndex}
            onPageSizeChange={resource.setPageSize}
            onQueryChange={resource.setTableQuery}
            onSortsChange={resource.setSorts}
            pageSizeOptions={[10, 20, 50]}
            pagination={{ current: resource.pageIndex, pageSize: resource.pageSize, total: resource.listQuery.data?.data.total ?? 0 }}
            rowActions={(row) => (
              <TableActions>
                <PermissionButton code="system:position:edit" className="hover:text-primary-600 transition-colors" title={translate('common.edit')} type="button" onClick={() => void openEditPosition(row)}>
                  <AppIcon className="text-base" name="pencil-simple" />
                </PermissionButton>
                <PermissionButton code="system:position:delete" className="hover:text-red-600 transition-colors" title={translate('common.delete')} type="button" onClick={() => void resource.handleDelete(row, row.positionName)}>
                  <AppIcon className="text-base" name="trash" />
                </PermissionButton>
              </TableActions>
            )}
            rowKey={(row) => row.id}
            rows={resource.listQuery.data?.data.items ?? []}
            selection={{ selectedRowKeys: pageState.selectedRowKeys, onChange: (keys) => setPageState((current) => ({ ...current, selectedRowKeys: keys as string[] })) }}
            sorts={resource.sorts}
            tableQuery={resource.tableQuery}
          />
        </div>
      </div>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => resource.setModalOpen(false), variant: 'ghost' },
          { label: translate('common.save'), onClick: () => void resource.handleSubmit(), type: 'button', variant: 'primary', loading: resource.createMutation.isPending || resource.updateMutation.isPending }
        ]}
        fields={formFields}
        open={resource.modalOpen}
        onClose={() => resource.setModalOpen(false)}
        onValueChange={(name, value) => resource.setFormState((current) => ({ ...current, [name]: value as PositionFormState[keyof PositionFormState] }))}
        title={resource.editingId ? formatMessage(translate('platform.modal.edit'), { itemName: translate('page.systemPositions.itemName') }) : formatMessage(translate('platform.modal.create'), { itemName: translate('page.systemPositions.itemName') })}
        value={resource.formState}
      >
        {translate('page.systemPositions.modal.description')}
      </ModalForm>
    </CrudPage>
  );
}
