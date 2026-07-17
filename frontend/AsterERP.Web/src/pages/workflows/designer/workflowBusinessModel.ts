import type { XYPosition } from '@xyflow/react';

import type { WorkflowParticipantDto } from '../../../api/workflow/workflows.api';

import type { TranslateFn } from './workflowBusinessI18n';

export type BusinessNodeType = 'start' | 'approval' | 'condition' | 'cc' | 'timeout' | 'subprocess' | 'end';
export type ParticipantType =
  | 'user'
  | 'role'
  | 'department'
  | 'position'
  | 'starter'
  | 'starterManager'
  | 'manager'
  | 'deptManager'
  | 'previousApprover'
  | 'formField'
  | 'dynamic';
export type ApprovalMode = 'all' | 'any';
export type ConditionFieldSource = 'form' | 'process' | 'currentUser' | 'department' | 'role' | 'position' | 'dict';
export type ConditionOperator = 'eq' | 'ne' | 'gt' | 'gte' | 'lt' | 'lte' | 'contains' | 'empty' | 'notEmpty' | 'range';
export type WorkflowActionKey = 'complete' | 'reject' | 'return' | 'transfer' | 'delegate' | 'add-sign' | 'remove-sign' | 'withdraw' | 'terminate' | 'resubmit';
export type AttachmentPolicy = 'none' | 'optional' | 'required';
export type NotificationTrigger = 'process-start' | 'node-enter' | 'task-complete' | 'timeout' | 'process-end';

export interface WorkflowConditionRule {
  id: string;
  fieldSource: ConditionFieldSource;
  fieldKey: string;
  fieldLabel: string;
  operator: ConditionOperator;
  value: string;
  valueEnd?: string;
  logical: 'and' | 'or';
}

export interface WorkflowFieldPermission {
  id: string;
  fieldKey: string;
  fieldLabel: string;
  subjectType: ParticipantType | 'approver';
  subjectId: string;
  subjectName: string;
  visible: boolean;
  readonly: boolean;
  required: boolean;
  hidden: boolean;
}

export interface WorkflowActionPolicy {
  action: WorkflowActionKey;
  label: string;
  color: string;
  permissionCode: string;
  commentRequired: boolean;
  attachmentPolicy: AttachmentPolicy;
  nextStatus: string;
  callbackCode: string;
  enabled: boolean;
}

export interface WorkflowTimeoutPolicy {
  enabled: boolean;
  hours: number;
  action: string;
  escalationType: ParticipantType;
  escalationTargetId: string;
  escalationTargetName: string;
  notificationTemplateCode: string;
}

export interface WorkflowNotificationRule {
  id: string;
  trigger: NotificationTrigger;
  receiverType: ParticipantType | 'approver';
  receiverValue: string;
  receiverName: string;
  channelCodes: string[];
  templateCode: string;
  conditionSummary: string;
  failurePolicy: 'ignore' | 'block';
  enabled: boolean;
}

export interface WorkflowVariableMapping {
  id: string;
  sourcePath: string;
  targetPath: string;
  direction: 'input' | 'output' | 'both';
  valueType: string;
}

export interface WorkflowSubProcessConfig {
  calledElement: string;
  businessKeyExpression: string;
  inheritVariables: boolean;
}

export interface WorkflowBusinessNode {
  id: string;
  type: BusinessNodeType;
  label: string;
  position: XYPosition;
  participantType: ParticipantType;
  participantId: string;
  participantName: string;
  participantCode: string;
  participantIds: string[];
  participantNames: string[];
  participantExpression: string;
  participantFieldKey: string;
  groupKey?: string | null;
  approvalMode: ApprovalMode;
  conditionExpression: string;
  ccTargets: string;
  timeoutHours: number;
  timeoutAction: string;
  subprocessKey: string;
  actions: string[];
  variableMappings: string;
  conditionRules: WorkflowConditionRule[];
  fieldPermissions: WorkflowFieldPermission[];
  actionPolicies: WorkflowActionPolicy[];
  timeoutPolicy: WorkflowTimeoutPolicy;
  notificationRules: WorkflowNotificationRule[];
  variableMappingRows: WorkflowVariableMapping[];
  subProcessConfig: WorkflowSubProcessConfig;
  nativeExtensions: string;
}

export interface WorkflowBusinessEdge {
  id: string;
  source: string;
  target: string;
  label?: string;
  conditionExpression?: string;
}

