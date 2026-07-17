import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { getApplicationDataSourceWorkbench } from '../../../../api/application-data-center/applicationDataCenter.api';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../../core/query/useApiQuery';
import { ApplicationConsolePageFrame } from '../../ApplicationConsolePageFrame';
import { DataCenterWorkspaceShell } from '../DataCenterWorkspaceShell';

import { WorkbenchShell } from './components/WorkbenchShell';
import { DataSourceMicroflowsPanel } from './DataSourceMicroflowsPanel';
import { DataSourceTablesPanel } from './DataSourceTablesPanel';
import { DataSourceViewsPanel } from './DataSourceViewsPanel';
import { MappingCachesPanel } from './MappingCachesPanel';
import type { WorkbenchTab, WorkbenchTabDefinition } from './workbenchTypes';

export function DataSourceWorkbenchPage() {
  const { appCode, dataSourceId = '', tenantId } = useParams();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<WorkbenchTab>('tables');
  const [selectedTable, setSelectedTable] = useState('');
  const queryClient = useQueryClient();

  const workbenchQuery = useApiQuery({
    enabled: Boolean(dataSourceId),
    queryFn: ({ signal }) => getApplicationDataSourceWorkbench(dataSourceId, signal),
    queryKey: ['application-data-center', 'workbench', dataSourceId]
  });
  const workbench = workbenchQuery.data?.data ?? null;
  const tabs = useMemo<WorkbenchTabDefinition[]>(() => [
    { badge: workbench?.stats.tableCount, description: '表/行数据', key: 'tables', title: '数据表管理' },
    { badge: workbench?.stats.viewCount, description: 'SQL 视图', key: 'views', title: '视图管理' },
    { badge: undefined, description: '', key: 'microflows', title: '微流管理' },
    { badge: workbench?.stats.mappingCacheCount, description: 'KEY 映射', key: 'mapping', title: '映射缓存' }
  ], [workbench?.stats]);
  const normalizedActiveTab = tabs.some((tab) => tab.key === activeTab) ? activeTab : tabs[0]?.key ?? 'tables';

  return (
    <ApplicationConsolePageFrame density="compact" hideDescription pageKey="data-center">
      {() => (
        <DataCenterWorkspaceShell moduleKey="data-source" selectedDataSourceId={dataSourceId}>
          <WorkbenchShell
            activeTab={normalizedActiveTab}
            loading={workbenchQuery.isFetching}
            onOpenAiWorkbench={openAiWorkbench}
            tabs={tabs}
            workbench={workbench}
            onBack={backToSources}
            onTabChange={setActiveTab}
          >
            {workbench && !workbench.isDatabase ? (
              <div className="rounded-md border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">{translateCurrentLiteral("当前数据源不是数据库，表、视图、微流 SQL 节点和映射缓存能力不可用。")}</div>
            ) : renderActivePanel()}
          </WorkbenchShell>
        </DataCenterWorkspaceShell>
      )}
    </ApplicationConsolePageFrame>
  );

  function renderActivePanel() {
    if (normalizedActiveTab === 'tables') {
      return <DataSourceTablesPanel dataSourceId={dataSourceId} selectedTable={selectedTable} onRefresh={refresh} onSelectedTableChange={setSelectedTable} />;
    }

    if (normalizedActiveTab === 'views') {
      return <DataSourceViewsPanel dataSourceId={dataSourceId} onRefresh={refresh} />;
    }

    if (normalizedActiveTab === 'microflows') {
      return <DataSourceMicroflowsPanel dataSourceId={dataSourceId} onRefresh={refresh} />;
    }

    if (normalizedActiveTab === 'mapping') {
      return <MappingCachesPanel dataSourceId={dataSourceId} onRefresh={refresh} />;
    }

    return <DataSourceTablesPanel dataSourceId={dataSourceId} selectedTable={selectedTable} onRefresh={refresh} onSelectedTableChange={setSelectedTable} />;
  }

  function refresh() {
    void queryClient.invalidateQueries({ queryKey: ['application-data-center'] });
  }

  function backToSources() {
    if (!tenantId || !appCode) {
      return;
    }

    navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/data-center/data-sources`);
  }

  function openAiWorkbench() {
    if (!tenantId || !appCode) {
      return;
    }

    const query = selectedTable ? `?dataSourceId=${encodeURIComponent(dataSourceId)}&selectedTable=${encodeURIComponent(selectedTable)}` : `?dataSourceId=${encodeURIComponent(dataSourceId)}`;
    navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/ai/workbench${query}`);
  }
}
