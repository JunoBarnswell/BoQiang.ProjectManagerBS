import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkflowFormResourceDto } from '../../api/workflow/workflows.api';
import { getWorkflowFormResources } from '../../api/workflow/workflows.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

export function WorkflowFormsPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const formsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowFormResources({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'forms', workspace?.tenantId, workspace?.appCode, keyword, pageIndex, pageSize]
  });

  const columns = useMemo<DataTableColumn<WorkflowFormResourceDto>[]>(() => [
    {
      key: 'resourceName',
      title: translate('page.workflowForms.column.form'),
      width: '280px',
      responsivePriority: 100,
      render: (row) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900" title={row.resourceName}>{row.resourceName}</div>
          <div className="truncate font-mono text-xs text-gray-500" title={row.resourceCode}>{row.resourceCode}</div>
        </div>
      )
    },
    { key: 'businessType', title: translate('page.workflowForms.column.businessType'), width: '150px', responsivePriority: 90 },
    { key: 'menuCode', title: translate('page.workflowForms.column.menuCode'), width: '180px', hideBelow: 'lg', render: (row) => renderCode(row.menuCode) },
    { key: 'modelCode', title: translate('page.workflowForms.column.modelCode'), width: '160px', hideBelow: 'lg', render: (row) => renderCode(row.modelCode) },
    { key: 'keyField', title: translate('page.workflowForms.column.keyField'), width: '130px', hideBelow: 'xl', render: (row) => renderCode(row.keyField) },
    { key: 'fields', title: translate('page.workflowForms.column.fields'), width: '90px', render: (row) => row.fields.length }
  ], [translate]);

  return (
    <CrudPage
      title={translate('page.workflowForms.title')}
      description={translate('page.workflowForms.description')}
      actions={(
        <label className="workflow-toolbar-search">
          <AppIcon name="magnifying-glass" />
          <input placeholder={translate('page.workflowForms.search.placeholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
        </label>
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="workflow-forms"
          columns={columns}
          emptyText={formsQuery.isError ? translate('page.workflowForms.empty.loadFailed') : translate('page.workflowForms.empty.resource')}
          fitScreen
          loading={formsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: formsQuery.data?.data.total ?? 0 }}
          rowActions={(row) => row.routePath ? <button className="hover:text-primary-600" title={translate('page.workflowForms.action.openForm')} type="button" onClick={() => navigate(row.routePath ?? '/home')}><AppIcon className="text-base" name="arrow-square-out" /></button> : null}
          rowKey={(row) => row.resourceCode}
          rows={formsQuery.data?.data.items ?? []}
        />
      </div>
    </CrudPage>
  );
}

function renderCode(value?: string | null) {
  return value ? <span className="font-mono text-xs text-gray-600">{value}</span> : <span className="text-gray-400">-</span>;
}