export interface WorkflowBusinessFormField {
  fieldCode: string;
  fieldName: string;
  dataType: string;
  binding: string;
  visible: boolean;
  queryable: boolean;
  sortable: boolean;
  renderer?: string | null;
  dictType?: string | null;
  order: number;
}

export interface WorkflowBusinessFormContext {
  resourceCode: string;
  resourceName: string;
  menuCode: string;
  businessType: string;
  pageCode: string;
  modelCode: string;
  keyField: string;
  routePath?: string | null;
  fields: WorkflowBusinessFormField[];
}

export interface WorkflowBusinessDesign {
  version: 'latest';
  selectedNodeId: string;
  formContext?: WorkflowBusinessFormContext | null;
  nodes: WorkflowBusinessNode[];
  edges: WorkflowBusinessEdge[];
}

export function createDefaultBusinessDesign(translate: TranslateFn): WorkflowBusinessDesign {
  return {
    version: 'latest',
    selectedNodeId: 'approveTask',
    formContext: null,
    nodes: [
      createBusinessNode('start', 'startEvent', translate('workflowBusiness.node.start'), { x: 40, y: 160 }, translate),
      createBusinessNode('approval', 'approveTask', translate('workflowBusiness.node.approval'), { x: 300, y: 150 }, translate),
      createBusinessNode('end', 'endEvent', translate('workflowBusiness.node.end'), { x: 580, y: 160 }, translate)
    ],
    edges: [
      { id: 'flow_start_approve', source: 'startEvent', target: 'approveTask' },
      { id: 'flow_approve_end', source: 'approveTask', target: 'endEvent' }
    ]
  };
}

export function createBusinessNode(
  type: BusinessNodeType,
  id: string,
  label: string,
  position: XYPosition,
  translate: TranslateFn = (key) => key
): WorkflowBusinessNode {
  return {
    id,
    type,
    label,
    position,
    participantType: type === 'approval' ? 'user' : 'dynamic',
    participantId: '',
    participantName: '',
    participantCode: '',
    participantIds: [],
    participantNames: [],
    participantExpression: '',
    participantFieldKey: '',
    groupKey: null,
    approvalMode: 'all',
    conditionExpression: '',
    ccTargets: '',
    timeoutHours: type === 'timeout' ? 24 : 0,
    timeoutAction: 'notify',
    subprocessKey: '',
    actions: ['complete', 'reject', 'transfer', 'delegate'],
    variableMappings: '{}',
    conditionRules: [],
    fieldPermissions: [createDefaultFieldPermission(translate)],
    actionPolicies: createDefaultActionPolicies(translate),
    timeoutPolicy: {
      enabled: type === 'timeout',
      hours: type === 'timeout' ? 24 : 0,
      action: 'notify',
      escalationType: 'starterManager',
      escalationTargetId: '',
      escalationTargetName: '',
      notificationTemplateCode: 'workflow-node-enter'
    },
    notificationRules: type === 'approval' ? [createDefaultNotificationRule(translate)] : [],
    variableMappingRows: [createDefaultVariableMapping()],
    subProcessConfig: {
      calledElement: '',
      businessKeyExpression: '${businessKey}',
      inheritVariables: true
    },
    nativeExtensions: '{}'
  };
}

export function readBusinessDesign(extensionJson: string | null | undefined, translate: TranslateFn): WorkflowBusinessDesign {
  if (!extensionJson) {
    return createDefaultBusinessDesign(translate);
  }

  try {
    const parsed = JSON.parse(extensionJson) as {
      version?: unknown;
      kind?: unknown;
      businessDesign?: WorkflowBusinessDesign;
    };
    if (parsed.version === 'latest' &&
      parsed.kind === 'WorkflowBusinessModelLatest' &&
      parsed.businessDesign?.version === 'latest' &&
      parsed.businessDesign.nodes?.length) {
      return normalizeBusinessDesign(parsed.businessDesign, translate);
    }
  } catch {
    throw new Error('Workflow business model is MigrationBlocked: invalid JSON.');
  }

  throw new Error('Workflow business model is MigrationBlocked: latest contract is required.');
}

export function serializeBusinessDesign(design: WorkflowBusinessDesign, translate: TranslateFn): string {
  const normalizedDesign = normalizeBusinessDesign(design, translate);
  return JSON.stringify({
    businessDesign: normalizedDesign,
    kind: 'WorkflowBusinessModelLatest',
    version: 'latest'
  });
}

