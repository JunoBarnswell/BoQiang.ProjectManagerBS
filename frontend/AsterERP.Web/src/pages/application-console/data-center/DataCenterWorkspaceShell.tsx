import { useParams } from 'react-router-dom';

import { getApplicationDataCenterWorkspace } from '../../../api/application-data-center/applicationDataCenter.api';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { WorkspaceShell, type WorkspaceShellNavItem } from '../workspace-shell/WorkspaceShell';

interface DataCenterWorkspaceShellProps {
  children: React.ReactNode;
  context?: React.ReactNode;
  moduleKey?: string;
  selectedDataSourceId?: string;
  toolbar?: React.ReactNode;
}

export function DataCenterWorkspaceShell({ children, context, moduleKey, selectedDataSourceId, toolbar }: DataCenterWorkspaceShellProps) {
  const { appCode, tenantId } = useParams();
  const prefix = tenantId && appCode
    ? `/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/data-center`
    : '/data-center';
  const workspaceQuery = useApiQuery({
    queryFn: ({ signal }) => getApplicationDataCenterWorkspace({ dataSourceId: selectedDataSourceId ?? null, moduleKey: moduleKey ?? null }, signal).then((response) => response.data),
    queryKey: queryKeys.applicationDataCenter.workspace(
      tenantId,
      appCode,
      moduleKey ?? 'all',
      selectedDataSourceId ?? 'none'
    )
  });
  const moduleCountByKey = new Map((workspaceQuery.data?.modules ?? []).map((item) => [item.moduleKey, item.totalCount]));

  const navItems: WorkspaceShellNavItem[] = [
    { badge: moduleCountByKey.get('data-source'), description: '管理应用数据库与工作台入口。', icon: 'database', key: 'data-sources', partialMatch: true, title: '数据源', to: `${prefix}/data-sources` },
    { badge: moduleCountByKey.get('entity-field'), description: '维护实体、字段和模型结构。', icon: 'table', key: 'entities-fields', title: '实体与字段', to: `${prefix}/entities-fields` },
    { badge: moduleCountByKey.get('query-dataset'), description: '配置报表查询视图与数据集。', icon: 'activity', key: 'query-datasets', title: '查询视图', to: `${prefix}/query-datasets` },
    { badge: moduleCountByKey.get('microflow'), description: '编排微流、节点、变量与接口。', icon: 'module', key: 'microflows', title: '微流管理', to: `${prefix}/microflows` }
  ];

  return (
    <WorkspaceShell
      context={context}
      density="tight"
      navDescription="数据源、实体字段、查询视图和微流。"
      navItems={navItems}
      navPlacement="top"
      navTitle="数据工作区"
      toolbar={toolbar}
    >
      {children}
    </WorkspaceShell>
  );
}
