import type {
  WorkflowBindingDto,
  WorkflowCallbackAssignmentDto,
  WorkflowCallbackConfigDto,
  WorkflowCallbackKeySource,
  WorkflowCallbackRuleDto,
  WorkflowCallbackTrigger,
  WorkflowCallbackValueSource,
  WorkflowFormResourceDto
} from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { translateCurrentLocale } from '../../core/i18n/I18nProvider';

const workflowCallbackTriggerValues: ReadonlyArray<WorkflowCallbackTrigger> = [
  'process-start',
  'node-enter',
  'task-complete',
  'task-reject',
  'task-return',
  'process-completed',
  'process-withdrawn',
  'process-terminated'
];

const workflowCallbackKeySourceValues: ReadonlyArray<WorkflowCallbackKeySource> = [
  'businessKey',
  'context',
  'variable',
  'submittedField'
];

const workflowCallbackValueSourceValues: ReadonlyArray<WorkflowCallbackValueSource> = [
  'constant',
  'context',
  'variable',
  'submittedField'
];

export function getWorkflowCallbackTriggerOptions(translate = translateCurrentLocale): ReadonlyArray<{ label: string; value: WorkflowCallbackTrigger }> {
  return workflowCallbackTriggerValues.map((value) => ({
    label: translate(getWorkflowCallbackTriggerKey(value)),
    value
  }));
}

export function getWorkflowCallbackKeySourceOptions(translate = translateCurrentLocale): ReadonlyArray<{ label: string; value: WorkflowCallbackKeySource }> {
  return workflowCallbackKeySourceValues.map((value) => ({
    label: translate(getWorkflowCallbackKeySourceKey(value)),
    value
  }));
}

export function getWorkflowCallbackValueSourceOptions(translate = translateCurrentLocale): ReadonlyArray<{ label: string; value: WorkflowCallbackValueSource }> {
  return workflowCallbackValueSourceValues.map((value) => ({
    label: translate(getWorkflowCallbackValueSourceKey(value)),
    value
  }));
}

export const workflowCallbackContextKeyOptions = [
  'tenantId',
  'appCode',
  'menuCode',
  'businessType',
  'businessKey',
  'processInstanceId',
  'processDefinitionKey',
  'instanceStatus',
  'trigger',
  'nodeId',
  'workflowTaskId',
  'action',
  'currentUserId',
  'startedBy',
  'startedAt',
  'finishedAt'
] as const;

export function createDefaultCallbackRule(resource?: WorkflowFormResourceDto | null): WorkflowCallbackRuleDto {
  return {
    assignments: [],
    enabled: true,
    nodeId: null,
    ruleId: createRuleId(),
    sortOrder: 0,
    target: {
      keyName: null,
      keySource: 'businessKey',
      modelCode: resource?.modelCode ?? null
    },
    trigger: 'process-completed'
  };
}

export function createDefaultCallbackAssignment(fieldCode = ''): WorkflowCallbackAssignmentDto {
  return {
    fieldCode,
    value: '',
    valueName: null,
    valueSource: 'constant'
  };
}

export function buildCallbackConfigForEdit(row: WorkflowBindingDto): WorkflowCallbackConfigDto {
  return normalizeCallbackConfig(row.callbackConfig);
}

export function normalizeCallbackConfig(config?: WorkflowCallbackConfigDto | null): WorkflowCallbackConfigDto & { rules: WorkflowCallbackRuleDto[] } {
  const rules = (config?.rules ?? []).map((rule, index) => {
    const target = rule.target ?? {};
    return {
      assignments: (rule.assignments ?? []).map(normalizeAssignment).slice(0, 10),
      enabled: rule.enabled,
      nodeId: normalizeString(rule.nodeId),
      ruleId: normalizeString(rule.ruleId) ?? createRuleId(),
      sortOrder: Number.isFinite(rule.sortOrder) ? rule.sortOrder : index,
      target: {
        keyName: normalizeString(target.keyName),
        keySource: normalizeKeySource(target.keySource),
        modelCode: normalizeString(target.modelCode)
      },
      trigger: normalizeTrigger(rule.trigger)
    };
  }).sort((left, right) => left.sortOrder - right.sortOrder)
    .map((rule, index) => ({ ...rule, sortOrder: index }))
    .slice(0, 20);

  return { rules, version: 'latest' };
}

