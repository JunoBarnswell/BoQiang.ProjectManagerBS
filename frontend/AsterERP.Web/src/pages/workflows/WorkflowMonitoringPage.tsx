import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkflowInstanceListItemDto } from '../../api/workflow/workflows.api';
import { getWorkflowInstances, terminateWorkflowInstance } from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

export function WorkflowMonitoringPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [status, setStatus] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const instancesQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowInstances({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, status, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'monitoring', workspace?.tenantId, workspace?.appCode, keyword, status, pageIndex, pageSize]
  });
  const terminateMutation = useApiMutation({ mutationFn: ({ processInstanceId }: { processInstanceId: string }) => terminateWorkflowInstance(processInstanceId, translate('page.workflowMonitoring.terminateReason')) });

  const columns = useMemo<DataTableColumn<WorkflowInstanceListItemDto>[]>(() => [
    { key: 'businessKey', title: translate('page.workflowMonitoring.column.business'), width: '240px', responsivePriority: 100, render: (row) => <><div className="font-medium text-gray-900">{row.businessType}</div><div className="font-mono text-xs text-gray-500">{row.businessKey}</div></> },
    { key: 'processDefinitionKey', title: translate('page.workflowMonitoring.column.processDefinition'), width: '190px', responsivePriority: 90, render: (row) => renderCode(row.processDefinitionKey) },
    { key: 'status', title: translate('page.workflowMonitoring.column.status'), width: '120px', render: (row) => row.status },
    { key: 'startedBy', title: translate('page.workflowMonitoring.column.startedBy'), width: '140px', hideBelow: 'lg' },
    { key: 'startedAt', title: translate('page.workflowMonitoring.column.startedAt'), width: '180px', hideBelow: 'lg', render: (row) => formatDateTime(row.startedAt) },
    { key: 'finishedAt', title: translate('page.workflowMonitoring.column.finishedAt'), width: '180px', hideBelow: 'xl', render: (row) => formatDateTime(row.finishedAt) }
  ], [translate]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['workflows', 'monitoring'] });

  const terminate = (row: WorkflowInstanceListItemDto) => {
    confirm({
      title: translate('page.workflowMonitoring.confirm.terminateTitle'),
      content: formatMessage(translate('page.workflowMonitoring.confirm.terminateContent'), { processInstanceId: row.processInstanceId }),
      confirmText: translate('page.workflowMonitoring.confirm.terminateAction'),
      onConfirm: async () => {
        try {
          await terminateMutation.mutateAsync({ processInstanceId: row.processInstanceId });
          await refresh();
          message.success(translate('page.workflowMonitoring.success.terminated'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('page.workflowMonitoring.error.terminateFailed')));
        }
      }
    });
  };

  return (
    <CrudPage
      title={translate('page.workflowMonitoring.title')}
      description={translate('page.workflowMonitoring.description')}
      actions={(
        <div className="workflow-page-actions">
          <label className="workflow-toolbar-search">
            <AppIcon name="magnifying-glass" />
            <input placeholder={translate('page.workflowMonitoring.search.placeholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          </label>
          <select className="h-9 rounded border border-gray-300 bg-white px-3 text-sm" value={status} onChange={(event) => { setStatus(event.target.value); setPageIndex(1); }}>
            <option value="">{translate('page.workflowMonitoring.status.all')}</option>
            <option value="Running">{translate('page.workflowMonitoring.status.running')}</option>
            <option value="Completed">{translate('page.workflowMonitoring.status.completed')}</option>
            <option value="Rejected">{translate('page.workflowMonitoring.status.rejected')}</option>
            <option value="Withdrawn">{translate('page.workflowMonitoring.status.withdrawn')}</option>
            <option value="Terminated">{translate('page.workflowMonitoring.status.terminated')}</option>
          </select>
        </div>
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="workflow-monitoring"
          columns={columns}
          emptyText={instancesQuery.isError ? translate('page.workflowMonitoring.error.loadFailed') : translate('page.workflowMonitoring.empty')}
          fitScreen
          loading={instancesQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: instancesQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              <button className="hover:text-primary-600" title={translate('common.view')} type="button" onClick={() => navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="eye" /></button>
              {row.status === 'Running' ? <PermissionButton code="workflow:instance:terminate" className="hover:text-red-600" title={translate('page.workflowMonitoring.action.terminate')} type="button" onClick={() => terminate(row)}><AppIcon className="text-base" name="stop-circle" /></PermissionButton> : null}
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={instancesQuery.data?.data.items ?? []}
        />
      </div>
    </CrudPage>
  );
}

function formatDateTime(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-';
}

function renderCode(value?: string | null) {
  return value ? <span className="font-mono text-xs text-gray-600">{value}</span> : <span className="text-gray-400">-</span>;
}
