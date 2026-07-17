import { useMemo } from 'react';

import { systemOnlineUserApi, type OnlineUserListItemDto, type OnlineUserQuery } from '../../../api/system/online-users.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

interface OnlineUserSearchState {
  keyword: string;
}

interface OnlineUserPageState {
  pageIndex: number;
  pageSize: number;
  search: OnlineUserSearchState;
  searchDraft: OnlineUserSearchState;
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
}

const defaultSearchState: OnlineUserSearchState = {
  keyword: ''
};
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function normalizeKeyword(value: string): string | undefined {
  const keyword = value.trim();
  return keyword.length > 0 ? keyword : undefined;
}

export function OnlineUsersPage() {
  const { translate } = useI18n();
  const confirm = useConfirm();
  const message = useMessage();
  const [pageState, setPageState, clearPageState] = useTabPageState<OnlineUserPageState>(
    {
      pageIndex: 1,
      pageSize: 20,
      search: defaultSearchState,
      searchDraft: defaultSearchState,
      sorts: [],
      tableQuery: defaultTableQuery
    },
    { cacheKey: 'system-online-users' }
  );

  const { pageIndex, pageSize, search, searchDraft, sorts, tableQuery } = pageState;

  const listQueryParams = useMemo<OnlineUserQuery>(
    () => ({
      filters: tableQuery.conditions,
      keyword: normalizeKeyword(search.keyword),
      pageIndex,
      pageSize,
      sorts
    }),
    [pageIndex, pageSize, search.keyword, sorts, tableQuery]
  );

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: ['system-online-users', pageIndex, pageSize, search.keyword, sorts, tableQuery],
    queryFn: ({ signal }) => systemOnlineUserApi.list(listQueryParams, signal)
  });

  const forceLogoutMutation = useApiMutation({
    mutationFn: (sessionId: string) => systemOnlineUserApi.forceLogout(sessionId)
  });

  const columns: DataTableColumn<OnlineUserListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemOnlineUsers.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      {
        key: 'displayName',
        title: translate('page.systemOnlineUsers.column.user'),
        width: '220px',
        responsivePriority: 100,
        sortable: true,
        filterable: true,
        filterField: 'displayName',
        filterType: 'text',
        render: (row) => (
          <>
            <div className="font-medium text-gray-900">{row.displayName}</div>
            <div className="text-xs text-gray-500">{row.userName}</div>
          </>
        )
      },
      { key: 'deptId', title: translate('page.systemOnlineUsers.column.deptId'), width: '160px', responsivePriority: 70, sortable: true, filterable: true, filterType: 'text', render: (row) => row.deptId ?? '-' },
      { key: 'clientIp', title: translate('page.systemOnlineUsers.column.clientIp'), width: '150px', responsivePriority: 90, sortable: true, filterable: true, filterType: 'text', render: (row) => row.clientIp ?? '-' },
      { key: 'lastSeenTime', title: translate('page.systemOnlineUsers.column.lastSeenTime'), width: '190px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'date', render: (row) => formatDateTime(row.lastSeenTime) },
      { key: 'expiresAt', title: translate('page.systemOnlineUsers.column.expiresAt'), width: '190px', responsivePriority: 85, sortable: true, filterable: true, filterType: 'date', render: (row) => formatDateTime(row.expiresAt) },
      {
        key: 'userAgent',
        title: translate('page.systemOnlineUsers.column.userAgent'),
        responsivePriority: 60,
        sortable: true,
        filterable: true,
        filterType: 'text',
        render: (row) => <span className="block max-w-xl truncate text-gray-600" title={row.userAgent ?? ''}>{row.userAgent ?? '-'}</span>
      },
      {
        key: 'sessionId',
        title: translate('page.systemOnlineUsers.column.sessionId'),
        width: '220px',
        responsivePriority: 50,
        sortable: true,
        filterable: true,
        filterType: 'text',
        render: (row) => <span className="font-mono text-xs text-gray-500">{row.sessionId}</span>
      }
    ],
    [pageIndex, pageSize, translate]
  );

  const handleSearch = () => {
    setPageState((current) => ({
      ...current,
      pageIndex: 1,
      search: { ...current.searchDraft }
    }));
  };

  const handleReset = () => {
    clearPageState();
  };

  const handleSortsChange = (nextSorts: DataTableSortRule[]) => {
    setPageState((current) => ({
      ...current,
      pageIndex: 1,
      sorts: nextSorts
    }));
  };

  const handleTableQueryChange = (nextQuery: DataTableQueryState) => {
    setPageState((current) => ({
      ...current,
      pageIndex: 1,
      tableQuery: nextQuery
    }));
  };

  const refresh = async () => {
    await listQuery.refetch();
  };

  const handleForceLogout = (row: OnlineUserListItemDto) => {
    confirm({
      title: translate('page.systemOnlineUsers.confirm.title'),
      content: translate('page.systemOnlineUsers.confirm.content').replace('{name}', row.displayName || row.userName),
      confirmText: translate('page.systemOnlineUsers.confirm.action'),
      onConfirm: async () => {
        try {
          await forceLogoutMutation.mutateAsync(row.sessionId);
          message.success(translate('page.systemOnlineUsers.success.logout'));
          await refresh();
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemOnlineUsers.error.logoutFailed')));
        }
      }
    });
  };

  const searchArea = (
    <div className="flex flex-wrap items-center gap-3">
      <input
        className="w-64 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm outline-none focus:border-primary-500"
        placeholder={translate('page.systemOnlineUsers.search.placeholder')}
        type="text"
        value={searchDraft.keyword}
        onChange={(event) => setPageState((current) => ({ ...current, searchDraft: { ...current.searchDraft, keyword: event.target.value } }))}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            handleSearch();
          }
        }}
      />
      <PermissionButton
        code="system:online-user:query"
        className="rounded border border-gray-300 bg-white px-4 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-primary-50 hover:text-primary-600"
        type="button"
        onClick={handleSearch}
      >
        {translate('common.query')}
      </PermissionButton>
      <button
        className="px-2 py-1.5 text-sm text-gray-500 transition-colors hover:text-gray-700"
        type="button"
        onClick={handleReset}
      >
        {translate('common.reset')}
      </button>
    </div>
  );

  const actions = (
    <PermissionButton
      code="system:online-user:query"
      className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-gray-50 hover:text-primary-600"
      type="button"
      onClick={() => void refresh()}
    >
      <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
    </PermissionButton>
  );

  return (
    <CrudPage
      actions={actions}
      description={translate('page.systemOnlineUsers.description')}
      eyebrow={translate('page.systemOnlineUsers.eyebrow')}
      searchArea={searchArea}
      title={translate('page.systemOnlineUsers.title')}
    >
      <div className="flex h-full min-w-0 flex-1 flex-col overflow-hidden">
        <DataTable
          columnSettingsKey="system-online-users"
          columns={columns}
          emptyText={listQuery.isError ? translate('page.systemOnlineUsers.error.loadFailed') : translate('page.systemOnlineUsers.empty')}
          fitScreen
          loading={listQuery.isLoading}
          onPageChange={(nextPage) => setPageState((current) => ({ ...current, pageIndex: nextPage }))}
          onPageSizeChange={(nextPageSize) => setPageState((current) => ({ ...current, pageIndex: 1, pageSize: nextPageSize }))}
          onQueryChange={handleTableQueryChange}
          onSortsChange={handleSortsChange}
          pageSizeOptions={[10, 20, 50, 100]}
          pagination={{
            current: pageIndex,
            pageSize,
            total: listQuery.data?.data.total ?? 0
          }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton
                code="system:online-user:kick"
                className="hover:text-red-600 transition-colors"
                disabled={forceLogoutMutation.isPending}
                title={translate('page.systemOnlineUsers.action.forceLogout')}
                type="button"
                onClick={() => handleForceLogout(row)}
              >
                <AppIcon className="text-base" name="sign-out" />
              </PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.sessionId}
          rows={listQuery.data?.data.items ?? []}
          sorts={sorts}
          tableQuery={tableQuery}
        />
      </div>
    </CrudPage>
  );
}
