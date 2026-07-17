import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import type { WorkflowBusinessDataReportItemDto } from '../../api/workflow/workflows.api';
import { getWorkflowReportOverview } from '../../api/workflow/workflows.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

type ReportTab = 'approval' | 'efficiency' | 'business';

const reportTabs: Array<{ label: string; value: ReportTab }> = [
  { label: 'workflow.reports.tab.approval', value: 'approval' },
  { label: 'workflow.reports.tab.efficiency', value: 'efficiency' },
  { label: 'workflow.reports.tab.business', value: 'business' }
];

export function WorkflowReportsPage() {
  const { translate } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const initialTab = normalizeReportTab(searchParams.get('tab'));
  const [tab, setTab] = useState<ReportTab>(initialTab);
  const queryTab = normalizeReportTab(searchParams.get('tab'));

  useEffect(() => {
    if (queryTab !== tab) {
      setTab(queryTab);
    }
  }, [queryTab, tab]);

  const reportQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowReportOverview(signal),
    queryKey: ['workflows', 'reports', 'overview']
  });
  const overview = reportQuery.data?.data;

  const businessColumns = useMemo<DataTableColumn<WorkflowBusinessDataReportItemDto>[]>(() => [
    { key: 'businessType', title: translate('workflow.reports.businessType'), width: '220px', responsivePriority: 100 },
    { key: 'total', title: translate('workflow.reports.total'), width: '100px' },
    { key: 'running', title: translate('workflow.reports.running'), width: '100px' },
    { key: 'finished', title: translate('workflow.reports.finished'), width: '100px' }
  ], [translate]);

  const setActiveTab = (next: ReportTab) => {
    setTab(next);
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set('tab', next);
    setSearchParams(nextSearchParams);
  };

  return (
    <CrudPage
      title={translate('workflow.reports.title')}
      description={translate('workflow.reports.description')}
      actions={(
        <div className="inline-flex overflow-hidden rounded border border-gray-300 bg-white">
          {reportTabs.map((item) => (
            <button key={item.value} className={`px-3 py-1.5 text-sm ${tab === item.value ? 'bg-primary-50 text-primary-700' : 'text-gray-700'}`} type="button" onClick={() => setActiveTab(item.value)}>
              {translate(item.label)}
            </button>
          ))}
        </div>
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
        {tab === 'approval' ? (
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            <MetricCard label={translate('workflow.reports.approval.started')} value={overview?.approvalStatistics.totalStarted ?? 0} />
            <MetricCard label={translate('workflow.reports.approval.running')} value={overview?.approvalStatistics.running ?? 0} />
            <MetricCard label={translate('workflow.reports.approval.completed')} value={overview?.approvalStatistics.completed ?? 0} />
            <MetricCard label={translate('workflow.reports.approval.rejected')} value={overview?.approvalStatistics.rejected ?? 0} />
            <MetricCard label={translate('workflow.reports.approval.todo')} value={overview?.approvalStatistics.todo ?? 0} />
            <MetricCard label={translate('workflow.reports.approval.done')} value={overview?.approvalStatistics.done ?? 0} />
            <MetricCard label={translate('workflow.reports.approval.cc')} value={overview?.approvalStatistics.cc ?? 0} />
            <MetricCard label={translate('workflow.reports.approval.terminated')} value={overview?.approvalStatistics.terminated ?? 0} />
          </div>
        ) : null}

        {tab === 'efficiency' ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
              <MetricCard label={translate('workflow.reports.efficiency.averageDuration')} value={`${(overview?.efficiencyAnalysis.averageDurationHours ?? 0).toFixed(2)}h`} />
              <MetricCard label={translate('workflow.reports.efficiency.overdueTaskCount')} value={overview?.efficiencyAnalysis.overdueTaskCount ?? 0} />
            </div>
            <div className="overflow-hidden rounded-md border border-gray-200">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-left text-xs text-gray-500">
                  <tr><th className="px-3 py-2">{translate('workflow.reports.efficiency.node')}</th><th className="px-3 py-2">{translate('workflow.reports.efficiency.completedCount')}</th><th className="px-3 py-2">{translate('workflow.reports.efficiency.averageStay')}</th></tr>
                </thead>
                <tbody>
                  {(overview?.efficiencyAnalysis.bottleneckNodes ?? []).map((item) => (
                    <tr key={item.nodeKey} className="border-t border-gray-100">
                      <td className="px-3 py-2"><div className="font-medium text-gray-900">{item.nodeName}</div><div className="font-mono text-xs text-gray-500">{item.nodeKey}</div></td>
                      <td className="px-3 py-2">{item.completedCount}</td>
                      <td className="px-3 py-2">{item.averageDurationHours.toFixed(2)}h</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}

        {tab === 'business' ? (
          <DataTable
            columnSettingsKey="workflow-report-business"
            columns={businessColumns}
            emptyText={reportQuery.isError ? translate('workflow.reports.loadFailed') : translate('workflow.reports.empty')}
            fitScreen
            loading={reportQuery.isLoading}
            rowKey={(row) => row.businessType}
            rows={overview?.businessData ?? []}
          />
        ) : null}
      </div>
    </CrudPage>
  );
}

function normalizeReportTab(value: string | null): ReportTab {
  const allowedTabs = new Set<ReportTab>(['approval', 'efficiency', 'business']);
  return allowedTabs.has(value as ReportTab) ? value as ReportTab : 'approval';
}

function MetricCard({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-md border border-gray-200 bg-gray-50 px-4 py-3">
      <div className="text-xs text-gray-500">{label}</div>
      <div className="mt-1 text-2xl font-semibold text-gray-900">{value}</div>
    </div>
  );
}
