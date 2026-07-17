import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useRef } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';


import {
  createApplicationDataCenterObject,
  diagnoseApplicationDataSource,
  diagnoseApplicationDataSourceDraft,
  executeApplicationQueryPlan,
  deleteApplicationDataCenterObject,
  disableApplicationDataCenterObject,
  enableApplicationDataCenterObject,
  getApplicationDataCenterObject,
  getApplicationDataCenterTemplates,
  getApplicationDataCenterTypeOptions,
  listApplicationDataCenterObjects,
  previewApplicationDataCenterObject,
  previewApplicationQueryPlan,
  publishApplicationDataCenterObject,
  replaceApplicationDataSourceSecret,
  clearApplicationDataSourceSecret,
  testApplicationDataCenterObject,
  updateApplicationDataCenterObject
} from '../../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataCenterActionResult,
  ApplicationConnectionDiagnostic,
  ApplicationDataCenterObjectDetail,
  ApplicationDataCenterObjectListItem,
  ApplicationDataCenterObjectUpsertRequest,
  ApplicationDataCenterPreviewResponse,
  ApplicationQueryPlanResponse
} from '../../../api/application-data-center/applicationDataCenter.types';
import { buildApplicationQueryPlanRequest } from '../../../api/application-data-center/applicationQueryPlan';
import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../shared/feedback/useConfirm';
import { useMessage } from '../../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../../shared/forms/formTypes';
import { SearchForm } from '../../../shared/forms/SearchForm';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { DataTable } from '../../../shared/table/DataTable';
import { TableActions } from '../../../shared/table/TableActions';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import { useTabPageState } from '../../../shared/tabs/useTabPageState';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { ApplicationConsolePageFrame } from '../ApplicationConsolePageFrame';
import { WorkspacePanel } from '../workspace-shell/WorkspacePanel';
import { WorkspaceToolbar } from '../workspace-shell/WorkspaceToolbar';

import { getConfigFormSchema } from './config-forms/configFormRegistry';
import { firstValidationMessage, validateConfigForm } from './config-forms/configFormValidation';
import { buildDefaultConfigJson, normalizeConfigRequest, parseJsonObject, stringifyJsonObject } from './config-forms/configJsonCodec';
import { DataCenterDetailPanel } from './DataCenterDetailPanel';
import { DataCenterLayout } from './DataCenterLayout';
import type { DataCenterModuleConfig } from './dataCenterModuleConfig';
import { DataCenterPrerequisiteBanner } from './DataCenterPrerequisiteBanner';
import { DataCenterStatusBadge } from './DataCenterStatusBadge';
import { DataCenterTypeTree } from './DataCenterTypeTree';
import { DataCenterWizardDrawer } from './DataCenterWizardDrawer';
import { DataCenterWorkspaceShell } from './DataCenterWorkspaceShell';
import { DataSourceContextBanner } from './DataSourceContextBanner';

interface DataCenterModulePageProps {
  config: DataCenterModuleConfig;
}

interface DataCenterSearchState {
  keyword: string;
  objectType: string;
  status: string;
}

interface DataCenterPageState {
  actionResult: ApplicationDataCenterActionResult | null;
  diagnostic: ApplicationConnectionDiagnostic | null;
  drawerOpen: boolean;
  editingId: string | null;
  editingPublicConfigJson: string | null;
  form: ApplicationDataCenterObjectUpsertRequest;
  pageIndex: number;
  pageSize: number;
  preview: ApplicationDataCenterPreviewResponse | null;
  search: DataCenterSearchState;
  searchDraft: DataCenterSearchState;
  selectedId: string | null;
}

const defaultSearchState: DataCenterSearchState = {
  keyword: '',
  objectType: '',
  status: ''
};

