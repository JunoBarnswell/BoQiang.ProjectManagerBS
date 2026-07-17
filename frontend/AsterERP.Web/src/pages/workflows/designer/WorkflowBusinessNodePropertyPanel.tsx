import type { ReactNode } from 'react';
import { useMemo } from 'react';

import type { WorkflowParticipantDto } from '../../../api/workflow/workflows.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';

import {
  createWorkflowBusinessActionLabels,
  createWorkflowBusinessConditionOperators,
  createWorkflowBusinessConditionSources,
  createWorkflowBusinessNodeLabels,
  createWorkflowBusinessNotificationTriggers,
  createWorkflowBusinessParticipantOptions,
  createWorkflowBusinessPermissionLabels,
  createWorkflowBusinessPropertyTabs,
  renderWorkflowBusinessAttachmentPolicy,
  renderWorkflowBusinessModeLabel,
  renderWorkflowBusinessTriggerSummary,
  type TranslateFn
} from './workflowBusinessI18n';
import {
  type ApprovalMode,
  type AttachmentPolicy,
  type ConditionFieldSource,
  type ConditionOperator,
  type NotificationTrigger,
  type ParticipantType,
  type WorkflowActionKey,
  type WorkflowActionPolicy,
  type WorkflowBusinessFormField,
  type WorkflowBusinessNode,
  type WorkflowConditionRule,
  type WorkflowFieldPermission,
  type WorkflowNotificationRule,
  type WorkflowVariableMapping
} from './workflowBusinessModel';
import { WorkflowParticipantSelector } from './WorkflowParticipantSelector';
export type PanelTab = 'base' | 'approver' | 'condition' | 'form' | 'actions' | 'timeout' | 'notify' | 'variables' | 'subprocess' | 'native';

export const businessNodeSize = {
  height: 62,
  width: 160
};

function useWorkflowBusinessText() {
  const { translate } = useI18n();
  return useMemo(() => ({
    actionLabels: createWorkflowBusinessActionLabels(translate),
    conditionOperators: createWorkflowBusinessConditionOperators(translate),
    conditionSources: createWorkflowBusinessConditionSources(translate),
    nodeLabels: createWorkflowBusinessNodeLabels(translate),
    notificationTriggerOptions: createWorkflowBusinessNotificationTriggers(translate),
    participantOptions: createWorkflowBusinessParticipantOptions(translate),
    permissionLabels: createWorkflowBusinessPermissionLabels(translate),
    propertyTabs: createWorkflowBusinessPropertyTabs(translate),
    translate
  }), [translate]);
}

interface NodePropertyPanelProps {
  node: WorkflowBusinessNode;
  panelTab: PanelTab;
  formFields: WorkflowBusinessFormField[];
  participants: WorkflowParticipantDto[];
  participantKeyword: string;
  onParticipantKeywordChange: (keyword: string) => void;
  onChange: (updater: (node: WorkflowBusinessNode) => WorkflowBusinessNode) => void;
}

export function NodePropertyPanel(props: NodePropertyPanelProps) {
  if (props.panelTab === 'base') {
    return <BasePanel {...props} />;
  }
  if (props.panelTab === 'approver') {
    return <ApproverPanel {...props} />;
  }
  if (props.panelTab === 'condition') {
    return <ConditionPanel {...props} />;
  }
  if (props.panelTab === 'form') {
    return <FieldPermissionPanel {...props} />;
  }
  if (props.panelTab === 'actions') {
    return <ActionPolicyPanel {...props} />;
  }
  if (props.panelTab === 'timeout') {
    return <TimeoutPanel {...props} />;
  }
  if (props.panelTab === 'notify') {
    return <NotificationPanel {...props} />;
  }
  if (props.panelTab === 'subprocess') {
    return <SubProcessPanel {...props} />;
  }
  if (props.panelTab === 'native') {
    return <NativePanel {...props} />;
  }

  return <VariablePanel {...props} />;
}

