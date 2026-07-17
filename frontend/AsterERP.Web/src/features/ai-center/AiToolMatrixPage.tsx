import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../shared/feedback/useMessage';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { AiSkCapabilitiesPanel } from './AiSkCapabilitiesPanel';
import type { AiToolBindingDto, AiToolDefinitionDto } from './api/aiCenter.api';
import { aiCapabilityApi } from './api/capability.api';

import './styles/ai-center.css';

export function AiToolMatrixPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [agentProfileId, setAgentProfileId] = useState('');
  const [toolCode, setToolCode] = useState('');
  const [workflowModelId, setWorkflowModelId] = useState('');
  const [workflowToolCode, setWorkflowToolCode] = useState('');
  const [saving, setSaving] = useState(false);

  const definitionsQuery = useApiQuery({
    queryKey: ['ai', 'tool-definitions'],
    queryFn: ({ signal }) => aiCapabilityApi.tools.definitions({ pageIndex: 1, pageSize: 200 }, signal)
  });
  const bindingsQuery = useApiQuery({
    queryKey: ['ai', 'tool-bindings', agentProfileId],
    queryFn: ({ signal }) => aiCapabilityApi.tools.bindings(agentProfileId || null, signal)
  });
  const agentsQuery = useApiQuery({
    queryKey: ['ai', 'agents', 'options'],
    queryFn: ({ signal }) => aiCapabilityApi.agents.options(signal)
  });
  const workflowsQuery = useApiQuery({
    queryKey: ['ai', 'workflow-tools', 'workflows'],
    queryFn: ({ signal }) => aiCapabilityApi.tools.availableWorkflows(signal)
  });

  const definitionRows = definitionsQuery.data?.data.items ?? [];
  const bindingRows = bindingsQuery.data?.data ?? [];
  const selectedWorkflow = workflowsQuery.data?.data.find((item) => item.workflowModelId === workflowModelId);

  const definitionColumns = useMemo<DataTableColumn<AiToolDefinitionDto>[]>(
    () => [
      { key: 'toolCode', responsivePriority: 100, title: translate('ai.toolMatrix.column.toolCode'), width: '220px' },
      { key: 'toolName', responsivePriority: 95, title: translate('ai.toolMatrix.column.toolName'), width: '180px' },
      { key: 'toolDomain', responsivePriority: 85, title: translate('ai.toolMatrix.column.toolDomain'), width: '120px' },
      { key: 'riskLevel', responsivePriority: 80, title: translate('ai.toolMatrix.column.riskLevel'), width: '100px' },
      {
        key: 'requiresConfirmation',
        responsivePriority: 75,
        title: translate('ai.toolMatrix.column.requiresConfirmation'),
        width: '90px',
        render: (row) => (row.requiresConfirmation ? translate('common.yes') : translate('common.no'))
      },
      { key: 'status', responsivePriority: 70, title: translate('ai.toolMatrix.column.status'), width: '110px', render: (row) => renderToolStatusPill(row.status, translate) },
      { key: 'permissionCode', hideBelow: 'xl', responsivePriority: 45, title: translate('ai.toolMatrix.column.permissionCode') }
    ],
    [translate]
  );

  const bindingColumns = useMemo<DataTableColumn<AiToolBindingDto>[]>(
    () => [
      { key: 'agentProfileId', hideBelow: 'lg', responsivePriority: 70, title: translate('ai.toolMatrix.column.agentProfileId'), width: '220px' },
      { key: 'toolCode', responsivePriority: 100, title: translate('ai.toolMatrix.column.toolCode'), width: '220px' },
      { key: 'autoInvokeAllowed', responsivePriority: 80, title: translate('ai.toolMatrix.column.autoInvokeAllowed'), width: '100px', render: (row) => (row.autoInvokeAllowed ? translate('ai.toolMatrix.option.allowed') : translate('ai.toolMatrix.option.forbidden')) },
      { key: 'status', responsivePriority: 75, title: translate('ai.toolMatrix.column.status'), width: '100px', render: (row) => renderToolStatusPill(row.status, translate) }
    ],
    [translate]
  );

  const handleSync = async () => {
    try {
      setSaving(true);
      await aiCapabilityApi.tools.syncDefinitions();
      await queryClient.invalidateQueries({ queryKey: ['ai', 'tool-definitions'] });
      message.success(translate('ai.toolMatrix.success.syncDefinitions'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.toolMatrix.error.syncDefinitions')));
    } finally {
      setSaving(false);
    }
  };

  const handleBindAgent = async () => {
    if (!agentProfileId || !toolCode) {
      message.info(translate('ai.toolMatrix.info.selectAgentAndTool'));
      return;
    }

    try {
      setSaving(true);
      await aiCapabilityApi.tools.upsertBinding({ agentProfileId, autoInvokeAllowed: false, status: 'Enabled', toolCode });
      await queryClient.invalidateQueries({ queryKey: ['ai', 'tool-bindings', agentProfileId] });
      message.success(translate('ai.toolMatrix.success.saveAgentBinding'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.toolMatrix.error.saveAgentBinding')));
    } finally {
      setSaving(false);
    }
  };

  const handleBindWorkflow = async () => {
    if (!selectedWorkflow || !workflowToolCode) {
      message.info(translate('ai.toolMatrix.info.selectWorkflowAndTool'));
      return;
    }

    try {
      setSaving(true);
      await aiCapabilityApi.tools.bindWorkflow({
        requiresConfirmation: true,
        riskLevel: 'high',
        status: 'Enabled',
        toolCode: workflowToolCode,
        workflowCode: selectedWorkflow.workflowCode,
        workflowModelId: selectedWorkflow.workflowModelId,
        workflowName: selectedWorkflow.workflowName
      });
      message.success(translate('ai.toolMatrix.success.saveWorkflowBinding'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.toolMatrix.error.saveWorkflowBinding')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <CrudPage
      actions={
        <PermissionButton className="primary-button" code="ai:tool:edit" disabled={saving} type="button" onClick={() => void handleSync()}>
          {translate('ai.toolMatrix.actions.syncDefinitions')}
        </PermissionButton>
      }
      className="ai-chat-page"
      description={translate('ai.toolMatrix.description')}
      eyebrow={translate('ai.eyebrow')}
      title={translate('ai.toolMatrix.title')}
    >
      <div className="ai-tool-matrix">
        <section className="ai-tool-form-row">
          <label className="ai-field">
            <span>{translate('ai.toolMatrix.field.agentProfileId')}</span>
            <select value={agentProfileId} onChange={(event) => setAgentProfileId(event.target.value)}>
              <option value="">{translate('common.select')}</option>
              {(agentsQuery.data?.data ?? []).map((agent) => (
                <option key={agent.id} value={agent.id}>
                  {agent.agentName}
                </option>
              ))}
            </select>
          </label>
          <label className="ai-field">
            <span>{translate('ai.toolMatrix.field.toolCode')}</span>
            <select value={toolCode} onChange={(event) => setToolCode(event.target.value)}>
              <option value="">{translate('common.select')}</option>
              {definitionRows.map((tool) => (
                <option key={tool.toolCode} value={tool.toolCode}>
                  {tool.toolName}
                </option>
              ))}
            </select>
          </label>
          <PermissionButton className="primary-button" code="ai:tool:edit" disabled={saving} type="button" onClick={() => void handleBindAgent()}>
            {translate('ai.toolMatrix.actions.saveAgentBinding')}
          </PermissionButton>
        </section>
        <section className="ai-tool-form-row">
          <label className="ai-field">
            <span>{translate('ai.toolMatrix.field.workflowModelId')}</span>
            <select value={workflowModelId} onChange={(event) => setWorkflowModelId(event.target.value)}>
              <option value="">{translate('common.select')}</option>
              {(workflowsQuery.data?.data ?? []).map((workflow) => (
                <option key={workflow.workflowModelId} value={workflow.workflowModelId}>
                  {workflow.workflowName}
                </option>
              ))}
            </select>
          </label>
          <label className="ai-field">
            <span>{translate('ai.toolMatrix.field.workflowToolCode')}</span>
            <select value={workflowToolCode} onChange={(event) => setWorkflowToolCode(event.target.value)}>
              <option value="">{translate('common.select')}</option>
              {definitionRows.map((tool) => (
                <option key={tool.toolCode} value={tool.toolCode}>
                  {tool.toolName}
                </option>
              ))}
            </select>
          </label>
          <PermissionButton className="primary-button" code="ai:tool:bind-workflow" disabled={saving} type="button" onClick={() => void handleBindWorkflow()}>
            {translate('ai.toolMatrix.actions.saveWorkflowBinding')}
          </PermissionButton>
        </section>
        <DataTable className="ai-admin-grid" columns={definitionColumns} fitScreen loading={definitionsQuery.isFetching} rowKey={(row) => row.id} rows={definitionRows} />
        <DataTable className="ai-admin-grid" columns={bindingColumns} fitScreen loading={bindingsQuery.isFetching} rowKey={(row) => row.id} rows={bindingRows} />
        <AiSkCapabilitiesPanel />
      </div>
    </CrudPage>
  );
}

function renderToolStatusPill(status: string, translate: (key: string) => string) {
  const enabled = status === 'Enabled';
  return (
    <span className={`ai-status-badge${enabled ? ' ai-status-badge--success' : ''}`}>
      {enabled ? translate('common.enabled') : translate('common.disabled')}
    </span>
  );
}
