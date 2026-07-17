import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import type { WorkflowBindingDto, WorkflowBindingUpsertRequest, WorkflowFormResourceDto, WorkflowProcessDefinitionDto } from '../../api/workflow/workflows.api';
import { deleteWorkflowBinding, getWorkflowBindings, getWorkflowFormResources, getWorkflowProcessDefinitions, saveWorkflowBinding } from '../../api/workflow/workflows.api';
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

import { buildCallbackConfigForEdit, normalizeCallbackConfig, summarizeCallbackConfig, validateCallbackConfig } from './workflowCallbackConfig';
import { WorkflowCallbackRulesEditor } from './WorkflowCallbackRulesEditor';


const defaultFormState: WorkflowBindingUpsertRequest = {
  appCode: '',
  businessType: '',
  detailRoute: null,
  formResourceCode: null,
  isEnabled: true,
  keyField: null,
  menuCode: '',
  modelCode: null,
  modelId: null,
  modelKey: null,
  pageCode: null,
  processDefinitionId: '',
  processDefinitionKey: '',
  remark: '',
  startFormJson: '',
  callbackConfig: { rules: [] },
  tenantId: '',
  titleTemplate: null
};

function renderCode(value?: string | null, fallback = '-') {
  if (!value) {
    return <span className="text-gray-400">{fallback}</span>;
  }

  return (
    <span className="block truncate font-mono text-xs text-gray-600" title={value}>
      {value}
    </span>
  );
}

function renderTinyBadge(value?: string | null, tone: 'gray' | 'green' = 'gray') {
  if (!value) {
    return <span className="text-gray-400">-</span>;
  }

  const toneClass = tone === 'green'
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : 'border-gray-200 bg-gray-50 text-gray-700';

  return (
    <span className={`inline-flex max-w-full items-center rounded border px-2 py-0.5 text-xs font-medium ${toneClass}`}>
      <span className="truncate">{value}</span>
    </span>
  );
}

function getResourceTitle(row: WorkflowBindingDto, resources: Map<string, WorkflowFormResourceDto>, translate: (key: string) => string) {
  if (!row.formResourceCode) {
    return translate('workflow.bindings.legacyResource');
  }

  return resources.get(row.formResourceCode)?.resourceName ?? row.formResourceCode;
}

function getResourceSubtitle(row: WorkflowBindingDto) {
  return row.formResourceCode ?? `${row.menuCode} / ${row.businessType}`;
}

function getDefinitionTitle(row: WorkflowBindingDto, definitions: Map<string, WorkflowProcessDefinitionDto>) {
  const definition = row.processDefinitionId ? definitions.get(row.processDefinitionId) : null;
  if (!definition) {
    return row.processDefinitionKey;
  }

  return `${definition.name ?? definition.key} v${definition.version}`;
}

function getDefinitionSubtitle(row: WorkflowBindingDto) {
  return row.processDefinitionId ?? row.processDefinitionKey;
}

