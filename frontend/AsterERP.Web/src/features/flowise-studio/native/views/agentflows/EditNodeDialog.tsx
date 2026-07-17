import { Badge, Tab, Tabs, TextField } from '@mui/material';
import { useMemo } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { aiChatApi } from '../../../../ai-center/api/aiCenter.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseCanvasEdge, FlowiseCanvasNode } from '../../../types/canvas.types';
import type { FlowiseNodeInputParam, FlowiseNodeOption } from '../../../types/node.types';

import type { FlowiseVariableOption } from './ConfigInput';
import { ConfigInput } from './ConfigInput';

export type EditNodeDialogTab = 'details' | 'additional' | 'info';

interface EditNodeDialogProps {
  activeTab: EditNodeDialogTab;
  edges?: FlowiseCanvasEdge[];
  node: FlowiseCanvasNode;
  nodes?: FlowiseCanvasNode[];
  onNodeConfigChange: (nodeId: string, name: string, value: unknown) => void;
  onTabChange: (tab: EditNodeDialogTab) => void;
}

export function EditNodeDialog({ activeTab, edges = [], node, nodes = [], onNodeConfigChange, onTabChange }: EditNodeDialogProps) {
  const { translate } = useI18n();
  const modelOptionsQuery = useApiQuery({
    queryKey: ['flowise', 'canvas', 'model-options'],
    queryFn: ({ signal }) => aiChatApi.models.options(signal)
  });
  const modelOptions = useMemo<FlowiseNodeOption[]>(
    () => (modelOptionsQuery.data?.data ?? [])
      .filter((item) => item.isEnabled)
      .map((item) => ({
        description: item.providerName,
        label: `${item.displayName} (${item.modelCode})`,
        value: item.id
      })),
    [modelOptionsQuery.data?.data]
  );
  const inputParams = useMemo(() => withModelOptions(node.data.inputParams ?? [], modelOptions, translate), [modelOptions, node.data.inputParams, translate]);
  const visibleParams = useMemo(
    () => inputParams.filter((param) => param.display !== false && !param.additionalParams),
    [inputParams]
  );
  const additionalParams = useMemo(
    () => inputParams.filter((param) => param.display !== false && param.additionalParams),
    [inputParams]
  );
  const variableOptions = useMemo(() => buildVariableOptions(node, nodes, edges, translate), [edges, node, nodes, translate]);

  return (
    <div className="flowise-inspector-body">
      <h3>{node.data.displayName}</h3>
      <p>{node.data.description ?? node.data.nodeType}</p>
      <Tabs
        className="flowise-inspector-tabs"
        value={activeTab}
        variant="scrollable"
        onChange={(_event, value: EditNodeDialogTab) => onTabChange(value)}
      >
        <Tab label={translate(flowiseI18nKeys.detail.configuration)} value="details" />
        <Tab
          label={
            additionalParams.length > 0 ? (
              <Badge color="primary" badgeContent={additionalParams.length}>
                <span>{translate(flowiseI18nKeys.canvas.additionalParams)}</span>
              </Badge>
            ) : (
              translate(flowiseI18nKeys.canvas.additionalParams)
            )
          }
          value="additional"
        />
        <Tab label={translate(flowiseI18nKeys.canvas.nodeInfo)} value="info" />
      </Tabs>
      {activeTab === 'details' ? (
        <>
          {visibleParams.map((param) => (
            <ConfigInput
              key={param.name}
              param={param}
              variableOptions={variableOptions}
              value={node.data.inputs?.[param.name] ?? node.data.config?.[param.name]}
              onChange={(name, value) => onNodeConfigChange(node.id, name, value)}
            />
          ))}
          {visibleParams.length === 0 ? (
            <label className="flowise-config-row">
              <span>{translate(flowiseI18nKeys.fields.configJson)}</span>
              <TextField
                fullWidth
                multiline
                minRows={8}
                size="small"
                value={JSON.stringify(node.data.config ?? {}, null, 2)}
                slotProps={{
                  input: {
                    readOnly: true
                  }
                }}
              />
            </label>
          ) : null}
        </>
      ) : null}
      {activeTab === 'additional' ? (
        <>
          {additionalParams.map((param) => (
            <ConfigInput
              key={param.name}
              param={param}
              variableOptions={variableOptions}
              value={node.data.inputs?.[param.name] ?? node.data.config?.[param.name]}
              onChange={(name, value) => onNodeConfigChange(node.id, name, value)}
            />
          ))}
          {additionalParams.length === 0 ? <p>{translate(flowiseI18nKeys.messages.noAdditionalParams)}</p> : null}
        </>
      ) : null}
      {activeTab === 'info' ? (
        <div className="flowise-node-info">
          <dl>
            <dt>{translate(flowiseI18nKeys.fields.type)}</dt>
            <dd>{node.data.nodeType}</dd>
            <dt>{translate(flowiseI18nKeys.fields.category)}</dt>
            <dd>{node.data.category ?? '-'}</dd>
            <dt>{translate(flowiseI18nKeys.canvas.input)}</dt>
            <dd>{node.data.inputAnchors?.length ?? 0}</dd>
            <dt>{translate(flowiseI18nKeys.canvas.output)}</dt>
            <dd>{node.data.outputAnchors?.length ?? 0}</dd>
          </dl>
          <pre>{JSON.stringify(node.data.nodeDefinition ?? node.data, null, 2)}</pre>
        </div>
      ) : null}
    </div>
  );
}

