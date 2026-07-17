import { useQueryClient } from '@tanstack/react-query';
import { useMemo, type SetStateAction } from 'react';

import {
  systemParameterApi,
  type SystemParameterListItemDto,
  type SystemParameterStatusUpdateRequest,
  type SystemParameterUpsertRequest
} from '../../../api/system/parameters.api';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
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

interface ParameterSearchState {
  category: string;
  keyword: string;
  keywordDraft: string;
  status: string;
}

type ParameterFormState = SystemParameterUpsertRequest;

interface ParametersPageState {
  editingId: string | null;
  formState: ParameterFormState;
  isModalOpen: boolean;
  pageIndex: number;
  pageSize: number;
  searchState: ParameterSearchState;
  selectedRowKeys: string[];
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
}

const defaultFormState: ParameterFormState = {
  category: 'general',
  isEnabled: true,
  paramKey: '',
  paramName: '',
  paramValue: '',
  remark: ''
};

const defaultSearchState: ParameterSearchState = {
  category: '',
  keyword: '',
  keywordDraft: '',
  status: ''
};
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

const paramKeyPattern = /^[A-Za-z][A-Za-z0-9_.:-]*$/;
const categoryPattern = /^[A-Za-z][A-Za-z0-9_-]*$/;
const sensitiveValueMask = '******';

function buildUpsertRequest(formState: ParameterFormState): SystemParameterUpsertRequest {
  return {
    category: formState.category.trim(),
    isEnabled: Boolean(formState.isEnabled),
    paramKey: formState.paramKey.trim(),
    paramName: formState.paramName.trim(),
    paramValue: formState.paramValue.trim(),
    remark: formState.remark?.trim() ?? ''
  };
}

function validateUpsertRequest(request: SystemParameterUpsertRequest, translate: (key: string) => string): string | null {
  if (!request.paramName || !request.paramKey || !request.paramValue || !request.category) {
    return translate('page.systemParameters.validation.completeInfo');
  }

  if (!paramKeyPattern.test(request.paramKey)) {
    return translate('page.systemParameters.validation.paramKeyPattern');
  }

  if (!categoryPattern.test(request.category)) {
    return translate('page.systemParameters.validation.categoryPattern');
  }

  return null;
}

