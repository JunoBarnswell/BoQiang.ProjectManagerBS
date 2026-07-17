import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkflowFormResourceDto, WorkflowRequestDraftUpsertRequest } from '../../api/workflow/workflows.api';
import { getWorkflowFormResources, saveWorkflowDraft } from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { buildWorkspaceRoute } from './workflowWorkspaceRoutes';

const defaultDraft: WorkflowRequestDraftUpsertRequest = {
  appCode: null,
  businessKey: '',
  businessType: '',
  draftJson: '{}',
  formResourceCode: '',
  menuCode: '',
  tenantId: null,
  title: ''
};

export function WorkflowInitiatePage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const message = useMessage();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modalOpen, setModalOpen] = useState(false);
  const [draft, setDraft] = useState<WorkflowRequestDraftUpsertRequest>(defaultDraft);

  const formsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowFormResources({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'initiate', workspace?.tenantId, workspace?.appCode, keyword, pageIndex, pageSize]
  });
  const saveDraftMutation = useApiMutation({ mutationFn: saveWorkflowDraft });

  const fields = useMemo<FormFieldConfig<WorkflowRequestDraftUpsertRequest>[]>(() => [
    { label: translate('page.workflowInitiate.field.title'), name: 'title', required: true, type: 'text' },
    { label: translate('page.workflowInitiate.field.businessKey'), name: 'businessKey', required: true, type: 'text' },
    { label: translate('page.workflowInitiate.field.draftJson'), name: 'draftJson', required: true, rows: 10, type: 'textarea' }
  ], [translate]);

  const columns = useMemo<DataTableColumn<WorkflowFormResourceDto>[]>(() => [
    {
      key: 'resourceName',
      title: translate('page.workflowInitiate.column.resourceName'),
      width: '320px',
      responsivePriority: 100,
      render: (row) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900" title={row.resourceName}>{row.resourceName}</div>
          <div className="truncate text-xs text-gray-500">{row.businessType} / {row.resourceCode}</div>
        </div>
      )
    },
    { key: 'menuCode', title: translate('page.workflowInitiate.column.menuCode'), width: '180px', hideBelow: 'lg', render: (row) => renderCode(row.menuCode) },
    { key: 'modelCode', title: translate('page.workflowInitiate.column.modelCode'), width: '180px', hideBelow: 'lg', render: (row) => renderCode(row.modelCode) },
    { key: 'fields', title: translate('page.workflowInitiate.column.fields'), width: '90px', render: (row) => row.fields.length }
  ], [translate]);

  const openDraft = (resource: WorkflowFormResourceDto) => {
    setDraft({
      ...defaultDraft,
      appCode: workspace?.appCode ?? 'SYSTEM',
      businessType: resource.businessType,
      formResourceCode: resource.resourceCode,
      menuCode: resource.menuCode,
      tenantId: workspace?.tenantId ?? 'tenant-system',
      title: formatMessage(translate('page.workflowInitiate.formTitle'), { resourceName: resource.resourceName })
    });
    setModalOpen(true);
  };

  const saveDraft = async () => {
    try {
      JSON.parse(draft.draftJson || '{}');
      await saveDraftMutation.mutateAsync(draft);
      setModalOpen(false);
      message.success(translate('page.workflowInitiate.success.saveDraft'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowInitiate.error.saveDraftFailed')));
    }
  };

  return (
    <CrudPage
      title={translate('page.workflowInitiate.title')}
      description={translate('page.workflowInitiate.description')}
      actions={(
        <label className="workflow-toolbar-search">
          <AppIcon name="magnifying-glass" />
          <input placeholder={translate('page.workflowInitiate.search.placeholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
        </label>
      )}
      editor={(
        <ModalForm
          actions={[
            { label: translate('common.cancel'), onClick: () => setModalOpen(false) },
            { label: translate('page.workflowInitiate.action.saveDraft'), loading: saveDraftMutation.isPending, onClick: () => void saveDraft(), variant: 'primary' }
          ]}
          fields={fields}
          open={modalOpen}
          title={translate('page.workflowInitiate.modal.saveDraftTitle')}
          value={draft}
          onClose={() => setModalOpen(false)}
          onValueChange={(name, value) => setDraft((current) => ({ ...current, [name]: value }))}
        />
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="workflow-initiate-forms"
          columns={columns}
          emptyText={formsQuery.isError ? translate('page.workflowInitiate.empty.loadFailed') : translate('page.workflowInitiate.empty.resource')}
          fitScreen
          loading={formsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: formsQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <div className="flex items-center gap-2">
              {row.routePath ? <PermissionButton code="workflow:instance:start" className="hover:text-primary-600" title={translate('page.workflowInitiate.action.openForm')} type="button" onClick={() => navigate(buildWorkspaceRoute(row.routePath, workspace))}><AppIcon className="text-base" name="paper-plane-tilt" /></PermissionButton> : null}
              <PermissionButton code="workflow:draft:edit" className="hover:text-primary-600" title={translate('page.workflowInitiate.action.saveDraft')} type="button" onClick={() => openDraft(row)}><AppIcon className="text-base" name="file-text" /></PermissionButton>
            </div>
          )}
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
