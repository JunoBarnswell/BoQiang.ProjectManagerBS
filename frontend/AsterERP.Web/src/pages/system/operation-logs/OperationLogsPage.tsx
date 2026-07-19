import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import {
  systemOperationLogApi,
  type OperationLogDetailDto,
  type OperationLogListItemDto,
  type OperationLogQuery
} from '../../../api/system/operation-logs.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn, DataTableQueryState, DataTableSortRule } from '../../../shared/table/tableTypes';

interface OperationLogSearchState {
  endTime?: string;
  isSuccess?: '' | 'false' | 'true';
  moduleName?: string;
  requestMethod?: string;
  requestPath?: string;
  startTime?: string;
  traceId?: string;
  user?: string;
}

const defaultPageSize = 20;
const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };
const requestMethods = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH'];

export function OperationLogsPage() {
  const { translate } = useI18n();
  const [searchParams] = useSearchParams();
  const requestedTraceId = searchParams.get('traceId')?.trim() || undefined;
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const [searchDraft, setSearchDraft] = useState<OperationLogSearchState>(() => ({ traceId: requestedTraceId }));
  const [searchState, setSearchState] = useState<OperationLogSearchState>(() => ({ traceId: requestedTraceId }));
  const [selectedLogId, setSelectedLogId] = useState<string | null>(null);
  const [sorts, setSorts] = useState<DataTableSortRule[]>([]);
  const [tableQuery, setTableQueryState] = useState<DataTableQueryState>(defaultTableQuery);

  useEffect(() => {
    if (!requestedTraceId) return;
    setSearchDraft((current) => current.traceId === requestedTraceId ? current : { ...current, traceId: requestedTraceId });
    setSearchState((current) => current.traceId === requestedTraceId ? current : { ...current, traceId: requestedTraceId });
    setPageIndex(1);
  }, [requestedTraceId]);

  const listQueryParams = useMemo<OperationLogQuery>(
    () => ({
      endTime: searchState.endTime,
      filters: tableQuery.conditions,
      isSuccess: parseSuccessState(searchState.isSuccess),
      moduleName: searchState.moduleName,
      pageIndex,
      pageSize,
      requestMethod: searchState.requestMethod,
      requestPath: searchState.requestPath,
      sorts,
      startTime: searchState.startTime,
      traceId: searchState.traceId,
      user: searchState.user
    }),
    [pageIndex, pageSize, searchState, sorts, tableQuery]
  );

  const logsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => systemOperationLogApi.list(listQueryParams, signal),
    queryKey: [
      'system-operation-logs',
      pageIndex,
      pageSize,
      searchState.startTime ?? '',
      searchState.endTime ?? '',
      searchState.user ?? '',
      searchState.moduleName ?? '',
      searchState.requestPath ?? '',
      searchState.requestMethod ?? '',
      searchState.isSuccess ?? '',
      searchState.traceId ?? '',
      sorts,
      tableQuery
    ]
  });

  const detailQuery = useApiQuery({
    enabled: selectedLogId !== null,
    queryFn: () => systemOperationLogApi.detail(selectedLogId ?? ''),
    queryKey: ['system-operation-logs', 'detail', selectedLogId ?? 'none']
  });

  const columns: DataTableColumn<OperationLogListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemOperationLogs.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      { key: 'createdTime', title: translate('page.systemOperationLogs.column.time'), width: '170px', responsivePriority: 100, sortable: true, filterable: true, filterType: 'date', render: (row) => formatDateTime(row.createdTime) },
      { key: 'userName', title: translate('page.systemOperationLogs.column.user'), width: '120px', responsivePriority: 95, sortable: true, filterable: true, filterType: 'text', render: (row) => row.userName || '-' },
      { key: 'moduleName', title: translate('page.systemOperationLogs.column.module'), width: '110px', responsivePriority: 90, sortable: true, filterable: true, filterType: 'text', render: (row) => row.moduleName || '-' },
      {
        key: 'requestMethod',
        title: translate('page.systemOperationLogs.column.method'),
        width: '88px',
        align: 'center',
        responsivePriority: 90,
        sortable: true,
        filterable: true,
        filterType: 'select',
        filterOperators: ['equals'],
        filterOptions: requestMethods.map((method) => ({ label: method, value: method })),
        render: (row) => <MethodBadge method={row.requestMethod} />
      },
      { key: 'requestPath', title: translate('page.systemOperationLogs.column.path'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text', render: (row) => <span className="font-mono text-xs">{row.requestPath}</span> },
      { key: 'actionName', title: translate('page.systemOperationLogs.column.action'), width: '160px', hideBelow: 'lg', sortable: true, filterable: true, filterType: 'text', render: (row) => row.actionName ?? '-' },
      { key: 'statusCode', title: translate('page.systemOperationLogs.column.statusCode'), width: '90px', align: 'center', responsivePriority: 85, sortable: true, filterable: true, filterType: 'number' },
      { key: 'durationMs', title: translate('page.systemOperationLogs.column.duration'), width: '90px', align: 'right', responsivePriority: 80, sortable: true, filterable: true, filterType: 'number', render: (row) => `${row.durationMs} ms` },
      {
        key: 'isSuccess',
        title: translate('page.systemOperationLogs.column.result'),
        width: '86px',
        align: 'center',
        responsivePriority: 95,
        sortable: true,
        filterable: true,
        filterType: 'boolean',
        filterOperators: ['equals'],
        filterOptions: [
          { label: translate('page.systemOperationLogs.result.success'), value: true },
          { label: translate('page.systemOperationLogs.result.failed'), value: false }
        ],
        render: (row) => <SuccessBadge isSuccess={row.isSuccess} translate={translate} />
      },
      { key: 'traceId', title: translate('page.systemOperationLogs.column.traceId'), width: '180px', responsivePriority: 70, sortable: true, filterable: true, filterType: 'text', render: (row) => <span className="font-mono text-xs">{row.traceId}</span> }
    ],
    [pageIndex, pageSize, translate]
  );

  const handleTableQueryChange = (nextQuery: DataTableQueryState) => {
    setPageIndex(1);
    setTableQueryState(nextQuery);
  };

  const handleSearch = () => {
    setPageIndex(1);
    setSearchState(normalizeSearchState(searchDraft));
  };

  const handleReset = () => {
    setPageIndex(1);
    setTableQueryState(defaultTableQuery);
    setSearchDraft({});
    setSearchState({});
  };

  const total = logsQuery.data?.data.total ?? 0;
  const rows = logsQuery.data?.data.items ?? [];

  return (
    <CrudPage
      actions={
        <PermissionButton
          code="system:operation-log:query"
          className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors"
          type="button"
          onClick={() => void logsQuery.refetch()}
        >
          <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
        </PermissionButton>
      }
      description={translate('page.systemOperationLogs.description')}
      eyebrow={translate('nav.systemOperationLogs')}
      searchArea={
        <OperationLogSearchBar
          value={searchDraft}
          onChange={setSearchDraft}
          onReset={handleReset}
          onSearch={handleSearch}
          translate={translate}
        />
      }
      title={translate('page.systemOperationLogs.title')}
    >
      <div className="flex-1 flex flex-col min-w-0 h-full overflow-hidden bg-white border border-gray-200 rounded-lg shadow-sm">
        <DataTable
          columnSettingsKey="system-operation-logs"
          columns={columns}
          emptyText={logsQuery.isError ? translate('page.systemOperationLogs.error.loadFailed') : translate('page.systemOperationLogs.empty')}
          fitScreen
          loading={logsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(value) => {
            setPageIndex(1);
            setPageSize(value);
          }}
          onQueryChange={handleTableQueryChange}
          onRow={(row) => setSelectedLogId(row.id)}
          onSortsChange={(nextSorts) => {
            setPageIndex(1);
            setSorts(nextSorts);
          }}
          pageSizeOptions={[10, 20, 50, 100]}
          pagination={{ current: pageIndex, pageSize, total }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton
                code="system:operation-log:query"
                className="hover:text-primary-600 transition-colors"
                title={translate('common.view')}
                type="button"
                onClick={() => setSelectedLogId(row.id)}
              >
                <AppIcon className="text-base" name="file-text" />
              </PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={rows}
          sorts={sorts}
          tableQuery={tableQuery}
        />
      </div>

      <OperationLogDetailModal
        log={detailQuery.data?.data ?? null}
        loading={detailQuery.isLoading}
        open={selectedLogId !== null}
        translate={translate}
        onClose={() => setSelectedLogId(null)}
      />
    </CrudPage>
  );
}

