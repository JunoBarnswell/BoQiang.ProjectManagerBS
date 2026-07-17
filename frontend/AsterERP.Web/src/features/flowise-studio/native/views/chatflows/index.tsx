import ListIcon from '@mui/icons-material/List';
import RefreshIcon from '@mui/icons-material/Refresh';
import ViewModuleIcon from '@mui/icons-material/ViewModule';
import { Button, FormControl, InputLabel, MenuItem, Select, Stack, TextField, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';

import { appEnv } from '../../../../../core/config/env';
import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { PermissionButton } from '../../../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { useConfirm } from '../../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { flowiseConfigurationResourcesApi } from '../../../api/configurationResources.api';
import { nativeChatflowsApi } from '../../../api/nativeChatflows.api';
import { flowiseNativeResourcesApi } from '../../../api/nativeResources.api';
import { parseFlowDataString } from '../../../canvas/FlowiseCanvasModel';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseFlowData } from '../../../types/canvas.types';
import type { FlowiseChatflowDto, FlowiseChatflowType, FlowiseChatflowUpsertRequest } from '../../../types/chatflow.types';
import { FlowListMenu, type FlowListMenuPermissions, type FlowListSaveAction } from '../../ui-component/button/FlowListMenu';
import { ItemCard } from '../../ui-component/cards/ItemCard';
import type { ExportAsTemplatePayload } from '../../ui-component/dialog/ExportAsTemplateDialog';
import { FlowListTable, type FlowListTableColumn } from '../../ui-component/table/FlowListTable';

type FlowiseNativeFlowListProps = {
  title: string;
  type: FlowiseChatflowType;
};

const defaultPageSize = 12;