function BasePanel({ node, onChange }: NodePropertyPanelProps) {
  const { nodeLabels, translate } = useWorkflowBusinessText();
  return (
    <div className="workflow-property-grid">
      <Field label={translate('workflowBusiness.field.nodeName')}>
        <input value={node.label} onChange={(event) => onChange((current) => ({ ...current, label: event.target.value }))} />
      </Field>
      <Field label={translate('workflowBusiness.field.nodeId')}>
        <input value={node.id} readOnly />
      </Field>
      <Field label={translate('workflowBusiness.field.nodeType')}>
        <input value={nodeLabels[node.type]} readOnly />
      </Field>
      <Field label={translate('workflowBusiness.field.approvalMode')}>
        <select value={node.approvalMode} onChange={(event) => onChange((current) => ({ ...current, approvalMode: event.target.value as ApprovalMode }))}>
          <option value="all">{renderWorkflowBusinessModeLabel('all', translate)}</option>
          <option value="any">{renderWorkflowBusinessModeLabel('any', translate)}</option>
        </select>
      </Field>
    </div>
  );
}

function ApproverPanel({
  formFields,
  node,
  participants,
  participantKeyword,
  onParticipantKeywordChange,
  onChange
}: NodePropertyPanelProps) {
  return (
    <WorkflowParticipantSelector
      formFields={formFields}
      node={node}
      participantKeyword={participantKeyword}
      participants={participants}
      onChange={onChange}
      onParticipantKeywordChange={onParticipantKeywordChange}
    />
  );
}

function ConditionPanel({ formFields, node, onChange }: NodePropertyPanelProps) {
  const { conditionOperators, conditionSources, translate } = useWorkflowBusinessText();
  const updateRule = (ruleId: string, updater: (rule: WorkflowConditionRule) => WorkflowConditionRule) => {
    onChange((current) => ({
      ...current,
      conditionRules: current.conditionRules.map((rule) => rule.id === ruleId ? updater(rule) : rule),
      conditionExpression: buildConditionExpression(current.conditionRules.map((rule) => rule.id === ruleId ? updater(rule) : rule))
    }));
  };

  return (
    <div className="workflow-property-grid">
      <div className="workflow-row-toolbar">
        <span>{translate('workflowBusiness.section.visualCondition')}</span>
        <button type="button" onClick={() => onChange((current) => ({ ...current, conditionRules: [...current.conditionRules, createConditionRule(formFields[0], translate)] }))}>
          <AppIcon name="plus" />
        </button>
      </div>
      {node.conditionRules.map((rule) => (
        <div key={rule.id} className="workflow-rule-row">
          <select value={rule.fieldSource} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, fieldSource: event.target.value as ConditionFieldSource }))}>
            {conditionSources.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
          </select>
          {rule.fieldSource === 'form' && formFields.length > 0 ? (
            <FormFieldSelect
              fields={formFields}
              value={rule.fieldKey}
              onSelect={(field) => updateRule(rule.id, (current) => ({
                ...current,
                fieldKey: field?.fieldCode ?? current.fieldKey,
                fieldLabel: field?.fieldName ?? current.fieldLabel
              }))}
            />
          ) : (
            <input placeholder={translate('workflowBusiness.placeholder.fieldCode')} value={rule.fieldKey} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, fieldKey: event.target.value, fieldLabel: event.target.value }))} />
          )}
          <select value={rule.operator} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, operator: event.target.value as ConditionOperator }))}>
            {conditionOperators.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
          </select>
          <input placeholder={translate('workflowBusiness.placeholder.value')} value={rule.value} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, value: event.target.value }))} />
          {rule.operator === 'range' ? <input placeholder={translate('workflowBusiness.placeholder.endValue')} value={rule.valueEnd ?? ''} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, valueEnd: event.target.value }))} /> : null}
          <button title={translate('workflowBusiness.action.deleteCondition')} type="button" onClick={() => onChange((current) => ({ ...current, conditionRules: current.conditionRules.filter((item) => item.id !== rule.id) }))}>
            <AppIcon name="x" />
          </button>
        </div>
      ))}
      <Field label={translate('workflowBusiness.field.generatedExpression')}>
        <textarea readOnly rows={3} value={buildConditionExpression(node.conditionRules)} />
      </Field>
    </div>
  );
}

