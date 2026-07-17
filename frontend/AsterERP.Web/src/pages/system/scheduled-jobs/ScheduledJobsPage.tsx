import { useQueryClient } from '@tanstack/react-query';
import { useMemo, type SetStateAction } from 'react';

import {
  systemScheduledJobApi,
  type ScheduledJobListItemDto,
  type ScheduledJobLogDto,
  type ScheduledJobUpsertRequest
} from '../../../api/system/scheduled-jobs.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { LOOKUP_STALE_TIME_MS } from '../../../core/query/cacheDurations';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { CrudPage } from '../../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { ResponsiveModal } from '../../../shared/responsive/ResponsiveModal';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { getErrorMessage } from '../../../shared/utils/errorMessage';

import {
  JobNameCell,
  JobTypeBadge,
  ResultBadge,
  ScheduledJobEditor,
  ScheduledJobSearchBar,
  StatusBadge,
  SummaryStrip,
  SyncBadge,
  buildUpsertRequest,
  defaultFormState,
  defaultSearchState,
  defaultTableQuery,
  formatDateTime,
  createJobResultFilterOptions,
  createJobStatusFilterOptions,
  createJobTypeFilterOptions,
  createTriggerTypeFilterOptions,
  mapDetailToForm,
  validateRequest,
  type ScheduledJobFormState,
  type ScheduledJobSearchState,
  type ScheduledJobsPageState
} from './scheduledJobsSupport';