function OperationLogSearchBar({
  onChange,
  onReset,
  onSearch,
  translate,
  value
}: {
  onChange: (value: OperationLogSearchState) => void;
  onReset: () => void;
  onSearch: () => void;
  translate: (key: string) => string;
  value: OperationLogSearchState;
}) {
  const updateField = (name: keyof OperationLogSearchState, fieldValue: string) => {
    onChange({ ...value, [name]: fieldValue });
  };

  return (
    <div className="flex flex-wrap gap-3 items-center">
      <SearchInput label={translate('page.systemOperationLogs.search.startTime')} type="datetime-local" value={value.startTime ?? ''} onChange={(nextValue) => updateField('startTime', nextValue)} onEnter={onSearch} />
      <SearchInput label={translate('page.systemOperationLogs.search.endTime')} type="datetime-local" value={value.endTime ?? ''} onChange={(nextValue) => updateField('endTime', nextValue)} onEnter={onSearch} />
      <SearchInput label={translate('page.systemOperationLogs.search.user')} placeholder={translate('page.systemOperationLogs.search.userPlaceholder')} value={value.user ?? ''} onChange={(nextValue) => updateField('user', nextValue)} onEnter={onSearch} />
      <SearchInput label={translate('page.systemOperationLogs.search.module')} placeholder={translate('page.systemOperationLogs.search.modulePlaceholder')} value={value.moduleName ?? ''} onChange={(nextValue) => updateField('moduleName', nextValue)} onEnter={onSearch} />
      <SearchInput label={translate('page.systemOperationLogs.search.requestPath')} placeholder={translate('page.systemOperationLogs.search.pathPlaceholder')} value={value.requestPath ?? ''} onChange={(nextValue) => updateField('requestPath', nextValue)} onEnter={onSearch} />
      <label className="flex items-center gap-2 text-sm text-gray-600">
        {translate('page.systemOperationLogs.search.requestMethod')}
        <select
          className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm focus:outline-none focus:border-primary-500"
          value={value.requestMethod ?? ''}
          onChange={(event) => updateField('requestMethod', event.target.value)}
        >
          <option value="">{translate('page.systemOperationLogs.search.allResults')}</option>
          {requestMethods.map((method) => (
            <option key={method} value={method}>
              {method}
            </option>
          ))}
        </select>
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-600">
        {translate('page.systemOperationLogs.search.isSuccess')}
        <select
          className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm focus:outline-none focus:border-primary-500"
          value={value.isSuccess ?? ''}
          onChange={(event) => updateField('isSuccess', event.target.value)}
        >
          <option value="">{translate('page.systemOperationLogs.search.allResults')}</option>
          <option value="true">{translate('page.systemOperationLogs.result.success')}</option>
          <option value="false">{translate('page.systemOperationLogs.result.failed')}</option>
        </select>
      </label>
      <SearchInput label={translate('page.systemOperationLogs.search.traceId')} placeholder={translate('page.systemOperationLogs.search.traceIdPlaceholder')} value={value.traceId ?? ''} onChange={(nextValue) => updateField('traceId', nextValue)} onEnter={onSearch} />
      <PermissionButton
        code="system:operation-log:query"
        className="bg-white border border-gray-300 text-gray-700 px-4 py-1.5 rounded text-sm hover:bg-primary-50 hover:text-primary-600 transition-colors shadow-sm"
        type="button"
        onClick={onSearch}
      >
        {translate('common.query')}
      </PermissionButton>
      <button className="text-gray-500 px-2 py-1.5 text-sm hover:text-gray-700 transition-colors" type="button" onClick={onReset}>
        {translate('common.reset')}
      </button>
    </div>
  );
}