export function validateCallbackConfig(
  config: WorkflowCallbackConfigDto | null | undefined,
  resources: WorkflowFormResourceDto[],
  selectedResource: WorkflowFormResourceDto | null
): string | null {
  const translate = translateCurrentLocale;
  const rules = config?.rules ?? [];
  if (rules.length > 20) {
    return formatMessage(translate('workflow.callback.validation.ruleCount'), { max: 20 });
  }

  const duplicateFields = new Set<string>();
  for (const [index, rule] of rules.entries()) {
    const displayIndex = index + 1;
    if ((rule.assignments?.length ?? 0) === 0) {
      return formatMessage(translate('workflow.callback.validation.assignmentMissing'), { index: displayIndex });
    }
    if ((rule.assignments?.length ?? 0) > 10) {
      return formatMessage(translate('workflow.callback.validation.assignmentTooMany'), { index: displayIndex, max: 10 });
    }

    const targetResource = findTargetResource(resources, selectedResource, rule.target?.modelCode);
    if (!targetResource) {
      return formatMessage(translate('workflow.callback.validation.targetMissing'), { index: displayIndex });
    }

    const keySource = normalizeKeySource(rule.target?.keySource);
    if (keySource !== 'businessKey' && !normalizeString(rule.target?.keyName)) {
      return formatMessage(translate('workflow.callback.validation.keyNameMissing'), { index: displayIndex });
    }

    for (const assignment of rule.assignments ?? []) {
      if (!assignment.fieldCode) {
        return formatMessage(translate('workflow.callback.validation.fieldMissing'), { index: displayIndex });
      }

      const field = targetResource.fields.find((item) => item.fieldCode === assignment.fieldCode);
      if (!field?.writable || field.fieldCode === targetResource.keyField) {
        return formatMessage(translate('workflow.callback.validation.fieldNotWritable'), { index: displayIndex, fieldCode: assignment.fieldCode });
      }

      const source = normalizeValueSource(assignment.valueSource);
      if (source !== 'constant' && !normalizeString(assignment.valueName)) {
        return formatMessage(translate('workflow.callback.validation.sourceNameMissing'), { index: displayIndex, fieldCode: assignment.fieldCode });
      }

      const duplicateKey = `${rule.trigger}|${rule.nodeId ?? ''}|${targetResource.modelCode}|${assignment.fieldCode}`;
      if (duplicateFields.has(duplicateKey)) {
        return formatMessage(translate('workflow.callback.validation.duplicateField'), { fieldCode: assignment.fieldCode });
      }
      duplicateFields.add(duplicateKey);
    }
  }

  return null;
}

export function summarizeCallbackConfig(config?: WorkflowCallbackConfigDto | null): string {
  const translate = translateCurrentLocale;
  const rules = (config?.rules ?? []).filter((rule) => rule.enabled);
  if (rules.length === 0) {
    return '-';
  }

  return rules.slice(0, 3).map((rule) => {
    const trigger = getWorkflowCallbackTriggerLabel(rule.trigger, translate);
    const assignments = (rule.assignments ?? []).slice(0, 2).map((assignment) => (
      `${assignment.fieldCode}=${formatAssignmentValue(assignment)}`
    )).join(', ');

    return assignments ? `${trigger}: ${assignments}` : trigger;
  }).join('; ') + (rules.length > 3 ? `; +${rules.length - 3}` : '');
}

export function findTargetResource(
  resources: WorkflowFormResourceDto[],
  selectedResource: WorkflowFormResourceDto | null,
  modelCode?: string | null
): WorkflowFormResourceDto | null {
  const normalized = normalizeString(modelCode);
  if (!normalized) {
    return selectedResource;
  }

  return resources.find((resource) => resource.modelCode === normalized) ?? null;
}

