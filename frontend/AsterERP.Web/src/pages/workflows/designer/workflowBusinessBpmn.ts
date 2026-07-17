import type { WorkflowBusinessDesign, WorkflowBusinessEdge, WorkflowBusinessNode } from './workflowBusinessModel';

const BPMN_NS = 'http://www.omg.org/spec/BPMN/20100524/MODEL';
const BPMNDI_NS = 'http://www.omg.org/spec/BPMN/20100524/DI';
const DC_NS = 'http://www.omg.org/spec/DD/20100524/DC';
const DI_NS = 'http://www.omg.org/spec/DD/20100524/DI';
const ACTIVITI_NS = 'http://activiti.org/bpmn';
const ASTER_NS = 'https://astererp.local/bpmn/extensions';

const nodeSize: Record<WorkflowBusinessNode['type'], { height: number; width: number }> = {
  approval: { height: 74, width: 140 },
  cc: { height: 68, width: 128 },
  condition: { height: 72, width: 72 },
  end: { height: 42, width: 42 },
  start: { height: 42, width: 42 },
  subprocess: { height: 76, width: 150 },
  timeout: { height: 52, width: 52 }
};

export function generateBpmnFromBusinessDesign(design: WorkflowBusinessDesign, processId: string, processName: string): string {
  const normalizedProcessId = normalizeId(processId || 'workflow_process');
  const nodes = design.nodes;
  const edges = design.edges.filter((edge) => nodes.some((node) => node.id === edge.source) && nodes.some((node) => node.id === edge.target));
  const nodeById = new Map(nodes.map((node) => [node.id, node]));
  const semanticNodes = nodes.map((node) => renderSemanticNode(node)).join('\n');
  const semanticEdges = edges.map((edge) => renderSequenceFlow(edge, nodeById.get(edge.source))).join('\n');
  const diagramShapes = nodes.map(renderDiagramShape).join('\n');
  const diagramEdges = edges.map((edge) => renderDiagramEdge(edge, nodeById)).join('\n');

  return `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="${BPMN_NS}" xmlns:bpmndi="${BPMNDI_NS}" xmlns:dc="${DC_NS}" xmlns:di="${DI_NS}" xmlns:activiti="${ACTIVITI_NS}" xmlns:astererp="${ASTER_NS}" id="${normalizedProcessId}_definitions" targetNamespace="https://astererp.local/workflows">
  <bpmn:process id="${normalizedProcessId}" name="${escapeXml(processName || processId)}" isExecutable="true">
${semanticNodes}
${semanticEdges}
  </bpmn:process>
  <bpmndi:BPMNDiagram id="${normalizedProcessId}_diagram">
    <bpmndi:BPMNPlane id="${normalizedProcessId}_plane" bpmnElement="${normalizedProcessId}">
${diagramShapes}
${diagramEdges}
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>`;
}

function renderSemanticNode(node: WorkflowBusinessNode): string {
  const incoming = '';
  const baseAttributes = `id="${escapeXml(node.id)}" name="${escapeXml(node.label)}" astererp:businessManaged="true"`;
  const extensionElements = renderNodeExtensionElements(node);
  if (node.type === 'start') {
    return extensionElements
      ? `    <bpmn:startEvent ${baseAttributes}>${incoming}
${extensionElements}
    </bpmn:startEvent>`
      : `    <bpmn:startEvent ${baseAttributes}>${incoming}</bpmn:startEvent>`;
  }

  if (node.type === 'end') {
    return extensionElements
      ? `    <bpmn:endEvent ${baseAttributes}>${incoming}
${extensionElements}
    </bpmn:endEvent>`
      : `    <bpmn:endEvent ${baseAttributes}>${incoming}</bpmn:endEvent>`;
  }

  if (node.type === 'condition') {
    return extensionElements
      ? `    <bpmn:exclusiveGateway ${baseAttributes}>
${extensionElements}
    </bpmn:exclusiveGateway>`
      : `    <bpmn:exclusiveGateway ${baseAttributes} />`;
  }

  if (node.type === 'subprocess') {
    return `    <bpmn:callActivity ${baseAttributes} calledElement="${escapeXml(node.subProcessConfig.calledElement || node.subprocessKey || node.id)}">
${extensionElements}
    </bpmn:callActivity>`;
  }

  if (node.type === 'timeout') {
    return `    <bpmn:intermediateCatchEvent ${baseAttributes}>
${extensionElements}
      <bpmn:timerEventDefinition>
        <bpmn:timeDuration>PT${Math.max(1, node.timeoutPolicy.hours || node.timeoutHours || 1)}H</bpmn:timeDuration>
      </bpmn:timerEventDefinition>
    </bpmn:intermediateCatchEvent>`;
  }

  if (node.type === 'cc') {
    return `    <bpmn:userTask ${baseAttributes}${renderParticipantAttributes(node)} activiti:category="cc">
${extensionElements}
    </bpmn:userTask>`;
  }

  const participantAttributes = renderParticipantAttributes(node);
  const multiInstance = renderMultiInstanceLoopCharacteristics(node);
  return `    <bpmn:userTask ${baseAttributes}${participantAttributes}>
${extensionElements}
${multiInstance}
    </bpmn:userTask>`;
}

