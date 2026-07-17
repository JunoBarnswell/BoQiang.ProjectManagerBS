import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useRef } from 'react';

import type { GridPageResult } from '../../../api/shared.types';
import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useConfirm } from '../../feedback/useConfirm';
import { useMessage } from '../../feedback/useMessage';
import type { DataTableCondition, DataTableQueryState, DataTableSortRule } from '../../table/tableTypes';
import { useTabPageState } from '../../tabs/useTabPageState';
import { getErrorMessage } from '../../utils/errorMessage';

export type CrudListQuery<TSearch extends object> = TSearch & {
  filters?: DataTableCondition[];
  pageIndex: number;
  pageSize: number;
  sorts?: DataTableSortRule[];
  tableQuery?: DataTableQueryState;
};

export interface CrudApi<TItem, TCreateRequest, TUpdateRequest = TCreateRequest, TSearch extends object = Record<string, never>> {
  list?: (query: CrudListQuery<TSearch>, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<TItem>>>;
  create: (request: TCreateRequest) => Promise<ApiEnvelope<TItem>>;
  update: (id: string, request: TUpdateRequest) => Promise<ApiEnvelope<TItem>>;
  delete: (id: string) => Promise<ApiEnvelope<boolean>>;
  batchDelete?: (ids: string[]) => Promise<ApiEnvelope<boolean>>;
}

export interface UseCrudResourceOptions<
  TItem,
  TCreateRequest,
  TUpdateRequest = TCreateRequest,
  TSearch extends object = Record<string, never>
> {
  api: CrudApi<TItem, TCreateRequest, TUpdateRequest, TSearch>;
  defaultFormState: TCreateRequest;
  defaultPageSize?: number;
  defaultSearchState?: TSearch;
  getId: (item: TItem) => string;
  itemName?: string;
  listQueryFn?: (query: CrudListQuery<TSearch>, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<TItem>>>;
  messages?: Partial<{
    batchDeleteUnsupported: string;
    confirmDeleteBatch: string;
    confirmDeleteSingle: string;
    confirmDeleteTitle: string;
    createFailed: string;
    createSuccess: string;
    deleteFailed: string;
    deleteSuccess: string;
    listUnsupported: string;
    updateFailed: string;
    updateSuccess: string;
  }>;
  queryKeyPrefix: readonly string[];
}

function cloneValue<T>(value: T): T {
  if (typeof structuredClone === 'function') {
    return structuredClone(value);
  }

  return JSON.parse(JSON.stringify(value)) as T;
}

export function useCrudResource<
  TItem,
  TCreateRequest,
  TUpdateRequest = TCreateRequest,
  TSearch extends object = Record<string, never>
>(options: UseCrudResourceOptions<TItem, TCreateRequest, TUpdateRequest, TSearch>) {
  const { translate } = useI18n();
  const [crudState, setCrudState] = useTabPageState(
    {
      editingId: null as string | null,
      formState: cloneValue(options.defaultFormState) as TCreateRequest | TUpdateRequest,
      modalOpen: false,
      pageIndex: 1,
      pageSize: options.defaultPageSize ?? 10,
      search: cloneValue(options.defaultSearchState ?? ({} as TSearch)),
      searchDraft: cloneValue(options.defaultSearchState ?? ({} as TSearch)),
      sorts: [] as DataTableSortRule[],
      tableQuery: { conditions: [], matchMode: 'and' } as DataTableQueryState
    },
    {
      cacheKey: `crud:${options.queryKeyPrefix.join('.')}`
    }
  );

  const {
    editingId,
    formState,
    modalOpen,
    pageIndex,
    pageSize,
    search,
    searchDraft,
    sorts,
    tableQuery
  } = crudState;

  const queryClient = useQueryClient();
  const message = useMessage();
  const confirm = useConfirm();
  const submitLockRef = useRef<string | null>(null);
  const itemName = options.itemName || translate('crud.defaultItemName');
  const resolveMessage = useCallback(
    (
      customMessage: string | undefined,
      defaultKey: string,
      params?: Record<string, string | number | boolean | null | undefined>
    ) => formatMessage(customMessage ?? translate(defaultKey), params),
    [translate]
  );

  const listQueryKey = useMemo(
    () => [...options.queryKeyPrefix, pageIndex, pageSize, search, tableQuery, sorts],
    [options.queryKeyPrefix, pageIndex, pageSize, search, tableQuery, sorts]
  );

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: listQueryKey,
    queryFn: (context) => {
      const query = {
        ...(search as TSearch),
        filters: tableQuery.conditions,
        pageIndex,
        pageSize,
        sorts,
        tableQuery
      };
      if (options.listQueryFn) {
        return options.listQueryFn(query, context.signal);
      }

      if (!options.api.list) {
        throw new Error(resolveMessage(options.messages?.listUnsupported, 'crud.listUnsupported'));
      }

      return options.api.list(query, context.signal);
    }
  });