function FieldPermissionPanel({ formFields, node, onChange }: NodePropertyPanelProps) {
  const { participantOptions, permissionLabels, translate } = useWorkflowBusinessText();
  const updatePermission = (ruleId: string, updater: (rule: WorkflowFieldPermission) => WorkflowFieldPermission) => {
    onChange((current) => ({
      ...current,
      fieldPermissions: current.fieldPermissions.map((rule) => rule.id === ruleId ? updater(rule) : rule)
    }));
  };

  return (
    <div className="workflow-property-grid">
      <div className="workflow-row-toolbar">
        <span>{translate('workflowBusiness.section.fieldPermissions')}</span>
        <button type="button" onClick={() => onChange((current) => ({ ...current, fieldPermissions: [...current.fieldPermissions, createFieldPermission(formFields[0], translate)] }))}>
          <AppIcon name="plus" />
        </button>
      </div>
      {node.fieldPermissions.map((rule) => (
        <div key={rule.id} className="workflow-permission-row">
          {formFields.length > 0 ? (
            <FormFieldSelect
              fields={formFields}
              value={rule.fieldKey}
              onSelect={(field) => updatePermission(rule.id, (current) => ({
                ...current,
                fieldKey: field?.fieldCode ?? current.fieldKey,
                fieldLabel: field?.fieldName ?? current.fieldLabel
              }))}
            />
          ) : (
            <input placeholder={translate('workflowBusiness.placeholder.fieldCode')} value={rule.fieldKey} onChange={(event) => updatePermission(rule.id, (current) => ({ ...current, fieldKey: event.target.value }))} />
          )}
          <input placeholder={translate('workflowBusiness.placeholder.fieldName')} value={rule.fieldLabel} onChange={(event) => updatePermission(rule.id, (current) => ({ ...current, fieldLabel: event.target.value }))} />
          <select value={rule.subjectType} onChange={(event) => updatePermission(rule.id, (current) => ({ ...current, subjectType: event.target.value as WorkflowFieldPermission['subjectType'] }))}>
            {participantOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
          </select>
          {(['visible', 'readonly', 'required', 'hidden'] as const).map((key) => (
            <label key={key}>
              <input checked={rule[key]} type="checkbox" onChange={(event) => updatePermission(rule.id, (current) => ({ ...current, [key]: event.target.checked }))} />
              <span>{permissionLabels[key]}</span>
            </label>
          ))}
          <button title={translate('workflowBusiness.action.deletePermission')} type="button" onClick={() => onChange((current) => ({ ...current, fieldPermissions: current.fieldPermissions.filter((item) => item.id !== rule.id) }))}>
            <AppIcon name="x" />
          </button>
        </div>
      ))}
    </div>
  );
}

function ActionPolicyPanel({ node, onChange }: NodePropertyPanelProps) {
  const { actionLabels, translate } = useWorkflowBusinessText();
  const actionOptions: WorkflowActionKey[] = ['complete', 'reject', 'return', 'transfer', 'delegate', 'add-sign', 'remove-sign', 'withdraw', 'terminate', 'resubmit'];
  const updateAction = (action: WorkflowActionKey, updater: (policy: WorkflowActionPolicy) => WorkflowActionPolicy) => {
    onChange((current) => ({
      ...current,
      actionPolicies: current.actionPolicies.map((policy) => policy.action === action ? updater(policy) : policy),
      actions: current.actionPolicies.map((policy) => policy.action === action ? updater(policy) : policy).filter((policy) => policy.enabled).map((policy) => policy.action)
    }));
  };

  return (
    <div className="workflow-action-policy-list">
      {node.actionPolicies.filter((policy) => actionOptions.includes(policy.action)).map((policy) => (
        <div key={policy.action} className="workflow-action-policy">
          <label className="workflow-action-policy__toggle">
            <input checked={policy.enabled} type="checkbox" onChange={(event) => updateAction(policy.action, (current) => ({ ...current, enabled: event.target.checked }))} />
            <span>{actionLabels[policy.action]}</span>
          </label>
          <input value={policy.label} onChange={(event) => updateAction(policy.action, (current) => ({ ...current, label: event.target.value }))} />
          <input title={translate('workflowBusiness.field.buttonColor')} type="color" value={policy.color} onChange={(event) => updateAction(policy.action, (current) => ({ ...current, color: event.target.value }))} />
          <input placeholder={translate('workflowBusiness.field.permissionCode')} value={policy.permissionCode} onChange={(event) => updateAction(policy.action, (current) => ({ ...current, permissionCode: event.target.value }))} />
          <select value={policy.attachmentPolicy} onChange={(event) => updateAction(policy.action, (current) => ({ ...current, attachmentPolicy: event.target.value as AttachmentPolicy }))}>
            <option value="none">{renderWorkflowBusinessAttachmentPolicy('none', translate)}</option>
            <option value="optional">{renderWorkflowBusinessAttachmentPolicy('optional', translate)}</option>
            <option value="required">{renderWorkflowBusinessAttachmentPolicy('required', translate)}</option>
          </select>
          <label>
            <input checked={policy.commentRequired} type="checkbox" onChange={(event) => updateAction(policy.action, (current) => ({ ...current, commentRequired: event.target.checked }))} />
            <span>{translate('workflowBusiness.action.commentRequired')}</span>
          </label>
          <input placeholder={translate('workflowBusiness.field.nextStatus')} value={policy.nextStatus} onChange={(event) => updateAction(policy.action, (current) => ({ ...current, nextStatus: event.target.value }))} />
          <input placeholder={translate('workflowBusiness.field.callbackCode')} value={policy.callbackCode} onChange={(event) => updateAction(policy.action, (current) => ({ ...current, callbackCode: event.target.value }))} />
        </div>
      ))}
    </div>
  );
}

function TimeoutPanel({ node, onChange }: NodePropertyPanelProps) {
  const { participantOptions, translate } = useWorkflowBusinessText();
  return (
    <div className="workflow-property-grid">
      <label className="workflow-inline-check">
        <input checked={node.timeoutPolicy.enabled} type="checkbox" onChange={(event) => onChange((current) => ({ ...current, timeoutPolicy: { ...current.timeoutPolicy, enabled: event.target.checked } }))} />
        <span>{translate('workflowBusiness.timeout.enablePolicy')}</span>
      </label>
      <Field label={translate('workflowBusiness.timeout.hours')}>
        <input min={0} type="number" value={node.timeoutPolicy.hours} onChange={(event) => onChange((current) => ({ ...current, timeoutHours: Number(event.target.value), timeoutPolicy: { ...current.timeoutPolicy, hours: Number(event.target.value) } }))} />
      </Field>
      <Field label={translate('workflowBusiness.timeout.action')}>
        <select value={node.timeoutPolicy.action} onChange={(event) => onChange((current) => ({ ...current, timeoutAction: event.target.value, timeoutPolicy: { ...current.timeoutPolicy, action: event.target.value } }))}>
          <option value="notify">{translate('workflowBusiness.timeout.notify')}</option>
          <option value="escalate">{translate('workflowBusiness.timeout.escalate')}</option>
          <option value="autoReject">{translate('workflowBusiness.timeout.autoReject')}</option>
        </select>
      </Field>
      <Field label={translate('workflowBusiness.timeout.escalationType')}>
        <select value={node.timeoutPolicy.escalationType} onChange={(event) => onChange((current) => ({ ...current, timeoutPolicy: { ...current.timeoutPolicy, escalationType: event.target.value as ParticipantType } }))}>
          {participantOptions.filter((item) => item.value !== 'approver').map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select>
      </Field>
      <Field label={translate('workflowBusiness.timeout.notificationTemplate')}>
        <input value={node.timeoutPolicy.notificationTemplateCode} onChange={(event) => onChange((current) => ({ ...current, timeoutPolicy: { ...current.timeoutPolicy, notificationTemplateCode: event.target.value } }))} />
      </Field>
    </div>
  );
}

function NotificationPanel({ node, onChange }: NodePropertyPanelProps) {
  const { notificationTriggerOptions, participantOptions, translate } = useWorkflowBusinessText();
  const updateRule = (ruleId: string, updater: (rule: WorkflowNotificationRule) => WorkflowNotificationRule) => {
    onChange((current) => ({
      ...current,
      notificationRules: current.notificationRules.map((rule) => rule.id === ruleId ? updater(rule) : rule)
    }));
  };

  const addRule = (trigger: NotificationTrigger) => {
    onChange((current) => ({
      ...current,
      notificationRules: [...current.notificationRules, createNotificationRule(trigger, translate)]
    }));
  };

  const toggleTrigger = (trigger: NotificationTrigger, enabled: boolean) => {
    onChange((current) => {
      const triggerRules = current.notificationRules.filter((rule) => rule.trigger === trigger);
      if (triggerRules.length === 0 && enabled) {
        return {
          ...current,
          notificationRules: [...current.notificationRules, createNotificationRule(trigger, translate)]
        };
      }

      return {
        ...current,
        notificationRules: current.notificationRules.map((rule) => rule.trigger === trigger ? { ...rule, enabled } : rule)
      };
    });
  };

  return (
    <div className="workflow-property-grid">
      <div className="workflow-row-toolbar">
        <span>{translate('workflowBusiness.notification.sectionTitle')}</span>
        <span className="workflow-row-toolbar__meta">{translate('workflowBusiness.notification.enabledCount').replace('{count}', String(node.notificationRules.filter((rule) => rule.enabled).length))}</span>
      </div>
      <div className="workflow-notification-trigger-list">
        {notificationTriggerOptions.map((trigger) => {
          const rules = node.notificationRules.filter((rule) => rule.trigger === trigger.value);
          const enabled = rules.some((rule) => rule.enabled);
          return (
            <section key={trigger.value} className={`workflow-notification-trigger${enabled ? ' workflow-notification-trigger--enabled' : ''}`}>
              <div className="workflow-notification-trigger__summary">
                <label className="workflow-notification-trigger__toggle">
                  <input checked={enabled} type="checkbox" onChange={(event) => toggleTrigger(trigger.value, event.target.checked)} />
                  <span className="workflow-notification-trigger__title">
                    <strong>{trigger.label}</strong>
                    <span>{renderWorkflowBusinessTriggerSummary(rules, trigger.hint, translate)}</span>
                  </span>
                </label>
                <button title={translate('workflowBusiness.notification.addRule').replace('{trigger}', trigger.label)} type="button" onClick={() => addRule(trigger.value)}>
                  <AppIcon name="plus" />
                </button>
              </div>
              {rules.length === 0 ? (
                <div className="workflow-notification-empty">{translate('workflowBusiness.notification.empty')}</div>
              ) : (
                <div className="workflow-notification-trigger__rules">
                  {rules.map((rule, index) => (
                    <div key={rule.id} className="workflow-notification-rule-editor">
                      <div className="workflow-notification-rule-editor__head">
                        <label>
                          <input checked={rule.enabled} type="checkbox" onChange={(event) => updateRule(rule.id, (current) => ({ ...current, enabled: event.target.checked }))} />
                          <span>{translate('workflowBusiness.notification.ruleIndex').replace('{index}', String(index + 1))}</span>
                        </label>
                        <button title={translate('workflowBusiness.notification.deleteRule')} type="button" onClick={() => onChange((current) => ({ ...current, notificationRules: current.notificationRules.filter((item) => item.id !== rule.id) }))}>
                          <AppIcon name="x" />
                        </button>
                      </div>
                      <div className="workflow-notification-rule-form">
                        <Field label={translate('workflowBusiness.notification.receiverType')}>
                          <select value={rule.receiverType} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, receiverType: event.target.value as WorkflowNotificationRule['receiverType'] }))}>
                            {participantOptions.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                          </select>
                        </Field>
                        <Field label={translate('workflowBusiness.notification.receiverValue')}>
                          <input placeholder={translate('workflowBusiness.notification.receiverValuePlaceholder')} value={rule.receiverValue} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, receiverValue: event.target.value }))} />
                        </Field>
                        <Field label={translate('workflowBusiness.notification.channelCodes')}>
                          <input placeholder={translate('workflowBusiness.notification.channelCodesPlaceholder')} value={rule.channelCodes.join(',')} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, channelCodes: splitCsv(event.target.value) }))} />
                        </Field>
                        <Field label={translate('workflowBusiness.notification.templateCode')}>
                          <input placeholder={translate('workflowBusiness.notification.templateCodePlaceholder')} value={rule.templateCode} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, templateCode: event.target.value }))} />
                        </Field>
                        <Field label={translate('workflowBusiness.notification.failurePolicy')}>
                          <select value={rule.failurePolicy} onChange={(event) => updateRule(rule.id, (current) => ({ ...current, failurePolicy: event.target.value as WorkflowNotificationRule['failurePolicy'] }))}>
                            <option value="ignore">{translate('workflowBusiness.notification.failurePolicy.ignore')}</option>
                            <option value="block">{translate('workflowBusiness.notification.failurePolicy.block')}</option>
                          </select>
                        </Field>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </section>
          );
        })}
      </div>
    </div>
  );
}