function renderParticipantAttributes(node: WorkflowBusinessNode): string {
  if (node.participantType === 'deptManager') {
    return ' activiti:candidateUsers="${starterDeptManagerUserIds}"';
  }

  const participantExpression = resolveParticipantExpression(node);
  if (participantExpression) {
    return ` activiti:assignee="${escapeXml(participantExpression)}"`;
  }

  if (node.participantIds.length > 1 && (node.approvalMode === 'all' || node.approvalMode === 'any')) {
    return ' activiti:assignee="${approver}"';
  }

  if (node.participantType === 'user' && node.participantId) {
    return ` activiti:assignee="${escapeXml(node.participantId)}"`;
  }

  if (node.groupKey) {
    return ` activiti:candidateGroups="${escapeXml(node.groupKey)}"`;
  }

  if (node.participantType === 'role' && node.participantId) {
    return ` activiti:candidateGroups="role:${escapeXml(node.participantId)}"`;
  }

  if (node.participantType === 'department' && node.participantId) {
    return ` activiti:candidateGroups="dept:${escapeXml(node.participantId)}"`;
  }

  if (node.participantType === 'position' && node.participantId) {
    return ` activiti:candidateGroups="position:${escapeXml(node.participantId)}"`;
  }

  if (node.participantCode) {
    return ` activiti:candidateUsers="${escapeXml(node.participantCode)}"`;
  }

  if (node.participantId) {
    return ` activiti:candidateUsers="${escapeXml(node.participantId)}"`;
  }

  return '';
}

function resolveParticipantExpression(node: WorkflowBusinessNode): string {
  if (node.participantType === 'starter') {
    return '${starterUserId}';
  }
  if (node.participantType === 'starterManager' || node.participantType === 'manager') {
    return '${starterManagerUserId}';
  }
  if (node.participantType === 'previousApprover') {
    return '${previousApproverUserId}';
  }
  if (node.participantType === 'formField') {
    return node.participantFieldKey ? `\${${node.participantFieldKey}}` : '';
  }
  if (node.participantType === 'dynamic') {
    return node.participantExpression;
  }

  return '';
}

function renderMultiInstanceLoopCharacteristics(node: WorkflowBusinessNode): string {
  if (node.participantIds.length <= 1 || (node.approvalMode !== 'all' && node.approvalMode !== 'any')) {
    return '';
  }

  const collectionVariable = `${normalizeId(node.id)}_approvers`;
  const completionCondition = node.approvalMode === 'any'
    ? '<![CDATA[${nrOfCompletedInstances > 0}]]>'
    : '<![CDATA[${nrOfCompletedInstances == nrOfInstances}]]>';
  return `      <bpmn:multiInstanceLoopCharacteristics activiti:collectionVariable="${collectionVariable}" activiti:elementVariable="approver">
        <bpmn:completionCondition>${completionCondition}</bpmn:completionCondition>
      </bpmn:multiInstanceLoopCharacteristics>`;
}

function renderSequenceFlow(edge: WorkflowBusinessEdge, source?: WorkflowBusinessNode): string {
  const condition = edge.conditionExpression || (source?.type === 'condition' ? source.conditionExpression || renderConditionExpression(source) : '');
  if (!condition) {
    return `    <bpmn:sequenceFlow id="${escapeXml(edge.id)}" sourceRef="${escapeXml(edge.source)}" targetRef="${escapeXml(edge.target)}" />`;
  }

  return `    <bpmn:sequenceFlow id="${escapeXml(edge.id)}" sourceRef="${escapeXml(edge.source)}" targetRef="${escapeXml(edge.target)}">
      <bpmn:conditionExpression xsi:type="bpmn:tFormalExpression" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">${escapeXml(condition)}</bpmn:conditionExpression>
    </bpmn:sequenceFlow>`;
}

