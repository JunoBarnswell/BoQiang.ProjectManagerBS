import type { WorkflowParticipantDto } from '../../../api/workflow/workflows.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';

import {
  createWorkflowBusinessParticipantOptions,
  renderWorkflowBusinessModeLabel
} from './workflowBusinessI18n';
import {
  updateNodeParticipant,
  type ApprovalMode,
  type ParticipantType,
  type WorkflowBusinessFormField,
  type WorkflowBusinessNode
} from './workflowBusinessModel';

interface WorkflowParticipantSelectorProps {
  node: WorkflowBusinessNode;
  formFields: WorkflowBusinessFormField[];
  participants: WorkflowParticipantDto[];
  participantKeyword: string;
  onParticipantKeywordChange: (keyword: string) => void;
  onChange: (updater: (node: WorkflowBusinessNode) => WorkflowBusinessNode) => void;
}

const DirectoryParticipantTypes: ParticipantType[] = ['user', 'role', 'department', 'position'];

export function WorkflowParticipantSelector({
  formFields,
  node,
  onChange,
  onParticipantKeywordChange,
  participantKeyword,
  participants
}: WorkflowParticipantSelectorProps) {
  const { translate } = useI18n();
  const participantOptions = createWorkflowBusinessParticipantOptions(translate)
    .filter((item) => item.value !== 'approver');
  const isDirectoryType = DirectoryParticipantTypes.includes(node.participantType);
  const selectedParticipant = participants.find((item) => item.id === node.participantId);
  const selectedNames = node.participantNames.length > 0
    ? node.participantNames
    : node.participantName ? [node.participantName] : [];

  return (
    <div className="workflow-participant-selector">
      <label className="workflow-property-field">
        <span>{translate('workflowBusiness.field.approver')}</span>
        <select value={node.participantType} onChange={(event) => onChange((current) => resetParticipantType(current, event.target.value as ParticipantType))}>
          {participantOptions.map((item) => (
            <option key={item.value} value={item.value}>{item.label}</option>
          ))}
        </select>
      </label>

      <label className="workflow-property-field">
        <span>{translate('workflowBusiness.field.approvalMode')}</span>
        <select value={node.approvalMode} onChange={(event) => onChange((current) => ({ ...current, approvalMode: event.target.value as ApprovalMode }))}>
          <option value="all">{renderWorkflowBusinessModeLabel('all', translate)}</option>
          <option value="any">{renderWorkflowBusinessModeLabel('any', translate)}</option>
        </select>
      </label>

      {isDirectoryType ? (
        <>
          <label className="workflow-property-field">
            <span>{translate('workflowBusiness.field.search')}</span>
            <input value={participantKeyword} onChange={(event) => onParticipantKeywordChange(event.target.value)} />
          </label>
          <label className="workflow-property-field">
            <span>{translate('workflowBusiness.field.candidate')}</span>
            <select value={node.participantId} onChange={(event) => onChange((current) => updateNodeParticipant(current, participants.find((item) => item.id === event.target.value)))}>
              <option value="">{translate('common.selectOne')}</option>
              {participants.map((participant) => (
                <option key={`${participant.type}:${participant.id}`} value={participant.id}>
                  {renderParticipantOption(participant)}
                </option>
              ))}
            </select>
          </label>
          {selectedNames.length > 0 ? (
            <div className="workflow-participant-chips">
              {selectedNames.map((name) => <span key={name}>{name}</span>)}
              <button type="button" onClick={() => onChange(clearParticipants)}>
                <AppIcon name="x" />
              </button>
            </div>
          ) : null}
          <div className="workflow-property-hint">{selectedParticipant?.employmentSummary ?? selectedParticipant?.description ?? node.groupKey ?? node.participantName ?? translate('workflowBusiness.hint.dynamicParticipant')}</div>
        </>
      ) : null}

      {node.participantType === 'formField' ? (
        <label className="workflow-property-field">
          <span>{translate('workflowBusiness.participant.formField')}</span>
          <select value={node.participantFieldKey} onChange={(event) => onChange((current) => ({ ...current, participantFieldKey: event.target.value, participantExpression: event.target.value ? `\${${event.target.value}}` : '' }))}>
            <option value="">{translate('workflowBusiness.form.manualInput')}</option>
            {formFields.map((field) => (
              <option key={field.fieldCode} value={field.fieldCode}>{field.fieldName} ({field.fieldCode})</option>
            ))}
          </select>
        </label>
      ) : null}

      {node.participantType === 'dynamic' ? (
        <label className="workflow-property-field">
          <span>{translate('workflowBusiness.participant.dynamic')}</span>
          <input
            placeholder="${customApproverUserId}"
            value={node.participantExpression}
            onChange={(event) => onChange((current) => ({ ...current, participantExpression: event.target.value }))}
          />
        </label>
      ) : null}

      {isBuiltInDynamic(node.participantType) ? (
        <div className="workflow-participant-static">
          <strong>{renderBuiltInDynamicTitle(node.participantType, translate)}</strong>
          <span>{renderBuiltInDynamicExpression(node.participantType)}</span>
        </div>
      ) : null}
    </div>
  );
}

function renderParticipantOption(participant: WorkflowParticipantDto): string {
  const summary = participant.type === 'user' && participant.employmentSummary ? ` - ${participant.employmentSummary}` : '';
  return `${participant.name} (${participant.code})${summary}`;
}

function resetParticipantType(node: WorkflowBusinessNode, participantType: ParticipantType): WorkflowBusinessNode {
  return {
    ...node,
    groupKey: null,
    participantCode: '',
    participantExpression: '',
    participantFieldKey: '',
    participantId: '',
    participantIds: [],
    participantName: '',
    participantNames: [],
    participantType
  };
}

function clearParticipants(node: WorkflowBusinessNode): WorkflowBusinessNode {
  return {
    ...node,
    groupKey: null,
    participantCode: '',
    participantId: '',
    participantIds: [],
    participantName: '',
    participantNames: []
  };
}

function isBuiltInDynamic(type: ParticipantType): boolean {
  return type === 'starter' ||
    type === 'starterManager' ||
    type === 'manager' ||
    type === 'deptManager' ||
    type === 'previousApprover';
}

function renderBuiltInDynamicTitle(type: ParticipantType, translate: (key: string) => string): string {
  if (type === 'starter') return translate('workflowBusiness.participant.starter');
  if (type === 'starterManager' || type === 'manager') return translate('workflowBusiness.participant.starterManager');
  if (type === 'deptManager') return translate('workflowBusiness.participant.deptManager');
  return translate('workflowBusiness.participant.previousApprover');
}

function renderBuiltInDynamicExpression(type: ParticipantType): string {
  if (type === 'starter') return '${starterUserId}';
  if (type === 'starterManager' || type === 'manager') return '${starterManagerUserId}';
  if (type === 'deptManager') return '${starterDeptManagerUserIds}';
  return '${previousApproverUserId}';
}
