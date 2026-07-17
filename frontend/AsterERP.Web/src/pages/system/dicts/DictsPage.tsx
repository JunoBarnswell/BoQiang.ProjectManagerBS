import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, type SetStateAction } from 'react';

import {
  createDictItem,
  createDictType,
  deleteDictItem,
  deleteDictType,
  getDictItem,
  getDictItems,
  getDictItemsPage,
  getDictType,
  getDictTypes,
  updateDictItem,
  updateDictType
} from '../../../api/system/dicts.api';
import type { DictItemListItemDto, DictItemUpsertRequest, DictTypeListItemDto, DictTypeUpsertRequest } from '../../../api/system/system.types';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { clearDictOptions, setDictOptions } from '../../../shared/dict/dictStore';
import type { DictOption } from '../../../shared/dict/dictTypes';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { ModalForm } from '../../../shared/forms/ModalForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { NetworkError } from '../../../shared/status/NetworkError';
import { PageEmpty } from '../../../shared/status/PageEmpty';
import { PageLoading } from '../../../shared/status/PageLoading';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface DictTypeSearchState {
  keywordDraft: string;
  keyword: string;
}

type DictTypeFormState = DictTypeUpsertRequest;

type DictItemFormState = DictItemUpsertRequest;

interface DictsPageState {
  editingItemId: string | null;
  editingTypeId: string | null;
  isItemModalOpen: boolean;
  isTypeModalOpen: boolean;
  itemForm: DictItemFormState;
  itemPage: number;
  itemPageSize: number;
  itemSorts: DataTableSortRule[];
  itemTableQuery: DataTableQueryState;
  selectedType: DictTypeListItemDto | null;
  typeTableQuery: DataTableQueryState;
  typeForm: DictTypeFormState;
  typePage: number;
  typeSearch: DictTypeSearchState;
  typeSorts: DataTableSortRule[];
}

const defaultTypeForm: DictTypeFormState = {
  dictCode: '',
  dictName: '',
  isEnabled: true,
  remark: ''
};

const defaultItemForm: DictItemFormState = {
  isEnabled: true,
  itemLabel: '',
  itemValue: '',
  remark: '',
  sortOrder: 1
};

const pageSize = 10;
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

function normalizeItemOptions(items: DictItemListItemDto[]): DictOption[] {
  return items
    .filter((item) => item.isEnabled)
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => ({
      disabled: false,
      label: item.itemLabel,
      value: item.itemValue
    }));
}