function VariablePanel({ formFields, node, onChange }: NodePropertyPanelProps) {
  const { translate } = useWorkflowBusinessText();
  const updateMapping = (mappingId: string, updater: (mapping: WorkflowVariableMapping) => WorkflowVariableMapping) => {
    onChange((current) => ({
      ...current,
      variableMappingRows: current.variableMappingRows.map((mapping) => mapping.id === mappingId ? updater(mapping) : mapping),
      variableMappings: JSON.stringify(current.variableMappingRows.map((mapping) => mapping.id === mappingId ? updater(mapping) : mapping))
    }));
  };

  return (
    <div className="workflow-property-grid">
      <div className="workflow-row-toolbar">
        <span>{translate('workflowBusiness.variable.mappingTitle')}</span>
        <button type="button" onClick={() => onChange((current) => ({ ...current, variableMappingRows: [...current.variableMappingRows, createVariableMapping(formFields[0])] }))}>
          <AppIcon name="plus" />
        </button>
      </div>
      {node.variableMappingRows.map((mapping) => (
        <div key={mapping.id} className="workflow-rule-row">
          {formFields.length > 0 ? (
            <FormFieldSelect
              fields={formFields}
              value={mapping.sourcePath}
              onSelect={(field) => updateMapping(mapping.id, (current) => ({
                ...current,
                sourcePath: field?.fieldCode ?? current.sourcePath,
                valueType: field?.dataType ?? current.valueType
              }))}
            />
          ) : (
            <input placeholder={translate('workflowBusiness.variable.sourcePath')} value={mapping.sourcePath} onChange={(event) => updateMapping(mapping.id, (current) => ({ ...current, sourcePath: event.target.value }))} />
          )}
          <input placeholder={translate('workflowBusiness.variable.targetPath')} value={mapping.targetPath} onChange={(event) => updateMapping(mapping.id, (current) => ({ ...current, targetPath: event.target.value }))} />
          <select value={mapping.direction} onChange={(event) => updateMapping(mapping.id, (current) => ({ ...current, direction: event.target.value as WorkflowVariableMapping['direction'] }))}>
            <option value="input">{translate('workflowBusiness.variable.input')}</option>
            <option value="output">{translate('workflowBusiness.variable.output')}</option>
            <option value="both">{translate('workflowBusiness.variable.both')}</option>
          </select>
          <input placeholder={translate('workflowBusiness.variable.valueType')} value={mapping.valueType} onChange={(event) => updateMapping(mapping.id, (current) => ({ ...current, valueType: event.target.value }))} />
          <button title={translate('workflowBusiness.variable.delete')} type="button" onClick={() => onChange((current) => ({ ...current, variableMappingRows: current.variableMappingRows.filter((item) => item.id !== mapping.id) }))}>
            <AppIcon name="x" />
          </button>
        </div>
      ))}
    </div>
  );
}

