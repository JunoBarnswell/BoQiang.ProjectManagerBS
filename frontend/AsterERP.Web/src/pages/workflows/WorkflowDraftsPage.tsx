import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkflowRequestDraftDto, WorkflowRequestDraftUpsertRequest } from '../../api/workflow/workflows.api';
import { deleteWorkflowDraft, getWorkflowDrafts, getWorkflowFormResources, saveWorkflowDraft, submitWorkflowDraft } from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig, FormOption } from '../../shared/forms/formTypes';
import { ModalForm } from '../../shared/forms/ModalForm';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

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

export function WorkflowDraftsPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modalOpen, setModalOpen] = useState(false);
  const [formState, setFormState] = useState<WorkflowRequestDraftUpsertRequest>(defaultDraft);

  const draftsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowDrafts({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'drafts', workspace?.tenantId, workspace?.appCode, keyword, pageIndex, pageSize]
  });
  const resourcesQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowFormResources({ appCode: workspace?.appCode, pageIndex: 1, pageSize: 200, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'drafts', 'resources', workspace?.tenantId, workspace?.appCode]
  });
  const saveMutation = useApiMutation({ mutationFn: saveWorkflowDraft });
  const submitMutation = useApiMutation({ mutationFn: ({ id }: { id: string }) => submitWorkflowDraft(id, { variables: null }) });
  const deleteMutation = useApiMutation({ mutationFn: deleteWorkflowDraft });

  const resourceOptions = useMemo<FormOption[]>(
    () => (resourcesQuery.data?.data.items ?? []).map((item) => ({ label: `${item.resourceName} (${item.businessType})`, value: item.resourceCode })),
    [resourcesQuery.data?.data.items]
  );
  const resourceMap = useMemo(
    () => new Map((resourcesQuery.data?.data.items ?? []).map((item) => [item.resourceCode, item])),
    [resourcesQuery.data?.data.items]
  );

  const fields = useMemo<FormFieldConfig<WorkflowRequestDraftUpsertRequest>[]>(() => [
    { label: translate('page.workflowDrafts.field.formResourceCode'), name: 'formResourceCode', options: resourceOptions, required: true, type: 'select' },
    { label: translate('page.workflowDrafts.field.title'), name: 'title', required: true, type: 'text' },
    { label: translate('page.workflowDrafts.field.businessKey'), name: 'businessKey', required: true, type: 'text' },
    { label: translate('page.workflowDrafts.field.draftJson'), name: 'draftJson', required: true, rows: 10, type: 'textarea' }
  ], [resourceOptions, translate]);

  const columns = useMemo<DataTableColumn<WorkflowRequestDraftDto>[]>(() => [
    { key: 'title', title: translate('page.workflowDrafts.column.title'), width: '280px', responsivePriority: 100, render: (row) => <><div className="font-medium text-gray-900">{row.title}</div><div className="font-mono text-xs text-gray-500">{row.formResourceCode}</div></> },
    { key: 'businessType', title: translate('page.workflowDrafts.column.businessType'), width: '180px', responsivePriority: 90, render: (row) => <><div>{row.businessType}</div><div className="font-mono text-xs text-gray-500">{row.businessKey || '-'}</div></> },
    { key: 'status', title: translate('page.workflowDrafts.column.status'), width: '100px', render: (row) => row.status },
    { key: 'lastSavedAt', title: translate('page.workflowDrafts.column.lastSavedAt'), width: '180px', hideBelow: 'lg', render: (row) => formatDateTime(row.lastSavedAt) },
    { key: 'processInstanceId', title: translate('page.workflowDrafts.column.processInstanceId'), width: '220px', hideBelow: 'xl', render: (row) => row.processInstanceId ? renderCode(row.processInstanceId) : '-' }
  ], [translate]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['workflows', 'drafts'] });

  const openCreate = () => {
    setFormState({ ...defaultDraft, appCode: workspace?.appCode ?? 'SYSTEM', tenantId: workspace?.tenantId ?? 'tenant-system' });
    setModalOpen(true);
  };

  const openEdit = (row: WorkflowRequestDraftDto) => {
    setFormState({
      appCode: row.appCode,
      businessKey: row.businessKey ?? '',
      businessType: row.businessType,
      draftJson: row.draftJson,
      formResourceCode: row.formResourceCode,
      id: row.id,
      menuCode: row.menuCode,
      tenantId: row.tenantId,
      title: row.title
    });
    setModalOpen(true);
  };

  const save = async () => {
    try {
      JSON.parse(formState.draftJson || '{}');
      const resource = resourceMap.get(formState.formResourceCode);
      await saveMutation.mutateAsync({
        ...formState,
        businessType: formState.businessType || resource?.businessType || '',
        menuCode: formState.menuCode || resource?.menuCode || ''
      });
      setModalOpen(false);
      await refresh();
      message.success(translate('page.workflowDrafts.success.save'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowDrafts.error.saveFailed')));
    }
  };

  const submit = (row: WorkflowRequestDraftDto) => {
    confirm({
      title: translate('page.workflowDrafts.confirm.submitTitle'),
      content: formatMessage(translate('page.workflowDrafts.confirm.submitContent'), { title: row.title }),
      confirmText: translate('page.workflowDrafts.confirm.submitAction'),
      onConfirm: async () => {
        const response = await submitMutation.mutateAsync({ id: row.id });
        await refresh();
        message.success(translate('page.workflowDrafts.success.submit'));
        navigate(`/workflows/instances/${response.data.processInstanceId}`);
      }
    });
  };

  const remove = (row: WorkflowRequestDraftDto) => {
    confirm({
      title: translate('page.workflowDrafts.confirm.deleteTitle'),
      content: formatMessage(translate('page.workflowDrafts.confirm.deleteContent'), { title: row.title }),
      confirmText: translate('page.workflowDrafts.confirm.deleteAction'),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(row.id);
        await refresh();
        message.success(translate('page.workflowDrafts.success.delete'));
      }
    });
  };

  return (
    <CrudPage
      title={translate('page.workflowDrafts.title')}
      description={translate('page.workflowDrafts.description')}
      actions={(
        <div className="workflow-page-actions">
          <label className="workflow-toolbar-search">
            <AppIcon name="magnifying-glass" />
            <input placeholder={translate('page.workflowDrafts.search.placeholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          </label>
          <PermissionButton code="workflow:draft:edit" className="primary-button" type="button" onClick={openCreate}><AppIcon name="plus" />{translate('page.workflowDrafts.action.create')}</PermissionButton>
        </div>
      )}
      editor={(
        <ModalForm
          actions={[
            { label: translate('common.cancel'), onClick: () => setModalOpen(false) },
            { label: translate('common.save'), loading: saveMutation.isPending, onClick: () => void save(), variant: 'primary' }
          ]}
          fields={fields}
          open={modalOpen}
          title={formState.id ? translate('page.workflowDrafts.modal.editTitle') : translate('page.workflowDrafts.modal.createTitle')}
          value={formState}
          onClose={() => setModalOpen(false)}
          onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: value }))}
        />
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="workflow-drafts"
          columns={columns}
          emptyText={draftsQuery.isError ? translate('page.workflowDrafts.empty.loadFailed') : translate('page.workflowDrafts.empty.resource')}
          fitScreen
          loading={draftsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: draftsQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              {row.processInstanceId ? <button className="hover:text-primary-600" title={translate('page.workflowDrafts.action.process')} type="button" onClick={() => navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="git-branch" /></button> : null}
              <PermissionButton code="workflow:draft:edit" className="hover:text-primary-600" title={translate('common.edit')} type="button" onClick={() => openEdit(row)}><AppIcon className="text-base" name="pencil-simple" /></PermissionButton>
              <PermissionButton code="workflow:draft:submit" className="hover:text-primary-600" title={translate('page.workflowDrafts.action.submit')} type="button" onClick={() => submit(row)}><AppIcon className="text-base" name="paper-plane-tilt" /></PermissionButton>
              <PermissionButton code="workflow:draft:delete" className="hover:text-red-600" title={translate('common.delete')} type="button" onClick={() => remove(row)}><AppIcon className="text-base" name="trash" /></PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={draftsQuery.data?.data.items ?? []}
        />
      </div>
    </CrudPage>
  );
}

function formatDateTime(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-';
}

function renderCode(value: string) {
  return <span className="font-mono text-xs text-gray-600">{value}</span>;
}