  const refresh = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: options.queryKeyPrefix });
  }, [options.queryKeyPrefix, queryClient]);

  const createMutation = useApiMutation({
    mutationFn: options.api.create,
    onSuccess: async () => {
      message.success(resolveMessage(options.messages?.createSuccess, 'crud.createSuccess', { itemName }));
      setCrudState((current) => ({ ...current, modalOpen: false }));
      await refresh();
    },
    onError: (error) => {
      message.error(getErrorMessage(error, resolveMessage(options.messages?.createFailed, 'crud.createFailed', { itemName })));
    }
  });

  const updateMutation = useApiMutation({
    mutationFn: (data: { id: string; request: TUpdateRequest }) => options.api.update(data.id, data.request),
    onSuccess: async () => {
      message.success(resolveMessage(options.messages?.updateSuccess, 'crud.updateSuccess', { itemName }));
      setCrudState((current) => ({ ...current, modalOpen: false }));
      await refresh();
    },
    onError: (error) => {
      message.error(getErrorMessage(error, resolveMessage(options.messages?.updateFailed, 'crud.updateFailed', { itemName })));
    }
  });

  const deleteMutation = useApiMutation({
    mutationFn: options.api.delete,
    onSuccess: async () => {
      message.success(resolveMessage(options.messages?.deleteSuccess, 'crud.deleteSuccess', { itemName }));
      await refresh();
    },
    onError: (error) => {
      message.error(getErrorMessage(error, resolveMessage(options.messages?.deleteFailed, 'crud.deleteFailed', { itemName })));
    }
  });

  const batchDeleteMutation = useApiMutation({
    mutationFn: async (ids: string[]) => {
      if (!options.api.batchDelete) {
        throw new Error(resolveMessage(options.messages?.batchDeleteUnsupported, 'crud.batchDeleteUnsupported'));
      }

      return options.api.batchDelete(ids);
    },
    onSuccess: async () => {
      message.success(resolveMessage(options.messages?.deleteSuccess, 'crud.deleteSuccess', { itemName }));
      await refresh();
    },
    onError: (error) => {
      message.error(getErrorMessage(error, resolveMessage(options.messages?.deleteFailed, 'crud.deleteFailed', { itemName })));
    }
  });

  const handleSearch = useCallback(
    (nextSearch?: TSearch) => {
      setCrudState((current) => ({
        ...current,
        pageIndex: 1,
        search: nextSearch ? cloneValue(nextSearch) : cloneValue(current.searchDraft)
      }));
    },
    [setCrudState]
  );

  const handleReset = useCallback(() => {
    const initialSearch = cloneValue(options.defaultSearchState ?? ({} as TSearch));
    setCrudState((current) => ({
      ...current,
      pageIndex: 1,
      search: initialSearch,
      searchDraft: initialSearch,
      sorts: [],
      tableQuery: { conditions: [], matchMode: 'and' }
    }));
  }, [options.defaultSearchState, setCrudState]);

  const handleTableQueryChange = useCallback(
    (nextQuery: DataTableQueryState) => {
      setCrudState((current) => ({
        ...current,
        pageIndex: 1,
        tableQuery: cloneValue(nextQuery)
      }));
    },
    [setCrudState]
  );

  const handleSortsChange = useCallback(
    (nextSorts: DataTableSortRule[]) => {
      setCrudState((current) => ({
        ...current,
        pageIndex: 1,
        sorts: cloneValue(nextSorts)
      }));
    },
    [setCrudState]
  );

  const handleCreate = useCallback(() => {
    setCrudState((current) => ({
      ...current,
      editingId: null,
      formState: cloneValue(options.defaultFormState),
      modalOpen: true
    }));
  }, [options.defaultFormState, setCrudState]);

  const handleEdit = useCallback(
    (item: TItem, mapToFormState: (item: TItem) => TUpdateRequest) => {
      setCrudState((current) => ({
        ...current,
        editingId: options.getId(item),
        formState: cloneValue(mapToFormState(item)),
        modalOpen: true
      }));
    },
    [options, setCrudState]
  );

  const handleDelete = useCallback(
    (item: TItem, displayName?: string) => {
      confirm({
        title: resolveMessage(options.messages?.confirmDeleteTitle, 'crud.confirmDeleteTitle'),
        content: resolveMessage(options.messages?.confirmDeleteSingle, 'crud.confirmDeleteSingle', {
          displayName: displayName || options.getId(item),
          itemName
        }),
        onConfirm: async () => {
          await deleteMutation.mutateAsync(options.getId(item));
        }
      });
    },
    [confirm, deleteMutation, itemName, options, resolveMessage]
  );

  const handleBatchDelete = useCallback(
    (items: TItem[] | string[]) => {
      if (!options.api.batchDelete) {
        return;
      }

      const ids = items.map((item) => (typeof item === 'string' ? item : options.getId(item)));
      if (ids.length === 0) {
        return;
      }

      confirm({
        title: resolveMessage(options.messages?.confirmDeleteTitle, 'crud.confirmDeleteTitle'),
        content: resolveMessage(options.messages?.confirmDeleteBatch, 'crud.confirmDeleteBatch', {
          count: ids.length,
          itemName
        }),
        onConfirm: async () => {
          await batchDeleteMutation.mutateAsync(ids);
        }
      });
    },
    [batchDeleteMutation, confirm, itemName, options, resolveMessage]
  );

  const handleSubmit = useCallback(async () => {
    const submitKey = editingId ? `update:${editingId}` : 'create';
    if (submitLockRef.current) {
      return;
    }

    submitLockRef.current = submitKey;
    if (editingId) {
      try {
        await updateMutation.mutateAsync({ id: editingId, request: formState as TUpdateRequest });
      } finally {
        submitLockRef.current = null;
      }
      return;
    }

    try {
      await createMutation.mutateAsync(formState as TCreateRequest);
    } finally {
      submitLockRef.current = null;
    }
  }, [createMutation, editingId, formState, updateMutation]);

  return {
    api: options.api,
    batchDelete: handleBatchDelete,
    canBatchDelete: Boolean(options.api.batchDelete),
    createMutation,
    deleteMutation,
    editingId,
    formState,
    handleCreate,
    handleDelete,
    handleEdit,
    handleReset,
    handleSearch,
    handleSubmit,
    listQuery,
    modalOpen,
    pageIndex,
    pageSize,
    refresh,
    search,
    searchDraft,
    setSorts: handleSortsChange,
    setTableQuery: handleTableQueryChange,
    setFormState: (value: TCreateRequest | TUpdateRequest | ((current: TCreateRequest | TUpdateRequest) => TCreateRequest | TUpdateRequest)) =>
      setCrudState((current) => ({
        ...current,
        formState:
          typeof value === 'function'
            ? (value as (current: TCreateRequest | TUpdateRequest) => TCreateRequest | TUpdateRequest)(current.formState)
            : value
      })),
    setModalOpen: (value: boolean) => setCrudState((current) => ({ ...current, modalOpen: value })),
    setPageIndex: (value: number) => setCrudState((current) => ({ ...current, pageIndex: value })),
    setPageSize: (value: number) => setCrudState((current) => ({ ...current, pageSize: value })),
    setSearchDraft: (value: TSearch | ((current: TSearch) => TSearch)) =>
      setCrudState((current) => ({
        ...current,
        searchDraft: typeof value === 'function' ? (value as (current: TSearch) => TSearch)(current.searchDraft) : value
      })),
    sorts,
    tableQuery,
    updateMutation
  };
}