function renderDiagramShape(node: WorkflowBusinessNode): string {
  const size = nodeSize[node.type];
  return `      <bpmndi:BPMNShape id="${escapeXml(node.id)}_di" bpmnElement="${escapeXml(node.id)}">
        <dc:Bounds x="${Math.round(node.position.x)}" y="${Math.round(node.position.y)}" width="${size.width}" height="${size.height}" />
      </bpmndi:BPMNShape>`;
}

function renderDiagramEdge(edge: WorkflowBusinessEdge, nodeById: Map<string, WorkflowBusinessNode>): string {
  const source = nodeById.get(edge.source);
  const target = nodeById.get(edge.target);
  if (!source || !target) {
    return '';
  }

  const sourceSize = nodeSize[source.type];
  const targetSize = nodeSize[target.type];
  const x1 = Math.round(source.position.x + sourceSize.width);
  const y1 = Math.round(source.position.y + sourceSize.height / 2);
  const x2 = Math.round(target.position.x);
  const y2 = Math.round(target.position.y + targetSize.height / 2);

  return `      <bpmndi:BPMNEdge id="${escapeXml(edge.id)}_di" bpmnElement="${escapeXml(edge.id)}">
        <di:waypoint x="${x1}" y="${y1}" />
        <di:waypoint x="${x2}" y="${y2}" />
      </bpmndi:BPMNEdge>`;
}

function normalizeId(value: string): string {
  return value.replace(/[^a-zA-Z0-9_:-]/g, '_');
}

function escapeXml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

function renderNodeExtensionElements(node: WorkflowBusinessNode): string {
  const config = {
    actionPolicies: node.actionPolicies,
    conditionRules: node.conditionRules,
    fieldPermissions: node.fieldPermissions,
    nativeExtensions: parseJsonObject(node.nativeExtensions),
    notificationRules: node.notificationRules,
    participantExpression: node.participantExpression,
    participantFieldKey: node.participantFieldKey,
    participantIds: node.participantIds,
    participantNames: node.participantNames,
    participantCollectionVariable: `${normalizeId(node.id)}_approvers`,
    subProcessConfig: node.subProcessConfig,
    timeoutPolicy: node.timeoutPolicy,
    variableMappings: node.variableMappingRows
  };

  return `      <bpmn:extensionElements>
        <astererp:nodeConfig>${escapeXml(JSON.stringify(config))}</astererp:nodeConfig>
      </bpmn:extensionElements>`;
}

function renderConditionExpression(node: WorkflowBusinessNode): string {
  return node.conditionRules
    .filter((rule) => rule.fieldKey)
    .map((rule, index) => `${index > 0 ? `${rule.logical.toUpperCase()} ` : ''}${renderConditionRule(rule)}`)
    .join(' ');
}

function renderConditionRule(rule: WorkflowBusinessNode['conditionRules'][number]): string {
  const field = `${rule.fieldSource}.${rule.fieldKey}`;
  if (rule.operator === 'empty') {
    return `${field} == null`;
  }
  if (rule.operator === 'notEmpty') {
    return `${field} != null`;
  }
  if (rule.operator === 'range') {
    return `${field} >= ${quoteExpressionValue(rule.value)} && ${field} <= ${quoteExpressionValue(rule.valueEnd ?? '')}`;
  }
  if (rule.operator === 'contains') {
    return `${field}.contains(${quoteExpressionValue(rule.value)})`;
  }

  const operatorMap = {
    eq: '==',
    gt: '>',
    gte: '>=',
    lt: '<',
    lte: '<=',
    ne: '!='
  } as const;
  return `${field} ${operatorMap[rule.operator]} ${quoteExpressionValue(rule.value)}`;
}

function quoteExpressionValue(value: string): string {
  return /^-?\d+(\.\d+)?$/.test(value) ? value : `'${value.replace(/'/g, "\\'")}'`;
}

function parseJsonObject(value: string): unknown {
  if (!value.trim()) {
    return {};
  }

  try {
    return JSON.parse(value);
  } catch {
    return { raw: value };
  }
}
