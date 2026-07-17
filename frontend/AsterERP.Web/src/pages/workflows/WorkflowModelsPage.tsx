import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkflowModelListItemDto, WorkflowModelUpsertRequest } from '../../api/workflow/workflows.api';
import { getWorkflowModels, publishWorkflowModel, saveWorkflowModel } from '../../api/workflow/workflows.api';
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
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { buildWorkspaceRoute } from './workflowWorkspaceRoutes';

const defaultFormState: WorkflowModelUpsertRequest = {
  appCode: '',
  categoryCode: 'FLOW_GENERAL',
  formType: 0,
  id: null,
  modelId: null,
  modelKey: '',
  modelType: 1,
  name: '',
  remark: ''
};

function renderStatus(status: number | null | undefined, translate: (key: string) => string) {
  if (status === 3) return translate('page.workflowModels.status.published');
  if (status === 2) return translate('page.workflowModels.status.pending');
  return translate('page.workflowModels.status.draft');
}

function formatUpdatedAt(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString([], {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit',
    year: 'numeric'
  });
}

export function WorkflowModelsPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const message = useMessage();
  const queryClient = useQueryClient();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modalOpen, setModalOpen] = useState(false);
  const [formState, setFormState] = useState<WorkflowModelUpsertRequest>({
    ...defaultFormState,
    appCode: workspace?.appCode ?? 'SYSTEM'
  });

  const modelsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowModels({ appCode: workspace?.appCode, keyword, pageIndex, pageSize }, signal),
    queryKey: ['workflows', 'models', workspace?.appCode, keyword, pageIndex, pageSize]
  });

  const createMutation = useApiMutation({ mutationFn: saveWorkflowModel });
  const publishMutation = useApiMutation({ mutationFn: publishWorkflowModel });

  const columns: DataTableColumn<WorkflowModelListItemDto>[] = useMemo(
    () => [
      { key: 'rowIndex', title: translate('page.workflowModels.column.rowIndex'), width: '64px', align: 'center', render: (_row, index) => (pageIndex - 1) * pageSize + index + 1 },
      {
        key: 'name',
        title: translate('page.workflowModels.column.name'),
        width: '360px',
        responsivePriority: 100,
        render: (row) => (
          <div className="min-w-0 max-w-full overflow-hidden py-0.5">
            <div className="truncate text-sm font-semibold leading-5 text-gray-900" title={row.name}>
              {row.name}
            </div>
            <div className="mt-0.5 truncate font-mono text-[11px] leading-4 text-gray-500" title={row.modelKey}>
              {row.modelKey}
            </div>
          </div>
        )
      },
      {
        key: 'appCode',
        title: translate('page.workflowModels.column.appCode'),
        width: '88px',
        align: 'center',
        responsivePriority: 90,
        render: (row) => (
          <span className="inline-flex max-w-full items-center justify-center rounded border border-gray-200 bg-gray-50 px-2 py-0.5 text-xs font-medium text-gray-700">
            {row.appCode}
          </span>
        )
      },
      {
        key: 'categoryCode',
        title: translate('page.workflowModels.column.categoryCode'),
        width: '132px',
        responsivePriority: 75,
        render: (row) => <span className="block truncate font-mono text-xs text-gray-600" title={row.categoryCode}>{row.categoryCode}</span>
      },
      {
        key: 'status',
        title: translate('page.workflowModels.column.status'),
        width: '88px',
        align: 'center',
        responsivePriority: 95,
        render: (row) => (
          <span className="inline-flex rounded bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700">
            {renderStatus(row.status, translate)}
          </span>
        )
      },
      { key: 'version', title: translate('page.workflowModels.column.version'), width: '72px', align: 'center', responsivePriority: 80, render: (row) => row.version ?? '-' },
      {
        key: 'updatedAt',
        title: translate('page.workflowModels.column.updatedAt'),
        width: '148px',
        hideBelow: 'lg',
        render: (row) => <span className="whitespace-nowrap text-xs text-gray-600">{formatUpdatedAt(row.updatedAt)}</span>
      }
    ],
    [pageIndex, pageSize, translate]
  );

  const fields: FormFieldConfig<WorkflowModelUpsertRequest>[] = [
    { label: translate('page.workflowModels.field.modelKey'), name: 'modelKey', placeholder: translate('page.workflowModels.placeholder.modelKey'), required: true, span: 2, type: 'text' },
    { label: translate('page.workflowModels.field.name'), name: 'name', placeholder: translate('page.workflowModels.placeholder.name'), required: true, span: 2, type: 'text' },
    { label: translate('page.workflowModels.field.appCode'), name: 'appCode', placeholder: translate('page.workflowModels.placeholder.appCode'), required: true, span: 1, type: 'text' },
    { label: translate('page.workflowModels.field.categoryCode'), name: 'categoryCode', placeholder: translate('page.workflowModels.placeholder.categoryCode'), span: 1, type: 'text' },
    { label: translate('page.workflowModels.field.remark'), name: 'remark', placeholder: translate('page.workflowModels.placeholder.remark'), rows: 3, span: 2, type: 'textarea' }
  ];

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['workflows', 'models'] });
  };

  const submitCreate = async () => {
    if (!formState.modelKey.trim() || !formState.name.trim()) {
      message.error(translate('page.workflowModels.error.completeInfo'));
      return;
    }

    try {
      const response = await createMutation.mutateAsync({ ...formState, appCode: (formState.appCode || workspace?.appCode || 'SYSTEM').toUpperCase() });
      setModalOpen(false);
      await refresh();
      navigate(buildWorkspaceRoute(`/workflows/models/${response.data.modelId}/designer`, workspace));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowModels.error.createFailed')));
    }
  };

  const handlePublish = async (modelId: string) => {
    try {
      const response = await publishMutation.mutateAsync(modelId);
      await refresh();
      message.success(`${translate('page.workflowModels.success.publishedPrefix')} v${response.data.version}`);
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowModels.error.publishFailed')));
    }
  };

  const actions = (
    <div className="flex flex-wrap items-center gap-2">
      <input
        className="border border-gray-300 rounded px-3 py-1.5 text-sm w-56"
        placeholder={translate('page.workflowModels.search.placeholder')}
        value={keyword}
        onChange={(event) => {
          setKeyword(event.target.value);
          setPageIndex(1);
        }}
      />
      <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50" type="button" onClick={() => void refresh()}>
        <AppIcon name="arrows-clockwise" />
      </button>
      <PermissionButton code="workflow:model:add" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1" type="button" onClick={() => { setFormState({ ...defaultFormState, appCode: workspace?.appCode ?? 'SYSTEM' }); setModalOpen(true); }}>
        <AppIcon name="plus" /> {translate('page.workflowModels.action.create')}
      </PermissionButton>
    </div>
  );

  return (
    <CrudPage title={translate('page.workflowModels.title')} actions={actions}>
      <div className="flex-1 bg-white border border-gray-200 rounded-lg shadow-sm min-h-0">
        <DataTable
          columnSettingsKey="workflow-models-v2"
          columns={columns}
          emptyText={modelsQuery.isError ? translate('page.workflowModels.error.loadFailed') : translate('page.workflowModels.empty')}
          fitScreen
          loading={modelsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: modelsQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton code="workflow:model:edit" className="hover:text-primary-600" title={translate('page.workflowModels.action.design')} type="button" onClick={() => navigate(buildWorkspaceRoute(`/workflows/models/${row.modelId}/designer`, workspace))}>
                <AppIcon className="text-base" name="flow-arrow" />
              </PermissionButton>
              <PermissionButton code="workflow:model:publish" className="hover:text-primary-600" title={translate('page.workflowModels.action.publish')} type="button" onClick={() => void handlePublish(row.modelId)}>
                <AppIcon className="text-base" name="upload-simple" />
              </PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.modelId}
          rows={modelsQuery.data?.data.items ?? []}
        />
      </div>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => setModalOpen(false), variant: 'ghost' },
          { label: translate('common.save'), loading: createMutation.isPending, onClick: () => void submitCreate(), variant: 'primary' }
        ]}
        fields={fields}
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onValueChange={(name, value) => setFormState((current) => ({ ...current, [name]: value }))}
        title={translate('page.workflowModels.modal.createTitle')}
        value={formState}
      />
    </CrudPage>
  );
}