export function ScheduledJobsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [pageState, setPageState] = useTabPageState<ScheduledJobsPageState>(
    {
      editingId: null,
      formOpen: false,
      formState: defaultFormState,
      logJobId: null,
      logPageIndex: 1,
      logPageSize: 10,
      logResult: '',
      logSorts: [],
      logTableQuery: defaultTableQuery,
      pageIndex: 1,
      pageSize: 20,
      searchState: defaultSearchState,
      sorts: [],
      tableQuery: defaultTableQuery
    },
    { cacheKey: 'scheduled-jobs-page' }
  );

  const { editingId, formOpen, formState, logJobId, logPageIndex, logPageSize, logResult, logSorts, logTableQuery, pageIndex, pageSize, searchState, sorts, tableQuery } = pageState;

  const setPageField = <K extends keyof ScheduledJobsPageState>(key: K, value: SetStateAction<ScheduledJobsPageState[K]>) => {
    setPageState((current) => ({
      ...current,
      [key]: typeof value === 'function' ? (value as (previous: ScheduledJobsPageState[K]) => ScheduledJobsPageState[K])(current[key]) : value
    }));
  };

  const setFormState = (value: SetStateAction<ScheduledJobFormState>) => setPageField('formState', value);
  const setSearchState = (value: SetStateAction<ScheduledJobSearchState>) => setPageField('searchState', value);

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) =>
      systemScheduledJobApi.list({
        filters: tableQuery.conditions,
        jobType: searchState.jobType,
        keyword: searchState.keyword,
        pageIndex,
        pageSize,
        result: searchState.result,
        sorts,
        status: searchState.status
      }, signal),
    queryKey: ['system-scheduled-jobs', 'list', pageIndex, pageSize, searchState.keyword, searchState.jobType, searchState.status, searchState.result, sorts, tableQuery]
  });

  const summaryQuery = useApiQuery({
    queryFn: systemScheduledJobApi.summary,
    queryKey: ['system-scheduled-jobs', 'summary'],
    staleTimeMs: LOOKUP_STALE_TIME_MS
  });

  const typesQuery = useApiQuery({
    queryFn: systemScheduledJobApi.jobTypes,
    queryKey: ['system-scheduled-jobs', 'types'],
    staleTimeMs: LOOKUP_STALE_TIME_MS
  });

  const logsQuery = useApiQuery({
    enabled: logJobId !== null,
    keepPreviousData: true,
    queryFn: ({ signal }) => systemScheduledJobApi.logs(logJobId ?? '', { filters: logTableQuery.conditions, pageIndex: logPageIndex, pageSize: logPageSize, result: logResult, sorts: logSorts }, signal),
    queryKey: ['system-scheduled-jobs', 'logs', logJobId ?? 'none', logPageIndex, logPageSize, logResult, logSorts, logTableQuery]
  });

  const createMutation = useApiMutation({ mutationFn: (request: ScheduledJobUpsertRequest) => systemScheduledJobApi.create(request) });
  const updateMutation = useApiMutation({ mutationFn: ({ id, request }: { id: string; request: ScheduledJobUpsertRequest }) => systemScheduledJobApi.update(id, request) });
  const deleteMutation = useApiMutation({ mutationFn: (id: string) => systemScheduledJobApi.delete(id) });
  const pauseMutation = useApiMutation({ mutationFn: (id: string) => systemScheduledJobApi.pause(id) });
  const resumeMutation = useApiMutation({ mutationFn: (id: string) => systemScheduledJobApi.resume(id) });
  const triggerMutation = useApiMutation({ mutationFn: (id: string) => systemScheduledJobApi.trigger(id) });

  const rows = listQuery.data?.data.items ?? [];
  const total = listQuery.data?.data.total ?? 0;
  const logs = logsQuery.data?.data.items ?? [];
  const logTotal = logsQuery.data?.data.total ?? 0;
  const selectedLogJob = rows.find((row) => row.id === logJobId) ?? null;

  const refreshAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ['system-scheduled-jobs'] });
  };

  const openCreate = () => {
    setPageField('editingId', null);
    setFormState(defaultFormState);
    setPageField('formOpen', true);
  };

  const openEdit = async (row: ScheduledJobListItemDto) => {
    try {
      const response = await systemScheduledJobApi.detail(row.id);
      setPageField('editingId', row.id);
      setFormState(mapDetailToForm(response.data));
      setPageField('formOpen', true);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemScheduledJobs.error.loadDetailFailed')));
    }
  };

  const handleSearch = () => {
    setPageField('pageIndex', 1);
    setSearchState((current) => ({ ...current, keyword: current.keywordDraft.trim() }));
  };

  const handleReset = () => {
    setPageField('pageIndex', 1);
    setPageField('sorts', []);
    setPageField('tableQuery', defaultTableQuery);
    setSearchState(defaultSearchState);
  };

  const handleSave = async () => {
    const request = buildUpsertRequest(formState);
    const validationMessage = validateRequest(request, translate);
    if (validationMessage) {
      message.error(validationMessage);
      return;
    }

    try {
      if (editingId) {
        await updateMutation.mutateAsync({ id: editingId, request });
        message.success(translate('page.systemScheduledJobs.success.update'));
      } else {
        await createMutation.mutateAsync(request);
        message.success(translate('page.systemScheduledJobs.success.create'));
      }

      setPageField('formOpen', false);
      await refreshAll();
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemScheduledJobs.error.saveFailed')));
    }
  };

  const handleDelete = (row: ScheduledJobListItemDto) => {
    confirm({
      title: translate('page.systemScheduledJobs.confirm.deleteTitle'),
      content: translate('page.systemScheduledJobs.confirm.deleteContent').replace('{name}', row.name),
      onConfirm: async () => {
        try {
          await deleteMutation.mutateAsync(row.id);
          await refreshAll();
          message.success(translate('page.systemScheduledJobs.success.delete'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.systemScheduledJobs.error.deleteFailed')));
        }
      }
    });
  };

  const handleStatusChange = async (row: ScheduledJobListItemDto) => {
    try {
      if (row.status === 'Enabled') {
        await pauseMutation.mutateAsync(row.id);
        message.success(translate('page.systemScheduledJobs.success.paused'));
      } else {
        await resumeMutation.mutateAsync(row.id);
        message.success(translate('page.systemScheduledJobs.success.resumed'));
      }

      await refreshAll();
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemScheduledJobs.error.statusChangeFailed')));
    }
  };

  const handleTrigger = async (row: ScheduledJobListItemDto) => {
    try {
      const response = await triggerMutation.mutateAsync(row.id);
      message.success(translate('page.systemScheduledJobs.success.triggered').replace('{jobId}', response.data));
      setPageField('logJobId', row.id);
      setPageField('logPageIndex', 1);
      await refreshAll();
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.systemScheduledJobs.error.triggerFailed')));
    }
  };

  const columns: DataTableColumn<ScheduledJobListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.systemScheduledJobs.column.index'), width: '70px', align: 'center', responsivePriority: 100, render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      { key: 'name', title: translate('page.systemScheduledJobs.column.name'), width: '190px', responsivePriority: 100, sortable: true, sortField: 'jobName', filterable: true, filterField: 'jobName', filterType: 'text', render: (row) => <JobNameCell row={row} /> },
      { key: 'jobType', title: translate('page.systemScheduledJobs.column.jobType'), width: '110px', align: 'center', responsivePriority: 95, sortable: true, filterable: true, filterType: 'select', filterOperators: ['equals'], filterOptions: createJobTypeFilterOptions(translate), render: (row) => <JobTypeBadge translate={translate} type={row.jobType} /> },
      { key: 'friendlySchedule', title: translate('page.systemScheduledJobs.column.schedule'), responsivePriority: 100, sortable: false, filterable: false },
      { key: 'status', title: translate('page.systemScheduledJobs.column.status'), width: '90px', align: 'center', responsivePriority: 100, sortable: true, filterable: true, filterType: 'select', filterOperators: ['equals'], filterOptions: createJobStatusFilterOptions(translate), render: (row) => <StatusBadge status={row.status} translate={translate} /> },
      { key: 'lastResult', title: translate('page.systemScheduledJobs.column.lastResult'), width: '100px', align: 'center', responsivePriority: 95, sortable: true, filterable: true, filterType: 'select', filterOperators: ['equals'], filterOptions: createJobResultFilterOptions(translate), render: (row) => <ResultBadge result={row.lastResult} translate={translate} /> },
      { key: 'lastRunAt', title: translate('page.systemScheduledJobs.column.lastRunAt'), width: '170px', responsivePriority: 80, sortable: true, filterable: true, filterType: 'date', render: (row) => formatDateTime(row.lastRunAt) },
      { key: 'nextRunAt', title: translate('page.systemScheduledJobs.column.nextRunAt'), width: '170px', responsivePriority: 90, sortable: true, filterable: true, filterType: 'date', render: (row) => formatDateTime(row.nextRunAt) },
      { key: 'scheduleSyncStatus', title: translate('page.systemScheduledJobs.column.syncStatus'), width: '86px', align: 'center', responsivePriority: 70, sortable: true, filterable: true, filterType: 'text', render: (row) => <SyncBadge translate={translate} value={row.scheduleSyncStatus} /> }
    ],
    [pageIndex, pageSize, translate]
  );

  const logColumns: DataTableColumn<ScheduledJobLogDto>[] = useMemo(
    () => [
      { key: 'startTime', title: translate('page.systemScheduledJobs.log.column.startTime'), width: '170px', responsivePriority: 100, sortable: true, sortField: 'startedAt', filterable: true, filterField: 'startedAt', filterType: 'date', render: (row) => formatDateTime(row.startTime) },
      { key: 'triggerType', title: translate('page.systemScheduledJobs.log.column.triggerType'), width: '90px', align: 'center', responsivePriority: 90, sortable: true, filterable: true, filterType: 'select', filterOperators: ['equals'], filterOptions: createTriggerTypeFilterOptions(translate), render: (row) => (row.triggerType === 'Manual' ? translate('page.systemScheduledJobs.trigger.manual') : translate('page.systemScheduledJobs.trigger.automatic')) },
      { key: 'result', title: translate('page.systemScheduledJobs.log.column.result'), width: '90px', align: 'center', responsivePriority: 100, sortable: true, filterable: true, filterType: 'select', filterOperators: ['equals'], filterOptions: createJobResultFilterOptions(translate), render: (row) => <ResultBadge result={row.result} translate={translate} /> },
      { key: 'durationMs', title: translate('page.systemScheduledJobs.log.column.duration'), width: '92px', align: 'right', responsivePriority: 80, sortable: true, filterable: true, filterType: 'number', render: (row) => `${row.durationMs} ms` },
      { key: 'outputSummary', title: translate('page.systemScheduledJobs.log.column.outputSummary'), responsivePriority: 100, sortable: true, filterable: true, filterType: 'text', render: (row) => row.outputSummary || row.errorMessage || '-' },
      { key: 'jobId', title: translate('page.systemScheduledJobs.log.column.jobId'), width: '110px', responsivePriority: 60, sortable: true, sortField: 'hangfireJobId', filterable: true, filterField: 'hangfireJobId', filterType: 'text', render: (row) => <span className="font-mono text-xs">{row.jobId || '-'}</span> },
      { key: 'traceId', title: translate('page.systemScheduledJobs.log.column.traceId'), width: '180px', responsivePriority: 60, sortable: true, filterable: true, filterType: 'text', render: (row) => <span className="font-mono text-xs">{row.traceId}</span> }
    ],
    [translate]
  );

  return (
    <CrudPage
      actions={
        <div className="flex items-center gap-2">
          <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50 hover:text-primary-600 flex items-center gap-1 shadow-sm transition-colors" type="button" onClick={() => void refreshAll()}>
            <AppIcon name="arrows-clockwise" /> {translate('common.refresh')}
          </button>
          <PermissionButton code="system:scheduled-job:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1 shadow-sm font-medium transition-colors" type="button" onClick={openCreate}>
            <AppIcon name="plus" /> {translate('page.systemScheduledJobs.action.create')}
          </PermissionButton>
        </div>
      }
      description={translate('page.systemScheduledJobs.description')}
      eyebrow={translate('nav.systemScheduledJobs')}
      searchArea={
        <ScheduledJobSearchBar
          translate={translate}
          value={searchState}
          onChange={setSearchState}
          onReset={handleReset}
          onSearch={handleSearch}
        />
      }
      title={translate('page.systemScheduledJobs.title')}
    >
      <SummaryStrip summary={summaryQuery.data?.data ?? null} translate={translate} />
      <div className="flex-1 flex flex-col min-w-0 h-full overflow-hidden bg-white border border-gray-200 rounded-lg shadow-sm">
        <DataTable
          columnSettingsKey="system-scheduled-jobs"
          columns={columns}
          emptyText={listQuery.isError ? translate('page.systemScheduledJobs.error.loadFailed') : translate('page.systemScheduledJobs.empty')}
          fitScreen
          loading={listQuery.isLoading}
          onPageChange={(value) => setPageField('pageIndex', value)}
          onPageSizeChange={(value) => {
            setPageField('pageIndex', 1);
            setPageField('pageSize', value);
          }}
          onQueryChange={(nextQuery) => setPageState((current) => ({ ...current, pageIndex: 1, tableQuery: nextQuery }))}
          onSortsChange={(nextSorts) => setPageState((current) => ({ ...current, pageIndex: 1, sorts: nextSorts }))}
          pageSizeOptions={[10, 20, 50, 100]}
          pagination={{ current: pageIndex, pageSize, total }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton code="system:scheduled-job:edit" className="hover:text-primary-600 transition-colors" title={translate('common.edit')} type="button" onClick={() => void openEdit(row)}>
                <AppIcon className="text-base" name="pencil-simple" />
              </PermissionButton>
              <PermissionButton code="system:scheduled-job:edit" className="hover:text-primary-600 transition-colors" title={row.status === 'Enabled' ? translate('page.systemScheduledJobs.action.pause') : translate('page.systemScheduledJobs.action.resume')} type="button" onClick={() => void handleStatusChange(row)}>
                <AppIcon className="text-base" name={row.status === 'Enabled' ? 'pause-circle' : 'play-circle'} />
              </PermissionButton>
              <PermissionButton code="system:scheduled-job:trigger" className="hover:text-primary-600 transition-colors" title={translate('page.systemScheduledJobs.action.trigger')} type="button" onClick={() => void handleTrigger(row)}>
                <AppIcon className="text-base" name="lightning" />
              </PermissionButton>
              <PermissionButton code="system:scheduled-job:log" className="hover:text-primary-600 transition-colors" title={translate('page.systemScheduledJobs.action.log')} type="button" onClick={() => {
                setPageField('logJobId', row.id);
                setPageField('logPageIndex', 1);
              }}>
                <AppIcon className="text-base" name="file-text" />
              </PermissionButton>
              <PermissionButton code="system:scheduled-job:delete" className="hover:text-red-600 transition-colors" title={translate('common.delete')} type="button" onClick={() => handleDelete(row)}>
                <AppIcon className="text-base" name="trash" />
              </PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={rows}
          sorts={sorts}
          tableQuery={tableQuery}
        />
      </div>

      <ScheduledJobEditor
        formState={formState}
        isSaving={createMutation.isPending || updateMutation.isPending}
        jobTypes={typesQuery.data?.data ?? null}
        open={formOpen}
        title={editingId ? translate('page.systemScheduledJobs.modal.editTitle') : translate('page.systemScheduledJobs.modal.createTitle')}
        translate={translate}
        onChange={setFormState}
        onClose={() => setPageField('formOpen', false)}
        onSave={() => void handleSave()}
      />

      <ResponsiveModal
        mode="drawer"
        open={logJobId !== null}
        title={selectedLogJob ? translate('page.systemScheduledJobs.log.titleWithName').replace('{name}', selectedLogJob.name) : translate('page.systemScheduledJobs.log.title')}
        description={translate('page.systemScheduledJobs.log.description')}
        onClose={() => setPageField('logJobId', null)}
      >
        <div className="flex flex-col gap-3 h-full">
          <div className="flex items-center gap-2">
            <select className="border border-gray-300 rounded bg-white px-2 py-1.5 text-sm" value={logResult} onChange={(event) => {
              setPageField('logPageIndex', 1);
              setPageField('logTableQuery', defaultTableQuery);
              setPageField('logResult', event.target.value);
            }}>
              <option value="">{translate('page.systemScheduledJobs.search.allResults')}</option>
              <option value="Queued">{translate('page.systemScheduledJobs.result.queued')}</option>
              <option value="Success">{translate('page.systemScheduledJobs.result.success')}</option>
              <option value="Failed">{translate('page.systemScheduledJobs.result.failed')}</option>
            </select>
            <button className="border border-gray-300 rounded px-3 py-1.5 text-sm hover:bg-gray-50" type="button" onClick={() => void logsQuery.refetch()}>
              {translate('page.systemScheduledJobs.action.refreshLogs')}
            </button>
          </div>
          <DataTable
            columnSettingsKey="system-scheduled-job-logs"
            columns={logColumns}
            emptyText={logsQuery.isError ? translate('page.systemScheduledJobs.error.logLoadFailed') : translate('page.systemScheduledJobs.emptyLogs')}
            fitScreen
            loading={logsQuery.isLoading}
            onPageChange={(value) => setPageField('logPageIndex', value)}
            onPageSizeChange={(value) => {
              setPageField('logPageIndex', 1);
              setPageField('logPageSize', value);
            }}
            onQueryChange={(nextQuery) => setPageState((current) => ({ ...current, logPageIndex: 1, logTableQuery: nextQuery }))}
            onSortsChange={(nextSorts) => setPageState((current) => ({ ...current, logPageIndex: 1, logSorts: nextSorts }))}
            pageSizeOptions={[10, 20, 50]}
            pagination={{ current: logPageIndex, pageSize: logPageSize, total: logTotal }}
            rowKey={(row) => row.id}
            rows={logs}
            sorts={logSorts}
            tableQuery={logTableQuery}
          />
        </div>
      </ResponsiveModal>
    </CrudPage>
  );
}
