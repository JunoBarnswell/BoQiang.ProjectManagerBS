import type { WorkflowBusinessNode } from './workflowBusinessModel';

export type TranslateFn = (key: string) => string;

export function createWorkflowBusinessNodeLabels(translate: TranslateFn) {
  return {
    approval: translate('workflowBusiness.node.approval'),
    cc: translate('workflowBusiness.node.cc'),
    condition: translate('workflowBusiness.node.condition'),
    end: translate('workflowBusiness.node.end'),
    start: translate('workflowBusiness.node.start'),
    subprocess: translate('workflowBusiness.node.subprocess'),
    timeout: translate('workflowBusiness.node.timeout')
  } as const;
}

export function createWorkflowBusinessNodePalette(translate: TranslateFn) {
  return [
    { icon: 'check-square-offset', label: translate('workflowBusiness.node.approval'), type: 'approval' },
    { icon: 'git-branch', label: translate('workflowBusiness.node.condition'), type: 'condition' },
    { icon: 'paper-plane-tilt', label: translate('workflowBusiness.node.cc'), type: 'cc' },
    { icon: 'timer', label: translate('workflowBusiness.node.timeout'), type: 'timeout' },
    { icon: 'tree-structure', label: translate('workflowBusiness.node.subprocess'), type: 'subprocess' },
    { icon: 'flag-checkered', label: translate('workflowBusiness.node.end'), type: 'end' }
  ] as const;
}

export function createWorkflowBusinessPropertyTabs(translate: TranslateFn) {
  return [
    { key: 'base', label: translate('workflowBusiness.tab.base') },
    { key: 'approver', label: translate('workflowBusiness.tab.approver') },
    { key: 'condition', label: translate('workflowBusiness.tab.condition') },
    { key: 'form', label: translate('workflowBusiness.tab.form') },
    { key: 'actions', label: translate('workflowBusiness.tab.actions') },
    { key: 'timeout', label: translate('workflowBusiness.tab.timeout') },
    { key: 'notify', label: translate('workflowBusiness.tab.notify') },
    { key: 'variables', label: translate('workflowBusiness.tab.variables') },
    { key: 'subprocess', label: translate('workflowBusiness.tab.subprocess') },
    { key: 'native', label: translate('workflowBusiness.tab.native') }
  ] as const;
}

export function createWorkflowBusinessParticipantOptions(translate: TranslateFn) {
  return [
    { label: translate('workflowBusiness.participant.user'), value: 'user' },
    { label: translate('workflowBusiness.participant.role'), value: 'role' },
    { label: translate('workflowBusiness.participant.department'), value: 'department' },
    { label: translate('workflowBusiness.participant.position'), value: 'position' },
    { label: translate('workflowBusiness.participant.starter'), value: 'starter' },
    { label: translate('workflowBusiness.participant.starterManager'), value: 'starterManager' },
    { label: translate('workflowBusiness.participant.deptManager'), value: 'deptManager' },
    { label: translate('workflowBusiness.participant.previousApprover'), value: 'previousApprover' },
    { label: translate('workflowBusiness.participant.formField'), value: 'formField' },
    { label: translate('workflowBusiness.participant.approver'), value: 'approver' },
    { label: translate('workflowBusiness.participant.dynamic'), value: 'dynamic' }
  ] as const;
}

export function createWorkflowBusinessConditionSources(translate: TranslateFn) {
  return [
    { label: translate('workflowBusiness.condition.formField'), value: 'form' },
    { label: translate('workflowBusiness.condition.processContext'), value: 'process' },
    { label: translate('workflowBusiness.condition.currentUser'), value: 'currentUser' },
    { label: translate('workflowBusiness.condition.department'), value: 'department' },
    { label: translate('workflowBusiness.condition.role'), value: 'role' },
    { label: translate('workflowBusiness.condition.position'), value: 'position' },
    { label: translate('workflowBusiness.condition.dictValue'), value: 'dict' }
  ] as const;
}

export function createWorkflowBusinessConditionOperators(translate: TranslateFn) {
  return [
    { label: translate('workflowBusiness.condition.eq'), value: 'eq' },
    { label: translate('workflowBusiness.condition.ne'), value: 'ne' },
    { label: translate('workflowBusiness.condition.gt'), value: 'gt' },
    { label: translate('workflowBusiness.condition.gte'), value: 'gte' },
    { label: translate('workflowBusiness.condition.lt'), value: 'lt' },
    { label: translate('workflowBusiness.condition.lte'), value: 'lte' },
    { label: translate('workflowBusiness.condition.contains'), value: 'contains' },
    { label: translate('workflowBusiness.condition.empty'), value: 'empty' },
    { label: translate('workflowBusiness.condition.notEmpty'), value: 'notEmpty' },
    { label: translate('workflowBusiness.condition.range'), value: 'range' }
  ] as const;
}