function SearchInput({
  label,
  onChange,
  onEnter,
  placeholder,
  type = 'text',
  value
}: {
  label: string;
  onChange: (value: string) => void;
  onEnter?: () => void;
  placeholder?: string;
  type?: 'datetime-local' | 'text';
  value: string;
}) {
  return (
    <label className="flex items-center gap-2 text-sm text-gray-600">
      {label}
      <input
        className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm focus:outline-none focus:border-primary-500"
        placeholder={placeholder}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            onEnter?.();
          }
        }}
      />
    </label>
  );
}

function OperationLogDetailModal({
  loading,
  log,
  translate,
  onClose,
  open
}: {
  loading: boolean;
  log: OperationLogDetailDto | null;
  translate: (key: string) => string;
  onClose: () => void;
  open: boolean;
}) {
  return (
    <ResponsiveModal mode="drawer" open={open} title={translate('page.systemOperationLogs.detail.title')} onClose={onClose}>
      {loading ? (
        <div className="text-sm text-gray-500">{translate('page.systemOperationLogs.detail.loading')}</div>
      ) : log ? (
        <div className="space-y-4 text-sm">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <DetailItem label={translate('page.systemOperationLogs.detail.time')} value={formatDateTime(log.createdTime)} />
            <DetailItem label={translate('page.systemOperationLogs.detail.result')} value={log.isSuccess ? translate('page.systemOperationLogs.result.success') : translate('page.systemOperationLogs.result.failed')} />
            <DetailItem label={translate('page.systemOperationLogs.detail.userId')} value={log.userId} />
            <DetailItem label={translate('page.systemOperationLogs.detail.userName')} value={log.userName} />
            <DetailItem label={translate('page.systemOperationLogs.detail.module')} value={log.moduleName} />
            <DetailItem label={translate('page.systemOperationLogs.detail.clientIp')} value={log.clientIp} />
            <DetailItem label={translate('page.systemOperationLogs.detail.requestMethod')} value={log.requestMethod} />
            <DetailItem label={translate('page.systemOperationLogs.detail.statusCode')} value={String(log.statusCode)} />
            <DetailItem label={translate('page.systemOperationLogs.detail.duration')} value={`${log.durationMs} ms`} />
            <DetailItem label={translate('page.systemOperationLogs.detail.operationType')} value={log.operationType} />
          </div>
          <DetailBlock label={translate('page.systemOperationLogs.detail.traceId')} value={log.traceId} mono />
          <DetailBlock label={translate('page.systemOperationLogs.detail.correlationId')} value={log.correlationId} mono />
          <DetailBlock label={translate('page.systemOperationLogs.detail.requestPath')} value={log.requestPath} mono />
          <DetailBlock label={translate('page.systemOperationLogs.detail.action')} value={log.actionName} mono />
          <DetailBlock label={translate('page.systemOperationLogs.detail.routeDisplayName')} value={log.routeDisplayName} />
          <DetailBlock label={translate('page.systemOperationLogs.detail.requestQuery')} value={log.requestQuery} mono />
          <DetailBlock label={translate('page.systemOperationLogs.detail.exceptionSummary')} value={log.exceptionSummary} />
          <DetailBlock label={translate('page.systemOperationLogs.detail.errorMessage')} value={log.errorMessage} />
        </div>
      ) : (
        <div className="text-sm text-gray-500">{translate('page.systemOperationLogs.detail.notFound')}</div>
      )}
    </ResponsiveModal>
  );
}

function DetailItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="rounded border border-gray-200 bg-gray-50 px-3 py-2">
      <div className="text-xs text-gray-500">{label}</div>
      <div className="mt-1 text-gray-800 break-all">{value || '-'}</div>
    </div>
  );
}

function DetailBlock({ label, mono = false, value }: { label: string; mono?: boolean; value?: string | null }) {
  return (
    <div>
      <div className="text-xs text-gray-500 mb-1">{label}</div>
      <div className={`rounded border border-gray-200 bg-gray-50 px-3 py-2 text-gray-800 break-all ${mono ? 'font-mono text-xs' : ''}`}>
        {value || '-'}
      </div>
    </div>
  );
}

function MethodBadge({ method }: { method: string }) {
  return <span className="inline-flex items-center justify-center min-w-[52px] rounded border border-gray-200 bg-gray-50 px-2 py-0.5 text-xs font-medium text-gray-700">{method}</span>;
}

function SuccessBadge({ isSuccess, translate }: { isSuccess: boolean; translate: (key: string) => string; }) {
  return (
    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${isSuccess ? 'bg-emerald-50 text-emerald-700' : 'bg-red-50 text-red-700'}`}>
      {isSuccess ? translate('page.systemOperationLogs.result.success') : translate('page.systemOperationLogs.result.failed')}
    </span>
  );
}

function normalizeSearchState(value: OperationLogSearchState): OperationLogSearchState {
  return {
    endTime: trimToUndefined(value.endTime),
    isSuccess: value.isSuccess || '',
    moduleName: trimToUndefined(value.moduleName),
    requestMethod: trimToUndefined(value.requestMethod),
    requestPath: trimToUndefined(value.requestPath),
    startTime: trimToUndefined(value.startTime),
    traceId: trimToUndefined(value.traceId),
    user: trimToUndefined(value.user)
  };
}

function parseSuccessState(value: OperationLogSearchState['isSuccess']) {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return undefined;
}

function trimToUndefined(value?: string) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function formatDateTime(value: string) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}
