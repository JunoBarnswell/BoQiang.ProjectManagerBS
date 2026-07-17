import { Button, Checkbox, FormControlLabel, IconButton, MenuItem, TextField } from '@mui/material';
import { useEffect, useMemo, useState } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../shared/auth/permissionCodes';
import { useMessage } from '../../../shared/feedback/useMessage';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { chatflowsApi } from '../api/chatflows.api';
import { documentStoresApi } from '../api/documentStores.api';
import { flowiseStudioApi } from '../api/flowiseStudio.api';
import { predictionsApi } from '../api/predictions.api';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { ScheduleHistoryDrawer } from '../native/views/schedule/ScheduleHistoryDrawer';
import { WebhookListenerDrawer } from '../native/views/webhooklistener/WebhookListenerDrawer';
import type { FlowiseCanvasMode } from '../types/canvas.types';
import type { FlowiseChatflowDto, FlowiseChatflowUpsertRequest, FlowiseMcpServerConfigDto, FlowiseMcpServerUpsertRequest } from '../types/chatflow.types';
import type { FlowiseDocumentStoreUpsertHistoryDto } from '../types/documentStore.types';
import type { FlowiseChatMessageDto, FlowiseLeadDto } from '../types/prediction.types';
import type { FlowiseSharedWorkspaceDto } from '../types/shared.types';

export type FlowiseCanvasHeaderDialogKind =
  | 'apiCode'
  | 'configuration'
  | 'exportTemplate'
  | 'leads'
  | 'messages'
  | 'share'
  | 'schedule'
  | 'upsert'
  | 'upsertHistory'
  | 'webhook';

interface FlowiseConfigurationDraft {
  analytic: string;
  apiConfig: string;
  apikeyid: string;
  category: string;
  chatbotConfig: string;
  deployed: boolean;
  followUpPrompts: string;
  isPublic: boolean;
  mcpServerConfig: string;
  name: string;
  speechToText: string;
  textToSpeech: string;
  webhookSecret: string;
  workspaceId: string;
}

type JsonRecord = Record<string, unknown>;

interface RateLimitConfig {
  limitDuration: string;
  limitMax: string;
  limitMsg: string;
  status: boolean;
}

interface OverrideConfigDraft {
  nodesJson: string;
  status: boolean;
  variablesJson: string;
}

interface LeadsConfigDraft {
  email: boolean;
  name: boolean;
  phone: boolean;
  status: boolean;
  successMessage: string;
  title: string;
}

interface FileUploadConfigDraft {
  allowedUploadFileTypes: string[];
  pdfUsage: string;
  status: boolean;
}

interface PostProcessingConfigDraft {
  customFunction: string;
  enabled: boolean;
}

interface McpServerConfigDraft {
  description: string;
  enabled: boolean;
  token: string;
  toolName: string;
}

interface FollowUpPromptsProviderConfig {
  baseUrl: string;
  credentialId: string;
  modelName: string;
  prompt: string;
  temperature: string;
}

interface FlowiseCanvasHeaderDialogsProps {
  activeDialog: FlowiseCanvasHeaderDialogKind | null;
  chatflow: FlowiseChatflowDto | null;
  configurationSaving: boolean;
  flowData: string;
  flowType: string;
  mode: FlowiseCanvasMode;
  resourceId: string;
  title: string;
  upsertAvailable: boolean;
  onSaveConfiguration: (request: FlowiseChatflowUpsertRequest) => Promise<unknown>;
  onClose: () => void;
}

export function FlowiseCanvasHeaderDialogs({
  activeDialog,
  chatflow,
  configurationSaving,
  flowData,
  flowType,
  mode,
  resourceId,
  title,
  upsertAvailable,
  onSaveConfiguration,
  onClose
}: FlowiseCanvasHeaderDialogsProps) {
  const { translate } = useI18n();
  const message = useMessage();
  const [copied, setCopied] = useState(false);
  const [apiCodeTab, setApiCodeTab] = useState('');
  const [configurationDraft, setConfigurationDraft] = useState<FlowiseConfigurationDraft>(() => createConfigurationDraft(chatflow, flowType, title));
  const apiEndpoint = `/api/ai/flowise/prediction`;
  const webhookEndpoint = `/api/v1/webhook/${resourceId}`;
  const scheduleInfo = useMemo(() => resolveScheduleInfo(flowData), [flowData]);
  const apiCodeModel = useMemo(() => resolveApiCodeModel(flowData, resourceId, apiEndpoint, webhookEndpoint), [apiEndpoint, flowData, resourceId, webhookEndpoint]);
  const flowKind = (chatflow?.type ?? flowType) === 'AGENTFLOW' ? 'agentflows' : 'chatflows';
  const upsertTarget = useMemo(() => resolveUpsertTarget(flowData), [flowData]);
  const messagesQuery = useApiQuery({
    enabled: activeDialog === 'messages' && Boolean(resourceId),
    queryKey: ['flowise', 'prediction', 'messages', resourceId],
    queryFn: ({ signal }) => predictionsApi.messages.list({ pageIndex: 1, pageSize: 50, resourceId }, signal)
  });
  const leadsQuery = useApiQuery({
    enabled: activeDialog === 'leads' && Boolean(resourceId),
    queryKey: ['flowise', 'prediction', 'leads', resourceId],
    queryFn: ({ signal }) => predictionsApi.leads.list({ pageIndex: 1, pageSize: 50, resourceId }, signal)
  });
  const sharedWorkspacesQuery = useApiQuery({
    enabled: activeDialog === 'share' && Boolean(resourceId),
    queryKey: ['flowise', 'shared-workspaces', resourceId],
    queryFn: ({ signal }) => flowiseStudioApi.sharedWorkspaces.list(resourceId, signal)
  });
  const mcpServerQuery = useApiQuery({
    enabled: activeDialog === 'configuration' && Boolean(resourceId),
    queryKey: ['flowise', 'mcp-server', resourceId],
    queryFn: ({ signal }) => chatflowsApi.mcpServer.get(resourceId, signal)
  });
  const upsertHistoryQuery = useApiQuery({
    enabled: (activeDialog === 'upsert' || activeDialog === 'upsertHistory') && Boolean(upsertTarget?.storeId),
    queryKey: ['flowise', 'document-store', 'upsert-history', upsertTarget?.storeId ?? 'none'],
    queryFn: ({ signal }) => documentStoresApi.upsertHistory(upsertTarget?.storeId ?? '', signal)
  });
  const upsertMutation = useApiMutation({
    mutationFn: (replaceExisting: boolean) =>
      documentStoresApi.upsert({
        chatflowId: resourceId,
        flowData,
        loaderId: upsertTarget?.loaderId ?? null,
        overrideConfigJson: '{}',
        replaceExisting,
        storeId: upsertTarget?.storeId ?? ''
      }),
    onSuccess: async () => {
      await upsertHistoryQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.upsertCompleted));
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.upsertFailed)));
    }
  });
  const saveShareMutation = useApiMutation({
    mutationFn: (workspaceIds: string[]) =>
      flowiseStudioApi.sharedWorkspaces.save(resourceId, { itemType: chatflow?.type ?? flowType, workspaceIds }),
    onSuccess: async () => {
      await sharedWorkspacesQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.shareSaved));
    }
  });
  const syncMcpServerConfig = (config: FlowiseMcpServerConfigDto) => {
    setConfigurationDraft((current) => ({
      ...current,
      mcpServerConfig: stringifyJsonRecord(normalizeMcpServerConfig(readMcpServerConfig(config)))
    }));
  };
  const saveMcpServerMutation = useApiMutation({
    mutationFn: (request: FlowiseMcpServerUpsertRequest) =>
      mcpServerQuery.data?.data.hasExistingConfig ? chatflowsApi.mcpServer.update(resourceId, request) : chatflowsApi.mcpServer.create(resourceId, request),
    onSuccess: async (response) => {
      syncMcpServerConfig(response.data);
      await mcpServerQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.mcpServerSaved));
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.mcpServerSaveFailed)));
    }
  });
  const disableMcpServerMutation = useApiMutation({
    mutationFn: () => chatflowsApi.mcpServer.disable(resourceId),
    onSuccess: async (response) => {
      syncMcpServerConfig(response.data);
      await mcpServerQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.mcpServerDisabled));
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.mcpServerSaveFailed)));
    }
  });
  const refreshMcpServerTokenMutation = useApiMutation({
    mutationFn: () => chatflowsApi.mcpServer.refresh(resourceId),
    onSuccess: async (response) => {
      syncMcpServerConfig(response.data);
      await mcpServerQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.mcpTokenRotated));
    },
    onError: (error) => {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.mcpServerSaveFailed)));
    }
  });
  const selectedApiCodeTab = apiCodeModel.tabs.includes(apiCodeTab) ? apiCodeTab : apiCodeModel.tabs[0];
  const selectedApiSnippet = selectedApiCodeTab ? apiCodeModel.snippets[selectedApiCodeTab] : '';

  useEffect(() => {
    if (activeDialog === 'configuration') {
      setConfigurationDraft(createConfigurationDraft(chatflow, flowType, title));
    }
  }, [activeDialog, chatflow, flowData, flowType, title]);

  useEffect(() => {
    if (activeDialog === 'apiCode') {
      setApiCodeTab(apiCodeModel.tabs[0] ?? '');
    }
  }, [activeDialog, apiCodeModel.tabs]);

  useEffect(() => {
    if (activeDialog === 'configuration' && mcpServerQuery.data?.data) {
      syncMcpServerConfig(mcpServerQuery.data.data);
    }
  }, [activeDialog, mcpServerQuery.data?.data]);

  if (!activeDialog) {
    return null;
  }

  const copy = async (value: string) => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    message.success(translate(flowiseI18nKeys.messages.copiedToClipboard));
    window.setTimeout(() => setCopied(false), 1200);
  };

  const exportTemplate = () => {
    downloadJson(`${title || flowType}-template.json`, {
      exportedAt: new Date().toISOString(),
      flowData: parseFlowData(flowData),
      flowType,
      mode,
      name: title,
      protocol: 'flowise.flowData',
      resourceId
    });
  };

  if (activeDialog === 'schedule') {
    return <ScheduleHistoryDrawer flowKind={flowKind} open resourceId={resourceId} scheduleInfo={scheduleInfo} translate={translate} onClose={onClose} />;
  }

  if (activeDialog === 'webhook') {
    return (
      <WebhookListenerDrawer
        chatflowId={resourceId}
        endpoint={webhookEndpoint}
        open
        translate={translate}
        webhookSecret={chatflow?.webhookSecretConfigured ? translate(flowiseI18nKeys.status.enabled) : translate(flowiseI18nKeys.common.none)}
        onClose={onClose}
        onCopyEndpoint={() => void copy(webhookEndpoint)}
      />
    );
  }

  const saveConfiguration = async () => {
    if (!chatflow) {
      return;
    }

    await onSaveConfiguration({
      analytic: configurationDraft.analytic,
      apiConfig: configurationDraft.apiConfig,
      apikeyid: normalizeOptional(configurationDraft.apikeyid),
      category: normalizeOptional(configurationDraft.category),
      chatbotConfig: configurationDraft.chatbotConfig,
      deployed: configurationDraft.deployed,
      flowData,
      followUpPrompts: configurationDraft.followUpPrompts,
      isPublic: configurationDraft.isPublic,
      mcpServerConfig: configurationDraft.mcpServerConfig,
      name: configurationDraft.name,
      speechToText: configurationDraft.speechToText,
      textToSpeech: configurationDraft.textToSpeech,
      type: chatflow.type,
      webhookSecret: normalizeOptional(configurationDraft.webhookSecret),
      workspaceId: normalizeOptional(configurationDraft.workspaceId)
    });
  };

  return (
    <div className="flowise-native-dialog-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="flowise-canvas-header-dialog" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}>
        <header>
          <h3>{dialogTitle(activeDialog, translate)}</h3>
          <IconButton aria-label={translate(flowiseI18nKeys.actions.close)} size="small" onClick={onClose}>
            <AppIcon name="x" />
          </IconButton>
        </header>
        {activeDialog === 'apiCode' ? (
          <div className="flowise-dialog-section">
            <label>
              <span>{translate(flowiseI18nKeys.detail.apiEndpoint)}</span>
              <code>{apiCodeModel.endpoint}</code>
            </label>
            <label>
              <span>Input Type</span>
              <code>{apiCodeModel.inputType}</code>
            </label>
            <ApiCodeTabs tabs={apiCodeModel.tabs} value={selectedApiCodeTab} onChange={setApiCodeTab} />
            {selectedApiCodeTab === 'Embed' ? (
              <EmbedApiPanel embedSnippet={apiCodeModel.snippets.Embed ?? ''} resourceId={resourceId} onCopy={copy} copied={copied} translate={translate} />
            ) : null}
            {selectedApiCodeTab === 'Share Chatbot' ? (
              <ShareChatbotApiPanel shareUrl={apiCodeModel.snippets['Share Chatbot'] ?? ''} onCopy={copy} copied={copied} translate={translate} />
            ) : null}
            {selectedApiCodeTab !== 'Embed' && selectedApiCodeTab !== 'Share Chatbot' ? (
              <>
                <pre>{selectedApiSnippet}</pre>
                <Button startIcon={<AppIcon name={copied ? 'check' : 'copy'} />} variant="outlined" onClick={() => void copy(selectedApiSnippet)}>
                  {translate(copied ? flowiseI18nKeys.actions.copied : flowiseI18nKeys.actions.copy)}
                </Button>
              </>
            ) : null}
          </div>
        ) : null}
        {activeDialog === 'configuration' ? (
          <ConfigurationDialogBody
            draft={configurationDraft}
            flowType={flowType}
            mcpServerConfig={mcpServerQuery.data?.data ?? null}
            mcpServerLoading={mcpServerQuery.isLoading}
            mcpServerSaving={saveMcpServerMutation.isPending || disableMcpServerMutation.isPending || refreshMcpServerTokenMutation.isPending}
            resourceId={resourceId}
            saving={configurationSaving}
            translate={translate}
            webhookSecretConfigured={chatflow?.webhookSecretConfigured ?? false}
            onChange={setConfigurationDraft}
            onDisableMcpServer={() => disableMcpServerMutation.mutateAsync()}
            onRefreshMcpServerToken={() => refreshMcpServerTokenMutation.mutateAsync()}
            onSave={() => void saveConfiguration()}
            onSaveMcpServer={(request) => saveMcpServerMutation.mutateAsync(request)}
          />
        ) : null}
        {activeDialog === 'exportTemplate' ? (
          <div className="flowise-dialog-section">
            <p>{translate(flowiseI18nKeys.detail.templateExport)}</p>
            <Button startIcon={<AppIcon name="download" />} variant="contained" onClick={exportTemplate}>
              {translate(flowiseI18nKeys.actions.export)}
            </Button>
          </div>
        ) : null}
        {activeDialog === 'share' ? (
          <ShareDialogBody
            loading={sharedWorkspacesQuery.isLoading}
            saving={saveShareMutation.isPending}
            title={chatflow?.name ?? title}
            translate={translate}
            workspaces={sharedWorkspacesQuery.data?.data ?? []}
            onSave={(workspaceIds) => saveShareMutation.mutate(workspaceIds)}
          />
        ) : null}
        {activeDialog === 'upsert' ? (
          <UpsertDialogBody
            available={upsertAvailable}
            history={upsertHistoryQuery.data?.data ?? []}
            loading={upsertHistoryQuery.isLoading}
            running={upsertMutation.isPending}
            target={upsertTarget}
            translate={translate}
            onRun={(replaceExisting) => upsertMutation.mutate(replaceExisting)}
          />
        ) : null}
        {activeDialog === 'upsertHistory' ? (
          <UpsertHistoryDialogBody
            history={upsertHistoryQuery.data?.data ?? []}
            loading={upsertHistoryQuery.isLoading}
            target={upsertTarget}
            translate={translate}
          />
        ) : null}
        {activeDialog === 'messages' || activeDialog === 'leads' ? (
          <HistoryDialogBody
            kind={activeDialog}
            leads={leadsQuery.data?.data.items ?? []}
            loading={activeDialog === 'messages' ? messagesQuery.isLoading : leadsQuery.isLoading}
            messages={messagesQuery.data?.data.items ?? []}
            resourceId={resourceId}
            total={activeDialog === 'messages' ? messagesQuery.data?.data.total ?? 0 : leadsQuery.data?.data.total ?? 0}
            translate={translate}
          />
        ) : null}
      </section>
    </div>
  );
}