const statusOptions = [
  { label: translateCurrentLiteral("全部状态"), value: '' },
  { label: translateCurrentLiteral("草稿"), value: 'Draft' },
  { label: translateCurrentLiteral("启用"), value: 'Enabled' },
  { label: translateCurrentLiteral("停用"), value: 'Disabled' },
  { label: translateCurrentLiteral("已发布"), value: 'Published' }
];

export function DataCenterModulePage({ config }: DataCenterModulePageProps) {
  const queryClient = useQueryClient();
  const message = useMessage();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { appCode, tenantId } = useParams();
  const dataSourceId = searchParams.get('dataSourceId')?.trim() ?? '';

  const [state, setState] = useTabPageState<DataCenterPageState>(
    {
      actionResult: null,
      diagnostic: null,
      drawerOpen: false,
      editingId: null,
      editingPublicConfigJson: null,
      form: createDefaultForm(config, config.defaultObjectType),
      pageIndex: 1,
      pageSize: 20,
      preview: null,
      search: defaultSearchState,
      searchDraft: defaultSearchState,
      selectedId: null
    },
    { cacheKey: `application-data-center:${config.resourcePath}` }
  );
  const saveInFlightRef = useRef(false);

  const moduleQueryKey = ['application-data-center', tenantId ?? '', appCode ?? '', config.resourcePath] as const;

  const dataSourceContextQuery = useApiQuery({
    enabled: Boolean(dataSourceId),
    queryFn: ({ signal }) => getApplicationDataCenterObject('data-sources', dataSourceId, signal),
    queryKey: ['application-data-center', tenantId ?? '', appCode ?? '', 'data-source-context', dataSourceId]
  });

  const dataSourceContext = {
    dataSource: dataSourceContextQuery.data?.data ?? null,
    dataSourceId,
    loading: dataSourceContextQuery.isFetching
  };

  const typeOptionsQuery = useApiQuery({
    queryFn: ({ signal }) => getApplicationDataCenterTypeOptions(signal),
    queryKey: ['application-data-center', tenantId ?? '', appCode ?? '', 'type-options']
  });

  const templatesQuery = useApiQuery({
    queryFn: ({ signal }) => getApplicationDataCenterTemplates(signal),
    queryKey: ['application-data-center', tenantId ?? '', appCode ?? '', 'templates']
  });

  const typeOptions = useMemo(
    () => (typeOptionsQuery.data?.data ?? []).filter((item) => item.moduleKey === config.moduleKey),
    [config.moduleKey, typeOptionsQuery.data?.data]
  );

  const templates = useMemo(
    () => (templatesQuery.data?.data ?? []).filter((item) => item.moduleKey === config.moduleKey),
    [config.moduleKey, templatesQuery.data?.data]
  );

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) =>
      listApplicationDataCenterObjects(
        config.resourcePath,
        {
          keyword: state.search.keyword,
          objectType: state.search.objectType,
          pageIndex: state.pageIndex,
          pageSize: state.pageSize,
          status: state.search.status
        },
        signal
      ),
    queryKey: [...moduleQueryKey, 'list', state.pageIndex, state.pageSize, state.search]
  });

  const detailQuery = useApiQuery({
    enabled: Boolean(state.selectedId),
    queryFn: ({ signal }) => getApplicationDataCenterObject(config.resourcePath, state.selectedId ?? '', signal),
    queryKey: [...moduleQueryKey, 'detail', state.selectedId]
  });

  const createMutation = useApiMutation({
    mutationFn: (request: ApplicationDataCenterObjectUpsertRequest) => createApplicationDataCenterObject(config.resourcePath, request),
    onError: (error) => { saveInFlightRef.current = false; message.error(getErrorMessage(error, `新增${config.itemName}失败`)); },
    onSuccess: async (response) => {
      message.success(`新增${config.itemName}成功`);
      await afterMutation(response);
    }
  });

  const updateMutation = useApiMutation({
    mutationFn: ({ id, request }: { id: string; request: ApplicationDataCenterObjectUpsertRequest }) =>
      updateApplicationDataCenterObject(config.resourcePath, id, request),
    onError: (error) => { saveInFlightRef.current = false; message.error(getErrorMessage(error, `编辑${config.itemName}失败`)); },
    onSuccess: async (response) => {
      message.success(`编辑${config.itemName}成功`);
      await afterMutation(response);
    }
  });

  const dataSourceEditMutation = useApiMutation({
    mutationFn: async ({ id, request }: { id: string; request: ApplicationDataCenterObjectUpsertRequest }) => {
      const secretConfigJson = request.secretConfigJson;
      const coreResponse = await updateApplicationDataCenterObject(config.resourcePath, id, { ...request, secretConfigJson: null });
      if (secretConfigJson?.trim()) {
        return replaceApplicationDataSourceSecret(id, {
          reason: '通过数据源编辑器替换凭据',
          secretConfigJson
        });
      }

      if (secretConfigJson === '') {
        return clearApplicationDataSourceSecret(id, { reason: '通过数据源编辑器清除凭据' });
      }

      return coreResponse;
    },
    onError: (error) => message.error(getErrorMessage(error, `缂栬緫${config.itemName}澶辫触`)),
    onSuccess: async (response) => {
      message.success(`缂栬緫${config.itemName}鎴愬姛`);
      await afterMutation(response);
    }
  });
  void dataSourceEditMutation;

  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => deleteApplicationDataCenterObject(config.resourcePath, id),
    onError: (error) => message.error(getErrorMessage(error, `删除${config.itemName}失败`)),
    onSuccess: async () => {
      message.success(`删除${config.itemName}成功`);
      setState((current) => ({ ...current, selectedId: null, preview: null, actionResult: null }));
      await refreshModule();
    }
  });

  const enableMutation = useApiMutation({
    mutationFn: (id: string) => enableApplicationDataCenterObject(config.resourcePath, id),
    onError: (error) => message.error(getErrorMessage(error, `启用${config.itemName}失败`)),
    onSuccess: async (response) => {
      message.success(`启用${config.itemName}成功`);
      await afterMutation(response);
    }
  });

  const disableMutation = useApiMutation({
    mutationFn: (id: string) => disableApplicationDataCenterObject(config.resourcePath, id),
    onError: (error) => message.error(getErrorMessage(error, `停用${config.itemName}失败`)),
    onSuccess: async (response) => {
      message.success(`停用${config.itemName}成功`);
      await afterMutation(response);
    }
  });

  const testMutation = useApiMutation<ApiEnvelope<ApplicationDataCenterActionResult> | ApiEnvelope<ApplicationQueryPlanResponse>, Error, string>({
    mutationFn: (id: string) => config.moduleKey !== 'query-dataset'
      ? testApplicationDataCenterObject(config.resourcePath, id, {})
      : getApplicationDataCenterObject(config.resourcePath, id).then((detailResponse) =>
        executeApplicationQueryPlan(buildApplicationQueryPlanRequest(detailResponse.data))),
    onError: (error) => message.error(getErrorMessage(error, `${config.itemName}测试失败`)),
    onSuccess: async (response) => {
      if ('plan' in response.data) {
        const queryResponse = response.data as ApplicationQueryPlanResponse;
        setState((current) => ({ ...current, actionResult: null, preview: queryResponse.data }));
        message.success('查询执行完成');
      } else {
        const actionResult = response.data as ApplicationDataCenterActionResult;
        setState((current) => ({ ...current, actionResult, preview: null }));
        message[actionResult.success ? 'success' : 'error'](actionResult.message);
      }
      await refreshModule();
    }
  });

  const diagnoseMutation = useApiMutation({
    mutationFn: (id: string) => diagnoseApplicationDataSource(id),
    onError: (error) => message.error(getErrorMessage(error, '连接诊断失败')),
    onSuccess: (response) => setState((current) => ({ ...current, diagnostic: response.data }))
  });

  const draftDiagnoseMutation = useApiMutation({
    mutationFn: (request: ApplicationDataCenterObjectUpsertRequest) => diagnoseApplicationDataSourceDraft(request),
    onError: (error) => message.error(getErrorMessage(error, '保存前连接诊断失败')),
    onSuccess: (response) => setState((current) => ({ ...current, diagnostic: { connectionFingerprint: response.data.connectionFingerprint, stages: response.data.stages, success: response.data.success, taskId: '' } }))
  });

  const previewMutation = useApiMutation({
    mutationFn: async (id: string) => {
      if (config.moduleKey !== 'query-dataset') {
        return previewApplicationDataCenterObject(config.resourcePath, id, { maxRows: 20 });
      }

      const detailResponse = await getApplicationDataCenterObject(config.resourcePath, id);
      return previewApplicationQueryPlan(buildApplicationQueryPlanRequest(detailResponse.data));
    },
    onError: (error) => message.error(getErrorMessage(error, `${config.itemName}预览失败`)),
    onSuccess: (response) => {
      setState((current) => ({ ...current, preview: 'plan' in response.data ? response.data.data : response.data, actionResult: null }));
      message.success('预览完成');
    }
  });

  const publishMutation = useApiMutation({
    mutationFn: (id: string) => publishApplicationDataCenterObject(config.resourcePath, id, {}),
    onError: (error) => message.error(getErrorMessage(error, `${config.itemName}发布失败`)),
    onSuccess: async (response) => {
      message.success(`${config.itemName}发布成功`);
      await afterMutation(response);
    }
  });

  const selectObject = useCallback(
    (id: string) => {
      setState((current) => ({ ...current, selectedId: id }));
    },
    [setState]
  );

  const objectColumnTitle = translateCurrentLiteral("对象");
  const objectTypeColumnTitle = translateCurrentLiteral("类型");
  const statusColumnTitle = translateCurrentLiteral("状态");
  const validationColumnTitle = translateCurrentLiteral("检测");
  const referenceColumnTitle = translateCurrentLiteral("引用");
  const updatedTimeColumnTitle = translateCurrentLiteral("更新时间");
  const columns = useMemo<DataTableColumn<ApplicationDataCenterObjectListItem>[]>(
    () => [
      {
        align: 'center',
        key: 'rowIndex',
        render: (_row, index) => (state.pageIndex - 1) * state.pageSize + index + 1,
        responsivePriority: 100,
        title: '#',
        width: '64px'
      },
      {
        key: 'objectName',
        render: (row) => (
          <button className="max-w-full text-left" type="button" onClick={() => selectObject(row.id)}>
            <div className="truncate font-medium text-slate-900">{row.objectName}</div>
            <div className="mt-0.5 truncate text-xs text-slate-500">{row.objectCode}</div>
          </button>
        ),
        responsivePriority: 100,
        title: objectColumnTitle,
        width: '220px'
      },
      { key: 'objectType', title: objectTypeColumnTitle, width: '130px', render: (row) => row.objectType },
      {
        align: 'center',
        key: 'status',
        render: (row) => <DataCenterStatusBadge status={row.status} />,
        title: statusColumnTitle,
        width: '100px'
      },
      {
        key: 'lastValidationStatus',
        render: (row) => (
          <div className="min-w-0">
            <DataCenterStatusBadge status={row.lastValidationStatus || 'Unknown'} />
            {row.lastValidationMessage ? <div className="mt-1 truncate text-xs text-slate-500">{row.lastValidationMessage}</div> : null}
          </div>
        ),
        title: validationColumnTitle,
        width: '170px'
      },
      { align: 'center', key: 'referenceCount', render: (row) => row.referenceCount, title: referenceColumnTitle, width: '80px' },
      { key: 'updatedTime', render: (row) => formatDate(row.updatedTime ?? row.createdTime), title: updatedTimeColumnTitle, width: '150px' }
    ],
    [
      objectColumnTitle,
      objectTypeColumnTitle,
      referenceColumnTitle,
      selectObject,
      state.pageIndex,
      state.pageSize,
      statusColumnTitle,
      updatedTimeColumnTitle,
      validationColumnTitle
    ]
  );

  const rows = listQuery.data?.data.items ?? [];
  const total = listQuery.data?.data.total ?? 0;
  const detail = detailQuery.data?.data ?? null;
  const saveLoading = createMutation.isPending || updateMutation.isPending;
  const searchFields = useMemo<FormFieldConfig<DataCenterSearchState>[]>(
    () => [
      { label: translateCurrentLiteral("关键字"), name: 'keyword', placeholder: '编码、名称、端点', type: 'text' },
      {
        emptyOptionLabel: '全部类型',
        label: translateCurrentLiteral("类型"),
        name: 'objectType',
        options: typeOptions.map((item) => ({ label: item.title, value: item.type })),
        type: 'select'
      },
      { label: translateCurrentLiteral("状态"), name: 'status', options: statusOptions, type: 'select' }
    ],
    [typeOptions]
  );

  const searchArea = (
    <SearchForm
      fields={searchFields}
      loading={listQuery.isFetching}
      onReset={() =>
        setState((current) => ({
          ...current,
          pageIndex: 1,
          search: defaultSearchState,
          searchDraft: defaultSearchState
        }))
      }
      onSubmit={(value) => setState((current) => ({ ...current, pageIndex: 1, search: value }))}
      onValueChange={(value) => setState((current) => ({ ...current, searchDraft: value }))}
      value={state.searchDraft}
    />
  );

  const actions = (
    <div className="flex items-center gap-2">
      <button className="secondary-button h-8 text-xs" type="button" onClick={() => void refreshModule()}>
        <AppIcon name="refresh" />{translateCurrentLiteral("刷新")}</button>
      <PermissionButton className="primary-button h-8 text-xs" code={config.permissions.add} type="button" onClick={openCreate}>
        新增{config.itemName}
      </PermissionButton>
    </div>
  );

  return (
    <ApplicationConsolePageFrame density="compact" hideDescription pageKey="data-center">
      {() => (
        <DataCenterWorkspaceShell
          moduleKey={config.moduleKey}
          selectedDataSourceId={dataSourceContext.dataSourceId}
          toolbar={
            <WorkspaceToolbar
              actions={actions}
              density="tight"
              description={config.description}
              icon={<AppIcon className="h-4 w-4" name={resolveModuleIcon(config.moduleKey)} />}
              subtitle={dataSourceContext.dataSource ? `${dataSourceContext.dataSource.objectName} / ${dataSourceContext.dataSource.objectCode}` : '当前模块'}
              title={config.title}
            />
          }
        >
          <div className="space-y-2">
            {config.moduleKey !== 'data-source' ? (
              <DataSourceContextBanner
                dataSource={dataSourceContext.dataSource}
                dataSourceId={dataSourceContext.dataSourceId}
                loading={dataSourceContext.loading}
                onChange={changeDataSourceContext}
                onClear={() => changeDataSourceContext('')}
              />
            ) : null}
            <DataCenterPrerequisiteBanner message={resolvePrerequisiteMessage(config.moduleKey)} />
            <DataCenterLayout
              density="tight"
              detail={
                <DataCenterDetailPanel
                  actionResult={state.actionResult}
                  config={config}
                  diagnostic={config.moduleKey === 'data-source' ? state.diagnostic : null}
                  detail={detail}
                  loading={detailQuery.isFetching}
                  preview={state.preview}
                  onDelete={confirmDelete}
                  onDiagnose={config.moduleKey === 'data-source' ? (item) => void diagnoseMutation.mutateAsync(item.id) : undefined}
                  onDisable={(item) => void disableMutation.mutateAsync(item.id)}
                  onEdit={(item) => openEdit(item)}
                  onEnable={(item) => void enableMutation.mutateAsync(item.id)}
                  onPreview={(item) => void previewMutation.mutateAsync(item.id)}
                  onPublish={confirmPublish}
                  onTest={(item) => void testMutation.mutateAsync(item.id)}
                  onEnterWorkspace={config.moduleKey === 'data-source' ? enterDataSourceWorkspace : undefined}
                />
              }
              table={
                <WorkspacePanel
                  bodyClassName="min-h-0 overflow-hidden p-0"
                  className="flex min-h-0 flex-col"
                  description="查询条件、对象列表和行级动作走同一块主面板，避免再次叠一层页面骨架。"
                  title={`${config.itemName}列表`}
                >
                  <div className="border-b border-slate-200 px-3 py-2">{searchArea}</div>
                  <DataTable
                    columnSettingsKey={`application-data-center-${config.resourcePath}`}
                    columns={columns}
                    emptyText={listQuery.isError ? '列表加载失败' : '暂无数据'}
                    fitScreen
                    loading={listQuery.isFetching}
                    onPageChange={(pageIndex) => setState((current) => ({ ...current, pageIndex }))}
                    onPageSizeChange={(pageSize) => setState((current) => ({ ...current, pageIndex: 1, pageSize }))}
                    pagination={{ current: state.pageIndex, pageSize: state.pageSize, total }}
                    rowActions={(row) => (
                      <TableActions>
                        <PermissionButton className="hover:text-primary-600" code={config.permissions.edit} title={translateCurrentLiteral("编辑")} type="button" onClick={() => void openEditFromRow(row)}>
                          <AppIcon className="h-4 w-4" name="edit" />
                        </PermissionButton>
                        <PermissionButton className="hover:text-primary-600" code={config.permissions.test} title={translateCurrentLiteral("测试")} type="button" onClick={() => void testMutation.mutateAsync(row.id)}>
                          <AppIcon className="h-4 w-4" name="play" />
                        </PermissionButton>
                        <PermissionButton className="hover:text-primary-600" code={config.permissions.preview} title={translateCurrentLiteral("预览")} type="button" onClick={() => void previewMutation.mutateAsync(row.id)}>
                          <AppIcon className="h-4 w-4" name="eye" />
                        </PermissionButton>
                        <PermissionButton className="hover:text-primary-600" code={config.permissions.publish} title={translateCurrentLiteral("发布")} type="button" onClick={() => confirmPublish(row)}>
                          <AppIcon className="h-4 w-4" name="rocket" />
                        </PermissionButton>
                        <PermissionButton className="hover:text-red-600" code={config.permissions.delete} title={translateCurrentLiteral("删除")} type="button" onClick={() => confirmDelete(row)}>
                          <AppIcon className="h-4 w-4" name="trash" />
                        </PermissionButton>
                        {config.moduleKey === 'data-source' ? (
                          <PermissionButton className="hover:text-primary-600" code={config.permissions.view} title={translateCurrentLiteral("进入数据库工作台")} type="button" onClick={() => enterDataSourceWorkspace(row)}>
                            <AppIcon className="h-4 w-4" name="arrow-right" />
                          </PermissionButton>
                        ) : null}
                      </TableActions>
                    )}
                    rowKey={(row) => row.id}
                    rows={rows}
                    onRow={(row) => selectObject(row.id)}
                    onRowDoubleClick={(row) => void openEditFromRow(row)}
                  />
                </WorkspacePanel>
              }
              typeTree={
                <DataCenterTypeTree
                  activeType={state.search.objectType}
                  loading={typeOptionsQuery.isFetching}
                  options={typeOptions}
                  onChange={(objectType) =>
                    setState((current) => ({
                      ...current,
                      pageIndex: 1,
                      search: { ...current.search, objectType },
                      searchDraft: { ...current.searchDraft, objectType }
                    }))
                  }
                />
              }
            />

            <DataCenterWizardDrawer
              config={config}
              dataSourceContext={dataSourceContext}
              editingId={state.editingId}
              publicConfigJson={state.editingPublicConfigJson}
              form={state.form}
              loading={saveLoading}
              diagnosing={draftDiagnoseMutation.isPending}
              diagnosticStages={state.diagnostic?.stages}
              diagnosticSuccess={state.diagnostic?.success}
              open={state.drawerOpen}
              templates={templates}
              typeOptions={typeOptions}
              onChange={(form) => setState((current) => ({ ...current, diagnostic: config.moduleKey === 'data-source' ? null : current.diagnostic, form }))}
              onClose={() => setState((current) => ({ ...current, drawerOpen: false }))}
              onDiagnose={config.moduleKey === 'data-source' ? () => void diagnoseDraftForm() : undefined}
              onSubmit={() => void saveForm()}
            />
          </div>
        </DataCenterWorkspaceShell>
      )}
    </ApplicationConsolePageFrame>
  );

  function openCreate() {
    const objectType = state.search.objectType || typeOptions[0]?.type || config.defaultObjectType;
    setState((current) => ({
      ...current,
      actionResult: null,
      diagnostic: null,
      drawerOpen: true,
      editingId: null,
      editingPublicConfigJson: null,
      form: applyDataSourceContext(createDefaultForm(config, objectType), config, dataSourceId),
      preview: null
    }));
  }

  async function openEditFromRow(row: ApplicationDataCenterObjectListItem) {
    try {
      const response = await getApplicationDataCenterObject(config.resourcePath, row.id);
      openEdit(response.data);
    } catch (error) {
      message.error(getErrorMessage(error, '详情加载失败'));
    }
  }

  function openEdit(item: ApplicationDataCenterObjectDetail) {
    setState((current) => ({
      ...current,
      actionResult: null,
      diagnostic: null,
      drawerOpen: true,
      editingId: item.id,
      editingPublicConfigJson: item.publicConfigJson ?? null,
      form: {
        configJson: item.configJson || '{}',
        confirmedRiskFields: [],
        endpoint: item.endpoint ?? '',
        environment: item.environment ?? '',
        objectCode: item.objectCode,
        objectName: item.objectName,
        objectType: item.objectType,
        ownerUserId: item.ownerUserId ?? '',
        remark: item.remark ?? '',
        secretConfigJson: null
      },
      preview: null,
      selectedId: item.id
    }));
  }

  async function saveForm() {
    if (saveInFlightRef.current) {
      return;
    }
    const validation = firstValidationMessage(validateConfigForm(state.form, getConfigFormSchema(config.moduleKey, state.form.objectType)));
    if (validation) {
      message.error(validation);
      return;
    }

    saveInFlightRef.current = true;
    const request = normalizeConfigRequest(state.form, { isEdit: Boolean(state.editingId) });
    if (config.moduleKey === 'data-source') {
      const diagnostic = await draftDiagnoseMutation.mutateAsync(request);
      if (!diagnostic.data.success) {
        message.error('连接诊断未通过，修复后才能保存');
        saveInFlightRef.current = false;
        return;
      }
      request.diagnosticFingerprint = diagnostic.data.connectionFingerprint ?? null;
    }
    if (state.editingId) {
      if (config.moduleKey === 'data-source') {
        await updateMutation.mutateAsync({ id: state.editingId, request });
      } else {
        await updateMutation.mutateAsync({ id: state.editingId, request });
      }
      saveInFlightRef.current = false;
      return;
    }

    await createMutation.mutateAsync(request);
    saveInFlightRef.current = false;
  }

  async function diagnoseDraftForm() {
    const validation = firstValidationMessage(validateConfigForm(state.form, getConfigFormSchema(config.moduleKey, state.form.objectType)));
    if (validation) {
      message.error(validation);
      return;
    }

    await draftDiagnoseMutation.mutateAsync(normalizeConfigRequest(state.form, { isEdit: Boolean(state.editingId) }));
  }

  function confirmDelete(item: ApplicationDataCenterObjectListItem | ApplicationDataCenterObjectDetail) {
    confirm({
      content: `删除 ${item.objectName} 前会由后端校验引用，存在引用时将返回影响范围。`,
      confirmText: '删除',
      onConfirm: async () => {
        await deleteMutation.mutateAsync(item.id);
      },
      title: `删除${config.itemName}`
    });
  }

  function confirmPublish(item: ApplicationDataCenterObjectListItem | ApplicationDataCenterObjectDetail) {
    confirm({
      content: `发布 ${item.objectName} 后会写入运行时配置，并重新计算引用与下一步动作。`,
      confirmText: '发布',
      onConfirm: async () => {
        await publishMutation.mutateAsync(item.id);
      },
      title: `发布${config.itemName}`
    });
  }

  async function refreshModule() {
    await queryClient.invalidateQueries({ queryKey: moduleQueryKey });
  }

  async function afterMutation(response: ApiEnvelope<{ object: ApplicationDataCenterObjectDetail }>) {
    setState((current) => ({
      ...current,
      actionResult: null,
      drawerOpen: false,
      editingId: null,
      editingPublicConfigJson: null,
      preview: null,
      selectedId: response.data.object.id
    }));
    await refreshModule();
  }

  function changeDataSourceContext(nextDataSourceId: string) {
    const next = new URLSearchParams(searchParams);
    if (nextDataSourceId) {
      next.set('dataSourceId', nextDataSourceId);
    } else {
      next.delete('dataSourceId');
    }

    setSearchParams(next);
  }

  function enterDataSourceWorkspace(item: ApplicationDataCenterObjectListItem | ApplicationDataCenterObjectDetail) {
    if (!tenantId || !appCode) {
      return;
    }

    navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/data-center/data-sources/${encodeURIComponent(item.id)}/workbench`);
  }
}

function createDefaultForm(config: DataCenterModuleConfig, objectType: string): ApplicationDataCenterObjectUpsertRequest {
  const schema = getConfigFormSchema(config.moduleKey, objectType);
  return {
    configJson: buildDefaultConfigJson(schema),
    confirmedRiskFields: [],
    endpoint: '',
    environment: 'default',
    objectCode: '',
    objectName: '',
    objectType,
    ownerUserId: '',
    remark: '',
    secretConfigJson: ''
  };
}

function applyDataSourceContext(
  form: ApplicationDataCenterObjectUpsertRequest,
  config: DataCenterModuleConfig,
  dataSourceId: string
): ApplicationDataCenterObjectUpsertRequest {
  if (!dataSourceId) {
    return form;
  }

  const values = parseJsonObject(form.configJson).value;
  if (config.moduleKey === 'connection-test') {
    values.dataSourceId = dataSourceId;
  }

  if (config.moduleKey === 'query-dataset') {
    values.sourceObjectId = dataSourceId;
  }

  if (config.moduleKey === 'integration-task') {
    values.sourceObjectId = dataSourceId;
  }

  return {
    ...form,
    configJson: stringifyJsonObject(values)
  };
}

function formatDate(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function resolvePrerequisiteMessage(moduleKey: string): string | null {
  if (moduleKey === 'data-source') {
    return null;
  }

  return '当前页面只处理应用级数据中心对象。引用数据源、微流或数据集时，后端会按应用库实时校验状态、引用和风险确认。';
}

function resolveModuleIcon(moduleKey: string): Parameters<typeof AppIcon>[0]['name'] {
  if (moduleKey === 'data-source') {
    return 'database';
  }

  if (moduleKey === 'entity-field') {
    return 'table';
  }

  if (moduleKey === 'query-dataset') {
    return 'activity';
  }

  if (moduleKey === 'microflow') {
    return 'module';
  }

  if (moduleKey === 'integration-task') {
    return 'refresh';
  }

  if (moduleKey === 'connection-test') {
    return 'shield';
  }

  return 'book';
}