export function FlowiseNativeFlowListPage({ title, type }: FlowiseNativeFlowListProps) {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const pageTitle = translate(title);
  const confirm = useConfirm();
  const message = useMessage();
  const viewStorageKey = `flowise.${type}.displayStyle`;
  const pageSizeStorageKey = `flowise.${type}.pageSize`;
  const sourceStoragePrefix = type === 'AGENTFLOW' ? 'agentcanvas' : 'chatflowcanvas';
  const orderStorageKey = `${sourceStoragePrefix}_order`;
  const orderByStorageKey = `${sourceStoragePrefix}_orderBy`;
  const [keyword, setKeyword] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(() => Number(localStorage.getItem(pageSizeStorageKey) || defaultPageSize));
  const [viewMode, setViewMode] = useState<'card' | 'list'>(() => localStorage.getItem(viewStorageKey) === 'list' ? 'list' : 'card');
  const [order, setOrder] = useState<'asc' | 'desc'>(() => localStorage.getItem(orderStorageKey) === 'desc' ? 'desc' : 'asc');
  const [orderBy, setOrderBy] = useState<'name' | 'updatedDate'>(() => localStorage.getItem(orderByStorageKey) === 'updatedDate' ? 'updatedDate' : 'name');
  const query = useApiQuery({
    queryKey: ['flowise-native', type, keyword, currentPage, pageSize],
    queryFn: ({ signal }) => nativeChatflowsApi.list(type, { keyword, pageIndex: currentPage, pageSize, type }, signal)
  });
  const credentialsQuery = useApiQuery({
    queryKey: ['flowise-native-credentials-for-stt'],
    queryFn: ({ signal }) => flowiseConfigurationResourcesApi.credentials.list({ pageIndex: 1, pageSize: 500 }, signal)
  });
  const deleteMutation = useApiMutation({
    mutationFn: (id: string) => nativeChatflowsApi.delete(type, id),
    onSuccess: async () => query.refetch()
  });
  const updateFlowMutation = useApiMutation({
    mutationFn: ({ action, item, request }: { action: FlowListSaveAction; item: FlowiseChatflowDto; request: FlowiseChatflowUpsertRequest }) => {
      if (action === 'config') {
        return nativeChatflowsApi.updateConfiguration(type, item.id, pickConfigurationRequest(request));
      }

      if (action === 'domains') {
        return nativeChatflowsApi.updateDomains(type, item.id, { chatbotConfig: request.chatbotConfig });
      }

      return nativeChatflowsApi.update(type, item.id, request);
    },
    onSuccess: async () => {
      message.success(translate(flowiseI18nKeys.messages.configurationSaved));
      await query.refetch();
    },
    onError: () => message.error(translate(flowiseI18nKeys.messages.saveFailed))
  });
  const templateMutation = useApiMutation({
    mutationFn: ({ item, payload }: { item: FlowiseChatflowDto; payload: ExportAsTemplatePayload }) =>
      flowiseNativeResourcesApi.marketplaces.createFromFlowTemplate({
        category: payload.category,
        definitionJson: JSON.stringify(generateExportFlowData(parseFlowDataString(item.flowData))),
        description: payload.description,
        displayName: payload.displayName,
        metadataJson: JSON.stringify({
          sourceFlowId: item.id,
          sourceFlowName: item.name,
          sourceFlowType: item.type
        }),
        resourceKey: payload.resourceKey,
        status: 'enabled',
        workspaceId: item.workspaceId
      }),
    onError: () => message.error(translate(flowiseI18nKeys.messages.templateSaveFailed)),
    onSuccess: () => message.success(translate(flowiseI18nKeys.messages.templateSaved))
  });
  const rows = useMemo(() => query.data?.data.items ?? [], [query.data?.data.items]);
  const credentials = credentialsQuery.data?.data.items ?? [];
  const total = query.data?.data.total ?? rows.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const displayType = type === 'AGENTFLOW' ? translate(flowiseI18nKeys.pages.workflows) : translate(flowiseI18nKeys.pages.chatflows);
  const canvasPrefix = type === 'AGENTFLOW' ? '/flowise/workflows' : '/flowise/canvas';
  const editPermission = type === 'AGENTFLOW' ? flowisePermissions.agentflowsEdit : flowisePermissions.chatflowsEdit;
  const menuPermissions = useMemo<FlowListMenuPermissions>(() => type === 'AGENTFLOW'
    ? {
        config: flowisePermissions.agentflowsConfig,
        delete: flowisePermissions.agentflowsDelete,
        domains: flowisePermissions.agentflowsDomains,
        duplicate: flowisePermissions.agentflowsDuplicate,
        export: flowisePermissions.agentflowsExport,
        templateExport: flowisePermissions.templatesFlowExport,
        update: flowisePermissions.agentflowsEdit
      }
    : {
        config: flowisePermissions.chatflowsConfig,
        delete: flowisePermissions.chatflowsDelete,
        domains: flowisePermissions.chatflowsDomains,
        duplicate: flowisePermissions.chatflowsDuplicate,
        export: flowisePermissions.chatflowsExport,
        templateExport: flowisePermissions.templatesFlowExport,
        update: flowisePermissions.chatflowsEdit
      }, [type]);
  const flowNodePreviews = useMemo(() => resolveFlowNodePreviews(rows), [rows]);

  const visibleRows = useMemo(
    () => rows.filter((row) => {
      if (!keyword) {
        return true;
      }
      const normalizedKeyword = keyword.toLowerCase();
      return [row.name, row.category, row.id, row.type].some((value) => value?.toLowerCase().includes(normalizedKeyword));
    }),
    [keyword, rows]
  );

  const sortedRows = useMemo(() => {
    return [...visibleRows].sort((left, right) => {
      const leftValue = orderBy === 'name' ? left.name : left.updatedDate ?? left.createdDate;
      const rightValue = orderBy === 'name' ? right.name : right.updatedDate ?? right.createdDate;
      const result = String(leftValue ?? '').localeCompare(String(rightValue ?? ''));
      return order === 'asc' ? result : -result;
    });
  }, [order, orderBy, visibleRows]);

  const tableColumns = useMemo<FlowListTableColumn<FlowiseChatflowDto>[]>(() => [
    {
      key: 'name',
      sortable: true,
      title: translate(flowiseI18nKeys.fields.name),
      width: 'minmax(220px, 1.2fr)',
      render: (item) => (
        <Link className="flowise-native-table-link" to={`${canvasPrefix}/${item.id}`}>
          {item.name}
        </Link>
      )
    },
    {
      key: 'category',
      title: translate(flowiseI18nKeys.fields.category),
      width: 'minmax(140px, 0.8fr)',
      render: (item) => <FlowCategoryChips category={item.category} />
    },
    {
      key: 'nodes',
      title: translate(flowiseI18nKeys.canvas.allNodes),
      width: 'minmax(220px, 1fr)',
      render: (item) => <FlowNodePreviewList previews={flowNodePreviews[item.id] ?? []} compact />
    },
    {
      key: 'updatedDate',
      sortable: true,
      title: translate(flowiseI18nKeys.fields.updated),
      width: 'minmax(190px, 0.9fr)',
      render: (item) => formatDateTime(item.updatedDate ?? item.createdDate)
    }
  ], [canvasPrefix, flowNodePreviews, translate]);

  const openCreate = () => {
    navigate(canvasPrefix);
  };

  const changeViewMode = (nextMode: 'card' | 'list') => {
    localStorage.setItem(viewStorageKey, nextMode);
    setViewMode(nextMode);
  };

  const changePageSize = (nextSize: number) => {
    localStorage.setItem(pageSizeStorageKey, String(nextSize));
    setPageSize(nextSize);
    setCurrentPage(1);
  };

  const changeSort = (key: string) => {
    if (key !== 'name' && key !== 'updatedDate') {
      return;
    }
    const nextOrder = orderBy === key && order === 'asc' ? 'desc' : 'asc';
    localStorage.setItem(orderByStorageKey, key);
    localStorage.setItem(orderStorageKey, nextOrder);
    setOrderBy(key);
    setOrder(nextOrder);
  };

  const exportFlow = (item: FlowiseChatflowDto) => {
    const payload = JSON.stringify(generateExportFlowData(parseFlowDataString(item.flowData)), null, 2);
    const url = URL.createObjectURL(new Blob([payload], { type: 'application/json' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${sanitizeFileName(`${item.name} ${type === 'AGENTFLOW' ? 'Agents' : 'Chatflow'}`)}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    message.success(translate(flowiseI18nKeys.messages.exportCompleted));
  };

  const exportTemplate = async (item: FlowiseChatflowDto, payload: ExportAsTemplatePayload) => {
    await templateMutation.mutateAsync({ item, payload });
  };

  const duplicateFlow = (item: FlowiseChatflowDto) => {
    try {
      localStorage.setItem('duplicatedFlowData', item.flowData);
      navigate(canvasPrefix);
    } catch {
      message.error(translate(flowiseI18nKeys.messages.flowCopyFailed));
    }
  };

  const confirmDelete = (item: FlowiseChatflowDto) => {
    confirm({
      title: translate(flowiseI18nKeys.messages.deleteConfirmTitle),
      content: translate(flowiseI18nKeys.messages.deleteFlowConfirm).replace('{name}', item.name),
      confirmText: translate(flowiseI18nKeys.actions.delete),
      onConfirm: async () => {
        await deleteMutation.mutateAsync(item.id);
        message.success(translate(flowiseI18nKeys.messages.flowDeleted));
      }
    });
  };

  const saveFlow = async (item: FlowiseChatflowDto, patch: Partial<FlowiseChatflowUpsertRequest>, action: FlowListSaveAction) => {
    await updateFlowMutation.mutateAsync({
      action,
      item,
      request: {
        ...toUpsertRequest(item),
        ...patch,
        type
      }
    });
  };

  const handleImportFile = async (file?: File | null) => {
    if (!file) {
      return;
    }

    try {
      const text = await file.text();
      const flowData = resolveImportFlowData(text);
      if (!flowData) {
        message.error(translate(flowiseI18nKeys.messages.importFailed));
        return;
      }
      localStorage.setItem('duplicatedFlowData', flowData);
      message.success(translate(flowiseI18nKeys.messages.importCompleted));
      navigate(canvasPrefix);
    } catch {
      message.error(translate(flowiseI18nKeys.messages.importFailed));
    }
  };

  const openImportPicker = () => {
    const input = document.createElement('input');
    input.accept = 'application/json';
    input.type = 'file';
    input.onchange = () => {
      void handleImportFile(input.files?.[0]);
    };
    input.click();
  };

  return (
    <section className="flowise-native-page">
      <header className="flowise-native-page__header">
        <div>
          <h1>{pageTitle}</h1>
          <span>{displayType}</span>
        </div>
        <Stack className="flowise-native-page__actions" direction={{ xs: 'column', sm: 'row' }} spacing={1}>
          <ToggleButtonGroup
            exclusive
            value={viewMode}
            onChange={(_, value: 'card' | 'list' | null) => {
              if (value) {
                changeViewMode(value);
              }
            }}
          >
            <ToggleButton title={translate(flowiseI18nKeys.actions.cardView)} value="card">
              <ViewModuleIcon fontSize="small" />
            </ToggleButton>
            <ToggleButton title={translate(flowiseI18nKeys.actions.listView)} value="list">
              <ListIcon fontSize="small" />
            </ToggleButton>
          </ToggleButtonGroup>
          <Button disabled={query.isFetching} startIcon={<RefreshIcon />} type="button" variant="outlined" onClick={() => void query.refetch()}>
            {translate(flowiseI18nKeys.actions.refresh)}
          </Button>
          <PermissionButton className="btn-secondary" code={editPermission} iconStart={false} onClick={openImportPicker} type="button">
            {translate(flowiseI18nKeys.actions.import)}
          </PermissionButton>
          <PermissionButton className="btn-primary" code={editPermission} onClick={openCreate} type="button">
            {translate(flowiseI18nKeys.common.new)}
          </PermissionButton>
        </Stack>
      </header>

      <Stack className="flowise-native-page__toolbar" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
        <TextField placeholder={translate(flowiseI18nKeys.search.placeholder)} value={keyword} onChange={(event) => setKeyword(event.target.value)} />
        {keyword ? <Button type="button" variant="text" onClick={() => setKeyword('')}>{translate(flowiseI18nKeys.actions.clear)}</Button> : null}
        <FormControl size="small">
          <InputLabel id={`${type.toLowerCase()}-page-size-label`}>{translate(flowiseI18nKeys.fields.pageSize)}</InputLabel>
          <Select
            label={translate(flowiseI18nKeys.fields.pageSize)}
            labelId={`${type.toLowerCase()}-page-size-label`}
            value={pageSize}
            onChange={(event) => changePageSize(Number(event.target.value))}
          >
            {[12, 24, 48].map((size) => <MenuItem key={size} value={size}>{size}</MenuItem>)}
          </Select>
        </FormControl>
      </Stack>

      {viewMode === 'card' ? (
        <div className="flowise-native-flow-grid">
          {sortedRows.map((item) => (
            <ItemCard
              actions={(
                <>
                  <Link className="btn-secondary" to={`${canvasPrefix}/${item.id}`}>
                    {translate(flowiseI18nKeys.common.canvas)}
                  </Link>
                  <FlowListMenu
                    credentials={credentials}
                    item={item}
                    permissions={menuPermissions}
                    saving={updateFlowMutation.isPending || deleteMutation.isPending || templateMutation.isPending}
                    onDelete={confirmDelete}
                    onDuplicate={duplicateFlow}
                    onExport={exportFlow}
                    onExportTemplate={exportTemplate}
                    onSaveFlow={saveFlow}
                  />
                </>
              )}
              icon={type === 'AGENTFLOW' ? 'robot' : 'git-branch'}
              key={item.id}
              meta={(
                <>
                  <span>{item.deployed ? translate(flowiseI18nKeys.status.deployed) : translate(flowiseI18nKeys.status.draft)}</span>
                  <span>{item.isPublic ? translate(flowiseI18nKeys.status.public) : translate(flowiseI18nKeys.status.private)}</span>
                  {item.category ? <span>{item.category}</span> : null}
                </>
              )}
              subtitle={item.id}
              title={(
                <Link className="flowise-native-card-title-link" to={`${canvasPrefix}/${item.id}`}>
                  {item.name}
                </Link>
              )}
            >
              <FlowNodePreviewList previews={flowNodePreviews[item.id] ?? []} />
            </ItemCard>
          ))}
        </div>
      ) : (
        <FlowListTable
          columns={tableColumns}
          emptyText={translate(flowiseI18nKeys.messages.noFlows)}
          getRowKey={(item) => item.id}
          loading={query.isLoading}
          order={order}
          orderBy={orderBy}
          rowActions={(item) => (
            <FlowListMenu
              credentials={credentials}
              item={item}
              permissions={menuPermissions}
              saving={updateFlowMutation.isPending || deleteMutation.isPending || templateMutation.isPending}
              variant="ghost"
              onDelete={confirmDelete}
              onDuplicate={duplicateFlow}
              onExport={exportFlow}
              onExportTemplate={exportTemplate}
              onSaveFlow={saveFlow}
            />
          )}
          rows={sortedRows}
          onSort={changeSort}
        />
      )}

      {!query.isLoading && viewMode === 'card' && sortedRows.length === 0 ? <div className="flowise-native-empty">{translate(flowiseI18nKeys.messages.noFlows)}</div> : null}

      {total > 0 ? (
        <Stack className="flowise-native-pagination" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
          <Button disabled={currentPage <= 1} type="button" variant="outlined" onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}>
            {translate(flowiseI18nKeys.actions.previous)}
          </Button>
          <span>{translate(flowiseI18nKeys.common.pagination).replace('{page}', String(currentPage)).replace('{total}', String(total))}</span>
          <Button disabled={currentPage >= totalPages} type="button" variant="outlined" onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}>
            {translate(flowiseI18nKeys.actions.next)}
          </Button>
        </Stack>
      ) : null}

    </section>
  );
}

interface FlowNodePreview {
  imageSrc: string;
  label: string;
  name: string;
}

function FlowNodePreviewList({ compact, previews }: { compact?: boolean; previews: FlowNodePreview[] }) {
  const { translate } = useI18n();
  if (previews.length === 0) {
    return null;
  }

  const previewLimit = compact ? 5 : 3;
  const visiblePreviews = previews.slice(0, previewLimit);
  const hiddenCount = previews.length - visiblePreviews.length;

  return (
    <div className={compact ? 'flowise-native-node-preview compact' : 'flowise-native-node-preview'}>
      {visiblePreviews.map((preview) => (
        <span className="flowise-native-node-preview__icon" key={preview.name} title={preview.label || preview.name}>
          <img alt={preview.label || preview.name} src={preview.imageSrc} />
        </span>
      ))}
      {hiddenCount > 0 ? (
        <span className="flowise-native-node-preview__more">
          {translate(flowiseI18nKeys.common.more).replace('{count}', String(hiddenCount))}
        </span>
      ) : null}
    </div>
  );
}

function FlowCategoryChips({ category }: { category?: string | null }) {
  const values = (category ?? '').split(';').map((item) => item.trim()).filter(Boolean);
  if (values.length === 0) {
    return null;
  }

  return (
    <div className="flowise-native-category-chips">
      {values.map((value) => <span key={value}>{value}</span>)}
    </div>
  );
}

function pickConfigurationRequest(request: FlowiseChatflowUpsertRequest) {
  return {
    analytic: request.analytic,
    apiConfig: request.apiConfig,
    chatbotConfig: request.chatbotConfig,
    followUpPrompts: request.followUpPrompts,
    mcpServerConfig: request.mcpServerConfig,
    speechToText: request.speechToText,
    textToSpeech: request.textToSpeech,
    webhookSecret: request.webhookSecret
  };
}

function resolveFlowNodePreviews(rows: FlowiseChatflowDto[]): Record<string, FlowNodePreview[]> {
  return rows.reduce<Record<string, FlowNodePreview[]>>((acc, row) => {
    const flowData = parseFlowDataString(row.flowData);
    const previews: FlowNodePreview[] = [];
    for (const node of flowData.nodes) {
      const data = node.data as { label?: string; name?: string; nodeType?: string; stickyNote?: boolean };
      const name = data.name ?? data.nodeType;
      if (!name || name === 'stickyNote' || name === 'stickyNoteAgentflow' || data.stickyNote) {
        continue;
      }
      if (previews.some((preview) => preview.name === name)) {
        continue;
      }
      previews.push({ imageSrc: resolveNodeIconUrl(name), label: data.label ?? node.data.displayName ?? name, name });
    }
    acc[row.id] = previews;
    return acc;
  }, {});
}

function resolveNodeIconUrl(name: string) {
  const baseUrl = appEnv.apiBaseUrl.replace(/\/+$/, '');
  return `${baseUrl}/v1/node-icon/${encodeURIComponent(name)}`;
}

function resolveImportFlowData(text: string): string | null {
  const parsed = JSON.parse(text) as unknown;
  if (!parsed || typeof parsed !== 'object') {
    return null;
  }

  const record = parsed as Record<string, unknown>;
  if (typeof record.flowData === 'string') {
    return record.flowData;
  }

  if (record.flow && typeof record.flow === 'object' && typeof (record.flow as Record<string, unknown>).flowData === 'string') {
    return (record.flow as Record<string, string>).flowData;
  }

  if (Array.isArray(record.nodes) && Array.isArray(record.edges)) {
    return JSON.stringify({ edges: record.edges, nodes: record.nodes, viewport: record.viewport });
  }

  return null;
}

function generateExportFlowData(flowData: FlowiseFlowData) {
  const nodes = flowData.nodes.map((node) => {
    const data = node.data as Record<string, unknown>;
    const inputParams = Array.isArray(data.inputParams) ? data.inputParams as Array<Record<string, unknown>> : [];
    const inputs = sanitizeInputs(data.inputs, inputParams);
    return {
      ...node,
      selected: false,
      data: removeCredentialIds({
        baseClasses: data.baseClasses,
        category: data.category,
        color: data.color,
        description: data.description,
        hideInput: data.hideInput,
        hideOutput: data.hideOutput,
        id: data.id,
        inputAnchors: data.inputAnchors,
        inputParams: data.inputParams,
        inputs,
        label: data.label ?? data.displayName,
        name: data.name ?? data.nodeType,
        outputAnchors: data.outputAnchors,
        outputs: data.outputs,
        selected: false,
        tags: data.tags,
        type: data.type,
        version: data.version
      })
    };
  });

  return { edges: flowData.edges, nodes };
}

function sanitizeInputs(inputs: unknown, inputParams: Array<Record<string, unknown>>) {
  if (!inputs || typeof inputs !== 'object' || Array.isArray(inputs)) {
    return {};
  }

  return Object.entries(inputs as Record<string, unknown>).reduce<Record<string, unknown>>((acc, [key, value]) => {
    const inputParam = inputParams.find((param) => param.name === key);
    if (inputParam?.type === 'password' || inputParam?.type === 'file' || inputParam?.type === 'folder') {
      return acc;
    }
    acc[key] = value;
    return acc;
  }, {});
}

function removeCredentialIds(value: unknown): unknown {
  if (!value || typeof value !== 'object') {
    return value;
  }

  if (Array.isArray(value)) {
    return value.map(removeCredentialIds);
  }

  return Object.entries(value as Record<string, unknown>).reduce<Record<string, unknown>>((acc, [key, nestedValue]) => {
    if (key !== 'FLOWISE_CREDENTIAL_ID') {
      acc[key] = removeCredentialIds(nestedValue);
    }
    return acc;
  }, {});
}

function toUpsertRequest(item: FlowiseChatflowDto): FlowiseChatflowUpsertRequest {
  return {
    analytic: item.analytic || '{}',
    apiConfig: item.apiConfig || '{}',
    apikeyid: item.apikeyid,
    category: item.category,
    chatbotConfig: item.chatbotConfig || '{}',
    deployed: item.deployed,
    flowData: item.flowData,
    followUpPrompts: item.followUpPrompts || '{}',
    isPublic: item.isPublic,
    mcpServerConfig: item.mcpServerConfig || '{}',
    name: item.name,
    speechToText: item.speechToText || '{}',
    textToSpeech: item.textToSpeech || '{}',
    type: item.type,
    workspaceId: item.workspaceId
  };
}

function formatDateTime(value?: string | null) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

const unsafeFileNameChars = new Set(['<', '>', ':', '"', '/', '\\', '|', '?', '*']);

function sanitizeFileName(fileName: string) {
  return Array.from(fileName, (char) => (unsafeFileNameChars.has(char) || char.charCodeAt(0) < 32 ? '_' : char)).join('').trim() || 'flowise-flow';
}