export function DictsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [pageState, setPageState] = useTabPageState<DictsPageState>(
    {
      editingItemId: null,
      editingTypeId: null,
      isItemModalOpen: false,
      isTypeModalOpen: false,
      itemForm: defaultItemForm,
      itemPage: 1,
      itemPageSize: 10,
      itemSorts: [],
      itemTableQuery: defaultTableQuery,
      selectedType: null,
      typeTableQuery: defaultTableQuery,
      typeForm: defaultTypeForm,
      typePage: 1,
      typeSearch: { keyword: '', keywordDraft: '' },
      typeSorts: []
    },
    { cacheKey: 'dicts-page' }
  );
  const {
    editingItemId,
    editingTypeId,
    isItemModalOpen,
    isTypeModalOpen,
    itemForm,
    itemPage,
    itemPageSize,
    itemSorts,
    itemTableQuery,
    selectedType,
    typeForm,
    typePage,
    typeSearch,
    typeSorts,
    typeTableQuery
  } = pageState;
  const setPageField = <K extends keyof DictsPageState>(key: K, value: SetStateAction<DictsPageState[K]>) => {
    setPageState((current) => ({
      ...current,
      [key]: typeof value === 'function' ? (value as (previous: DictsPageState[K]) => DictsPageState[K])(current[key]) : value
    }));
  };
  const setSelectedType = (value: SetStateAction<DictTypeListItemDto | null>) => setPageField('selectedType', value);
  const setTypePage = (value: number) => setPageField('typePage', value);
  const setTypeSorts = (value: DataTableSortRule[]) => {
    setPageState((current) => ({ ...current, typePage: 1, typeSorts: value }));
  };
  const setTypeTableQuery = (value: DataTableQueryState) => {
    setPageState((current) => ({ ...current, typePage: 1, typeTableQuery: value }));
  };
  const setItemSorts = (value: DataTableSortRule[]) => {
    setPageState((current) => ({ ...current, itemPage: 1, itemSorts: value }));
  };
  const setItemTableQuery = (value: DataTableQueryState) => {
    setPageState((current) => ({ ...current, itemPage: 1, itemTableQuery: value }));
  };
  const selectType = useCallback((value: DictTypeListItemDto | null) => {
    setPageState((current) => ({
      ...current,
      itemPage: 1,
      itemSorts: [],
      itemTableQuery: defaultTableQuery,
      selectedType: value
    }));
  }, [setPageState]);
  const setTypeSearch = (value: SetStateAction<DictTypeSearchState>) => setPageField('typeSearch', value);
  const setIsTypeModalOpen = (value: boolean) => setPageField('isTypeModalOpen', value);
  const setEditingTypeId = (value: string | null) => setPageField('editingTypeId', value);
  const setTypeForm = (value: SetStateAction<DictTypeFormState>) => setPageField('typeForm', value);
  const setIsItemModalOpen = (value: boolean) => setPageField('isItemModalOpen', value);
  const setEditingItemId = (value: string | null) => setPageField('editingItemId', value);
  const setItemForm = (value: SetStateAction<DictItemFormState>) => setPageField('itemForm', value);

  const typesQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) =>
      getDictTypes({
        filters: typeTableQuery.conditions,
        keyword: typeSearch.keyword,
        pageIndex: typePage,
        pageSize,
        sorts: typeSorts
      }, signal),
    queryKey: [
      ...queryKeys.systemDicts.types(typePage, pageSize, typeSearch.keyword, typeSorts),
      typeTableQuery
    ]
  });

  const typeRows = useMemo(() => typesQuery.data?.data.items ?? [], [typesQuery.data?.data.items]);
  const typeTotal = typesQuery.data?.data.total ?? 0;

  const itemsQuery = useApiQuery({
    enabled: Boolean(selectedType),
    keepPreviousData: true,
    queryFn: ({ signal }) =>
      getDictItemsPage(selectedType?.id ?? '', {
        filters: itemTableQuery.conditions,
        pageIndex: itemPage,
        pageSize: itemPageSize,
        sorts: itemSorts
      }, signal),
    queryKey: ['system-dicts', 'items-page', selectedType?.id ?? 'none', itemPage, itemPageSize, itemSorts, itemTableQuery]
  });

  const itemRows = itemsQuery.data?.data.items ?? [];
  const itemTotal = itemsQuery.data?.data.total ?? 0;

  const createTypeMutation = useApiMutation({
    mutationFn: (request: DictTypeUpsertRequest) => createDictType(request)
  });

  const updateTypeMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: DictTypeUpsertRequest }) => updateDictType(id, request)
  });

  const deleteTypeMutation = useApiMutation({
    mutationFn: (id: string) => deleteDictType(id)
  });

  const createItemMutation = useApiMutation({
    mutationFn: (request: DictItemUpsertRequest) => createDictItem(selectedType?.id ?? '', request)
  });

  const updateItemMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: DictItemUpsertRequest }) => updateDictItem(id, request)
  });

  const deleteItemMutation = useApiMutation({
    mutationFn: (id: string) => deleteDictItem(id)
  });

  useEffect(() => {
    if (!selectedType && typeRows.length > 0) {
      selectType(typeRows[0]);
    }
  }, [selectType, selectedType, typeRows]);

  const syncItemsAndOptionsToStore = async (dictType: DictTypeListItemDto) => {
    const response = await getDictItems(dictType.id);
    queryClient.setQueryData(queryKeys.systemDicts.items(dictType.id), response);
    await queryClient.invalidateQueries({ queryKey: ['system-dicts', 'items-page', dictType.id] });

    if (dictType.isEnabled) {
      setDictOptions(dictType.dictCode, normalizeItemOptions(response.data));
      return;
    }

    clearDictOptions(dictType.dictCode);
  };

  const refreshTypeQuery = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.systemDicts.all });
  };

  const openCreateTypeModal = () => {
    setEditingTypeId(null);
    setTypeForm(defaultTypeForm);
    setIsTypeModalOpen(true);
  };

  const openEditTypeModal = async (row: DictTypeListItemDto) => {
    try {
      const response = await getDictType(row.id);
      const detail = response.data;
      setEditingTypeId(detail.id);
      setTypeForm({
        dictCode: detail.dictCode,
        dictName: detail.dictName,
        isEnabled: detail.isEnabled,
        remark: detail.remark ?? ''
      });
      setIsTypeModalOpen(true);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemDicts.error.loadTypeDetailFailed')));
    }
  };

  const openCreateItemModal = () => {
    if (!selectedType) {
      message.error(translate('page.systemDicts.alert.typeSelectRequired'));
      return;
    }

    setEditingItemId(null);
    setItemForm(defaultItemForm);
    setIsItemModalOpen(true);
  };

  const openEditItemModal = async (row: DictItemListItemDto) => {
    try {
      const response = await getDictItem(row.id);
      const detail = response.data;
      setEditingItemId(detail.id);
      setItemForm({
        isEnabled: detail.isEnabled,
        itemLabel: detail.itemLabel,
        itemValue: detail.itemValue,
        remark: detail.remark ?? '',
        sortOrder: detail.sortOrder
      });
      setIsItemModalOpen(true);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemDicts.error.loadItemDetailFailed')));
    }
  };

  const handleTypeSearch = () => {
    setTypePage(1);
    setTypeSearch((current) => ({ ...current, keyword: current.keywordDraft.trim() }));
  };

  const handleTypeSearchReset = () => {
    setTypePage(1);
    setTypeSorts([]);
    setTypeTableQuery(defaultTableQuery);
    setTypeSearch({ keyword: '', keywordDraft: '' });
  };

  const handleTypeSave = async () => {
    const request: DictTypeUpsertRequest = {
      dictCode: typeForm.dictCode.trim(),
      dictName: typeForm.dictName.trim(),
      isEnabled: typeForm.isEnabled,
      remark: typeForm.remark?.trim() ?? ''
    };

    if (!request.dictCode || !request.dictName) {
      message.error(translate('page.systemDicts.alert.typeRequired'));
      return;
    }

    const editingType = editingTypeId ? typeRows.find((row) => row.id === editingTypeId) : undefined;
    const previousCode = editingType?.dictCode;

    try {
      if (editingTypeId) {
        const response = await updateTypeMutation.mutateAsync({ id: editingTypeId, request });
        setSelectedType((current) => (current?.id === response.data.id ? response.data : current));
        setIsTypeModalOpen(false);
        await refreshTypeQuery();

        if (previousCode && previousCode !== response.data.dictCode) {
          clearDictOptions(previousCode);
        }

        await syncItemsAndOptionsToStore(response.data);
        return;
      }

      const response = await createTypeMutation.mutateAsync(request);
      selectType(response.data);
      setIsTypeModalOpen(false);
      await refreshTypeQuery();
      setDictOptions(response.data.dictCode, []);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemDicts.error.saveFailed')));
    }
  };

  const handleTypeStatusToggle = async (row: DictTypeListItemDto) => {
    const request: DictTypeUpsertRequest = {
      dictCode: row.dictCode,
      dictName: row.dictName,
      isEnabled: !row.isEnabled,
      remark: row.remark ?? ''
    };

    try {
      const response = await updateTypeMutation.mutateAsync({ id: row.id, request });
      setSelectedType((current) => (current?.id === row.id ? response.data : current));
      await refreshTypeQuery();
      await syncItemsAndOptionsToStore(response.data);
      message.success(response.data.isEnabled ? translate('page.systemDicts.success.typeEnabled') : translate('page.systemDicts.success.typeDisabled'));
    } catch (error) {
      message.error(getErrorMessage(error, row.isEnabled ? translate('page.systemDicts.error.typeDisableFailed') : translate('page.systemDicts.error.typeEnableFailed')));
    }
  };

  const handleTypeDelete = async (row: DictTypeListItemDto) => {
    confirm({
      title: translate('page.systemDicts.confirm.deleteTitle'),
      content: translate('page.systemDicts.confirm.deleteType').replace('{name}', row.dictName),
      onConfirm: async () => {
        try {
          await deleteTypeMutation.mutateAsync(row.id);
          clearDictOptions(row.dictCode);
          if (selectedType?.id === row.id) {
            selectType(null);
          }
          await refreshTypeQuery();
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemDicts.error.deleteFailed')));
        }
      }
    });
  };

  const handleItemSave = async () => {
    if (!selectedType) {
      message.error(translate('page.systemDicts.alert.itemRequired'));
      return;
    }

    const request: DictItemUpsertRequest = {
      isEnabled: itemForm.isEnabled,
      itemLabel: itemForm.itemLabel.trim(),
      itemValue: itemForm.itemValue.trim(),
      remark: itemForm.remark?.trim() ?? '',
      sortOrder: itemForm.sortOrder
    };

    if (!request.itemLabel || !request.itemValue) {
      message.error(translate('page.systemDicts.alert.itemRequired'));
      return;
    }

    try {
      if (editingItemId) {
        await updateItemMutation.mutateAsync({ id: editingItemId, request });
      } else {
        await createItemMutation.mutateAsync(request);
      }

      setIsItemModalOpen(false);
      await syncItemsAndOptionsToStore(selectedType);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemDicts.error.saveFailed')));
    }
  };

  const handleItemStatusToggle = async (row: DictItemListItemDto) => {
    if (!selectedType) {
      return;
    }

    const request: DictItemUpsertRequest = {
      isEnabled: !row.isEnabled,
      itemLabel: row.itemLabel,
      itemValue: row.itemValue,
      remark: row.remark ?? '',
      sortOrder: row.sortOrder
    };

    try {
      await updateItemMutation.mutateAsync({ id: row.id, request });
      await syncItemsAndOptionsToStore(selectedType);
      message.success(request.isEnabled ? translate('page.systemDicts.success.itemEnabled') : translate('page.systemDicts.success.itemDisabled'));
    } catch (error) {
      message.error(getErrorMessage(error, row.isEnabled ? translate('page.systemDicts.error.itemDisableFailed') : translate('page.systemDicts.error.itemEnableFailed')));
    }
  };

  const handleItemDelete = async (row: DictItemListItemDto) => {
    if (!selectedType) {
      return;
    }

    confirm({
      title: translate('page.systemDicts.confirm.deleteTitle'),
      content: translate('page.systemDicts.confirm.deleteItem').replace('{name}', row.itemLabel),
      onConfirm: async () => {
        try {
          await deleteItemMutation.mutateAsync(row.id);
          await syncItemsAndOptionsToStore(selectedType);
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemDicts.error.deleteFailed')));
        }
      }
    });
  };

  const typeColumns: DataTableColumn<DictTypeListItemDto>[] = useMemo(
    () => [
      { key: 'dictName', title: translate('page.systemDicts.table.name'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text' },
      { key: 'dictCode', title: translate('page.systemDicts.table.code'), width: '130px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      {
        key: 'isEnabled',
        title: translate('page.systemDicts.table.status'),
        width: '80px',
        align: 'center',
        sortable: true,
        filterable: true,
        filterType: 'boolean',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('page.systemDicts.status.enabled'), value: true },
          { label: translate('page.systemDicts.status.disabled'), value: false }
        ],
        render: (row) => (row.isEnabled ? translate('page.systemDicts.status.enabled') : translate('page.systemDicts.status.disabled'))
      }
    ],
    [translate]
  );

  const itemColumns: DataTableColumn<DictItemListItemDto>[] = useMemo(
    () => [
      { key: 'sortOrder', title: translate('page.systemDicts.table.sort'), width: '90px', align: 'center', responsivePriority: 100, sortable: true, filterable: true, filterType: 'number' },
      { key: 'itemLabel', title: translate('page.systemDicts.table.label'), responsivePriority: 95, sortable: true, filterable: true, filterType: 'text' },
      { key: 'itemValue', title: translate('page.systemDicts.table.code'), width: '150px', responsivePriority: 90, sortable: true, filterable: true, filterType: 'text' },
      {
        key: 'isEnabled',
        title: translate('page.systemDicts.table.status'),
        width: '100px',
        align: 'center',
        sortable: true,
        filterable: true,
        filterType: 'boolean',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('page.systemDicts.status.enabled'), value: true },
          { label: translate('page.systemDicts.status.disabled'), value: false }
        ],
        render: (row) => (row.isEnabled ? translate('page.systemDicts.status.enabled') : translate('page.systemDicts.status.disabled'))
      },
      { key: 'remark', title: translate('page.systemDicts.table.remark'), responsivePriority: 30, hideBelow: 'lg', sortable: false, filterable: true, filterType: 'text' }
    ],
    [translate]
  );

  const typeFormFields: FormFieldConfig<DictTypeFormState>[] = useMemo(
    () => [
      {
        label: translate('page.systemDicts.form.typeCode.label'),
        name: 'dictCode',
        placeholder: translate('page.systemDicts.form.typeCode.placeholder'),
        required: true,
        span: 1,
        type: 'text'
      },
      {
        label: translate('page.systemDicts.form.typeName.label'),
        name: 'dictName',
        placeholder: translate('page.systemDicts.form.typeName.placeholder'),
        required: true,
        span: 1,
        type: 'text'
      },
      {
        label: translate('page.systemDicts.form.typeStatus.label'),
        name: 'isEnabled',
        required: true,
        span: 2,
        type: 'switch'
      },
      {
        label: translate('page.systemDicts.form.typeRemark.label'),
        name: 'remark',
        placeholder: translate('page.systemDicts.form.typeRemark.placeholder'),
        rows: 4,
        span: 2,
        type: 'textarea'
      }
    ],
    [translate]
  );

  const itemFormFields: FormFieldConfig<DictItemFormState>[] = useMemo(
    () => [
      {
        label: translate('page.systemDicts.form.item.label'),
        name: 'itemLabel',
        placeholder: translate('page.systemDicts.form.item.placeholder'),
        required: true,
        span: 1,
        type: 'text'
      },
      {
        label: translate('page.systemDicts.form.value.label'),
        name: 'itemValue',
        placeholder: translate('page.systemDicts.form.value.placeholder'),
        required: true,
        span: 1,
        type: 'text'
      },
      {
        label: translate('page.systemDicts.form.sort.label'),
        name: 'sortOrder',
        required: true,
        span: 1,
        type: 'number'
      },
      {
        label: translate('page.systemDicts.form.typeStatus.label'),
        name: 'isEnabled',
        required: true,
        span: 1,
        type: 'switch'
      },
      {
        label: translate('page.systemDicts.form.remark.label'),
        name: 'remark',
        placeholder: translate('page.systemDicts.form.remark.placeholder'),
        rows: 4,
        span: 2,
        type: 'textarea'
      }
    ],
    [translate]
  );

  return (
    <CrudPage description={translate('page.systemDicts.description')} eyebrow={translate('nav.systemDicts')} title={translate('page.systemDicts.title')}>
      <div className="flex-1 flex gap-3 h-full overflow-hidden">
        {/* Left Panel: Dict Types */}
        <div className="w-1/3 min-w-[360px] bg-white border border-gray-200 rounded-lg shadow-sm flex flex-col shrink-0">
          <div className="p-3 flex items-center justify-between border-b border-gray-100 shrink-0">
            <div>
              <p className="text-xs text-gray-500 mb-1">{translate('page.systemDicts.search.title')}</p>
              <h2 className="text-sm font-semibold text-gray-800">{translate('page.systemDicts.toolbar.title')}</h2>
            </div>
            <span className="text-xs text-primary-600 bg-primary-50 px-2 py-0.5 rounded-full">
              {typeTotal} {translate('page.systemDicts.item.title')}
            </span>
          </div>

          <div className="p-2 border-b border-gray-100 flex flex-wrap items-center gap-2 bg-gray-50/50 shrink-0">
            <div className="flex items-center border border-gray-300 rounded bg-white overflow-hidden shadow-sm flex-1 min-w-[120px]">
              <input
                className="py-1.5 px-3 text-sm w-full focus:outline-none"
                value={typeSearch.keywordDraft}
                placeholder={translate('page.systemDicts.search.placeholder')}
                onChange={(event) => setTypeSearch((current) => ({ ...current, keywordDraft: event.target.value }))}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    handleTypeSearch();
                  }
                }}
              />
            </div>
            <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-primary-50 hover:text-primary-600 transition-colors shadow-sm" type="button" onClick={handleTypeSearch}>
              {translate('common.query')}
            </button>
            <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 transition-colors shadow-sm" type="button" onClick={handleTypeSearchReset}>
              {translate('common.reset')}
            </button>
            <PermissionButton code="system:dict:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 transition-colors shadow-sm font-medium" type="button" onClick={openCreateTypeModal}>
              <AppIcon className="mr-1" name="plus" /> {translate('page.systemDicts.action.createType')}
            </PermissionButton>
          </div>

          <div className="flex-1 min-h-0 overflow-hidden">
            {typesQuery.isLoading ? (
              <PageLoading />
            ) : typesQuery.isError ? (
              <NetworkError
                action={
                  <button className="primary-button" type="button" onClick={() => void typesQuery.refetch()}>
                    {translate('common.retry')}
                  </button>
                }
                description={typesQuery.error instanceof Error ? typesQuery.error.message : translate('page.systemDicts.toolbar.description')}
              />
            ) : typeRows.length === 0 ? (
              <PageEmpty description={translate('page.systemDicts.empty.types')} />
            ) : (
              <DataTable
                columnSettingsKey="system-dicts-types"
                columns={typeColumns}
                emptyText={translate('page.systemDicts.empty.types')}
                fitScreen
                loading={typesQuery.isLoading}
                onPageChange={(nextPage) => setTypePage(nextPage)}
                onQueryChange={setTypeTableQuery}
                onSortsChange={setTypeSorts}
                pagination={{ current: typePage, pageSize, total: typeTotal }}
                rowActions={(row) => (
                  <TableActions>
                    <button className={selectedType?.id === row.id ? 'text-primary-600 font-medium hover:text-primary-700 transition-colors' : 'text-gray-500 hover:text-primary-600 transition-colors'} type="button" onClick={() => selectType(row)}>
                      <AppIcon className="text-base" name="eye" />
                    </button>
                    <PermissionButton className="hover:text-primary-600 transition-colors" code="system:dict:edit" type="button" onClick={() => void openEditTypeModal(row)}>
                      <AppIcon className="text-base" name="pencil-simple" />
                    </PermissionButton>
                    <PermissionButton className="hover:text-primary-600 transition-colors" code="system:dict:edit" type="button" onClick={() => void handleTypeStatusToggle(row)}>
                      <AppIcon className="text-base" name={row.isEnabled ? 'pause-circle' : 'play-circle'} />
                    </PermissionButton>
                    <PermissionButton className="hover:text-red-600 transition-colors" code="system:dict:delete" type="button" onClick={() => void handleTypeDelete(row)}>
                      <AppIcon className="text-base" name="trash" />
                    </PermissionButton>
                  </TableActions>
                )}
                rowClassName={(row) => (selectedType?.id === row.id ? 'bg-primary-50/50' : '')}
                rowKey={(row) => row.id}
                rows={typeRows}
                sorts={typeSorts}
                tableQuery={typeTableQuery}
              />
            )}
          </div>
        </div>

        {/* Right Panel: Dict Items */}
        <div className="flex-1 flex flex-col h-full min-w-0 bg-white border border-gray-200 rounded-lg shadow-sm">
          <div className="p-3 flex items-center justify-between border-b border-gray-100 shrink-0">
            <div>
              <p className="text-xs text-gray-500 mb-1">{translate('page.systemDicts.detail.title')}</p>
              <h2 className="text-sm font-semibold text-gray-800">{selectedType ? selectedType.dictName : translate('page.systemDicts.detail.title')}</h2>
            </div>
            <div className="flex items-center gap-2">
              {selectedType ? <span className="text-xs text-green-600 bg-green-50 border border-green-200 px-2 py-0.5 rounded-full">{selectedType.dictCode}</span> : null}
              <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void itemsQuery.refetch()}>
                <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
              </button>
              <PermissionButton code="system:dict:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={openCreateItemModal}>
                <AppIcon name="plus" /> {translate('page.systemDicts.action.createItem')}
              </PermissionButton>
            </div>
          </div>

          <div className="flex-1 min-h-0 overflow-hidden">
            {!selectedType ? (
              <PageEmpty description={translate('page.systemDicts.detail.empty')} />
            ) : itemsQuery.isLoading ? (
              <PageLoading />
            ) : itemsQuery.isError ? (
              <NetworkError
                action={
                  <button className="primary-button" type="button" onClick={() => void itemsQuery.refetch()}>
                    {translate('common.retry')}
                  </button>
                }
                description={itemsQuery.error instanceof Error ? itemsQuery.error.message : translate('page.systemDicts.toolbar.description')}
              />
            ) : (
              <DataTable
                columnSettingsKey="system-dicts-items"
                columns={itemColumns}
                emptyText={translate('page.systemDicts.empty.items')}
                fitScreen
                loading={itemsQuery.isLoading}
                onPageChange={(nextPage) => setPageField('itemPage', nextPage)}
                onPageSizeChange={(nextPageSize) => setPageState((current) => ({ ...current, itemPage: 1, itemPageSize: nextPageSize }))}
                onQueryChange={setItemTableQuery}
                onSortsChange={setItemSorts}
                pageSizeOptions={[10, 20, 50]}
                pagination={{ current: itemPage, pageSize: itemPageSize, total: itemTotal }}
                rowActions={(row) => (
                  <TableActions>
                    <PermissionButton className="hover:text-primary-600 transition-colors" code="system:dict:edit" type="button" onClick={() => void openEditItemModal(row)}>
                      <AppIcon className="text-base" name="pencil-simple" />
                    </PermissionButton>
                    <PermissionButton className="hover:text-primary-600 transition-colors" code="system:dict:edit" type="button" onClick={() => void handleItemStatusToggle(row)}>
                      <AppIcon className="text-base" name={row.isEnabled ? 'pause-circle' : 'play-circle'} />
                    </PermissionButton>
                    <PermissionButton className="hover:text-red-600 transition-colors" code="system:dict:delete" type="button" onClick={() => void handleItemDelete(row)}>
                      <AppIcon className="text-base" name="trash" />
                    </PermissionButton>
                  </TableActions>
                )}
                rowKey={(row) => row.id}
                rows={itemRows}
                sorts={itemSorts}
                tableQuery={itemTableQuery}
              />
            )}
          </div>
        </div>
      </div>

      <ModalForm
        actions={[
          {
            label: translate('common.cancel'),
            onClick: () => setIsTypeModalOpen(false),
            variant: 'ghost'
          },
          {
            label: translate('common.save'),
            onClick: () => void handleTypeSave(),
            type: 'button',
            variant: 'primary',
            loading: createTypeMutation.isPending || updateTypeMutation.isPending
          }
        ]}
        fields={typeFormFields}
        open={isTypeModalOpen}
        onClose={() => setIsTypeModalOpen(false)}
        onValueChange={(name, value) =>
          setTypeForm((currentValue) => ({
            ...currentValue,
            [name]: value as DictTypeFormState[keyof DictTypeFormState]
          }))
        }
        title={editingTypeId ? translate('common.edit') : translate('page.systemDicts.action.createType')}
        value={typeForm}
      >
        {translate('page.systemDicts.toolbar.description')}
      </ModalForm>

      <ModalForm
        actions={[
          {
            label: translate('common.cancel'),
            onClick: () => setIsItemModalOpen(false),
            variant: 'ghost'
          },
          {
            label: translate('common.save'),
            onClick: () => void handleItemSave(),
            type: 'button',
            variant: 'primary',
            loading: createItemMutation.isPending || updateItemMutation.isPending
          }
        ]}
        fields={itemFormFields}
        open={isItemModalOpen}
        onClose={() => setIsItemModalOpen(false)}
        onValueChange={(name, value) =>
          setItemForm((currentValue) => ({
            ...currentValue,
            [name]: value as DictItemFormState[keyof DictItemFormState]
          }))
        }
        title={editingItemId ? translate('common.edit') : translate('page.systemDicts.action.createItem')}
        value={itemForm}
      >
        {selectedType ? `${selectedType.dictName} (${selectedType.dictCode})` : translate('page.systemDicts.detail.empty')}
      </ModalForm>
    </CrudPage>
  );
}