function withModelOptions(
  params: FlowiseNodeInputParam[],
  modelOptions: FlowiseNodeOption[],
  translate: (key: string) => string
): FlowiseNodeInputParam[] {
  if (modelOptions.length === 0) {
    return params;
  }

  return params.map((param) => {
    if (param.name !== 'llmModel' && param.name !== 'agentModel') {
      return param;
    }

    return {
      ...param,
      options: modelOptions,
      placeholder: param.placeholder ?? translate(flowiseI18nKeys.native.agentflows.configInput.selectModel),
      type: 'options'
    };
  });
}

function buildVariableOptions(
  currentNode: FlowiseCanvasNode,
  nodes: FlowiseCanvasNode[],
  edges: FlowiseCanvasEdge[],
  translate: (key: string) => string
): FlowiseVariableOption[] {
  const nodeById = new Map(nodes.map((item) => [item.id, item]));
  const upstreamIds = collectUpstreamNodeIds(currentNode.id, edges);
  const candidateNodes = upstreamIds.length > 0
    ? upstreamIds.map((id) => nodeById.get(id)).filter((item): item is FlowiseCanvasNode => Boolean(item))
    : nodes.filter((item) => item.id !== currentNode.id);
  const upstreamOptions = candidateNodes.flatMap((item) => buildNodeVariableOptions(item, translate));

  return upstreamOptions.length > 0
    ? upstreamOptions
    : nodes.filter((item) => item.id !== currentNode.id).flatMap((item) => buildNodeVariableOptions(item, translate));
}

function collectUpstreamNodeIds(nodeId: string, edges: FlowiseCanvasEdge[]): string[] {
  const upstreamIds: string[] = [];
  const visited = new Set<string>();
  const visit = (targetId: string): void => {
    edges.filter((edge) => edge.target === targetId).forEach((edge) => {
      if (visited.has(edge.source)) {
        return;
      }

      visited.add(edge.source);
      visit(edge.source);
      upstreamIds.push(edge.source);
    });
  };

  visit(nodeId);
  return upstreamIds;
}

function buildNodeVariableOptions(node: FlowiseCanvasNode, translate: (key: string) => string): FlowiseVariableOption[] {
  const label = node.data.displayName || node.id;
  const reference = (suffix: string): string => `{{$${node.id}.output.${suffix}}}`;
  const nodeType = node.data.nodeType;
  const suffix = (key: keyof typeof flowiseI18nKeys.native.agentflows.variableSuffix): string =>
    translate(flowiseI18nKeys.native.agentflows.variableSuffix[key]);

  if (nodeType === 'runtime-data-model') {
    return [
      { label: `${label} / ${suffix('content')}`, value: reference('content') },
      { label: `${label} / ${suffix('total')}`, value: reference('total') },
      { label: `${label} / ${suffix('rowCount')}`, value: reference('rowCount') },
      { label: `${label} / ${suffix('rows')}`, value: reference('rows') }
    ];
  }

  if (nodeType === 'httpAgentflow') {
    return [
      { label: `${label} / ${suffix('responseData')}`, value: reference('http.data') },
      { label: `${label} / ${suffix('statusCode')}`, value: reference('http.status') },
      { label: `${label} / ${suffix('statusText')}`, value: reference('http.statusText') },
      { label: `${label} / ${suffix('responseHeaders')}`, value: reference('http.headers') }
    ];
  }

  if (nodeType === 'executeFlowAgentflow') {
    return [
      { label: `${label} / ${suffix('content')}`, value: reference('content') },
      { label: `${label} / ${suffix('status')}`, value: reference('status') },
      { label: `${label} / ${suffix('sourceDocuments')}`, value: reference('sourceDocuments') },
      { label: `${label} / ${suffix('usedTools')}`, value: reference('usedTools') }
    ];
  }

  if (nodeType === 'customFunctionAgentflow') {
    return [
      { label: `${label} / ${suffix('content')}`, value: reference('content') },
      { label: `${label} / ${suffix('inputVariables')}`, value: reference('inputVariables') }
    ];
  }

  if (nodeType === 'llmAgentflow') {
    return [
      { label: `${label} / ${suffix('content')}`, value: reference('content') },
      { label: `${label} / ${suffix('returnResponseAs')}`, value: reference('returnResponseAs') }
    ];
  }

  if (nodeType === 'agentAgentflow') {
    return [
      { label: `${label} / ${suffix('content')}`, value: reference('content') },
      { label: `${label} / ${suffix('usedTools')}`, value: reference('usedTools') },
      { label: `${label} / ${suffix('sourceDocuments')}`, value: reference('sourceDocuments') },
      { label: `${label} / ${suffix('returnResponseAs')}`, value: reference('returnResponseAs') }
    ];
  }

  return [];
}
