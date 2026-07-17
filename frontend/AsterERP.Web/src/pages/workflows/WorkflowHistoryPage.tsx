import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkflowHistoricProcessDto, WorkflowHistoricTaskDto } from '../../api/workflow/workflows.api';
import { getWorkflowHistoryProcesses, getWorkflowHistoryTasks } from '../../api/workflow/workflows.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

type HistoryTab = 'processes' | 'tasks';

export function WorkflowHistoryPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const [tab, setTab] = useState<HistoryTab>('processes');
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const processQuery = useApiQuery({
    enabled: tab === 'processes',
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowHistoryProcesses({ keyword, pageIndex, pageSize }, signal),
    queryKey: ['workflows', 'history', 'processes', keyword, pageIndex, pageSize]
  });
  const taskQuery = useApiQuery({
    enabled: tab === 'tasks',
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowHistoryTasks({ keyword, pageIndex, pageSize }, signal),
    queryKey: ['workflows', 'history', 'tasks', keyword, pageIndex, pageSize]
  });

  const processColumns: DataTableColumn<WorkflowHistoricProcessDto>[] = [
    { key: 'id', title: translate('page.workflowHistory.column.processInstance'), width: '260px', responsivePriority: 100, render: (row) => <><div className="font-medium text-gray-900">{row.processName ?? row.processDefinitionId ?? row.id}</div><div className="text-xs text-gray-500">{row.id}</div></> },
    { key: 'businessKey', title: translate('page.workflowHistory.column.business'), width: '180px', render: (row) => <><div>{row.businessType ?? '-'}</div><div className="text-xs text-gray-500">{row.businessKey ?? '-'}</div></> },
    { key: 'status', title: translate('page.workflowHistory.column.status'), width: '110px', render: (row) => row.status ?? '-' },
    { key: 'startUserId', title: translate('page.workflowHistory.column.initiator'), width: '140px', render: (row) => row.starterUserName ?? row.startUserId ?? '-' },
    { key: 'startTime', title: translate('page.workflowHistory.column.startTime'), width: '180px', render: (row) => row.startTime ? new Date(row.startTime).toLocaleString() : '-' },
    { key: 'endTime', title: translate('page.workflowHistory.column.endTime'), width: '180px', render: (row) => row.endTime ? new Date(row.endTime).toLocaleString() : '-' }
  ];
  const taskColumns: DataTableColumn<WorkflowHistoricTaskDto>[] = [
    { key: 'name', title: translate('page.workflowHistory.column.task'), width: '220px', responsivePriority: 100, render: (row) => <><div className="font-medium text-gray-900">{row.name ?? row.id}</div><div className="text-xs text-gray-500">{row.processName ?? row.processDefinitionId ?? '-'}</div></> },
    { key: 'business', title: translate('page.workflowHistory.column.business'), width: '180px', render: (row) => <><div>{row.businessType ?? '-'}</div><div className="text-xs text-gray-500">{row.businessKey ?? row.processInstanceId ?? '-'}</div></> },
    { key: 'assignee', title: translate('page.workflowHistory.column.assignee'), width: '140px', render: (row) => row.assigneeName ?? row.assignee ?? '-' },
    { key: 'counts', title: translate('page.workflowHistory.column.counts'), width: '120px', render: (row) => `${row.commentsCount}/${row.attachmentsCount}` },
    { key: 'startTime', title: translate('page.workflowHistory.column.startTime'), width: '180px', render: (row) => row.startTime ? new Date(row.startTime).toLocaleString() : '-' },
    { key: 'endTime', title: translate('page.workflowHistory.column.endTime'), width: '180px', render: (row) => row.endTime ? new Date(row.endTime).toLocaleString() : '-' }
  ];

  const total = tab === 'processes' ? processQuery.data?.data.total ?? 0 : taskQuery.data?.data.total ?? 0;

  return (
    <CrudPage
      title={translate('page.workflowHistory.title')}
      actions={(
        <div className="flex flex-wrap items-center gap-2">
          <div className="inline-flex border border-gray-300 rounded overflow-hidden bg-white">
            <button className={`px-3 py-1.5 text-sm ${tab === 'processes' ? 'bg-primary-50 text-primary-700' : 'text-gray-700'}`} type="button" onClick={() => { setTab('processes'); setPageIndex(1); }}>{translate('page.workflowHistory.tab.processes')}</button>
            <button className={`px-3 py-1.5 text-sm ${tab === 'tasks' ? 'bg-primary-50 text-primary-700' : 'text-gray-700'}`} type="button" onClick={() => { setTab('tasks'); setPageIndex(1); }}>{translate('page.workflowHistory.tab.tasks')}</button>
          </div>
          <input className="border border-gray-300 rounded px-3 py-1.5 text-sm w-56" placeholder={translate('page.workflowHistory.search.placeholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
        </div>
      )}
    >
      <div className="flex-1 bg-white border border-gray-200 rounded-lg shadow-sm min-h-0">
        {tab === 'processes' ? (
          <DataTable
            columnSettingsKey="workflow-history-processes"
            columns={processColumns}
            emptyText={processQuery.isError ? translate('page.workflowHistory.empty.processesLoadFailed') : translate('page.workflowHistory.empty.processes')}
            fitScreen
            loading={processQuery.isLoading}
            onPageChange={setPageIndex}
            onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
            pagination={{ current: pageIndex, pageSize, total }}
            rowActions={(row) => <button className="hover:text-primary-600" title={translate('common.view')} type="button" onClick={() => navigate(`/workflows/instances/${row.id}`)}><AppIcon className="text-base" name="eye" /></button>}
            rowKey={(row) => row.id}
            rows={processQuery.data?.data.items ?? []}
          />
        ) : (
          <DataTable
            columnSettingsKey="workflow-history-tasks"
            columns={taskColumns}
            emptyText={taskQuery.isError ? translate('page.workflowHistory.empty.tasksLoadFailed') : translate('page.workflowHistory.empty.tasks')}
            fitScreen
            loading={taskQuery.isLoading}
            onPageChange={setPageIndex}
            onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
            pagination={{ current: pageIndex, pageSize, total }}
            rowActions={(row) => row.processInstanceId ? <button className="hover:text-primary-600" title={translate('common.view')} type="button" onClick={() => navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="eye" /></button> : null}
            rowKey={(row) => row.id}
            rows={taskQuery.data?.data.items ?? []}
          />
        )}
      </div>
    </CrudPage>
  );
}
