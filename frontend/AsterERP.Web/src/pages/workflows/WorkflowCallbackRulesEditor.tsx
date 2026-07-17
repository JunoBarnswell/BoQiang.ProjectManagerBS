import type {
  WorkflowCallbackAssignmentDto,
  WorkflowCallbackConfigDto,
  WorkflowCallbackKeySource,
  WorkflowCallbackRuleDto,
  WorkflowCallbackTrigger,
  WorkflowCallbackValueSource,
  WorkflowFormResourceDto
} from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { translateCurrentLocale, useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../../shared/icons/AppIcon';

import {
  createDefaultCallbackAssignment,
  createDefaultCallbackRule,
  getWorkflowCallbackKeySourceOptions,
  getWorkflowCallbackTriggerOptions,
  getWorkflowCallbackValueSourceOptions,
  findTargetResource,
  normalizeCallbackConfig,
  workflowCallbackContextKeyOptions
} from './workflowCallbackConfig';

interface WorkflowCallbackRulesEditorProps {
  config?: WorkflowCallbackConfigDto | null;
  resources: WorkflowFormResourceDto[];
  selectedResource: WorkflowFormResourceDto | null;
  onChange: (config: WorkflowCallbackConfigDto) => void;
}

const inputClass = 'h-8 min-w-0 rounded border border-gray-300 bg-white px-2 text-xs text-gray-700 outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100';
const iconButtonClass = 'inline-flex h-8 w-8 shrink-0 items-center justify-center rounded border border-gray-300 bg-white text-gray-600 hover:bg-gray-50';

export function WorkflowCallbackRulesEditor({
  config,
  resources,
  selectedResource,
  onChange
}: WorkflowCallbackRulesEditorProps) {
  const { translate } = useI18n();
  const rules = normalizeCallbackConfig(config).rules;
  const triggerOptions = getWorkflowCallbackTriggerOptions(translate);
  const keySourceOptions = getWorkflowCallbackKeySourceOptions(translate);
  const valueSourceOptions = getWorkflowCallbackValueSourceOptions(translate);

  const updateRules = (nextRules: WorkflowCallbackRuleDto[]) => {
    onChange({ rules: nextRules.map((rule, index) => ({ ...rule, sortOrder: index })) });
  };

  const addRule = () => {
    updateRules([...rules, { ...createDefaultCallbackRule(selectedResource), sortOrder: rules.length }]);
  };

  const updateRule = (index: number, patch: Partial<WorkflowCallbackRuleDto>) => {
    updateRules(rules.map((rule, ruleIndex) => ruleIndex === index ? { ...rule, ...patch } : rule));
  };

  const removeRule = (index: number) => {
    updateRules(rules.filter((_, ruleIndex) => ruleIndex !== index));
  };

  const moveRule = (index: number, offset: -1 | 1) => {
    const nextIndex = index + offset;
    if (nextIndex < 0 || nextIndex >= rules.length) {
      return;
    }

    const nextRules = [...rules];
    const current = nextRules[index];
    nextRules[index] = nextRules[nextIndex];
    nextRules[nextIndex] = current;
    updateRules(nextRules);
  };

  return (
    <section className="rounded-md border border-gray-200 bg-white">
      <div className="flex items-center justify-between gap-3 border-b border-gray-200 px-3 py-2">
        <div className="min-w-0">
          <div className="text-xs font-semibold text-gray-800">{translate('workflow.callback.title')}</div>
          <div className="mt-0.5 truncate text-[11px] text-gray-500">{formatMessage(translate('workflow.callback.subtitle'), { assignmentLimit: 10, ruleLimit: 20 })}</div>
        </div>
        <button className="inline-flex h-8 items-center gap-1 rounded bg-primary-600 px-2.5 text-xs font-medium text-white hover:bg-primary-700" type="button" onClick={addRule}>
          <AppIcon className="text-sm" name="plus" />
          {translate('workflow.callback.addRule')}
        </button>
      </div>

      <div className="space-y-3 p-3">
        {rules.length === 0 ? (
          <div className="rounded border border-dashed border-gray-300 px-3 py-5 text-center text-xs text-gray-500">
            {translate('workflow.callback.empty')}
          </div>
        ) : null}

        {rules.map((rule, index) => (
          <RuleEditor
            key={rule.ruleId ?? index}
            index={index}
            isFirst={index === 0}
            isLast={index === rules.length - 1}
            keySourceOptions={keySourceOptions}
            resources={resources}
            rule={rule}
            selectedResource={selectedResource}
            onMove={moveRule}
            onRemove={removeRule}
            onUpdate={updateRule}
            translate={translate}
            triggerOptions={triggerOptions}
            valueSourceOptions={valueSourceOptions}
          />
        ))}
      </div>
    </section>
  );
}

interface RuleEditorProps {
  index: number;
  isFirst: boolean;
  isLast: boolean;
  keySourceOptions: ReadonlyArray<{ label: string; value: WorkflowCallbackKeySource }>;
  resources: WorkflowFormResourceDto[];
  rule: WorkflowCallbackRuleDto;
  selectedResource: WorkflowFormResourceDto | null;
  onMove: (index: number, offset: -1 | 1) => void;
  onRemove: (index: number) => void;
  onUpdate: (index: number, patch: Partial<WorkflowCallbackRuleDto>) => void;
  translate: (key: string, params?: Record<string, string | number>) => string;
  triggerOptions: ReadonlyArray<{ label: string; value: WorkflowCallbackTrigger }>;
  valueSourceOptions: ReadonlyArray<{ label: string; value: WorkflowCallbackValueSource }>;
}

function RuleEditor({
  index,
  isFirst,
  isLast,
  keySourceOptions,
  resources,
  rule,
  selectedResource,
  onMove,
  onRemove,
  onUpdate,
  translate,
  triggerOptions,
  valueSourceOptions
}: RuleEditorProps) {
  const targetModelCode = rule.target?.modelCode ?? null;
  const targetResource = findTargetResource(resources, selectedResource, targetModelCode);
  const writableFields = (targetResource?.fields ?? [])
    .filter((field) => field.writable && field.fieldCode !== targetResource?.keyField)
    .sort((left, right) => left.order - right.order);
  const assignments = rule.assignments ?? [];

  const updateAssignment = (assignmentIndex: number, patch: Partial<WorkflowCallbackAssignmentDto>) => {
    const nextAssignments = assignments.map((assignment, currentIndex) => (
      currentIndex === assignmentIndex ? { ...assignment, ...patch } : assignment
    ));
    onUpdate(index, { assignments: nextAssignments });
  };

  const addAssignment = () => {
    onUpdate(index, {
      assignments: [...assignments, createDefaultCallbackAssignment(writableFields[0]?.fieldCode ?? '')]
    });
  };

  const removeAssignment = (assignmentIndex: number) => {
    onUpdate(index, { assignments: assignments.filter((_, currentIndex) => currentIndex !== assignmentIndex) });
  };

  return (
    <div className="rounded-md border border-gray-200 bg-gray-50">
      <div className="flex items-center gap-2 border-b border-gray-200 bg-white px-2.5 py-2">
        <label className="inline-flex shrink-0 items-center gap-1.5 text-xs font-medium text-gray-700">
          <input
            checked={rule.enabled}
            className="h-4 w-4 rounded border-gray-300 text-primary-600"
            type="checkbox"
            onChange={(event) => onUpdate(index, { enabled: event.target.checked })}
          />
          {translate('workflow.callback.enabled')}
        </label>
        <select
          className={`${inputClass} flex-1`}
          value={rule.trigger}
          onChange={(event) => onUpdate(index, { trigger: event.target.value as WorkflowCallbackTrigger })}
        >
          {triggerOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
        <button className={iconButtonClass} disabled={isFirst} title={translate('workflow.callback.moveUp')} type="button" onClick={() => onMove(index, -1)}>
          <AppIcon className="text-sm" name="arrow-up" />
        </button>
        <button className={iconButtonClass} disabled={isLast} title={translate('workflow.callback.moveDown')} type="button" onClick={() => onMove(index, 1)}>
          <AppIcon className="text-sm" name="arrow-down" />
        </button>
        <button className={`${iconButtonClass} text-red-600 hover:border-red-200 hover:bg-red-50`} title={translate('workflow.callback.delete')} type="button" onClick={() => onRemove(index)}>
          <AppIcon className="text-sm" name="trash" />
        </button>
      </div>

      <div className="space-y-2.5 p-2.5">
        <div className="grid grid-cols-2 gap-2">
          <label className="min-w-0 text-[11px] font-medium text-gray-500">
            {translate('workflow.callback.nodeId')}
            <input
              className={`${inputClass} mt-1 w-full`}
              placeholder={translate('workflow.callback.nodeIdPlaceholder')}
              value={rule.nodeId ?? ''}
              onChange={(event) => onUpdate(index, { nodeId: event.target.value || null })}
            />
          </label>
          <label className="min-w-0 text-[11px] font-medium text-gray-500">
            {translate('workflow.callback.targetModel')}
            <select
              className={`${inputClass} mt-1 w-full`}
              value={targetModelCode ?? ''}
              onChange={(event) => onUpdate(index, {
                assignments: [],
                target: {
                  ...(rule.target ?? {}),
                  modelCode: event.target.value || null
                }
              })}
            >
              <option value="">{translate('workflow.callback.currentForm')}</option>
              {resources.map((resource) => (
                <option key={resource.resourceCode} value={resource.modelCode}>
                  {resource.resourceName}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="grid grid-cols-2 gap-2">
          <label className="min-w-0 text-[11px] font-medium text-gray-500">
            {translate('workflow.callback.keySource')}
            <select
              className={`${inputClass} mt-1 w-full`}
              value={rule.target?.keySource ?? 'businessKey'}
              onChange={(event) => onUpdate(index, {
                target: {
                  ...(rule.target ?? {}),
                  keyName: event.target.value === 'businessKey' ? null : rule.target?.keyName ?? '',
                  keySource: event.target.value as WorkflowCallbackKeySource
                }
              })}
            >
              {keySourceOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <SourceNameInput
            disabled={(rule.target?.keySource ?? 'businessKey') === 'businessKey'}
            label={translate('workflow.callback.sourceField')}
            source={rule.target?.keySource ?? 'businessKey'}
            value={rule.target?.keyName ?? ''}
            onChange={(keyName) => onUpdate(index, { target: { ...(rule.target ?? {}), keyName } })}
          />
        </div>

        <div className="rounded border border-gray-200 bg-white">
          <div className="flex items-center justify-between gap-2 border-b border-gray-100 px-2 py-1.5">
            <span className="text-[11px] font-semibold text-gray-600">{translate('workflow.callback.fieldAssignments')}</span>
            <button className="inline-flex h-7 items-center gap-1 rounded border border-gray-300 bg-white px-2 text-[11px] text-gray-700 hover:bg-gray-50" type="button" onClick={addAssignment}>
              <AppIcon className="text-xs" name="plus" />
              {translate('workflow.callback.addField')}
            </button>
          </div>
          <div className="space-y-2 p-2">
            {assignments.length === 0 ? (
              <div className="rounded bg-gray-50 px-2 py-2 text-center text-[11px] text-gray-400">{translate('workflow.callback.fieldEmpty')}</div>
            ) : null}
            {assignments.map((assignment, assignmentIndex) => (
              <AssignmentEditor
                key={`${rule.ruleId ?? index}-${assignmentIndex}`}
                assignment={assignment}
                fields={writableFields}
                translate={translate}
                onRemove={() => removeAssignment(assignmentIndex)}
                onUpdate={(patch) => updateAssignment(assignmentIndex, patch)}
                valueSourceOptions={valueSourceOptions}
              />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

interface AssignmentEditorProps {
  assignment: WorkflowCallbackAssignmentDto;
  fields: WorkflowFormResourceDto['fields'];
  translate: (key: string, params?: Record<string, string | number>) => string;
  valueSourceOptions: ReadonlyArray<{ label: string; value: WorkflowCallbackValueSource }>;
  onRemove: () => void;
  onUpdate: (patch: Partial<WorkflowCallbackAssignmentDto>) => void;
}

function AssignmentEditor({ assignment, fields, onRemove, onUpdate, translate, valueSourceOptions }: AssignmentEditorProps) {
  return (
    <div className="grid grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)_minmax(0,1fr)_32px] gap-1.5">
      <select
        className={inputClass}
        value={assignment.fieldCode}
        onChange={(event) => onUpdate({ fieldCode: event.target.value })}
      >
        <option value="">{translate('workflow.callback.targetField')}</option>
        {fields.map((field) => (
          <option key={field.fieldCode} value={field.fieldCode}>
            {field.fieldName || field.fieldCode}
          </option>
        ))}
      </select>
      <select
        className={inputClass}
        value={assignment.valueSource}
        onChange={(event) => onUpdate({
          value: event.target.value === 'constant' ? assignment.value ?? '' : null,
          valueName: event.target.value === 'constant' ? null : assignment.valueName ?? '',
          valueSource: event.target.value as WorkflowCallbackValueSource
        })}
      >
        {valueSourceOptions.map((option) => (
          <option key={option.value} value={option.value}>{option.label}</option>
        ))}
      </select>
      {assignment.valueSource === 'constant' ? (
        <input
          className={inputClass}
          placeholder={translate('workflow.callback.constantValue')}
          value={String(assignment.value ?? '')}
          onChange={(event) => onUpdate({ value: event.target.value })}
        />
      ) : (
        <SourceNameInput
          label=""
          source={assignment.valueSource}
          value={assignment.valueName ?? ''}
          onChange={(valueName) => onUpdate({ valueName })}
        />
      )}
      <button className={`${iconButtonClass} text-red-600 hover:border-red-200 hover:bg-red-50`} title={translate('workflow.callback.deleteField')} type="button" onClick={onRemove}>
        <AppIcon className="text-sm" name="x" />
      </button>
    </div>
  );
}

interface SourceNameInputProps {
  disabled?: boolean;
  label: string;
  source: WorkflowCallbackKeySource | WorkflowCallbackValueSource;
  value: string;
  onChange: (value: string | null) => void;
}

function SourceNameInput({ disabled = false, label, source, value, onChange }: SourceNameInputProps) {
  const control = source === 'context' ? (
    <select
      className={`${inputClass} ${label ? 'mt-1' : ''} w-full`}
      disabled={disabled}
      value={value}
      onChange={(event) => onChange(event.target.value || null)}
    >
      <option value="">{translateCurrentLocale('workflow.callback.contextField')}</option>
      {workflowCallbackContextKeyOptions.map((key) => (
        <option key={key} value={key}>{key}</option>
      ))}
    </select>
  ) : (
    <input
      className={`${inputClass} ${label ? 'mt-1' : ''} w-full`}
      disabled={disabled}
      placeholder={disabled ? translateCurrentLocale('workflow.callback.defaultBusinessKey') : translateCurrentLocale('workflow.callback.fieldName')}
      value={value}
      onChange={(event) => onChange(event.target.value || null)}
    />
  );

  if (!label) {
    return control;
  }

  return (
    <label className="min-w-0 text-[11px] font-medium text-gray-500">
      {label}
      {control}
    </label>
  );
}