export function createWorkflowBusinessActionLabels(translate: TranslateFn) {
  return {
    complete: translate('workflowBusiness.action.complete'),
    reject: translate('workflowBusiness.action.reject'),
    return: translate('workflowBusiness.action.return'),
    transfer: translate('workflowBusiness.action.transfer'),
    delegate: translate('workflowBusiness.action.delegate'),
    'add-sign': translate('workflowBusiness.action.addSign'),
    'remove-sign': translate('workflowBusiness.action.removeSign'),
    withdraw: translate('workflowBusiness.action.withdraw'),
    terminate: translate('workflowBusiness.action.terminate'),
    resubmit: translate('workflowBusiness.action.resubmit')
  } as const;
}

export function createWorkflowBusinessNotificationTriggers(translate: TranslateFn) {
  return [
    { hint: translate('workflowBusiness.notification.hint.processStart'), label: translate('workflowBusiness.notification.trigger.processStart'), value: 'process-start' },
    { hint: translate('workflowBusiness.notification.hint.nodeEnter'), label: translate('workflowBusiness.notification.trigger.nodeEnter'), value: 'node-enter' },
    { hint: translate('workflowBusiness.notification.hint.taskComplete'), label: translate('workflowBusiness.notification.trigger.taskComplete'), value: 'task-complete' },
    { hint: translate('workflowBusiness.notification.hint.timeout'), label: translate('workflowBusiness.notification.trigger.timeout'), value: 'timeout' },
    { hint: translate('workflowBusiness.notification.hint.processEnd'), label: translate('workflowBusiness.notification.trigger.processEnd'), value: 'process-end' }
  ] as const;
}

export function createWorkflowBusinessPermissionLabels(translate: TranslateFn) {
  return {
    hidden: translate('workflowBusiness.permission.hidden'),
    readonly: translate('workflowBusiness.permission.readonly'),
    required: translate('workflowBusiness.permission.required'),
    visible: translate('workflowBusiness.permission.visible')
  } as const;
}

export function renderWorkflowBusinessNodeHint(node: WorkflowBusinessNode, translate: TranslateFn): string {
  if (node.participantName || node.groupKey) {
    return node.participantName || node.groupKey || '';
  }
  if (node.participantFieldKey) {
    return node.participantFieldKey;
  }
  if (node.participantExpression) {
    return node.participantExpression;
  }
  if (node.conditionRules.length > 0) {
    return translate('workflowBusiness.hint.conditionCount').replace('{count}', String(node.conditionRules.length));
  }
  if (node.notificationRules.length > 0) {
    return translate('workflowBusiness.hint.notificationCount').replace('{count}', String(node.notificationRules.length));
  }
  return node.type === 'subprocess' && node.subProcessConfig.calledElement
    ? node.subProcessConfig.calledElement
    : translate('workflowBusiness.hint.unconfigured');
}

export function renderWorkflowBusinessNodePanelSubtitle(node: WorkflowBusinessNode, translate: TranslateFn) {
  return nodeLabelsForType(node.type, translate);
}

export function nodeLabelsForType(type: WorkflowBusinessNode['type'], translate: TranslateFn) {
  return createWorkflowBusinessNodeLabels(translate)[type];
}

export function renderWorkflowBusinessModeLabel(mode: 'all' | 'any', translate: TranslateFn) {
  return mode === 'all' ? translate('workflowBusiness.approvalMode.all') : translate('workflowBusiness.approvalMode.any');
}

export function renderWorkflowBusinessAttachmentPolicy(policy: string, translate: TranslateFn) {
  if (policy === 'none') return translate('workflowBusiness.attachment.none');
  if (policy === 'optional') return translate('workflowBusiness.attachment.optional');
  return translate('workflowBusiness.attachment.required');
}

export function renderWorkflowBusinessTriggerSummary(rules: Array<{ enabled: boolean; channelCodes: string[] }>, fallback: string, translate: TranslateFn): string {
  if (rules.length === 0) {
    return fallback;
  }

  const enabledRules = rules.filter((rule) => rule.enabled);
  if (enabledRules.length === 0) {
    return translate('workflowBusiness.notification.summary.unenabled').replace('{count}', String(rules.length));
  }

  if (enabledRules.length === 1) {
    return translate('workflowBusiness.notification.summary.single').replace('{channels}', enabledRules[0].channelCodes.join(', ') || translate('workflowBusiness.notification.summary.noChannels'));
  }

  const channels = Array.from(new Set(enabledRules.flatMap((rule) => rule.channelCodes))).join(', ') || translate('workflowBusiness.notification.summary.noChannels');
  return translate('workflowBusiness.notification.summary.multiple')
    .replace('{enabled}', String(enabledRules.length))
    .replace('{total}', String(rules.length))
    .replace('{channels}', channels);
}

export function renderWorkflowBusinessRuleSummary(rule: {
  channelCodes: string[];
  receiverType: string;
  receiverValue: string;
  templateCode: string;
}, translate: TranslateFn): string {
  const receiver = translate(`workflowBusiness.participant.${rule.receiverType}`) || rule.receiverType;
  const receiverValue = rule.receiverValue ? ` ${rule.receiverValue}` : '';
  const channels = rule.channelCodes.length > 0 ? rule.channelCodes.join(', ') : translate('workflowBusiness.notification.summary.noChannels');
  const template = rule.templateCode || translate('workflowBusiness.notification.summary.noTemplate');
  return `${receiver}${receiverValue} · ${channels} · ${template}`;
}