function SubProcessPanel({ node, onChange }: NodePropertyPanelProps) {
  const { translate } = useWorkflowBusinessText();
  return (
    <div className="workflow-property-grid">
      <Field label={translate('workflowBusiness.subprocess.calledElement')}>
        <input value={node.subProcessConfig.calledElement} onChange={(event) => onChange((current) => ({ ...current, subprocessKey: event.target.value, subProcessConfig: { ...current.subProcessConfig, calledElement: event.target.value } }))} />
      </Field>
      <Field label={translate('workflowBusiness.subprocess.businessKeyExpression')}>
        <input value={node.subProcessConfig.businessKeyExpression} onChange={(event) => onChange((current) => ({ ...current, subProcessConfig: { ...current.subProcessConfig, businessKeyExpression: event.target.value } }))} />
      </Field>
      <label className="workflow-inline-check">
        <input checked={node.subProcessConfig.inheritVariables} type="checkbox" onChange={(event) => onChange((current) => ({ ...current, subProcessConfig: { ...current.subProcessConfig, inheritVariables: event.target.checked } }))} />
        <span>{translate('workflowBusiness.subprocess.inheritVariables')}</span>
      </label>
    </div>
  );
}

function NativePanel({ node, onChange }: NodePropertyPanelProps) {
  const { translate } = useWorkflowBusinessText();
  return (
    <div className="workflow-property-grid">
      <Field label={translate('workflowBusiness.native.extensionJson')}>
        <textarea rows={8} value={node.nativeExtensions} onChange={(event) => onChange((current) => ({ ...current, nativeExtensions: event.target.value }))} />
      </Field>
      <div className="workflow-property-hint">{translate('workflowBusiness.native.hint')}</div>
    </div>
  );
}