export function updateNodeParticipant(node: WorkflowBusinessNode, participant?: WorkflowParticipantDto | null): WorkflowBusinessNode {
  if (!participant) {
    return {
      ...node,
      participantId: '',
      participantName: '',
      participantCode: '',
      participantIds: [],
      participantNames: [],
      participantExpression: '',
      participantFieldKey: '',
      groupKey: null
    };
  }

  const participantIds = node.approvalMode === 'all' || node.approvalMode === 'any'
    ? Array.from(new Set([...(node.participantIds ?? []), participant.id].filter(Boolean)))
    : [participant.id];
  const participantNames = node.approvalMode === 'all' || node.approvalMode === 'any'
    ? Array.from(new Set([...(node.participantNames ?? []), participant.name].filter(Boolean)))
    : [participant.name];

  return {
    ...node,
    participantId: participant.id,
    participantName: participant.name,
    participantCode: participant.code,
    participantIds,
    participantNames,
    groupKey: participant.groupKey ?? null
  };
}

export function normalizeBusinessDesign(design: WorkflowBusinessDesign, translate: TranslateFn): WorkflowBusinessDesign {
  const fallback = createDefaultBusinessDesign(translate);
  const nodes = (design.nodes?.length ? design.nodes : fallback.nodes).map((node) => {
    const baseNode = createBusinessNode(node.type, node.id, node.label, node.position);
    const participantType = node.participantType === 'manager' ? 'starterManager' : node.participantType ?? baseNode.participantType;
    return {
      ...baseNode,
      ...node,
      actionPolicies: normalizeActionPolicies(node.actionPolicies, node.actions, translate),
      conditionRules: node.conditionRules ?? [],
      fieldPermissions: node.fieldPermissions?.length ? node.fieldPermissions : [createDefaultFieldPermission(translate)],
      nativeExtensions: node.nativeExtensions ?? '{}',
      notificationRules: node.notificationRules ?? (node.type === 'approval' ? [createDefaultNotificationRule(translate)] : []),
      participantExpression: node.participantExpression ?? '',
      participantFieldKey: node.participantFieldKey ?? '',
      participantIds: node.participantIds?.length ? node.participantIds : node.participantId ? [node.participantId] : [],
      participantNames: node.participantNames?.length ? node.participantNames : node.participantName ? [node.participantName] : [],
      participantType,
      position: node.position ?? { x: 0, y: 0 },
      subProcessConfig: {
        ...baseNode.subProcessConfig,
        ...node.subProcessConfig,
        calledElement: node.subProcessConfig?.calledElement || node.subprocessKey || ''
      },
      timeoutPolicy: {
        ...baseNode.timeoutPolicy,
        ...node.timeoutPolicy,
        enabled: node.timeoutPolicy?.enabled ?? node.type === 'timeout',
        hours: node.timeoutPolicy?.hours ?? node.timeoutHours ?? 0,
        action: node.timeoutPolicy?.action ?? node.timeoutAction ?? 'notify'
      },
      variableMappingRows: node.variableMappingRows?.length ? node.variableMappingRows : [createDefaultVariableMapping()]
    };
  });
  const selectedNodeId = nodes.some((node) => node.id === design.selectedNodeId)
    ? design.selectedNodeId
    : nodes.find((node) => node.type === 'approval')?.id ?? nodes[0]?.id ?? '';
  return {
    formContext: normalizeFormContext(design.formContext),
    version: 'latest',
    selectedNodeId,
    nodes,
    edges: design.edges?.length ? design.edges : fallback.edges
  };
}

function normalizeFormContext(context?: WorkflowBusinessFormContext | null): WorkflowBusinessFormContext | null {
  if (!context?.resourceCode || !context.modelCode || !context.pageCode) {
    return null;
  }

  return {
    businessType: context.businessType ?? context.modelCode,
    fields: (context.fields ?? [])
      .map((field) => ({
        binding: field.binding ?? field.fieldCode,
        dataType: field.dataType ?? 'text',
        dictType: field.dictType ?? null,
        fieldCode: field.fieldCode,
        fieldName: field.fieldName || field.fieldCode,
        order: field.order ?? 0,
        queryable: Boolean(field.queryable),
        renderer: field.renderer ?? null,
        sortable: Boolean(field.sortable),
        visible: field.visible !== false
      }))
      .sort((left, right) => left.order - right.order),
    keyField: context.keyField || 'id',
    menuCode: context.menuCode,
    modelCode: context.modelCode,
    pageCode: context.pageCode,
    resourceCode: context.resourceCode,
    resourceName: context.resourceName || context.resourceCode,
    routePath: context.routePath ?? null
  };
}