function normalizeAssignment(assignment: WorkflowCallbackAssignmentDto): WorkflowCallbackAssignmentDto {
  const source = normalizeValueSource(assignment.valueSource);
  return {
    fieldCode: assignment.fieldCode.trim(),
    value: source === 'constant' ? assignment.value : null,
    valueName: source === 'constant' ? null : normalizeString(assignment.valueName),
    valueSource: source
  };
}

function normalizeTrigger(trigger: WorkflowCallbackTrigger): WorkflowCallbackTrigger {
  return workflowCallbackTriggerValues.includes(trigger) ? trigger : 'process-completed';
}

function normalizeKeySource(source?: WorkflowCallbackKeySource | null): WorkflowCallbackKeySource {
  const candidate = source ?? 'businessKey';
  return workflowCallbackKeySourceValues.includes(candidate) ? candidate : 'businessKey';
}

function normalizeValueSource(source: WorkflowCallbackValueSource): WorkflowCallbackValueSource {
  return workflowCallbackValueSourceValues.includes(source) ? source : 'constant';
}

function normalizeString(value?: string | null): string | null {
  const normalized = value?.trim();
  return normalized ? normalized : null;
}

function formatAssignmentValue(assignment: WorkflowCallbackAssignmentDto): string {
  if (assignment.valueSource === 'constant') {
    return String(assignment.value ?? '');
  }

  const sourceLabel = getWorkflowCallbackValueSourceLabel(assignment.valueSource, translateCurrentLocale);
  return `${sourceLabel}.${assignment.valueName ?? ''}`;
}

function getWorkflowCallbackTriggerKey(trigger: WorkflowCallbackTrigger): string {
  switch (trigger) {
    case 'process-start':
      return 'workflow.callback.trigger.processStart';
    case 'node-enter':
      return 'workflow.callback.trigger.nodeEnter';
    case 'task-complete':
      return 'workflow.callback.trigger.taskComplete';
    case 'task-reject':
      return 'workflow.callback.trigger.taskReject';
    case 'task-return':
      return 'workflow.callback.trigger.taskReturn';
    case 'process-completed':
      return 'workflow.callback.trigger.processCompleted';
    case 'process-withdrawn':
      return 'workflow.callback.trigger.processWithdrawn';
    case 'process-terminated':
      return 'workflow.callback.trigger.processTerminated';
    default:
      return String(trigger);
  }
}

function getWorkflowCallbackTriggerLabel(trigger: WorkflowCallbackTrigger, translate = translateCurrentLocale): string {
  const key = getWorkflowCallbackTriggerKey(trigger);
  return key.startsWith('workflow.') ? translate(key) : trigger;
}

function getWorkflowCallbackKeySourceKey(source: WorkflowCallbackKeySource): string {
  switch (source) {
    case 'businessKey':
      return 'workflow.callback.keySource.businessKey';
    case 'context':
      return 'workflow.callback.keySource.context';
    case 'variable':
      return 'workflow.callback.keySource.variable';
    case 'submittedField':
      return 'workflow.callback.keySource.submittedField';
    default:
      return String(source);
  }
}

function getWorkflowCallbackValueSourceKey(source: WorkflowCallbackValueSource): string {
  switch (source) {
    case 'constant':
      return 'workflow.callback.valueSource.constant';
    case 'context':
      return 'workflow.callback.valueSource.context';
    case 'variable':
      return 'workflow.callback.valueSource.variable';
    case 'submittedField':
      return 'workflow.callback.valueSource.submittedField';
    default:
      return String(source);
  }
}

function getWorkflowCallbackValueSourceLabel(source: WorkflowCallbackValueSource, translate = translateCurrentLocale): string {
  const key = getWorkflowCallbackValueSourceKey(source);
  return key.startsWith('workflow.') ? translate(key) : source;
}

function createRuleId(prefix = 'rule'): string {
  if (globalThis.crypto?.randomUUID) {
    return `${prefix}-${globalThis.crypto.randomUUID()}`;
  }

  return `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