export function WorkflowBindingsPage() {
  const { translate } = useI18n();
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [searchParams] = useSearchParams();
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modalOpen, setModalOpen] = useState(false);
  const [handledPrefillKey, setHandledPrefillKey] = useState('');
  const [formState, setFormState] = useState<WorkflowBindingUpsertRequest>({
    ...defaultFormState,
    appCode: workspace?.appCode ?? 'SYSTEM',
    tenantId: workspace?.tenantId ?? 'tenant-system'
  });

  const bindingsQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowBindings({ appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'bindings', workspace?.tenantId, workspace?.appCode, keyword, pageIndex, pageSize]
  });
  const definitionsQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowProcessDefinitions(undefined, signal),
    queryKey: ['workflows', 'process-definitions']
  });
  const formResourcesQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowFormResources({ appCode: workspace?.appCode, pageIndex: 1, pageSize: 100, tenantId: workspace?.tenantId }, signal),
    queryKey: ['workflows', 'form-resources', workspace?.tenantId, workspace?.appCode]
  });

  const saveMutation = useApiMutation({ mutationFn: saveWorkflowBinding });
  const deleteMutation = useApiMutation({ mutationFn: deleteWorkflowBinding });

  const definitionOptions: FormOption[] = useMemo(
    () => (definitionsQuery.data?.data ?? []).map((item) => ({
      label: `${item.name ?? item.key} v${item.version}`,
      value: item.id
    })),
    [definitionsQuery.data?.data]
  );
  const formResourceOptions: FormOption[] = useMemo(
    () => (formResourcesQuery.data?.data.items ?? []).map((item) => ({
      label: `${item.resourceName} (${item.modelCode})`,
      value: item.resourceCode
    })),
    [formResourcesQuery.data?.data.items]
  );
  const formResourceMap = useMemo(
    () => new Map((formResourcesQuery.data?.data.items ?? []).map((item) => [item.resourceCode, item])),
    [formResourcesQuery.data?.data.items]
  );
  const selectedResource = useMemo(
    () => formState.formResourceCode ? formResourceMap.get(formState.formResourceCode) ?? null : null,
    [formResourceMap, formState.formResourceCode]
  );
  const selectedDefinition = useMemo(
    () => (definitionsQuery.data?.data ?? []).find((item) => item.id === formState.processDefinitionId) ?? null,
    [definitionsQuery.data?.data, formState.processDefinitionId]
  );
  const definitionMap = useMemo(
    () => new Map((definitionsQuery.data?.data ?? []).map((item) => [item.id, item])),
    [definitionsQuery.data?.data]
  );

  useEffect(() => {
    const prefillKey = searchParams.get('formResourceCode') ?? searchParams.get('pageCode') ?? searchParams.get('menuCode') ?? '';
    if (!prefillKey || handledPrefillKey === prefillKey || formResourcesQuery.isLoading) {
      return;
    }

    const resources = formResourcesQuery.data?.data.items ?? [];
    const resource = resources.find((item) =>
      item.resourceCode === searchParams.get('formResourceCode') ||
      item.pageCode === searchParams.get('pageCode') ||
      item.menuCode === searchParams.get('menuCode'));
    if (!resource) {
      return;
    }

    setFormState({
      ...defaultFormState,
      appCode: workspace?.appCode ?? 'SYSTEM',
      businessType: resource.businessType,
      callbackConfig: { rules: [] },
      detailRoute: resource.routePath ?? '',
      formResourceCode: resource.resourceCode,
      keyField: resource.keyField,
      menuCode: resource.menuCode,
      modelCode: resource.modelCode,
      pageCode: resource.pageCode,
      tenantId: workspace?.tenantId ?? 'tenant-system'
    });
    setModalOpen(true);
    setHandledPrefillKey(prefillKey);
  }, [formResourcesQuery.data?.data.items, formResourcesQuery.isLoading, handledPrefillKey, searchParams, workspace?.appCode, workspace?.tenantId]);

  const columns: DataTableColumn<WorkflowBindingDto>[] = useMemo(
    () => [
      {
        key: 'formResourceCode',
        title: translate('workflow.bindings.table.resource'),
        width: '340px',
        responsivePriority: 100,
        render: (row) => (
          <div className="min-w-0 max-w-full overflow-hidden py-0.5">
            <div className={`truncate text-sm font-semibold leading-5 ${row.formResourceCode ? 'text-gray-900' : 'text-amber-700'}`} title={getResourceTitle(row, formResourceMap, translate)}>
              {getResourceTitle(row, formResourceMap, translate)}
            </div>
            <div className="mt-0.5 truncate font-mono text-[11px] leading-4 text-gray-500" title={getResourceSubtitle(row)}>
              {getResourceSubtitle(row)}
            </div>
          </div>
        )
      },
      {
        key: 'menuCode',
        title: translate('workflow.bindings.table.menuBusiness'),
        width: '190px',
        responsivePriority: 95,
        render: (row) => (
          <div className="space-y-1 overflow-hidden">
            {renderCode(row.menuCode)}
            {renderCode(row.businessType)}
          </div>
        )
      },
      {
        key: 'modelCode',
        title: translate('workflow.bindings.table.model'),
        width: '170px',
        hideBelow: 'lg',
        render: (row) => (
          <div className="space-y-1 overflow-hidden">
            {renderCode(row.modelCode)}
            {renderTinyBadge(row.keyField)}
          </div>
        )
      },
      {
        key: 'processDefinitionKey',
        title: translate('workflow.bindings.table.definition'),
        width: '310px',
        responsivePriority: 90,
        render: (row) => (
          <div className="min-w-0 max-w-full overflow-hidden py-0.5">
            <div className="truncate text-sm font-medium leading-5 text-gray-900" title={getDefinitionTitle(row, definitionMap)}>
              {getDefinitionTitle(row, definitionMap)}
            </div>
            <div className="mt-0.5 truncate font-mono text-[11px] leading-4 text-gray-500" title={getDefinitionSubtitle(row)}>
              {getDefinitionSubtitle(row)}
            </div>
          </div>
        )
      },
      {
        key: 'isEnabled',
        title: translate('workflow.bindings.table.status'),
        width: '82px',
        align: 'center',
        render: (row) => renderTinyBadge(row.isEnabled ? translate('platform.common.enabled') : translate('platform.common.disabled'), row.isEnabled ? 'green' : 'gray')
      },
      {
        key: 'callbackConfig',
        title: translate('workflow.bindings.table.callbackSummary'),
        width: '300px',
        hideBelow: 'xl',
        render: (row) => (
          <span className="block truncate text-xs text-gray-600" title={summarizeCallbackConfig(row.callbackConfig)}>
            {summarizeCallbackConfig(row.callbackConfig)}
          </span>
        )
      },
      {
        key: 'remark',
        title: translate('workflow.bindings.table.remark'),
        width: '180px',
        hideBelow: 'xl',
        render: (row) => <span className="block truncate text-xs text-gray-500" title={row.remark ?? ''}>{row.remark ?? '-'}</span>
      }
    ],
    [definitionMap, formResourceMap, translate]
  );

  const fields: FormFieldConfig<WorkflowBindingUpsertRequest>[] = [
    { label: translate('workflow.bindings.workspaceTenant'), name: 'tenantId', required: true, section: translate('workflow.bindings.section.workspace'), span: 1, type: 'text' },
    { label: translate('workflow.bindings.workspaceApp'), name: 'appCode', required: true, section: translate('workflow.bindings.section.workspace'), span: 1, type: 'text' },
    { emptyOptionLabel: formResourcesQuery.isLoading ? translate('workflow.bindings.loadingFormResources') : translate('workflow.bindings.selectFormResource'), helpText: translate('workflow.bindings.resourceFromWorkspace'), label: translate('workflow.bindings.formResource'), name: 'formResourceCode', options: formResourceOptions, required: false, section: translate('workflow.bindings.section.resource'), span: 2, type: 'select' },
    { disabled: true, label: translate('workflow.bindings.menuCode'), name: 'menuCode', placeholder: translate('workflow.bindings.autoFill'), required: true, section: translate('workflow.bindings.section.resource'), span: 2, type: 'text' },
    { disabled: true, label: translate('workflow.bindings.businessType'), name: 'businessType', placeholder: translate('workflow.bindings.autoFill'), required: true, section: translate('workflow.bindings.section.resource'), span: 2, type: 'text' },
    { disabled: true, label: translate('workflow.bindings.pageCode'), name: 'pageCode', placeholder: translate('workflow.bindings.autoFill'), section: translate('workflow.bindings.section.resource'), span: 2, type: 'text' },
    { disabled: true, label: translate('workflow.bindings.modelCode'), name: 'modelCode', placeholder: translate('workflow.bindings.autoFill'), section: translate('workflow.bindings.section.resource'), span: 2, type: 'text' },
    { disabled: true, label: translate('workflow.bindings.keyField'), name: 'keyField', placeholder: translate('workflow.bindings.autoFill'), section: translate('workflow.bindings.section.resource'), span: 1, type: 'text' },
    { disabled: true, label: translate('workflow.bindings.detailRoute'), name: 'detailRoute', placeholder: translate('workflow.bindings.autoFill'), section: translate('workflow.bindings.section.resource'), span: 1, type: 'text' },
    { label: translate('workflow.bindings.processDefinitionId'), name: 'processDefinitionId', options: definitionOptions, required: true, section: translate('workflow.bindings.section.process'), span: 2, type: 'select' },
    { label: translate('workflow.bindings.processDefinitionKey'), name: 'processDefinitionKey', placeholder: translate('workflow.bindings.autoFill'), required: true, section: translate('workflow.bindings.section.process'), span: 2, type: 'text' },
    { label: translate('workflow.bindings.titleTemplate'), name: 'titleTemplate', placeholder: translate('workflow.bindings.titleTemplatePlaceholder'), section: translate('workflow.bindings.section.advanced'), span: 1, type: 'text' },
    { helpText: translate('workflow.bindings.disableRuntimeStart'), label: translate('workflow.bindings.enabled'), name: 'isEnabled', section: translate('workflow.bindings.section.advanced'), span: 1, type: 'switch' },
    { label: translate('workflow.bindings.remarkField'), name: 'remark', rows: 3, section: translate('workflow.bindings.section.advanced'), span: 2, type: 'textarea' }
  ];

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['workflows', 'bindings'] });
  };

  const openCreate = () => {
    setFormState({
      ...defaultFormState,
      appCode: workspace?.appCode ?? 'SYSTEM',
      callbackConfig: { rules: [] },
      tenantId: workspace?.tenantId ?? 'tenant-system'
    });
    setModalOpen(true);
  };

  const openEdit = (row: WorkflowBindingDto) => {
    setFormState({
      appCode: row.appCode,
      businessType: row.businessType,
      detailRoute: row.detailRoute ?? null,
      formResourceCode: row.formResourceCode ?? null,
      isEnabled: row.isEnabled,
      keyField: row.keyField ?? null,
      menuCode: row.menuCode,
      modelCode: row.modelCode ?? null,
      modelId: row.modelId,
      modelKey: row.modelKey,
      pageCode: row.pageCode ?? null,
      processDefinitionId: row.processDefinitionId ?? '',
      processDefinitionKey: row.processDefinitionKey,
      remark: row.remark ?? '',
      startFormJson: row.startFormJson ?? '',
      callbackConfig: buildCallbackConfigForEdit(row),
      tenantId: row.tenantId,
      titleTemplate: row.titleTemplate ?? null
    });
    setModalOpen(true);
  };

  const submit = async () => {
    if (!formState.menuCode.trim() || !formState.businessType.trim() || !formState.processDefinitionKey.trim()) {
      message.error(translate('workflow.bindings.validation.required'));
      return;
    }

    const callbackConfig = normalizeCallbackConfig(formState.callbackConfig);
    const validationError = validateCallbackConfig(callbackConfig, formResourcesQuery.data?.data.items ?? [], selectedResource);
    if (validationError) {
      message.error(validationError);
      return;
    }

    try {
      await saveMutation.mutateAsync({ ...formState, appCode: formState.appCode.toUpperCase(), callbackConfig });
      setModalOpen(false);
      await refresh();
      message.success(translate('workflow.bindings.saveSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.bindings.saveFailed')));
    }
  };

  const handleDelete = (row: WorkflowBindingDto) => {
    confirm({
      title: translate('workflow.bindings.confirmDeleteTitle'),
      content: formatMessage(translate('workflow.bindings.confirmDeleteContent'), { businessType: row.businessType, menuCode: row.menuCode }),
      confirmText: translate('workflow.bindings.confirmDeleteConfirm'),
      onConfirm: async () => {
        try {
          await deleteMutation.mutateAsync(row.id);
          await refresh();
          message.success(translate('workflow.bindings.deleteSuccess'));
        } catch (error) {
          message.error(getErrorMessage(error, translate('workflow.bindings.deleteFailed')));
        }
      }
    });
  };

  return (
    <CrudPage
      title={translate('workflow.bindings.title')}
      actions={(
        <div className="flex items-center gap-2">
          <input className="border border-gray-300 rounded px-3 py-1.5 text-sm w-56" placeholder={translate('workflow.bindings.searchPlaceholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          <button className="bg-white border border-gray-300 text-gray-700 px-3 py-1.5 rounded text-sm hover:bg-gray-50" title={translate('workflow.bindings.refresh')} type="button" onClick={() => void refresh()}><AppIcon name="arrows-clockwise" /></button>
          <PermissionButton code="workflow:binding:edit" className="bg-primary-600 text-white px-3 py-1.5 rounded text-sm hover:bg-primary-700 flex items-center gap-1" type="button" onClick={openCreate}><AppIcon name="plus" /> {translate('workflow.bindings.create')}</PermissionButton>
        </div>
      )}
    >
      <div className="flex-1 bg-white border border-gray-200 rounded-lg shadow-sm min-h-0">
        <DataTable
          columnSettingsKey="workflow-bindings-v2"
          columns={columns}
          emptyText={bindingsQuery.isError ? translate('workflow.bindings.loadFailed') : translate('workflow.bindings.empty')}
          fitScreen
          loading={bindingsQuery.isLoading}
          onPageChange={setPageIndex}
          onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
          pagination={{ current: pageIndex, pageSize, total: bindingsQuery.data?.data.total ?? 0 }}
          rowActions={(row) => (
            <TableActions>
              <PermissionButton code="workflow:binding:edit" className="hover:text-primary-600" title={translate('workflow.bindings.edit')} type="button" onClick={() => openEdit(row)}><AppIcon className="text-base" name="pencil-simple" /></PermissionButton>
              <PermissionButton code="workflow:binding:delete" className="hover:text-red-600" title={translate('workflow.bindings.delete')} type="button" onClick={() => handleDelete(row)}><AppIcon className="text-base" name="trash" /></PermissionButton>
            </TableActions>
          )}
          rowKey={(row) => row.id}
          rows={bindingsQuery.data?.data.items ?? []}
        />
      </div>

      <ModalForm
        actions={[
          { label: translate('workflow.drawer.cancel'), onClick: () => setModalOpen(false), variant: 'ghost' },
          { label: translate('workflow.drawer.submit'), loading: saveMutation.isPending, onClick: () => void submit(), variant: 'primary' }
        ]}
        fields={fields}
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        onValueChange={(name, value) => {
          if (name === 'formResourceCode') {
            const resource = (formResourcesQuery.data?.data.items ?? []).find((item) => item.resourceCode === value);
            setFormState((current) => resource ? {
              ...current,
              businessType: resource.businessType,
              detailRoute: resource.routePath ?? '',
              formResourceCode: resource.resourceCode,
              keyField: resource.keyField,
              menuCode: resource.menuCode,
              modelCode: resource.modelCode,
              pageCode: resource.pageCode
            } : { ...current, formResourceCode: String(value ?? '') });
            return;
          }
          if (name === 'processDefinitionId') {
            const definition = (definitionsQuery.data?.data ?? []).find((item) => item.id === value);
            setFormState((current) => ({ ...current, processDefinitionId: String(value), processDefinitionKey: definition?.key ?? current.processDefinitionKey }));
            return;
          }
          setFormState((current) => ({ ...current, [name]: value }));
        }}
        title={translate('workflow.bindings.modalTitle')}
        value={formState}
      >
        <div className="space-y-3">
          <div className="rounded-md border border-blue-100 bg-blue-50 px-3 py-2">
            <div className="text-xs font-medium text-blue-700">{translate('workflow.bindings.resource')}</div>
            <div className="mt-1 truncate text-sm font-semibold text-gray-900" title={selectedResource?.resourceName ?? formState.formResourceCode ?? undefined}>
              {selectedResource?.resourceName ?? formState.formResourceCode ?? translate('workflow.bindings.unselected')}
            </div>
            {selectedResource ? (
              <div className="mt-1 flex flex-wrap gap-1.5 text-[11px] text-blue-700">
                <span className="rounded bg-white/80 px-1.5 py-0.5 font-mono">{selectedResource.modelCode}</span>
                <span className="rounded bg-white/80 px-1.5 py-0.5 font-mono">{selectedResource.keyField}</span>
                <span className="rounded bg-white/80 px-1.5 py-0.5">{selectedResource.routePath}</span>
              </div>
            ) : null}
          </div>
          <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-2">
            <div className="text-xs font-medium text-gray-500">{translate('workflow.bindings.definition')}</div>
            <div className="mt-1 truncate text-sm font-semibold text-gray-900" title={selectedDefinition?.name ?? formState.processDefinitionKey}>
              {selectedDefinition ? `${selectedDefinition.name ?? selectedDefinition.key} v${selectedDefinition.version}` : formState.processDefinitionKey || translate('workflow.bindings.unselected')}
            </div>
          </div>
          <WorkflowCallbackRulesEditor
            config={formState.callbackConfig}
            resources={formResourcesQuery.data?.data.items ?? []}
            selectedResource={selectedResource}
            onChange={(callbackConfig) => setFormState((current) => ({ ...current, callbackConfig }))}
          />
        </div>
      </ModalForm>
    </CrudPage>
  );
}
