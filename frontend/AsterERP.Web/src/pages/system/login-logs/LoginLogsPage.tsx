import { useMemo } from 'react';

import { systemLoginLogsApi, type SystemLoginLogListItemDto } from '../../../api/system/login-logs.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';

interface LoginLogSearchState {
  keyword: string;
  loginResult: string;
  startTime: string;
  endTime: string;
}

interface LoginLogPageState {
  pageIndex: number;
  pageSize: number;
  search: LoginLogSearchState;
  searchDraft: LoginLogSearchState;
  sorts: DataTableSortRule[];
  tableQuery: DataTableQueryState;
}

const defaultSearchState: LoginLogSearchState = {
  keyword: '',
  loginResult: '',
  startTime: '',
  endTime: ''
};
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

const loginResultLabelKeys: Record<string, string> = {
  AccountDisabled: 'page.systemLoginLogs.result.accountDisabled',
  AccountNotFound: 'page.systemLoginLogs.result.accountNotFound',
  PasswordError: 'page.systemLoginLogs.result.passwordError',
  Success: 'page.systemLoginLogs.result.success'
};

const loginResultStyles: Record<string, string> = {
  AccountDisabled: 'bg-amber-50 text-amber-700 border-amber-200',
  AccountNotFound: 'bg-slate-50 text-slate-700 border-slate-200',
  PasswordError: 'bg-rose-50 text-rose-700 border-rose-200',
  Success: 'bg-emerald-50 text-emerald-700 border-emerald-200'
};

function formatDateTime(value: string): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function renderLoginResult(row: SystemLoginLogListItemDto, translate: (key: string) => string) {
  return (
    <span className={`inline-flex items-center rounded border px-2 py-0.5 text-xs font-medium ${loginResultStyles[row.loginResult] ?? 'bg-gray-50 text-gray-700 border-gray-200'}`}>
      {translate(loginResultLabelKeys[row.loginResult] ?? 'page.systemLoginLogs.result.unknown')}
    </span>
  );
}

export function LoginLogsPage() {
  const { translate } = useI18n();
  const [pageState, setPageState, clearPageState] = useTabPageState<LoginLogPageState>(
    {
      pageIndex: 1,
      pageSize: 20,
      search: defaultSearchState,
      searchDraft: defaultSearchState,
      sorts: [],
      tableQuery: defaultTableQuery
    },
    { cacheKey: 'system-login-logs' }
  );

  const { pageIndex, pageSize, search, searchDraft, sorts, tableQuery } = pageState;

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryKey: ['system-login-logs', pageIndex, pageSize, search, sorts, tableQuery],
    queryFn: ({ signal }) =>
      systemLoginLogsApi.list({
        ...search,
        filters: tableQuery.conditions,
        pageIndex,
        pageSize,
        sorts
      }, signal)
  });

  const columns: DataTableColumn<SystemLoginLogListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemLoginLogs.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      {
        key: 'userName',
        title: translate('page.systemLoginLogs.column.userName'),
        width: '180px',
        responsivePriority: 100,
        sortable: true,
        filterable: true,
        filterType: 'text',
        render: (row) => (
          <>
            <div className="font-medium text-gray-900">{row.userName}</div>
            <div className="text-xs text-gray-500">{row.userDisplayName ?? row.userId ?? '-'}</div>
          </>
        )
      },
      { key: 'loginResult', title: translate('page.systemLoginLogs.column.loginResult'), width: '120px', align: 'center', responsivePriority: 95, render: (row) => renderLoginResult(row, translate) },
      { key: 'failureReason', title: translate('page.systemLoginLogs.column.failureReason'), width: '160px', responsivePriority: 90, sortable: true, filterable: true, filterType: 'text', render: (row) => row.failureReason ?? '-' },
      { key: 'clientIp', title: translate('page.systemLoginLogs.column.clientIp'), width: '150px', responsivePriority: 85, sortable: true, filterable: true, filterType: 'text', render: (row) => row.clientIp ?? '-' },
      { key: 'createdTime', title: translate('page.systemLoginLogs.column.createdTime'), width: '190px', responsivePriority: 92, sortable: true, filterable: true, filterType: 'date', render: (row) => formatDateTime(row.createdTime) },
      {
        key: 'userAgent',
        title: translate('page.systemLoginLogs.column.userAgent'),
        responsivePriority: 55,
        sortable: true,
        filterable: true,
        filterType: 'text',
        render: (row) => <span className="block max-w-xl truncate text-gray-600" title={row.userAgent ?? ''}>{row.userAgent ?? '-'}</span>
      },
      { key: 'traceId', title: translate('page.systemLoginLogs.column.traceId'), width: '220px', responsivePriority: 50, sortable: true, filterable: true, filterType: 'text', render: (row) => <span className="font-mono text-xs text-gray-500">{row.traceId}</span> }
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

  const searchArea = (
    <div className="flex flex-wrap items-center gap-3">
      <input
        className="w-56 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm outline-none focus:border-primary-500"
        placeholder={translate('page.systemLoginLogs.search.placeholder')}
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
      <select
        className="rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 outline-none focus:border-primary-500"
        value={searchDraft.loginResult}
        onChange={(event) => setPageState((current) => ({ ...current, searchDraft: { ...current.searchDraft, loginResult: event.target.value } }))}
      >
        <option value="">{translate('page.systemLoginLogs.search.allResults')}</option>
        <option value="Success">{translate('page.systemLoginLogs.result.success')}</option>
        <option value="AccountNotFound">{translate('page.systemLoginLogs.result.accountNotFound')}</option>
        <option value="PasswordError">{translate('page.systemLoginLogs.result.passwordError')}</option>
        <option value="AccountDisabled">{translate('page.systemLoginLogs.result.accountDisabled')}</option>
      </select>
      <input
        className="rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 outline-none focus:border-primary-500"
        type="datetime-local"
        value={searchDraft.startTime}
        onChange={(event) => setPageState((current) => ({ ...current, searchDraft: { ...current.searchDraft, startTime: event.target.value } }))}
      />
      <span className="text-sm text-gray-400">{translate('page.systemLoginLogs.search.to')}</span>
      <input
        className="rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 outline-none focus:border-primary-500"
        type="datetime-local"
        value={searchDraft.endTime}
        onChange={(event) => setPageState((current) => ({ ...current, searchDraft: { ...current.searchDraft, endTime: event.target.value } }))}
      />
      <button
        className="rounded border border-gray-300 bg-white px-4 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-primary-50 hover:text-primary-600"
        type="button"
        onClick={handleSearch}
      >
        {translate('common.query')}
      </button>
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
    <button
      className="flex items-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-gray-50 hover:text-primary-600"
      type="button"
      onClick={() => void listQuery.refetch()}
    >
      <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
    </button>
  );

  return (
    <CrudPage
      actions={actions}
      description={translate('page.systemLoginLogs.description')}
      eyebrow={translate('nav.systemLoginLogs')}
      searchArea={searchArea}
      title={translate('page.systemLoginLogs.title')}
    >
      <div className="flex h-full min-w-0 flex-1 flex-col overflow-hidden">
        <DataTable
          columnSettingsKey="system-login-logs"
          columns={columns}
          emptyText={listQuery.isError ? translate('page.systemLoginLogs.error.loadFailed') : translate('page.systemLoginLogs.empty')}
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
          rowKey={(row) => row.id}
          rows={listQuery.data?.data.items ?? []}
          sorts={sorts}
          tableQuery={tableQuery}
        />
      </div>
    </CrudPage>
  );
}