function createDefaultActionPolicies(translate: TranslateFn): WorkflowActionPolicy[] {
  return [
    createActionPolicy('complete', translate('workflowBusiness.action.complete'), '#16a34a', 'workflow:task:approve', false, 'approved'),
    createActionPolicy('reject', translate('workflowBusiness.action.reject'), '#dc2626', 'workflow:task:approve', true, 'rejected'),
    createActionPolicy('return', translate('workflowBusiness.action.return'), '#f97316', 'workflow:task:return', true, 'returned'),
    createActionPolicy('transfer', translate('workflowBusiness.action.transfer'), '#2563eb', 'workflow:task:transfer', true, 'transferred'),
    createActionPolicy('delegate', translate('workflowBusiness.action.delegate'), '#7c3aed', 'workflow:task:delegate', false, 'delegated'),
    createActionPolicy('add-sign', translate('workflowBusiness.action.addSign'), '#0891b2', 'workflow:task:add-sign', false, 'added'),
    createActionPolicy('remove-sign', translate('workflowBusiness.action.removeSign'), '#64748b', 'workflow:task:remove-sign', true, 'removed'),
    createActionPolicy('withdraw', translate('workflowBusiness.action.withdraw'), '#475569', 'workflow:instance:withdraw', true, 'withdrawn'),
    createActionPolicy('terminate', translate('workflowBusiness.action.terminate'), '#b91c1c', 'workflow:instance:terminate', true, 'terminated'),
    createActionPolicy('resubmit', translate('workflowBusiness.action.resubmit'), '#0d9488', 'workflow:instance:start', false, 'resubmitted')
  ];
}

function createActionPolicy(
  action: WorkflowActionKey,
  label: string,
  color: string,
  permissionCode: string,
  commentRequired: boolean,
  nextStatus: string
): WorkflowActionPolicy {
  return {
    action,
    attachmentPolicy: 'optional',
    callbackCode: '',
    color,
    commentRequired,
    enabled: ['complete', 'reject', 'transfer', 'delegate'].includes(action),
    label,
    nextStatus,
    permissionCode
  };
}

function normalizeActionPolicies(
  policies?: WorkflowActionPolicy[],
  legacyActions?: string[],
  translate?: TranslateFn
): WorkflowActionPolicy[] {
  const defaults = createDefaultActionPolicies(translate ?? ((key) => key));
  const byAction = new Map((policies ?? []).map((policy) => [policy.action, policy]));
  const legacy = new Set(legacyActions ?? []);
  return defaults.map((policy) => ({
    ...policy,
    ...byAction.get(policy.action),
    enabled: byAction.has(policy.action) ? byAction.get(policy.action)?.enabled ?? policy.enabled : legacy.size > 0 ? legacy.has(policy.action) : policy.enabled
  }));
}

function createDefaultFieldPermission(translate: TranslateFn): WorkflowFieldPermission {
  return {
    fieldKey: 'businessName',
    fieldLabel: translate('workflowBusiness.default.businessName'),
    hidden: false,
    id: `field_${Date.now().toString(36)}`,
    readonly: false,
    required: false,
    subjectId: '',
    subjectName: translate('workflowBusiness.default.currentApprover'),
    subjectType: 'approver',
    visible: true
  };
}

function createDefaultNotificationRule(translate: TranslateFn): WorkflowNotificationRule {
  return {
    channelCodes: ['in-app'],
    conditionSummary: '',
    enabled: true,
    failurePolicy: 'ignore',
    id: `notice_${Date.now().toString(36)}`,
    receiverName: translate('workflowBusiness.default.currentApprover'),
    receiverType: 'approver',
    receiverValue: '',
    templateCode: 'workflow-node-enter',
    trigger: 'node-enter'
  };
}

function createDefaultVariableMapping(): WorkflowVariableMapping {
  return {
    direction: 'input',
    id: `var_${Date.now().toString(36)}`,
    sourcePath: 'businessKey',
    targetPath: 'businessKey',
    valueType: 'string'
  };
}