function HistoryDialogBody({
  kind,
  leads,
  loading,
  messages,
  resourceId,
  total,
  translate
}: {
  kind: 'leads' | 'messages';
  leads: FlowiseLeadDto[];
  loading: boolean;
  messages: FlowiseChatMessageDto[];
  resourceId: string;
  total: number;
  translate: (key: string) => string;
}) {
  return (
    <div className="flowise-dialog-section">
      <dl>
        <dt>{translate(flowiseI18nKeys.fields.resource)}</dt>
        <dd>{resourceId}</dd>
        <dt>{translate(flowiseI18nKeys.detail.rows)}</dt>
        <dd>{total}</dd>
      </dl>
      {loading ? <p>{translate(flowiseI18nKeys.status.running)}</p> : null}
      {!loading && kind === 'messages' ? <MessagesExplorer messages={messages} resourceId={resourceId} translate={translate} /> : null}
      {!loading && kind === 'leads' ? <LeadsExplorer leads={leads} resourceId={resourceId} translate={translate} /> : null}
    </div>
  );
}

function ShareDialogBody({
  loading,
  saving,
  title,
  translate,
  workspaces,
  onSave
}: {
  loading: boolean;
  saving: boolean;
  title: string;
  translate: (key: string) => string;
  workspaces: FlowiseSharedWorkspaceDto[];
  onSave: (workspaceIds: string[]) => void;
}) {
  const [rows, setRows] = useState<FlowiseSharedWorkspaceDto[]>(workspaces);

  useEffect(() => {
    setRows(workspaces);
  }, [workspaces]);

  const updateRow = (workspaceId: string, shared: boolean) => {
    setRows((current) => current.map((item) => (item.workspaceId === workspaceId ? { ...item, shared } : item)));
  };

  return (
    <div className="flowise-dialog-section">
      <TextField disabled fullWidth label={translate(flowiseI18nKeys.fields.name)} size="small" value={title} />
      {loading ? <p>{translate(flowiseI18nKeys.status.running)}</p> : null}
      {!loading && rows.length === 0 ? <p>{translate(flowiseI18nKeys.messages.noWorkspaces)}</p> : null}
      {!loading && rows.length > 0 ? (
        <div className="flowise-dialog-table-wrap">
          <table className="flowise-dialog-table">
            <thead>
              <tr>
                <th>{translate(flowiseI18nKeys.fields.workspace)}</th>
                <th>{translate(flowiseI18nKeys.actions.share)}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((item) => (
                <tr key={item.workspaceId}>
                  <td>{item.workspaceName}</td>
                  <td>
                    <Checkbox checked={item.shared} size="small" onChange={(event) => updateRow(item.workspaceId, event.target.checked)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      <div className="flowise-dialog-actions">
        <Button disabled={saving || loading} startIcon={<AppIcon name="upload-simple" />} variant="contained" onClick={() => onSave(rows.filter((item) => item.shared).map((item) => item.workspaceId))}>
          {translate(flowiseI18nKeys.actions.share)}
        </Button>
      </div>
    </div>
  );
}

function ApiCodeTabs({
  tabs,
  value,
  onChange
}: {
  tabs: string[];
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="flowise-api-code-tabs" role="tablist" aria-label="API code examples">
      {tabs.map((tab) => (
        <button aria-selected={value === tab} key={tab} role="tab" type="button" onClick={() => onChange(tab)}>
          {tab}
        </button>
      ))}
    </div>
  );
}

function EmbedApiPanel({
  copied,
  embedSnippet,
  onCopy,
  resourceId,
  translate
}: {
  copied: boolean;
  embedSnippet: string;
  onCopy: (value: string) => Promise<void>;
  resourceId: string;
  translate: (key: string) => string;
}) {
  return (
    <div className="flowise-api-embed-panel">
      <p>Embed this workflow chat widget in an internal page by adding the script below before the closing body tag.</p>
      <dl>
        <dt>Chatflow ID</dt>
        <dd>{resourceId}</dd>
      </dl>
      <pre>{embedSnippet}</pre>
      <Button startIcon={<AppIcon name={copied ? 'check' : 'copy'} />} variant="outlined" onClick={() => void onCopy(embedSnippet)}>
        {translate(copied ? flowiseI18nKeys.actions.copied : flowiseI18nKeys.actions.copy)}
      </Button>
    </div>
  );
}

function ShareChatbotApiPanel({
  copied,
  onCopy,
  shareUrl,
  translate
}: {
  copied: boolean;
  onCopy: (value: string) => Promise<void>;
  shareUrl: string;
  translate: (key: string) => string;
}) {
  return (
    <div className="flowise-api-embed-panel">
      <p>Use this public chatbot link when the workflow is shared without API key authorization.</p>
      <code>{shareUrl}</code>
      <Button startIcon={<AppIcon name={copied ? 'check' : 'copy'} />} variant="outlined" onClick={() => void onCopy(shareUrl)}>
        {translate(copied ? flowiseI18nKeys.actions.copied : flowiseI18nKeys.actions.copy)}
      </Button>
    </div>
  );
}

interface FlowiseUpsertTarget {
  label: string;
  loaderId?: string | null;
  nodeId: string;
  storeId: string;
}

function UpsertDialogBody({
  available,
  history,
  loading,
  running,
  target,
  translate,
  onRun
}: {
  available: boolean;
  history: FlowiseDocumentStoreUpsertHistoryDto[];
  loading: boolean;
  running: boolean;
  target: FlowiseUpsertTarget | null;
  translate: (key: string) => string;
  onRun: (replaceExisting: boolean) => void;
}) {
  const [replaceExisting, setReplaceExisting] = useState(true);
  if (!available || !target) {
    return (
      <div className="flowise-dialog-section">
        <p>{translate(flowiseI18nKeys.messages.noUpsertTarget)}</p>
      </div>
    );
  }

  const latest = history[0];
  return (
    <div className="flowise-dialog-section">
      <dl>
        <dt>{translate(flowiseI18nKeys.detail.documentStore)}</dt>
        <dd>{target.storeId}</dd>
        <dt>{translate(flowiseI18nKeys.fields.loader)}</dt>
        <dd>{target.loaderId ?? '-'}</dd>
        <dt>{translate(flowiseI18nKeys.canvas.nodeInfo)}</dt>
        <dd>{target.label}</dd>
      </dl>
      <FormControlLabel
        className="flowise-dialog-check-inline"
        control={<Checkbox checked={replaceExisting} size="small" onChange={(event) => setReplaceExisting(event.target.checked)} />}
        label={translate(flowiseI18nKeys.detail.replaceExisting)}
      />
      {loading ? <p>{translate(flowiseI18nKeys.status.running)}</p> : null}
      {latest ? <UpsertHistorySummary item={latest} translate={translate} /> : <p>{translate(flowiseI18nKeys.messages.noUpsertHistory)}</p>}
      <div className="flowise-dialog-actions">
        <PermissionButton
          className="btn-primary"
          code={flowisePermissions.documentStoresUpsert}
          disabled={running}
          iconStart={false}
          type="button"
          onClick={() => onRun(replaceExisting)}
        >
          <AppIcon name="database" /> {translate(flowiseI18nKeys.actions.upsert)}
        </PermissionButton>
      </div>
    </div>
  );
}

function UpsertHistoryDialogBody({
  history,
  loading,
  target,
  translate
}: {
  history: FlowiseDocumentStoreUpsertHistoryDto[];
  loading: boolean;
  target: FlowiseUpsertTarget | null;
  translate: (key: string) => string;
}) {
  return (
    <div className="flowise-dialog-section">
      <dl>
        <dt>{translate(flowiseI18nKeys.detail.documentStore)}</dt>
        <dd>{target?.storeId ?? '-'}</dd>
      </dl>
      {loading ? <p>{translate(flowiseI18nKeys.status.running)}</p> : null}
      {!loading && history.length === 0 ? <p>{translate(flowiseI18nKeys.messages.noUpsertHistory)}</p> : null}
      {!loading && history.length > 0 ? (
        <div className="flowise-dialog-table-wrap">
          <table className="flowise-dialog-table">
            <thead>
              <tr>
                <th>{translate(flowiseI18nKeys.fields.created)}</th>
                <th>{translate(flowiseI18nKeys.fields.status)}</th>
                <th>{translate(flowiseI18nKeys.detail.processed)}</th>
                <th>{translate(flowiseI18nKeys.detail.added)}</th>
                <th>{translate(flowiseI18nKeys.detail.replaced)}</th>
                <th>{translate(flowiseI18nKeys.detail.skipped)}</th>
              </tr>
            </thead>
            <tbody>
              {history.map((item) => (
                <tr key={item.id}>
                  <td>{formatDateTime(item.createdTime)}</td>
                  <td>{item.status}</td>
                  <td>{item.processedCount}</td>
                  <td>{item.addedCount}</td>
                  <td>{item.replacedCount}</td>
                  <td>{item.skippedCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}

function UpsertHistorySummary({ item, translate }: { item: FlowiseDocumentStoreUpsertHistoryDto; translate: (key: string) => string }) {
  return (
    <dl>
      <dt>{translate(flowiseI18nKeys.detail.lastUpsert)}</dt>
      <dd>{formatDateTime(item.createdTime)}</dd>
      <dt>{translate(flowiseI18nKeys.fields.status)}</dt>
      <dd>{item.status}</dd>
      <dt>{translate(flowiseI18nKeys.detail.processed)}</dt>
      <dd>{item.processedCount}</dd>
    </dl>
  );
}

function ConfigurationDialogBody({
  draft,
  flowType,
  mcpServerConfig,
  mcpServerLoading,
  mcpServerSaving,
  resourceId,
  saving,
  translate,
  webhookSecretConfigured,
  onChange,
  onDisableMcpServer,
  onRefreshMcpServerToken,
  onSave,
  onSaveMcpServer
}: {
  draft: FlowiseConfigurationDraft;
  flowType: string;
  mcpServerConfig: FlowiseMcpServerConfigDto | null;
  mcpServerLoading: boolean;
  mcpServerSaving: boolean;
  resourceId: string;
  saving: boolean;
  translate: (key: string) => string;
  webhookSecretConfigured: boolean;
  onChange: (next: FlowiseConfigurationDraft) => void;
  onDisableMcpServer: () => Promise<unknown>;
  onRefreshMcpServerToken: () => Promise<unknown>;
  onSave: () => void;
  onSaveMcpServer: (request: FlowiseMcpServerUpsertRequest) => Promise<unknown>;
}) {
  const update = (patch: Partial<FlowiseConfigurationDraft>) => onChange({ ...draft, ...patch });
  const chatbotConfig = parseJsonRecord(draft.chatbotConfig);
  const apiConfig = parseJsonRecord(draft.apiConfig);
  const followUpPromptsConfig = parseJsonRecord(draft.followUpPrompts);
  const mcpServerDraft = readMcpServerConfig(mcpServerConfig ?? parseJsonRecord(draft.mcpServerConfig));
  const speechToText = parseJsonRecord(draft.speechToText);
  const textToSpeech = parseJsonRecord(draft.textToSpeech);
  const updateChatbotConfig = (next: JsonRecord) => update({ chatbotConfig: stringifyJsonRecord(next) });
  const updateApiConfig = (next: JsonRecord) => update({ apiConfig: stringifyJsonRecord(next) });
  const updateFollowUpPromptsConfig = (next: JsonRecord) => update({ followUpPrompts: stringifyJsonRecord(next) });
  const updateMcpServerConfig = (next: McpServerConfigDraft) => update({ mcpServerConfig: stringifyJsonRecord(normalizeMcpServerConfig(next)) });
  const updateSpeechToText = (next: JsonRecord) => update({ speechToText: stringifyJsonRecord(next) });
  const updateTextToSpeech = (next: JsonRecord) => update({ textToSpeech: stringifyJsonRecord(next) });
  return (
    <div className="flowise-dialog-section">
      <dl>
        <dt>{translate(flowiseI18nKeys.fields.resource)}</dt>
        <dd>{resourceId}</dd>
        <dt>{translate(flowiseI18nKeys.fields.type)}</dt>
        <dd>{flowType}</dd>
      </dl>
      <div className="flowise-dialog-form-grid">
        <TextField fullWidth label={translate(flowiseI18nKeys.fields.name)} size="small" value={draft.name} onChange={(event) => update({ name: event.target.value })} />
        <TextField fullWidth label={translate(flowiseI18nKeys.fields.category)} size="small" value={draft.category} onChange={(event) => update({ category: event.target.value })} />
        <TextField fullWidth label={translate(flowiseI18nKeys.fields.workspace)} size="small" value={draft.workspaceId} onChange={(event) => update({ workspaceId: event.target.value })} />
        <TextField fullWidth label={translate(flowiseI18nKeys.fields.key)} size="small" value={draft.apikeyid} onChange={(event) => update({ apikeyid: event.target.value })} />
      </div>
      <div className="flowise-dialog-check-row">
        <FormControlLabel
          className="flowise-dialog-check-inline"
          control={<Checkbox checked={draft.deployed} size="small" onChange={(event) => update({ deployed: event.target.checked })} />}
          label={translate(flowiseI18nKeys.status.deployed)}
        />
        <FormControlLabel
          className="flowise-dialog-check-inline"
          control={<Checkbox checked={draft.isPublic} size="small" onChange={(event) => update({ isPublic: event.target.checked })} />}
          label={translate(flowiseI18nKeys.status.public)}
        />
      </div>
      <div className="flowise-configuration-sections">
        <RateLimitSection apiConfig={apiConfig} translate={translate} onChange={updateApiConfig} />
        <AllowedDomainsSection chatbotConfig={chatbotConfig} translate={translate} onChange={updateChatbotConfig} />
        <StarterPromptsSection chatbotConfig={chatbotConfig} translate={translate} onChange={updateChatbotConfig} />
        <FollowUpPromptsSection
          chatbotConfig={chatbotConfig}
          followUpPromptsConfig={followUpPromptsConfig}
          translate={translate}
          onChatbotConfigChange={updateChatbotConfig}
          onConfigChange={updateFollowUpPromptsConfig}
        />
        <ChatFeedbackSection chatbotConfig={chatbotConfig} translate={translate} onChange={updateChatbotConfig} />
        <LeadsSection chatbotConfig={chatbotConfig} translate={translate} onChange={updateChatbotConfig} />
        <FileUploadSection chatbotConfig={chatbotConfig} translate={translate} onChange={updateChatbotConfig} />
        <PostProcessingSection chatbotConfig={chatbotConfig} translate={translate} onChange={updateChatbotConfig} />
        <OverrideConfigSection apiConfig={apiConfig} translate={translate} onChange={updateApiConfig} />
        <SpeechToTextSection speechToText={speechToText} translate={translate} onChange={updateSpeechToText} />
        <TextToSpeechSection textToSpeech={textToSpeech} translate={translate} onChange={updateTextToSpeech} />
        <McpServerSection
          config={mcpServerDraft}
          endpointPath={mcpServerConfig?.endpointPath ?? `/api/v1/mcp/${resourceId}`}
          loading={mcpServerLoading}
          saving={mcpServerSaving}
          translate={translate}
          onChange={updateMcpServerConfig}
          onCopyToken={(token) => navigator.clipboard.writeText(token)}
          onDisable={onDisableMcpServer}
          onRefreshToken={onRefreshMcpServerToken}
          onSave={() =>
            onSaveMcpServer({
              description: mcpServerDraft.description,
              enabled: mcpServerDraft.enabled,
              toolName: mcpServerDraft.toolName
            })
          }
        />
      </div>
      <details className="flowise-configuration-advanced">
        <summary>{translate(flowiseI18nKeys.configuration.advancedJson)}</summary>
        <JsonTextArea label={translate(flowiseI18nKeys.detail.configuration)} value={draft.chatbotConfig} onChange={(chatbotConfig) => update({ chatbotConfig })} />
        <JsonTextArea label={translate(flowiseI18nKeys.actions.apiCode)} value={draft.apiConfig} onChange={(apiConfig) => update({ apiConfig })} />
        <JsonTextArea label={translate(flowiseI18nKeys.canvas.input)} value={draft.speechToText} onChange={(speechToText) => update({ speechToText })} />
        <JsonTextArea label={translate(flowiseI18nKeys.canvas.output)} value={draft.textToSpeech} onChange={(textToSpeech) => update({ textToSpeech })} />
      </details>
      <JsonTextArea label={translate(flowiseI18nKeys.fields.metadata)} value={draft.analytic} onChange={(analytic) => update({ analytic })} />
      <JsonTextArea label={translate(flowiseI18nKeys.detail.sourceDocuments)} value={draft.followUpPrompts} onChange={(followUpPrompts) => update({ followUpPrompts })} />
      <JsonTextArea label={translate(flowiseI18nKeys.detail.vectorStore)} value={draft.mcpServerConfig} onChange={(mcpServerConfig) => update({ mcpServerConfig })} />
      <TextField
        fullWidth
        label={translate(flowiseI18nKeys.detail.webhookSecret)}
        placeholder={webhookSecretConfigured ? '********' : ''}
        size="small"
        type="password"
        value={draft.webhookSecret}
        onChange={(event) => update({ webhookSecret: event.target.value })}
      />
      <div className="flowise-dialog-actions">
        <Button disabled={saving} startIcon={<AppIcon name="floppy-disk" />} variant="contained" onClick={onSave}>
          {translate(flowiseI18nKeys.actions.save)}
        </Button>
      </div>
    </div>
  );
}

function JsonTextArea({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <TextField
      fullWidth
      label={label}
      minRows={5}
      multiline
      size="small"
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}

function RateLimitSection({ apiConfig, translate, onChange }: { apiConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const rateLimit = readRateLimit(apiConfig.rateLimit);
  const updateRateLimit = (patch: Partial<RateLimitConfig>) => {
    const next = { ...rateLimit, ...patch };
    onChange({ ...apiConfig, rateLimit: normalizeRateLimit(next) });
  };
  const invalid = rateLimit.status && [rateLimit.limitMax, rateLimit.limitDuration, rateLimit.limitMsg].some((value) => !value.trim());
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.rateLimit)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={<Checkbox checked={rateLimit.status} size="small" onChange={(event) => updateRateLimit({ status: event.target.checked })} />}
          label={translate(rateLimit.status ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
      {rateLimit.status ? (
        <div className="flowise-config-section__grid">
          <TextField
            fullWidth
            label={translate(flowiseI18nKeys.configuration.limitMax)}
            size="small"
            slotProps={{ htmlInput: { min: 1 } }}
            type="number"
            value={rateLimit.limitMax}
            onChange={(event) => updateRateLimit({ limitMax: event.target.value })}
          />
          <TextField
            fullWidth
            label={translate(flowiseI18nKeys.configuration.limitDuration)}
            size="small"
            slotProps={{ htmlInput: { min: 1 } }}
            type="number"
            value={rateLimit.limitDuration}
            onChange={(event) => updateRateLimit({ limitDuration: event.target.value })}
          />
          <TextField
            fullWidth
            label={translate(flowiseI18nKeys.configuration.limitMessage)}
            size="small"
            value={rateLimit.limitMsg}
            onChange={(event) => updateRateLimit({ limitMsg: event.target.value })}
          />
          {invalid ? <small className="flowise-config-section__error">{translate(flowiseI18nKeys.messages.rateLimitIncomplete)}</small> : null}
        </div>
      ) : null}
    </section>
  );
}

function AllowedDomainsSection({ chatbotConfig, translate, onChange }: { chatbotConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const origins = readStringArray(chatbotConfig.allowedOrigins);
  const errorMessage = typeof chatbotConfig.allowedOriginsError === 'string' ? chatbotConfig.allowedOriginsError : '';
  const updateOrigins = (nextOrigins: string[]) => onChange({ ...chatbotConfig, allowedOrigins: nextOrigins.filter((item) => item.trim()), allowedOriginsError: errorMessage });
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.allowedDomains)}</strong>
        <Button size="small" startIcon={<AppIcon name="plus" />} variant="outlined" onClick={() => updateOrigins([...origins, ''])}>
          {translate(flowiseI18nKeys.actions.addNew)}
        </Button>
      </header>
      <div className="flowise-dynamic-list">
        {(origins.length ? origins : ['']).map((origin, index) => (
          <div key={`${origin}-${index}`} className="flowise-dynamic-list__row">
            <TextField
              fullWidth
              placeholder="https://example.com"
              size="small"
              value={origin}
              onChange={(event) => updateOrigins(replaceAt(origins.length ? origins : [''], index, event.target.value))}
            />
            <IconButton aria-label={translate(flowiseI18nKeys.actions.delete)} size="small" onClick={() => updateOrigins(removeAt(origins, index))}>
              <AppIcon name="x" />
            </IconButton>
          </div>
        ))}
      </div>
      <TextField
        fullWidth
        label={translate(flowiseI18nKeys.configuration.allowedDomainsError)}
        size="small"
        value={errorMessage}
        onChange={(event) => onChange({ ...chatbotConfig, allowedOrigins: origins, allowedOriginsError: event.target.value })}
      />
    </section>
  );
}

function StarterPromptsSection({ chatbotConfig, translate, onChange }: { chatbotConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const prompts = readStarterPromptRows(chatbotConfig.starterPrompts);
  const effectivePrompts = prompts.length ? prompts : [''];
  const updatePrompts = (nextPrompts: string[]) => onChange({ ...chatbotConfig, starterPrompts: writeStarterPromptRows(nextPrompts) });
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.detail.starterPrompts)}</strong>
        <Button size="small" startIcon={<AppIcon name="plus" />} variant="outlined" onClick={() => updatePrompts([...effectivePrompts, ''])}>
          {translate(flowiseI18nKeys.actions.addNew)}
        </Button>
      </header>
      <div className="flowise-dynamic-list">
        {effectivePrompts.map((prompt, index) => (
          <div key={`${prompt}-${index}`} className="flowise-dynamic-list__row">
            <TextField
              fullWidth
              placeholder={translate(flowiseI18nKeys.detail.starterPrompts)}
              size="small"
              value={prompt}
              onChange={(event) => updatePrompts(replaceAt(effectivePrompts, index, event.target.value))}
            />
            <IconButton aria-label={translate(flowiseI18nKeys.actions.delete)} size="small" onClick={() => updatePrompts(removeAt(effectivePrompts, index))}>
              <AppIcon name="x" />
            </IconButton>
          </div>
        ))}
      </div>
    </section>
  );
}

function FollowUpPromptsSection({
  chatbotConfig,
  followUpPromptsConfig,
  translate,
  onChatbotConfigChange,
  onConfigChange
}: {
  chatbotConfig: JsonRecord;
  followUpPromptsConfig: JsonRecord;
  translate: (key: string) => string;
  onChatbotConfigChange: (next: JsonRecord) => void;
  onConfigChange: (next: JsonRecord) => void;
}) {
  const status = followUpPromptsConfig.status === true;
  const selectedProvider = readString(followUpPromptsConfig.selectedProvider) || 'none';
  const providerConfig = selectedProvider !== 'none' ? readFollowUpProviderConfig(followUpPromptsConfig[selectedProvider]) : readFollowUpProviderConfig({});
  const updateStatus = (nextStatus: boolean) => {
    onConfigChange({ ...followUpPromptsConfig, status: nextStatus });
    onChatbotConfigChange({ ...chatbotConfig, followUpPrompts: { ...readRecord(chatbotConfig.followUpPrompts), status: nextStatus } });
  };
  const updateProvider = (providerName: string) => {
    onConfigChange({ ...followUpPromptsConfig, selectedProvider: providerName });
  };
  const updateProviderConfig = (patch: Partial<FollowUpPromptsProviderConfig>) => {
    if (selectedProvider === 'none') {
      return;
    }

    onConfigChange({
      ...followUpPromptsConfig,
      [selectedProvider]: {
        ...providerConfig,
        ...patch
      }
    });
  };
  const invalid = status && (selectedProvider === 'none' || (selectedProvider !== 'ollama' && !providerConfig.credentialId.trim()) || !providerConfig.modelName.trim());
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.followUpPrompts)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={<Checkbox checked={status} size="small" onChange={(event) => updateStatus(event.target.checked)} />}
          label={translate(status ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
      {status ? (
        <div className="flowise-config-section__grid">
          <TextField
            fullWidth
            label={translate(flowiseI18nKeys.configuration.provider)}
            select
            size="small"
            value={selectedProvider}
            onChange={(event) => updateProvider(event.target.value)}
          >
            <MenuItem value="none">{translate(flowiseI18nKeys.common.none)}</MenuItem>
            {followUpPromptProviders.map((provider) => (
              <MenuItem key={provider.name} value={provider.name}>
                {provider.label}
              </MenuItem>
            ))}
          </TextField>
          {selectedProvider !== 'none' ? (
            <>
              {selectedProvider === 'ollama' ? (
                <TextField
                  fullWidth
                  label={translate(flowiseI18nKeys.configuration.baseUrl)}
                  size="small"
                  value={providerConfig.baseUrl}
                  onChange={(event) => updateProviderConfig({ baseUrl: event.target.value })}
                />
              ) : (
                <TextField
                  fullWidth
                  label={translate(flowiseI18nKeys.configuration.credential)}
                  size="small"
                  value={providerConfig.credentialId}
                  onChange={(event) => updateProviderConfig({ credentialId: event.target.value })}
                />
              )}
              <TextField
                fullWidth
                label={translate(flowiseI18nKeys.configuration.modelName)}
                size="small"
                value={providerConfig.modelName}
                onChange={(event) => updateProviderConfig({ modelName: event.target.value })}
              />
              <TextField
                fullWidth
                label={translate(flowiseI18nKeys.configuration.temperature)}
                size="small"
                slotProps={{ htmlInput: { step: 0.1 } }}
                type="number"
                value={providerConfig.temperature}
                onChange={(event) => updateProviderConfig({ temperature: event.target.value })}
              />
              <TextField
                fullWidth
                label={translate(flowiseI18nKeys.configuration.prompt)}
                minRows={4}
                multiline
                size="small"
                value={providerConfig.prompt}
                onChange={(event) => updateProviderConfig({ prompt: event.target.value })}
              />
            </>
          ) : null}
          {invalid ? <small className="flowise-config-section__error">{translate(flowiseI18nKeys.messages.followUpPromptsIncomplete)}</small> : null}
        </div>
      ) : null}
    </section>
  );
}

function ChatFeedbackSection({ chatbotConfig, translate, onChange }: { chatbotConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const config = readRecord(chatbotConfig.chatFeedback);
  const status = config.status === true;
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.chatFeedback)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={
            <Checkbox
              checked={status}
              size="small"
              onChange={(event) => onChange({ ...chatbotConfig, chatFeedback: { ...config, status: event.target.checked } })}
            />
          }
          label={translate(status ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
    </section>
  );
}

function LeadsSection({ chatbotConfig, translate, onChange }: { chatbotConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const leads = readLeadsConfig(chatbotConfig.leads);
  const updateLeads = (patch: Partial<LeadsConfigDraft>) => onChange({ ...chatbotConfig, leads: normalizeLeadsConfig({ ...leads, ...patch }) });
  const invalid = leads.status && !leads.name && !leads.email && !leads.phone;
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.leads)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={<Checkbox checked={leads.status} size="small" onChange={(event) => updateLeads({ status: event.target.checked })} />}
          label={translate(leads.status ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
      {leads.status ? (
        <div className="flowise-config-section__grid">
          <TextField
            fullWidth
            label={translate(flowiseI18nKeys.configuration.formTitle)}
            minRows={3}
            multiline
            placeholder={leadFormTitle}
            size="small"
            value={leads.title}
            onChange={(event) => updateLeads({ title: event.target.value })}
          />
          <TextField
            fullWidth
            label={translate(flowiseI18nKeys.configuration.successMessage)}
            minRows={3}
            multiline
            placeholder={leadSuccessMessage}
            size="small"
            value={leads.successMessage}
            onChange={(event) => updateLeads({ successMessage: event.target.value })}
          />
          <div className="flowise-config-toggle-list">
            <FormControlLabel
              className="flowise-switch-row"
              control={<Checkbox checked={leads.name} size="small" onChange={(event) => updateLeads({ name: event.target.checked })} />}
              label={translate(flowiseI18nKeys.fields.name)}
            />
            <FormControlLabel
              className="flowise-switch-row"
              control={<Checkbox checked={leads.email} size="small" onChange={(event) => updateLeads({ email: event.target.checked })} />}
              label={translate(flowiseI18nKeys.fields.email)}
            />
            <FormControlLabel
              className="flowise-switch-row"
              control={<Checkbox checked={leads.phone} size="small" onChange={(event) => updateLeads({ phone: event.target.checked })} />}
              label={translate(flowiseI18nKeys.fields.phone)}
            />
          </div>
          {invalid ? <small className="flowise-config-section__error">{translate(flowiseI18nKeys.messages.leadFieldsRequired)}</small> : null}
        </div>
      ) : null}
    </section>
  );
}

function FileUploadSection({ chatbotConfig, translate, onChange }: { chatbotConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const config = readFileUploadConfig(chatbotConfig.fullFileUpload);
  const updateConfig = (patch: Partial<FileUploadConfigDraft>) => onChange({ ...chatbotConfig, fullFileUpload: normalizeFileUploadConfig({ ...config, ...patch }) });
  const toggleFileType = (fileType: string, checked: boolean) => {
    const nextTypes = checked ? [...config.allowedUploadFileTypes, fileType] : config.allowedUploadFileTypes.filter((item) => item !== fileType);
    updateConfig({ allowedUploadFileTypes: Array.from(new Set(nextTypes)) });
  };
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.fileUpload)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={<Checkbox checked={config.status} size="small" onChange={(event) => updateConfig({ status: event.target.checked })} />}
          label={translate(config.status ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
      {config.status ? (
        <>
          <div className="flowise-file-type-grid">
            {availableUploadFileTypes.map((fileType) => (
              <FormControlLabel
                key={fileType.ext}
                className="flowise-switch-row"
                control={
                  <Checkbox
                    checked={config.allowedUploadFileTypes.includes(fileType.ext)}
                    size="small"
                    onChange={(event) => toggleFileType(fileType.ext, event.target.checked)}
                  />
                }
                label={
                  <span>
                    {fileType.name} ({fileType.extension})
                  </span>
                }
              />
            ))}
          </div>
          {config.allowedUploadFileTypes.includes('application/pdf') ? (
            <TextField
              fullWidth
              label={translate(flowiseI18nKeys.configuration.pdfProcessing)}
              select
              size="small"
              value={config.pdfUsage}
              onChange={(event) => updateConfig({ pdfUsage: event.target.value })}
            >
              <MenuItem value="perPage">{translate(flowiseI18nKeys.configuration.pdfPerPage)}</MenuItem>
              <MenuItem value="perFile">{translate(flowiseI18nKeys.configuration.pdfPerFile)}</MenuItem>
            </TextField>
          ) : null}
        </>
      ) : null}
    </section>
  );
}

function PostProcessingSection({ chatbotConfig, translate, onChange }: { chatbotConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const config = readPostProcessingConfig(chatbotConfig.postProcessing);
  const updateConfig = (patch: Partial<PostProcessingConfigDraft>) => onChange({ ...chatbotConfig, postProcessing: normalizePostProcessingConfig({ ...config, ...patch }) });
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.postProcessing)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={<Checkbox checked={config.enabled} size="small" onChange={(event) => updateConfig({ enabled: event.target.checked })} />}
          label={translate(config.enabled ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
      {config.enabled ? (
        <TextField
          fullWidth
          label={translate(flowiseI18nKeys.configuration.jsFunction)}
          minRows={8}
          multiline
          size="small"
          value={config.customFunction}
          onChange={(event) => updateConfig({ customFunction: event.target.value })}
        />
      ) : null}
    </section>
  );
}

function OverrideConfigSection({ apiConfig, translate, onChange }: { apiConfig: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  const overrideConfig = readOverrideConfig(apiConfig.overrideConfig);
  const updateOverride = (patch: Partial<OverrideConfigDraft>) => onChange({ ...apiConfig, overrideConfig: normalizeOverrideConfig({ ...overrideConfig, ...patch }) });
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.overrideConfig)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={<Checkbox checked={overrideConfig.status} size="small" onChange={(event) => updateOverride({ status: event.target.checked })} />}
          label={translate(overrideConfig.status ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
      {overrideConfig.status ? (
        <div className="flowise-config-section__grid">
          <JsonTextArea label={translate(flowiseI18nKeys.canvas.nodeInfo)} value={overrideConfig.nodesJson} onChange={(nodesJson) => updateOverride({ nodesJson })} />
          <JsonTextArea label={translate(flowiseI18nKeys.pages.variables)} value={overrideConfig.variablesJson} onChange={(variablesJson) => updateOverride({ variablesJson })} />
        </div>
      ) : null}
    </section>
  );
}

function SpeechToTextSection({ speechToText, translate, onChange }: { speechToText: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  return (
    <ProviderConfigSection
      config={speechToText}
      providers={speechToTextProviders}
      title={translate(flowiseI18nKeys.configuration.speechToText)}
      translate={translate}
      onChange={onChange}
    />
  );
}

function TextToSpeechSection({ textToSpeech, translate, onChange }: { textToSpeech: JsonRecord; translate: (key: string) => string; onChange: (next: JsonRecord) => void }) {
  return (
    <ProviderConfigSection
      config={textToSpeech}
      providers={textToSpeechProviders}
      title={translate(flowiseI18nKeys.configuration.textToSpeech)}
      translate={translate}
      onChange={onChange}
    />
  );
}

function McpServerSection({
  config,
  endpointPath,
  loading,
  saving,
  translate,
  onChange,
  onCopyToken,
  onDisable,
  onRefreshToken,
  onSave
}: {
  config: McpServerConfigDraft;
  endpointPath: string;
  loading: boolean;
  saving: boolean;
  translate: (key: string) => string;
  onChange: (next: McpServerConfigDraft) => void;
  onCopyToken: (token: string) => Promise<void>;
  onDisable: () => Promise<unknown>;
  onRefreshToken: () => Promise<unknown>;
  onSave: () => Promise<unknown>;
}) {
  const endpointUrl = typeof window === 'undefined' ? endpointPath : `${window.location.origin}${endpointPath}`;
  const toolNameInvalid = config.enabled && (!config.toolName.trim() || config.toolName.length > 64 || !/^[A-Za-z0-9_-]+$/.test(config.toolName.trim()));
  const descriptionInvalid = config.enabled && !config.description.trim();
  const canSave = config.enabled && !loading && !saving && !toolNameInvalid && !descriptionInvalid;
  const update = (patch: Partial<McpServerConfigDraft>) => onChange({ ...config, ...patch });
  const toggle = (enabled: boolean) => {
    if (enabled) {
      update({ enabled: true });
      return;
    }

    void onDisable();
  };
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{translate(flowiseI18nKeys.configuration.mcpServer)}</strong>
        <FormControlLabel
          className="flowise-switch-row"
          control={<Checkbox checked={config.enabled} disabled={loading || saving} size="small" onChange={(event) => toggle(event.target.checked)} />}
          label={translate(config.enabled ? flowiseI18nKeys.status.enabled : flowiseI18nKeys.status.disabled)}
        />
      </header>
      {config.enabled ? (
        <div className="flowise-config-section__grid">
          <TextField
            error={toolNameInvalid}
            label={translate(flowiseI18nKeys.configuration.toolName)}
            size="small"
            slotProps={{ htmlInput: { maxLength: 64 } }}
            value={config.toolName}
            onChange={(event) => update({ toolName: event.target.value })}
          />
          {toolNameInvalid ? <small className="flowise-config-section__error">{translate(flowiseI18nKeys.messages.mcpToolNameInvalid)}</small> : null}
          <TextField
            error={descriptionInvalid}
            label={translate(flowiseI18nKeys.fields.description)}
            multiline
            minRows={3}
            size="small"
            value={config.description}
            onChange={(event) => update({ description: event.target.value })}
          />
          {descriptionInvalid ? <small className="flowise-config-section__error">{translate(flowiseI18nKeys.messages.mcpDescriptionRequired)}</small> : null}
          <TextField
            label={translate(flowiseI18nKeys.configuration.endpoint)}
            size="small"
            slotProps={{ input: { readOnly: true } }}
            value={endpointUrl}
          />
          {config.token ? (
            <TextField
              label={translate(flowiseI18nKeys.configuration.token)}
              size="small"
              slotProps={{ input: { readOnly: true } }}
              type="password"
              value={config.token}
            />
          ) : null}
          <div className="flowise-config-section__actions">
            <Button disabled={!canSave} startIcon={<AppIcon name="floppy-disk" />} variant="contained" onClick={() => void onSave()}>
              {translate(flowiseI18nKeys.actions.save)}
            </Button>
            <Button disabled={!config.token || saving} startIcon={<AppIcon name="copy" />} variant="outlined" onClick={() => void onCopyToken(config.token)}>
              {translate(flowiseI18nKeys.actions.copy)}
            </Button>
            <Button disabled={!config.token || saving} startIcon={<AppIcon name="refresh" />} variant="outlined" onClick={() => void onRefreshToken()}>
              {translate(flowiseI18nKeys.actions.rotateToken)}
            </Button>
          </div>
        </div>
      ) : null}
    </section>
  );
}

function ProviderConfigSection({
  config,
  providers,
  title,
  translate,
  onChange
}: {
  config: JsonRecord;
  providers: string[];
  title: string;
  translate: (key: string) => string;
  onChange: (next: JsonRecord) => void;
}) {
  const selectedProvider = resolveEnabledProvider(config, providers);
  const providerConfig = selectedProvider && selectedProvider !== 'none' ? readProviderConfig(config[selectedProvider]) : {};
  const updateProvider = (providerName: string) => {
    const next = disableProviders(config, providers);
    if (providerName !== 'none') {
      next[providerName] = { ...(readProviderConfig(config[providerName])), status: true };
    }
    onChange(next);
  };
  const updateProviderConfig = (patch: JsonRecord) => {
    if (!selectedProvider || selectedProvider === 'none') {
      return;
    }

    onChange({
      ...config,
      [selectedProvider]: {
        ...providerConfig,
        ...patch,
        status: true
      }
    });
  };
  return (
    <section className="flowise-config-section">
      <header>
        <strong>{title}</strong>
        <TextField select size="small" value={selectedProvider ?? 'none'} onChange={(event) => updateProvider(event.target.value)}>
          <MenuItem value="none">{translate(flowiseI18nKeys.common.none)}</MenuItem>
          {providers.map((provider) => (
            <MenuItem key={provider} value={provider}>
              {provider}
            </MenuItem>
          ))}
        </TextField>
      </header>
      {selectedProvider && selectedProvider !== 'none' ? (
        <div className="flowise-config-section__grid">
          <TextField
            label={translate(flowiseI18nKeys.fields.key)}
            size="small"
            value={readString(providerConfig.credentialId)}
            onChange={(event) => updateProviderConfig({ credentialId: event.target.value })}
          />
          <TextField
            label={translate(flowiseI18nKeys.configuration.model)}
            size="small"
            value={readString(providerConfig.model)}
            onChange={(event) => updateProviderConfig({ model: event.target.value })}
          />
          <TextField
            label={translate(flowiseI18nKeys.configuration.voice)}
            size="small"
            value={readString(providerConfig.voice)}
            onChange={(event) => updateProviderConfig({ voice: event.target.value })}
          />
          <FormControlLabel
            className="flowise-switch-row"
            control={<Checkbox checked={providerConfig.autoPlay === true} size="small" onChange={(event) => updateProviderConfig({ autoPlay: event.target.checked })} />}
            label={translate(flowiseI18nKeys.configuration.autoPlay)}
          />
        </div>
      ) : null}
    </section>
  );
}

function MessagesExplorer({
  messages,
  resourceId,
  translate
}: {
  messages: FlowiseChatMessageDto[];
  resourceId: string;
  translate: (key: string) => string;
}) {
  const sessions = useMemo(() => groupMessagesByChatId(messages), [messages]);
  const [selectedChatId, setSelectedChatId] = useState('');
  const activeChatId = sessions.some((session) => session.chatId === selectedChatId) ? selectedChatId : sessions[0]?.chatId ?? '';
  const activeSession = sessions.find((session) => session.chatId === activeChatId);

  if (messages.length === 0) {
    return <p>{translate(flowiseI18nKeys.messages.noRows)}</p>;
  }

  return (
    <div className="flowise-message-explorer">
      <div className="flowise-history-toolbar">
        <strong>{sessions.length} conversations</strong>
        <Button size="small" startIcon={<AppIcon name="download" />} variant="outlined" onClick={() => downloadJson(`${resourceId}-messages.json`, { messages })}>
          {translate(flowiseI18nKeys.actions.export)}
        </Button>
      </div>
      <aside className="flowise-message-session-list" aria-label="Messages conversations">
        {sessions.map((session) => (
          <button aria-selected={session.chatId === activeChatId} key={session.chatId} type="button" onClick={() => setSelectedChatId(session.chatId)}>
            <strong>{session.chatId}</strong>
            <span>{session.messages.length} messages</span>
            <small>{formatDateTime(session.lastMessage.createdTime)}</small>
          </button>
        ))}
      </aside>
      <section className="flowise-message-detail">
        {activeSession ? (
          <>
            <div className="flowise-message-detail__meta">
              <span>Chat ID</span>
              <code>{activeSession.chatId}</code>
              <span>{translate(flowiseI18nKeys.detail.sourceDocuments)}</span>
              <code>{activeSession.sourceDocumentCount}</code>
              <span>{translate(flowiseI18nKeys.detail.usedTools)}</span>
              <code>{activeSession.usedToolCount}</code>
              <span>{translate(flowiseI18nKeys.detail.feedback)}</span>
              <code>{activeSession.feedbackCount}</code>
            </div>
            <div className="flowise-message-thread">
              {activeSession.messages.map((item) => (
                <article className={`flowise-message-thread__item flowise-message-thread__item--${item.role}`} key={item.id}>
                  <header>
                    <strong>{item.role}</strong>
                    <span>{formatDateTime(item.createdTime)}</span>
                  </header>
                  <p>{item.message}</p>
                  <MessageEvidenceSummary message={item} translate={translate} />
                </article>
              ))}
            </div>
          </>
        ) : null}
      </section>
    </div>
  );
}

function MessageEvidenceSummary({ message, translate }: { message: FlowiseChatMessageDto; translate: (key: string) => string }) {
  const hasEvidence =
    message.sourceDocuments.length > 0
    || message.usedTools.length > 0
    || message.agentReasoning.length > 0
    || message.agentExecutedData.length > 0
    || Boolean(message.feedback);

  if (!hasEvidence) {
    return null;
  }

  return (
    <div className="flowise-message-evidence">
      {message.sourceDocuments.length > 0 ? <span>{translate(flowiseI18nKeys.detail.sourceDocuments)}: {message.sourceDocuments.length}</span> : null}
      {message.usedTools.length > 0 ? <span>{translate(flowiseI18nKeys.detail.usedTools)}: {message.usedTools.map((tool) => tool.tool).join(', ')}</span> : null}
      {message.agentReasoning.length > 0 ? <span>{translate(flowiseI18nKeys.detail.agentReasoning)}: {message.agentReasoning.length}</span> : null}
      {message.agentExecutedData.length > 0 ? <span>{translate(flowiseI18nKeys.detail.executedData)}: {message.agentExecutedData.length}</span> : null}
      {message.feedback ? <span>{translate(flowiseI18nKeys.detail.feedback)}: {message.feedback.rating}</span> : null}
    </div>
  );
}

function LeadsExplorer({ leads, resourceId, translate }: { leads: FlowiseLeadDto[]; resourceId: string; translate: (key: string) => string }) {
  const [keyword, setKeyword] = useState('');
  const filteredLeads = useMemo(() => leads.filter((lead) => leadMatchesKeyword(lead, keyword)), [keyword, leads]);

  return (
    <div className="flowise-leads-explorer">
      <div className="flowise-history-toolbar">
        <TextField label={translate(flowiseI18nKeys.actions.search)} size="small" value={keyword} onChange={(event) => setKeyword(event.target.value)} />
        <Button size="small" startIcon={<AppIcon name="download" />} variant="outlined" onClick={() => downloadJson(`${resourceId}-leads.json`, { leads })}>
          {translate(flowiseI18nKeys.actions.export)}
        </Button>
      </div>
      {leads.length === 0 ? <p>{translate(flowiseI18nKeys.messages.noRows)}</p> : null}
      <table className="flowise-dialog-table">
        <thead>
          <tr>
            <th>{translate(flowiseI18nKeys.fields.created)}</th>
            <th>{translate(flowiseI18nKeys.fields.content)}</th>
          </tr>
        </thead>
        <tbody>
          {filteredLeads.map((item) => (
            <tr key={item.id}>
              <td>{formatDateTime(item.createdTime)}</td>
              <td>
                <pre className="flowise-dialog-inline-json">{formatJson(item.contactJson)}</pre>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

interface MessageSession {
  chatId: string;
  feedbackCount: number;
  lastMessage: FlowiseChatMessageDto;
  messages: FlowiseChatMessageDto[];
  sourceDocumentCount: number;
  usedToolCount: number;
}

function groupMessagesByChatId(messages: FlowiseChatMessageDto[]): MessageSession[] {
  const grouped = new Map<string, FlowiseChatMessageDto[]>();
  for (const message of messages) {
    const chatId = message.chatId?.trim() || 'unknown';
    grouped.set(chatId, [...(grouped.get(chatId) ?? []), message]);
  }

  return Array.from(grouped.entries())
    .map(([chatId, items]) => {
      const sorted = [...items].sort((left, right) => new Date(left.createdTime).getTime() - new Date(right.createdTime).getTime());
      return {
        chatId,
        feedbackCount: sorted.filter((item) => Boolean(item.feedback)).length,
        lastMessage: sorted[sorted.length - 1],
        messages: sorted,
        sourceDocumentCount: sorted.reduce((total, item) => total + item.sourceDocuments.length, 0),
        usedToolCount: sorted.reduce((total, item) => total + item.usedTools.length, 0)
      };
    })
    .sort((left, right) => new Date(right.lastMessage.createdTime).getTime() - new Date(left.lastMessage.createdTime).getTime());
}

function leadMatchesKeyword(lead: FlowiseLeadDto, keyword: string): boolean {
  const normalized = keyword.trim().toLowerCase();
  if (!normalized) {
    return true;
  }

  return [lead.contactJson, lead.createdTime].some((value) => value.toLowerCase().includes(normalized));
}

function dialogTitle(kind: FlowiseCanvasHeaderDialogKind, translate: (key: string) => string): string {
  if (kind === 'apiCode') {
    return translate(flowiseI18nKeys.canvas.apiCodeDialog);
  }

  if (kind === 'webhook') {
    return translate(flowiseI18nKeys.canvas.webhookDialog);
  }

  if (kind === 'schedule') {
    return translate(flowiseI18nKeys.canvas.scheduleDialog);
  }

  if (kind === 'configuration') {
    return translate(flowiseI18nKeys.detail.configuration);
  }

  if (kind === 'exportTemplate') {
    return translate(flowiseI18nKeys.detail.templateExport);
  }

  if (kind === 'messages') {
    return translate(flowiseI18nKeys.detail.messages);
  }

  if (kind === 'share') {
    return translate(flowiseI18nKeys.actions.share);
  }

  if (kind === 'upsert') {
    return translate(flowiseI18nKeys.actions.upsert);
  }

  if (kind === 'upsertHistory') {
    return translate(flowiseI18nKeys.detail.upsertHistory);
  }

  return translate(flowiseI18nKeys.detail.leads);
}

interface ApiCodeModel {
  endpoint: string;
  inputType: string;
  snippets: Record<string, string>;
  tabs: string[];
}

function resolveApiCodeModel(flowData: string, resourceId: string, apiEndpoint: string, webhookEndpoint: string): ApiCodeModel {
  const startInputs = resolveStartInputs(flowData);
  const inputType = String(startInputs.startInputType ?? 'chatInput');
  if (inputType === 'webhookTrigger') {
    const method = String(startInputs.webhookMethod ?? 'POST').toUpperCase();
    const contentType = String(startInputs.webhookContentType ?? 'application/json');
    const body = buildWebhookSampleBody(startInputs);
    return {
      endpoint: webhookEndpoint,
      inputType,
      snippets: {
        Python: buildPythonSnippet(webhookEndpoint, method, contentType, body),
        JavaScript: buildJavascriptSnippet(webhookEndpoint, method, contentType, body),
        cURL: buildCurlSnippet(webhookEndpoint, method, contentType, body)
      },
      tabs: ['Python', 'JavaScript', 'cURL']
    };
  }

  if (inputType === 'scheduleInput') {
    const scheduleSnippet = '# This workflow is triggered by its schedule. Change the Start node input type to call it from API.';
    return {
      endpoint: 'Schedule trigger',
      inputType,
      snippets: {
        Python: scheduleSnippet,
        JavaScript: '// This workflow is triggered by its schedule.\n// Change the Start node input type to call it from API.',
        cURL: scheduleSnippet
      },
      tabs: ['Python', 'JavaScript', 'cURL']
    };
  }

  const body = inputType === 'formInput'
    ? {
        resourceId,
        form: buildFormInputSample(startInputs)
      }
    : {
        resourceId,
        question: 'Hello'
      };
  return {
    endpoint: apiEndpoint,
    inputType,
    snippets: {
      Embed: buildEmbedSnippet(resourceId),
      Python: buildPythonSnippet(apiEndpoint, 'POST', 'application/json', body),
      JavaScript: buildJavascriptSnippet(apiEndpoint, 'POST', 'application/json', body),
      cURL: buildCurlSnippet(apiEndpoint, 'POST', 'application/json', body),
      'Share Chatbot': `${window.location.origin}/flowise/chatbot/${resourceId}`
    },
    tabs: ['Embed', 'Python', 'JavaScript', 'cURL', 'Share Chatbot']
  };
}

function resolveStartInputs(flowData: string): Record<string, unknown> {
  const parsed = parseFlowData(flowData);
  const startNode = parsed.nodes?.find(isStartAgentflowNode);
  const inputs = startNode?.data?.inputs;
  const config = startNode?.data?.config;
  return {
    ...(config && typeof config === 'object' ? config : {}),
    ...(inputs && typeof inputs === 'object' ? inputs : {})
  };
}

function buildFormInputSample(startInputs: Record<string, unknown>): Record<string, unknown> {
  const formInputTypes = Array.isArray(startInputs.formInputTypes) ? startInputs.formInputTypes : [];
  const entries = formInputTypes
    .map((input) => {
      if (!input || typeof input !== 'object') {
        return null;
      }

      const record = input as Record<string, unknown>;
      const name = typeof record.name === 'string' ? record.name.trim() : '';
      if (!name) {
        return null;
      }

      return [name, resolveFormInputExampleValue(record)] as const;
    })
    .filter((entry): entry is readonly [string, unknown] => Boolean(entry));
  return Object.fromEntries(entries.length > 0 ? entries : [['modelCode', 'runtime.menu']]);
}

function resolveFormInputExampleValue(input: Record<string, unknown>): unknown {
  const type = String(input.type ?? 'string').toLowerCase();
  if (type === 'number') {
    return 1;
  }

  if (type === 'boolean' || type === 'checkbox') {
    return true;
  }

  if (type === 'options') {
    const addOptions = Array.isArray(input.addOptions) ? input.addOptions : [];
    const firstAddOption = addOptions.find((option) => option && typeof option === 'object') as Record<string, unknown> | undefined;
    if (typeof firstAddOption?.option === 'string' && firstAddOption.option.trim()) {
      return firstAddOption.option.trim();
    }

    const options = Array.isArray(input.options) ? input.options : [];
    const firstOption = options.find((option) => option && typeof option === 'object') as Record<string, unknown> | undefined;
    return firstString(firstOption ?? {}, ['value', 'name', 'label']) ?? 'option';
  }

  return input.name === 'modelCode' ? 'runtime.menu' : `sample ${String(input.label ?? input.name ?? 'value')}`;
}

function buildWebhookSampleBody(startInputs: Record<string, unknown>): Record<string, unknown> {
  const bodyParams = Array.isArray(startInputs.webhookBodyParams) ? startInputs.webhookBodyParams : [];
  const entries: Array<readonly [string, string]> = [];
  for (const param of bodyParams) {
    if (!param || typeof param !== 'object') {
      continue;
    }

    const record = param as Record<string, unknown>;
    const name = firstString(record, ['name', 'key', 'field']);
    if (name) {
      entries.push([name, 'sample value']);
    }
  }

  const body = Object.fromEntries(entries);
  return Object.keys(body).length > 0 ? body : { question: 'Hello' };
}

function buildJavascriptSnippet(endpoint: string, method: string, contentType: string, body: unknown): string {
  return [
    `const response = await fetch('${endpoint}', {`,
    `  method: '${method}',`,
    `  headers: { 'Content-Type': '${contentType}' },`,
    '  body: JSON.stringify(' + JSON.stringify(body, null, 2).replace(/\n/g, '\n  ') + ')',
    '});',
    'const result = await response.json();'
  ].join('\n');
}

function buildPythonSnippet(endpoint: string, method: string, contentType: string, body: unknown): string {
  const methodName = method.toLowerCase();
  const bodyJson = JSON.stringify(body, null, 4).replace(/\n/g, '\n    ');
  return [
    'import requests',
    '',
    `API_URL = "${endpoint}"`,
    `headers = {"Content-Type": "${contentType}"}`,
    '',
    'def query(payload):',
    `    response = requests.${methodName}(API_URL, headers=headers, json=payload)`,
    '    return response.json()',
    '',
    `output = query(${bodyJson})`,
    'print(output)'
  ].join('\n');
}

function buildCurlSnippet(endpoint: string, method: string, contentType: string, body: unknown): string {
  return [
    `curl ${endpoint} \\`,
    `  -X ${method} \\`,
    `  -H "Content-Type: ${contentType}" \\`,
    `  -d '${JSON.stringify(body)}'`
  ].join('\n');
}

function buildEmbedSnippet(resourceId: string): string {
  return [
    '<flowise-fullchatbot></flowise-fullchatbot>',
    '<script type="module">',
    '  import Chatbot from "/flowise/web.js";',
    '  Chatbot.initFull({',
    `    chatflowid: "${resourceId}",`,
    `    apiHost: "${window.location.origin}",`,
    '  });',
    '</script>'
  ].join('\n');
}

function resolveScheduleInfo(flowData: string): { description: string; enabled: boolean; type: string } {
  const parsed = parseFlowData(flowData);
  const startNode = parsed.nodes?.find(isStartAgentflowNode);
  const inputs = startNode?.data?.inputs ?? {};
  const config = startNode?.data?.config ?? {};
  const type = String(inputs.startInputType ?? config.startInputType ?? 'manual');
  return {
    description: type,
    enabled: type === 'scheduleInput',
    type
  };
}

function isStartAgentflowNode(value: unknown): value is { data?: { config?: Record<string, unknown>; inputs?: Record<string, unknown>; name?: string; nodeType?: string; type?: string } } {
  if (!value || typeof value !== 'object' || !('data' in value)) {
    return false;
  }

  const data = (value as { data?: { name?: string; nodeType?: string; type?: string } }).data;
  return [data?.name, data?.nodeType, data?.type].some((item) => String(item ?? '').toLowerCase() === 'startagentflow');
}

function parseFlowData(flowData: string): { nodes?: unknown[]; edges?: unknown[]; viewport?: unknown } {
  try {
    const parsed = JSON.parse(flowData) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as { nodes?: unknown[]; edges?: unknown[]; viewport?: unknown }) : {};
  } catch {
    return {};
  }
}

function resolveUpsertTarget(flowData: string): FlowiseUpsertTarget | null {
  const parsed = parseFlowData(flowData);
  const nodes = Array.isArray(parsed.nodes) ? parsed.nodes : [];
  for (const node of nodes) {
    const target = resolveNodeUpsertTarget(node);
    if (target) {
      return target;
    }
  }

  return null;
}

function resolveNodeUpsertTarget(value: unknown): FlowiseUpsertTarget | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const record = value as Record<string, unknown>;
  const data = typeof record.data === 'object' && record.data ? (record.data as Record<string, unknown>) : {};
  const inputs = typeof data.inputs === 'object' && data.inputs ? (data.inputs as Record<string, unknown>) : {};
  const label = String(data.label ?? data.name ?? record.id ?? '');
  const storeId = firstString(inputs, ['storeId', 'documentStoreId', 'documentStore', 'selectedStoreId'])
    ?? firstString(data, ['storeId', 'documentStoreId']);
  if (!storeId) {
    return null;
  }

  const haystack = [data.name, data.type, data.nodeType, data.category, data.label].filter(Boolean).join(' ').toLowerCase();
  if (!haystack.includes('document') && !haystack.includes('vector') && !haystack.includes('store')) {
    return null;
  }

  return {
    label,
    loaderId: firstString(inputs, ['loaderId', 'documentId', 'fileId']),
    nodeId: String(record.id ?? ''),
    storeId
  };
}

function firstString(record: Record<string, unknown>, keys: string[]): string | null {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
  }

  return null;
}

function formatDateTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

const speechToTextProviders = ['openAIWhisper', 'assemblyAiTranscribe', 'localAISTT', 'azureCognitive', 'groqWhisper'];
const textToSpeechProviders = ['openai', 'elevenlabs'];
const leadFormTitle = `Hey, thanks for your interest!
Let us know where we can reach you`;
const leadSuccessMessage = `Thank you!
What can I do for you?`;
const defaultFollowUpPrompt =
  'Given the following conversations: {history}. Please help me predict the three most likely questions that human would ask and keeping each question short and concise.';
const availableUploadFileTypes = [
  { ext: 'text/css', extension: '.css', name: 'CSS' },
  { ext: 'text/csv', extension: '.csv', name: 'CSV' },
  { ext: 'text/html', extension: '.html', name: 'HTML' },
  { ext: 'application/json', extension: '.json', name: 'JSON' },
  { ext: 'text/markdown', extension: '.md', name: 'Markdown' },
  { ext: 'application/x-yaml', extension: '.yaml', name: 'YAML' },
  { ext: 'application/pdf', extension: '.pdf', name: 'PDF' },
  { ext: 'application/sql', extension: '.sql', name: 'SQL' },
  { ext: 'text/plain', extension: '.txt', name: 'Text File' },
  { ext: 'application/xml', extension: '.xml', name: 'XML' },
  { ext: 'application/msword', extension: '.doc', name: 'DOC' },
  { ext: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', extension: '.docx', name: 'DOCX' },
  { ext: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', extension: '.xlsx', name: 'XLSX' },
  { ext: 'application/vnd.openxmlformats-officedocument.presentationml.presentation', extension: '.pptx', name: 'PPTX' }
];
const followUpPromptProviders = [
  { defaultTemperature: '0.9', label: 'Anthropic Claude', name: 'chatAnthropic' },
  { defaultTemperature: '0.9', label: 'Azure ChatOpenAI', name: 'azureChatOpenAI' },
  { defaultTemperature: '0.9', label: 'Google Gemini', name: 'chatGoogleGenerativeAI' },
  { defaultTemperature: '0.9', label: 'Groq', name: 'groqChat' },
  { defaultTemperature: '0.9', label: 'Mistral AI', name: 'chatMistralAI' },
  { defaultTemperature: '0.9', label: 'OpenAI', name: 'chatOpenAI' },
  { defaultTemperature: '0.7', label: 'Ollama', name: 'ollama' }
];

function parseJsonRecord(value: string): JsonRecord {
  try {
    const parsed = JSON.parse(value || '{}');
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as JsonRecord) : {};
  } catch {
    return {};
  }
}

function stringifyJsonRecord(value: JsonRecord): string {
  return JSON.stringify(value, null, 2);
}

function readRecord(value: unknown): JsonRecord {
  return value && typeof value === 'object' && !Array.isArray(value) ? (value as JsonRecord) : {};
}

function readRateLimit(value: unknown): RateLimitConfig {
  const record = value && typeof value === 'object' && !Array.isArray(value) ? (value as JsonRecord) : {};
  return {
    limitDuration: readString(record.limitDuration),
    limitMax: readString(record.limitMax),
    limitMsg: readString(record.limitMsg),
    status: record.status === true
  };
}

function normalizeRateLimit(value: RateLimitConfig): JsonRecord {
  if (!value.status) {
    return { status: false };
  }

  const next: JsonRecord = { status: true };
  if (value.limitMax.trim()) {
    next.limitMax = value.limitMax.trim();
  }
  if (value.limitDuration.trim()) {
    next.limitDuration = value.limitDuration.trim();
  }
  if (value.limitMsg.trim()) {
    next.limitMsg = value.limitMsg.trim();
  }
  return next;
}

function readOverrideConfig(value: unknown): OverrideConfigDraft {
  const record = value && typeof value === 'object' && !Array.isArray(value) ? (value as JsonRecord) : {};
  return {
    nodesJson: JSON.stringify(record.nodes ?? {}, null, 2),
    status: record.status === true,
    variablesJson: JSON.stringify(record.variables ?? [], null, 2)
  };
}

function normalizeOverrideConfig(value: OverrideConfigDraft): JsonRecord {
  if (!value.status) {
    return { status: false };
  }

  return {
    nodes: parseJsonValue(value.nodesJson, {}),
    status: true,
    variables: parseJsonValue(value.variablesJson, [])
  };
}

function readLeadsConfig(value: unknown): LeadsConfigDraft {
  const record = readRecord(value);
  return {
    email: record.email === true,
    name: record.name === true,
    phone: record.phone === true,
    status: record.status === true,
    successMessage: readString(record.successMessage),
    title: readString(record.title)
  };
}

function normalizeLeadsConfig(value: LeadsConfigDraft): JsonRecord {
  return {
    email: value.email,
    name: value.name,
    phone: value.phone,
    status: value.status,
    successMessage: value.successMessage,
    title: value.title
  };
}

function readFileUploadConfig(value: unknown): FileUploadConfigDraft {
  const record = readRecord(value);
  return {
    allowedUploadFileTypes: csvToStringArray(readString(record.allowedUploadFileTypes), availableUploadFileTypes.map((item) => item.ext)),
    pdfUsage: readRecord(record.pdfFile).usage === 'perFile' ? 'perFile' : 'perPage',
    status: record.status === true
  };
}

function normalizeFileUploadConfig(value: FileUploadConfigDraft): JsonRecord {
  return {
    allowedUploadFileTypes: value.allowedUploadFileTypes.join(','),
    pdfFile: {
      usage: value.pdfUsage
    },
    status: value.status
  };
}

function readPostProcessingConfig(value: unknown): PostProcessingConfigDraft {
  const record = readRecord(value);
  return {
    customFunction: parseMaybeJsonString(record.customFunction),
    enabled: record.enabled === true
  };
}

function normalizePostProcessingConfig(value: PostProcessingConfigDraft): JsonRecord {
  return {
    customFunction: JSON.stringify(value.customFunction),
    enabled: value.enabled
  };
}

function readMcpServerConfig(value: unknown): McpServerConfigDraft {
  const record = readRecord(value);
  return {
    description: readString(record.description),
    enabled: record.enabled === true,
    token: readString(record.token),
    toolName: readString(record.toolName)
  };
}

function normalizeMcpServerConfig(value: McpServerConfigDraft): JsonRecord {
  return {
    description: value.description,
    enabled: value.enabled,
    token: value.token,
    toolName: value.toolName
  };
}

function readFollowUpProviderConfig(value: unknown): FollowUpPromptsProviderConfig {
  const record = readRecord(value);
  return {
    baseUrl: readString(record.baseUrl) || 'http://127.0.0.1:11434',
    credentialId: readString(record.credentialId),
    modelName: readString(record.modelName),
    prompt: readString(record.prompt) || defaultFollowUpPrompt,
    temperature: readString(record.temperature) || '0.9'
  };
}

function parseMaybeJsonString(value: unknown): string {
  if (typeof value !== 'string') {
    return '';
  }

  try {
    const parsed = JSON.parse(value);
    return typeof parsed === 'string' ? parsed : value;
  } catch {
    return value;
  }
}

function parseJsonValue(value: string, fallback: unknown): unknown {
  try {
    return JSON.parse(value || JSON.stringify(fallback));
  } catch {
    return fallback;
  }
}

function readStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string').map((item) => item.trim()).filter(Boolean) : [];
}

function csvToStringArray(value: string, fallback: string[]): string[] {
  const values = value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
  return values.length ? values : fallback;
}

function readStarterPromptRows(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map(readPromptValue).filter(Boolean);
  }

  if (value && typeof value === 'object') {
    return Object.values(value as JsonRecord).map(readPromptValue).filter(Boolean);
  }

  return [];
}

function readPromptValue(value: unknown): string {
  if (typeof value === 'string') {
    return value.trim();
  }

  if (value && typeof value === 'object' && typeof (value as { prompt?: unknown }).prompt === 'string') {
    return (value as { prompt: string }).prompt.trim();
  }

  return '';
}

function writeStarterPromptRows(prompts: string[]): JsonRecord {
  return prompts
    .map((prompt) => prompt.trim())
    .filter(Boolean)
    .reduce<JsonRecord>((acc, prompt, index) => {
      acc[index] = { prompt };
      return acc;
    }, {});
}

function replaceAt(items: string[], index: number, value: string): string[] {
  return items.map((item, itemIndex) => (itemIndex === index ? value : item));
}

function removeAt(items: string[], index: number): string[] {
  return items.filter((_, itemIndex) => itemIndex !== index);
}

function resolveEnabledProvider(config: JsonRecord, providers: string[]): string {
  return providers.find((provider) => readProviderConfig(config[provider]).status === true) ?? 'none';
}

function readProviderConfig(value: unknown): JsonRecord {
  return value && typeof value === 'object' && !Array.isArray(value) ? (value as JsonRecord) : {};
}

function disableProviders(config: JsonRecord, providers: string[]): JsonRecord {
  return providers.reduce<JsonRecord>((acc, provider) => {
    const existing = readProviderConfig(config[provider]);
    acc[provider] = { ...existing, status: false };
    return acc;
  }, {});
}

function readString(value: unknown): string {
  return typeof value === 'string' ? value : value == null ? '' : String(value);
}

function createConfigurationDraft(chatflow: FlowiseChatflowDto | null, flowType: string, title: string): FlowiseConfigurationDraft {
  return {
    analytic: chatflow?.analytic ?? '{}',
    apiConfig: chatflow?.apiConfig ?? '{}',
    apikeyid: chatflow?.apikeyid ?? '',
    category: chatflow?.category ?? '',
    chatbotConfig: chatflow?.chatbotConfig ?? '{}',
    deployed: chatflow?.deployed ?? false,
    followUpPrompts: chatflow?.followUpPrompts ?? '{}',
    isPublic: chatflow?.isPublic ?? false,
    mcpServerConfig: chatflow?.mcpServerConfig ?? '{}',
    name: chatflow?.name ?? (title || flowType),
    speechToText: chatflow?.speechToText ?? '{}',
    textToSpeech: chatflow?.textToSpeech ?? '{}',
    webhookSecret: '',
    workspaceId: chatflow?.workspaceId ?? ''
  };
}

function normalizeOptional(value: string): string | null {
  const normalized = value.trim();
  return normalized ? normalized : null;
}

function downloadJson(fileName: string, payload: unknown): void {
  const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