function Field({ children, label }: { children: ReactNode; label: string }) {
  return (
    <label className="workflow-property-field">
      <span>{label}</span>
      {children}
    </label>
  );
}

function FormFieldSelect({
  fields,
  onSelect,
  value
}: {
  fields: WorkflowBusinessFormField[];
  onSelect: (field?: WorkflowBusinessFormField) => void;
  value: string;
}) {
  const selected = fields.find((field) =>
    field.fieldCode === value ||
    field.binding === value
  );
  const { translate } = useWorkflowBusinessText();

  return (
    <select value={selected?.fieldCode ?? ''} onChange={(event) => onSelect(fields.find((field) => field.fieldCode === event.target.value))}>
      <option value="">{translate('workflowBusiness.form.manualInput')}</option>
      {fields.map((field) => (
        <option key={field.fieldCode} value={field.fieldCode}>
          {field.fieldName} ({field.fieldCode})
        </option>
      ))}
    </select>
  );
}

export function renderNodeHint(node: WorkflowBusinessNode, translate: TranslateFn = (key) => key): string {
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
  return node.type === 'subprocess' && node.subProcessConfig.calledElement ? node.subProcessConfig.calledElement : translate('workflowBusiness.hint.unconfigured');
}

function createConditionRule(field?: WorkflowBusinessFormField, translate: TranslateFn = (key) => key): WorkflowConditionRule {
  return {
    fieldKey: field?.fieldCode ?? 'amount',
    fieldLabel: field?.fieldName ?? translate('workflowBusiness.default.amount'),
    fieldSource: 'form',
    id: `condition_${Date.now().toString(36)}`,
    logical: 'and',
    operator: 'gte',
    value: '0'
  };
}