export function ParametersPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [pageState, setPageState] = useTabPageState<ParametersPageState>(
    {
      editingId: null,
      formState: defaultFormState,
      isModalOpen: false,
      pageIndex: 1,
      pageSize: 10,
      searchState: defaultSearchState,
      selectedRowKeys: [],
      sorts: [],
      tableQuery: defaultTableQuery
    },
    { cacheKey: 'parameters-page' }
  );

  const { editingId, formState, isModalOpen, pageIndex, pageSize, searchState, selectedRowKeys, sorts, tableQuery } = pageState;

  const setPageField = <K extends keyof ParametersPageState>(key: K, value: SetStateAction<ParametersPageState[K]>) => {
    setPageState((current) => ({
      ...current,
      [key]: typeof value === 'function' ? (value as (previous: ParametersPageState[K]) => ParametersPageState[K])(current[key]) : value
    }));
  };

  const setEditingId = (value: string | null) => setPageField('editingId', value);
  const setFormState = (value: SetStateAction<ParameterFormState>) => setPageField('formState', value);
  const setIsModalOpen = (value: boolean) => setPageField('isModalOpen', value);
  const setPageIndex = (value: number) => setPageField('pageIndex', value);
  const setSearchState = (value: SetStateAction<ParameterSearchState>) => setPageField('searchState', value);
  const setSelectedRowKeys = (value: SetStateAction<string[]>) => setPageField('selectedRowKeys', value);
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

  const parametersQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) =>
      systemParameterApi.list({
        category: searchState.category,
        filters: tableQuery.conditions,
        keyword: searchState.keyword,
        pageIndex,
        pageSize,
        sorts,
        status: searchState.status
      }, signal),
    queryKey: [
      ...queryKeys.systemParameters.list(pageIndex, pageSize, searchState.keyword, searchState.category, searchState.status, sorts),
      tableQuery
    ]
  });

  const createMutation = useApiMutation({
    mutationFn: (request: SystemParameterUpsertRequest) => systemParameterApi.create(request)
  });

  const updateMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: SystemParameterUpsertRequest }) => systemParameterApi.update(id, request)
  });

  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => systemParameterApi.delete(id)
  });

  const statusMutation = useApiMutation({
    mutationFn: (request: SystemParameterStatusUpdateRequest) => systemParameterApi.batchUpdateStatus(request)
  });

  const rows = parametersQuery.data?.data.items ?? [];
  const total = parametersQuery.data?.data.total ?? 0;

  const refreshParameters = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemParameters.root() });
  };

  const openCreateModal = () => {
    setEditingId(null);
    setFormState(defaultFormState);
    setIsModalOpen(true);
  };

  const openEditModal = (row: SystemParameterListItemDto) => {
    setEditingId(row.id);
    setFormState({
      category: row.category,
      isEnabled: row.isEnabled,
      paramKey: row.paramKey,
      paramName: row.paramName,
      paramValue: row.paramValue,
      remark: row.remark ?? ''
    });
    setIsModalOpen(true);
  };

  const handleReset = () => {
    setPageIndex(1);
    setTableQuery(defaultTableQuery);
    setSearchState(defaultSearchState);
  };

  const handleSave = async () => {
    const request = buildUpsertRequest(formState);
    const validationMessage = validateUpsertRequest(request, translate);
    if (validationMessage) {
      message.error(validationMessage);
      return;
    }

    try {
      if (editingId) {
        await updateMutation.mutateAsync({ id: editingId, request });
        message.success(translate('page.systemParameters.success.update'));
      } else {
        await createMutation.mutateAsync(request);
        message.success(translate('page.systemParameters.success.create'));
      }

      setIsModalOpen(false);
      await refreshParameters();
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemParameters.error.saveFailed')));
    }
  };

  const handleDelete = (row: SystemParameterListItemDto) => {
    confirm({
      title: translate('page.systemParameters.confirm.deleteTitle'),
      content: formatMessage(translate('page.systemParameters.confirm.deleteContent'), { key: row.paramKey, name: row.paramName }),
      onConfirm: async () => {
        try {
          await deleteMutation.mutateAsync(row.id);
          setSelectedRowKeys((current) => current.filter((id) => id !== row.id));
          await refreshParameters();
          message.success(translate('page.systemParameters.success.delete'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemParameters.error.deleteFailed')));
        }
      }
    });
  };

  const handleChangeStatus = async (ids: string[], status: 'Enabled' | 'Disabled') => {
    if (ids.length === 0) {
      message.error(translate('page.systemParameters.error.selectToOperate'));
      return;
    }

    try {
      await statusMutation.mutateAsync({ ids, status });
      setSelectedRowKeys((current) => current.filter((id) => !ids.includes(id)));
      await refreshParameters();
      message.success(status === 'Enabled' ? translate('page.systemParameters.success.enabled') : translate('page.systemParameters.success.disabled'));
    } catch (error) {
      message.error(getErrorMessage(error, status === 'Enabled' ? translate('page.systemParameters.error.enableFailed') : translate('page.systemParameters.error.disableFailed')));
    }
  };

  const columns: DataTableColumn<SystemParameterListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemParameters.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      { key: 'paramName', title: translate('page.systemParameters.column.paramName'), width: '180px', responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'paramKey', title: translate('page.systemParameters.column.paramKey'), width: '220px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      {
        key: 'paramValue',
        title: translate('page.systemParameters.column.paramValue'),
        responsivePriority: 90,
        sortable: true,
        filterable: true,
        filterType: 'text',
        render: (row) => (
          <span className={row.isSensitive ? 'font-mono text-gray-500' : undefined}>
            {row.isSensitive ? row.paramValue || sensitiveValueMask : row.paramValue}
          </span>
        )
      },
      { key: 'category', title: translate('page.systemParameters.column.category'), width: '120px', responsivePriority: 80, sortable: true, filterable: true, filterType: 'text' },
      {
        key: 'isEnabled',
        title: translate('page.systemParameters.column.status'),
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
      { key: 'remark', title: translate('page.systemParameters.column.remark'), width: '180px', responsivePriority: 50, hideBelow: 'xl', sortable: false, render: (row) => row.remark ?? '-' }
    ],
    [pageIndex, pageSize, translate]
  );

  const formFields: FormFieldConfig<ParameterFormState>[] = useMemo(
    () => [
      { label: translate('page.systemParameters.field.paramName'), name: 'paramName', placeholder: translate('page.systemParameters.placeholder.paramName'), required: true, span: 1, type: 'text', section: translate('page.systemParameters.section.basicInfo') },
      { label: translate('page.systemParameters.field.paramKey'), name: 'paramKey', placeholder: translate('page.systemParameters.placeholder.paramKey'), required: true, span: 1, type: 'text', section: translate('page.systemParameters.section.basicInfo') },
      { label: translate('page.systemParameters.field.category'), name: 'category', placeholder: translate('page.systemParameters.placeholder.category'), required: true, span: 1, type: 'text', section: translate('page.systemParameters.section.config') },
      {
        label: translate('page.systemParameters.field.status'),
        name: 'isEnabled',
        required: true,
        span: 1,
        type: 'switch',
        section: translate('page.systemParameters.section.config')
      },
      { label: translate('page.systemParameters.field.paramValue'), name: 'paramValue', placeholder: translate('page.systemParameters.placeholder.paramValue'), required: true, span: 2, type: 'text', section: translate('page.systemParameters.section.config') },
      { label: translate('page.systemParameters.field.remark'), name: 'remark', placeholder: translate('page.systemParameters.placeholder.remark'), rows: 3, span: 2, type: 'textarea', section: translate('page.systemParameters.section.remark') }
    ],
    [translate]
  );

  const actionNode = (
    <div className="flex items-center gap-2">
      {selectedRowKeys.length > 0 && (
        <div className="mr-2 flex items-center gap-1 border-r pr-2 border-gray-300">
          <span className="text-xs text-gray-500 mr-2">{formatMessage(translate('common.selectedCount'), { count: selectedRowKeys.length })}</span>
          <PermissionButton code="system:parameter:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Enabled')}>
            {translate('page.systemParameters.action.enable')}
          </PermissionButton>
          <PermissionButton code="system:parameter:edit" className="text-primary-600 hover:bg-primary-50 px-2 py-1 rounded text-xs transition-colors" type="button" onClick={() => void handleChangeStatus(selectedRowKeys, 'Disabled')}>
            {translate('page.systemParameters.action.disable')}
          </PermissionButton>
        </div>
      )}
      <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void refreshParameters()}>
        <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
      </button>
      <PermissionButton code="system:parameter:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={openCreateModal}>
        <AppIcon name="plus" /> {formatMessage(translate('platform.actions.create'), { itemName: translate('page.systemParameters.itemName') })}
      </PermissionButton>
    </div>
  );

  const searchNode = (
    <SearchForm
      fields={[
        { label: translate('page.systemParameters.field.keyword'), name: 'keywordDraft', placeholder: translate('page.systemParameters.placeholder.keyword'), type: 'text' },
        { label: translate('page.systemParameters.field.category'), name: 'category', placeholder: translate('page.systemParameters.placeholder.categorySearch'), type: 'text' },
        {
          emptyOptionLabel: translate('platform.search.allStatus'),
          label: translate('page.systemParameters.field.status'),
          name: 'status',
          options: [
            { label: translate('common.enabled'), value: 'Enabled' },
            { label: translate('common.disabled'), value: 'Disabled' }
          ],
          type: 'select'
        }
      ]}
      onReset={handleReset}
      onSubmit={(value) => {
        setPageIndex(1);
        setSearchState((current) => ({
          ...current,
          ...value,
          category: value.category.trim(),
          keyword: value.keywordDraft.trim()
        }));
      }}
      onValueChange={(value) => setSearchState((current) => ({ ...current, ...value }))}
      value={searchState}
    />
  );

  return (
    <CrudPage
      description={translate('page.systemParameters.description')}
      eyebrow={translate('nav.systemParameters')}
      title={translate('page.systemParameters.title')}
      actions={actionNode}
      searchArea={searchNode}
    >
      <div className="flex-1 flex gap-3 h-full overflow-hidden">
        <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
          <DataTable
            columnSettingsKey="system-parameters"
            columns={columns}
            emptyText={parametersQuery.isError ? translate('page.systemParameters.error.loadFailed') : translate('common.empty')}
            fitScreen
            loading={parametersQuery.isLoading}
            onPageChange={setPageIndex}
            onPageSizeChange={setPageSize}
            onQueryChange={setTableQuery}
            onSortsChange={setSorts}
            pageSizeOptions={[10, 20, 50]}
            pagination={{
              current: pageIndex,
              pageSize,
              total
            }}
            rowActions={(row) => (
              <TableActions>
                <PermissionButton code="system:parameter:edit" className="hover:text-primary-600 transition-colors" title={translate('common.edit')} type="button" onClick={() => openEditModal(row)}>
                  <AppIcon className="text-base" name="pencil-simple" />
                </PermissionButton>
                <PermissionButton
                  code="system:parameter:edit"
                  className="hover:text-primary-600 transition-colors"
                  title={row.isEnabled ? translate('page.systemParameters.action.disable') : translate('page.systemParameters.action.enable')}
                  type="button"
                  onClick={() => void handleChangeStatus([row.id], row.isEnabled ? 'Disabled' : 'Enabled')}
                >
                  <AppIcon className="text-base" name={row.isEnabled ? 'pause-circle' : 'play-circle'} />
                </PermissionButton>
                <PermissionButton code="system:parameter:delete" className="hover:text-red-600 transition-colors" title={translate('common.delete')} type="button" onClick={() => handleDelete(row)}>
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
          {
            disabled: createMutation.isPending || updateMutation.isPending,
            label: translate('common.save'),
            onClick: () => void handleSave(),
            type: 'button',
            variant: 'primary'
          }
        ]}
        fields={formFields}
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onValueChange={(name, value) =>
          setFormState((currentValue) => ({
            ...currentValue,
            [name]: value as ParameterFormState[keyof ParameterFormState]
          }))
        }
        title={editingId ? formatMessage(translate('platform.modal.edit'), { itemName: translate('page.systemParameters.itemName') }) : formatMessage(translate('platform.modal.create'), { itemName: translate('page.systemParameters.itemName') })}
        value={formState}
      >
        {translate('page.systemParameters.modal.description')}
      </ModalForm>
    </CrudPage>
  );
}
