import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

import type { WorkflowDelegationRuleDto, WorkflowDelegationRuleUpsertRequest, WorkflowParticipantDto } from '../../api/workflow/workflows.api';
import { deleteWorkflowDelegation, getWorkflowDelegations, getWorkflowParticipants, saveWorkflowDelegation } from '../../api/workflow/workflows.api';
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

const defaultDelegation: WorkflowDelegationRuleUpsertRequest = {
  appCode: null,
  delegateUserId: '',
  endAt: '',
  isEnabled: true,
  processDefinitionKey: '',
  reason: '',
  scopeType: 'All',
  startAt: '',
  tenantId: null
};

export function WorkflowDelegationsPage() {
  const { translate } = useI18n();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modalOpen, setModalOpen] = useState(false);
  const [formState, setFormState] = useState<WorkflowDelegationRuleUpsertRequest>(defaultDelegation);

  const delegationsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowDelegations({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'delegations', workspace?.tenantId, workspace?.appCode, keyword, pageIndex, pageSize]
  });
  const participantsQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowParticipants({ type: 'user' }, signal),
    queryKey: ['workflows', 'participants', 'user', 'delegations']
  });
  const saveMutation = useApiMutation({ mutationFn: saveWorkflowDelegation });
  const deleteMutation = useApiMutation({ mutationFn: deleteWorkflowDelegation });

  const userOptions = useMemo<FormOption[]>(
    () => (participantsQuery.data?.data ?? []).map((item: WorkflowParticipantDto) => ({ label: `${item.name} (${item.code})`, value: item.id })),
    [participantsQuery.data?.data]
  );

  const fields = useMemo<FormFieldConfig<WorkflowDelegationRuleUpsertRequest>[]>(() => [
    { label: translate('workflow.delegations.agent'), name: 'delegateUserId', options: userOptions, required: true, type: 'select' },
    { label: translate('workflow.delegations.scope'), name: 'scopeType', options: [{ label: translate('workflow.delegations.scopeAll'), value: 'All' }, { label: translate('workflow.delegations.scopeProcessDefinition'), value: 'ProcessDefinition' }], required: true, type: 'select' },
    { label: translate('workflow.delegations.processDefinitionKey'), name: 'processDefinitionKey', type: 'text' },
    { label: translate('workflow.delegations.startAt'), name: 'startAt', required: true, type: 'datetime-local' },
    { label: translate('workflow.delegations.endAt'), name: 'endAt', required: true, type: 'datetime-local' },
    { label: translate('workflow.delegations.enabled'), name: 'isEnabled', type: 'switch' },
    { label: translate('workflow.delegations.reason'), name: 'reason', rows: 4, type: 'textarea' }
  ], [translate, userOptions]);

  const columns = useMemo<DataTableColumn<WorkflowDelegationRuleDto>[]>(() => [
    { key: 'delegateUserName', title: translate('workflow.delegations.table.agent'), width: '180px', responsivePriority: 100, render: (row) => row.delegateUserName ?? row.delegateUserId },
    { key: 'scopeType', title: translate('workflow.delegations.table.scope'), width: '160px', render: (row) => row.scopeType === 'ProcessDefinition' ? row.processDefinitionKey ?? translate('workflow.delegations.scopeProcessDefinition') : translate('workflow.delegations.scopeAll') },
    { key: 'startAt', title: translate('workflow.delegations.table.startAt'), width: '180px', render: (row) => formatDateTime(row.startAt) },
    { key: 'endAt', title: translate('workflow.delegations.table.endAt'), width: '180px', render: (row) => formatDateTime(row.endAt) },
    { key: 'isEnabled', title: translate('workflow.delegations.table.status'), width: '100px', render: (row) => row.isEnabled ? translate('workflow.delegations.statusEnabled') : translate('workflow.delegations.statusDisabled') },
    { key: 'reason', title: translate('workflow.delegations.table.reason'), width: '220px', hideBelow: 'xl', render: (row) => row.reason || '-' }
  ], [translate]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['workflows', 'delegations'] });

  const openCreate = () => {
    setFormState({ ...defaultDelegation, appCode: workspace?.appCode ?? 'SYSTEM', tenantId: workspace?.tenantId ?? 'tenant-system' });
    setModalOpen(true);
  };

  const openEdit = (row: WorkflowDelegationRuleDto) => {
    setFormState({
      appCode: row.appCode,
      delegateUserId: row.delegateUserId,
      endAt: toDatetimeLocal(row.endAt),
      id: row.id,
      isEnabled: row.isEnabled,
      processDefinitionKey: row.processDefinitionKey ?? '',
      reason: row.reason ?? '',
      scopeType: row.scopeType,
      startAt: toDatetimeLocal(row.startAt),
      tenantId: row.tenantId
    });
    setModalOpen(true);
  };

  const save = async () => {
    try {
      await saveMutation.mutateAsync(formState);
      setModalOpen(false);
      await refresh();
      message.success(translate('workflow.delegations.saveSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.delegations.saveFailed')));
    }
  };

  const remove = (row: WorkflowDelegationRuleDto) => {
    confirm({
      title: translate('workflow.delegations.confirmDeleteTitle'),
      content: formatMessage(translate('workflow.delegations.confirmDeleteContent'), { name: row.delegateUserName ?? row.delegateUserId }),
      confirmText: translate('workflow.delegations.confirmDeleteConfirm'),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(row.id);
        await refresh();
        message.success(translate('workflow.delegations.deleteSuccess'));
      }
    });
  };

  return (
    <CrudPage
      title={translate('workflow.delegations.title')}
      description={translate('workflow.delegations.description')}
      actions={(
        <div className="workflow-page-actions">
          <label className="workflow-toolbar-search">
            <AppIcon name="magnifying-glass" />
            <input placeholder={translate('workflow.delegations.searchPlaceholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          </label>
          <PermissionButton code="workflow:delegation:edit" className="primary-button" type="button" onClick={openCreate}><AppIcon name="plus" />{translate('workflow.delegations.create')}</PermissionButton>
        </div>
      )}
      editor={(
        <ModalForm
          actions={[
            { label: translate('workflow.drawer.cancel'), onClick: () => setModalOpen(false) },
            { label: translate('workflow.drawer.submit'), loading: saveMutation.isPending, onClick: () => void save(), variant: 'primary' }
          ]}
          fields={fields}
          open={modalOpen}
          title={formState.id ? translate('workflow.delegations.modalEdit') : translate('workflow.delegations.modalCreate')}
          value={formState}
          onClose={() => setModalOpen(false)}
          onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: value }))}
        />
      )}
    >
      <div className="flex-1 min-h-0 rounded-lg border border-gray-200 bg-white shadow-sm">
        <DataTable
          columnSettingsKey="workflow-delegations"
          columns={columns}
          emptyText={delegationsQuery.isError ? translate('workflow.delegations.loadFailed') : translate('workflow.delegations.empty')}
          fitScreen
          loading={delegationsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: delegationsQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton code="workflow:delegation:edit" className="hover:text-primary-600" title={translate('workflow.delegations.edit')} type="button" onClick={() => openEdit(row)}><AppIcon className="text-base" name="pencil-simple" /></PermissionButton>
              <PermissionButton code="workflow:delegation:delete" className="hover:text-red-600" title={translate('workflow.delegations.delete')} type="button" onClick={() => remove(row)}><AppIcon className="text-base" name="trash" /></PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={delegationsQuery.data?.data.items ?? []}
        />
      </div>
    </CrudPage>
  );
}

function formatDateTime(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-';
}

function toDatetimeLocal(value: string) {
  const date = new Date(value);
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}