function createFieldPermission(field?: WorkflowBusinessFormField, translate: TranslateFn = (key) => key): WorkflowFieldPermission {
  return {
    fieldKey: field?.fieldCode ?? 'amount',
    fieldLabel: field?.fieldName ?? translate('workflowBusiness.default.amount'),
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

function createNotificationRule(trigger: NotificationTrigger = 'node-enter', translate: TranslateFn = (key) => key): WorkflowNotificationRule {
  return {
    channelCodes: ['in-app'],
    conditionSummary: '',
    enabled: true,
    failurePolicy: 'ignore',
    id: `notice_${trigger}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`,
    receiverName: translate('workflowBusiness.default.currentApprover'),
    receiverType: 'approver',
    receiverValue: '',
    templateCode: 'workflow-node-enter',
    trigger
  };
}

function createVariableMapping(field?: WorkflowBusinessFormField): WorkflowVariableMapping {
  return {
    direction: 'input',
    id: `var_${Date.now().toString(36)}`,
    sourcePath: field?.fieldCode ?? 'businessKey',
    targetPath: field?.fieldCode ?? 'businessKey',
    valueType: field?.dataType ?? 'string'
  };
}

function buildConditionExpression(rules: WorkflowConditionRule[]): string {
  return rules
    .filter((rule) => rule.fieldKey)
    .map((rule, index) => `${index > 0 ? rule.logical.toUpperCase() + ' ' : ''}${renderCondition(rule)}`)
    .join(' ');
}

function renderCondition(rule: WorkflowConditionRule): string {
  const field = `${rule.fieldSource}.${rule.fieldKey}`;
  if (rule.operator === 'empty') {
    return `${field} == null`;
  }
  if (rule.operator === 'notEmpty') {
    return `${field} != null`;
  }
  if (rule.operator === 'range') {
    return `${field} >= ${quoteValue(rule.value)} && ${field} <= ${quoteValue(rule.valueEnd ?? '')}`;
  }
  const operatorMap: Record<Exclude<ConditionOperator, 'empty' | 'notEmpty' | 'range'>, string> = {
    contains: 'contains',
    eq: '==',
    gt: '>',
    gte: '>=',
    lt: '<',
    lte: '<=',
    ne: '!='
  };
  if (rule.operator === 'contains') {
    return `${field}.contains(${quoteValue(rule.value)})`;
  }
  return `${field} ${operatorMap[rule.operator]} ${quoteValue(rule.value)}`;
}

function quoteValue(value: string): string {
  return /^-?\d+(\.\d+)?$/.test(value) ? value : `'${value.replace(/'/g, "\\'")}'`;
}

function splitCsv(value: string): string[] {
  return value.split(',').map((item) => item.trim()).filter(Boolean);
}
