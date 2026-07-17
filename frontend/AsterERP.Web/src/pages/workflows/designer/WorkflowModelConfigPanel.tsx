import { useMemo } from 'react';

import type { WorkflowFormResourceDto } from '../../../api/workflow/workflows.api';
import { useI18n } from '../../../core/i18n/I18nProvider';

import type { WorkflowBusinessDesign } from './workflowBusinessModel';

interface WorkflowModelConfigPanelProps {
  businessDesign: WorkflowBusinessDesign;
  formResources: WorkflowFormResourceDto[];
  onSelectFormContext: (resourceCode: string) => void;
}

export function WorkflowModelConfigPanel({
  businessDesign,
  formResources,
  onSelectFormContext
}: WorkflowModelConfigPanelProps) {
  const { translate } = useI18n();
  const approvalNodes = useMemo(
    () => businessDesign.nodes.filter((node) => node.type === 'approval'),
    [businessDesign.nodes]
  );
  const enabledActions = useMemo(
    () => approvalNodes.flatMap((node) => node.actionPolicies.filter((policy) => policy.enabled).map((policy) => policy.label)),
    [approvalNodes]
  );
  const notificationCount = useMemo(
    () => approvalNodes.reduce((count, node) => count + node.notificationRules.filter((rule) => rule.enabled).length, 0),
    [approvalNodes]
  );

  return (
    <div className="workflow-designer-panel workflow-model-config-panel">
      <div className="workflow-panel-title">{translate('workflow.modelDesigner.config.title')}</div>
      <div className="workflow-panel-subtitle">{translate('workflow.modelDesigner.config.subtitle')}</div>

      <div className="workflow-property-grid mt-3">
        <label className="workflow-property-field">
          <span>{translate('workflow.modelDesigner.config.formResource')}</span>
          <select value={businessDesign.formContext?.resourceCode ?? ''} onChange={(event) => onSelectFormContext(event.target.value)}>
            <option value="">{translate('page.workflowDesigner.form.placeholder')}</option>
            {formResources.map((resource) => (
              <option key={resource.resourceCode} value={resource.resourceCode}>
                {resource.resourceName} ({resource.modelCode})
              </option>
            ))}
          </select>
        </label>

        <div className="workflow-model-config-grid">
          <ConfigMetric label={translate('workflow.modelDesigner.config.approvalNodes')} value={approvalNodes.length} />
          <ConfigMetric label={translate('workflow.modelDesigner.config.enabledActions')} value={Array.from(new Set(enabledActions)).length} />
          <ConfigMetric label={translate('workflow.modelDesigner.config.notifications')} value={notificationCount} />
        </div>

        <details className="workflow-config-details" open>
          <summary>{translate('workflow.modelDesigner.config.formContext')}</summary>
          <div className="workflow-config-kv">
            <span>{translate('workflow.modelDesigner.config.menuCode')}</span>
            <strong>{businessDesign.formContext?.menuCode ?? '-'}</strong>
            <span>{translate('workflow.modelDesigner.config.businessType')}</span>
            <strong>{businessDesign.formContext?.businessType ?? '-'}</strong>
            <span>{translate('workflow.modelDesigner.config.keyField')}</span>
            <strong>{businessDesign.formContext?.keyField ?? '-'}</strong>
          </div>
        </details>

        <details className="workflow-config-details">
          <summary>{translate('workflow.modelDesigner.config.actionPolicy')}</summary>
          <div className="workflow-config-chip-list">
            {Array.from(new Set(enabledActions)).map((action) => <span key={action}>{action}</span>)}
            {enabledActions.length === 0 ? <em>{translate('workflow.modelDesigner.config.noActions')}</em> : null}
          </div>
        </details>
      </div>
    </div>
  );
}

function ConfigMetric({ label, value }: { label: string; value: number }) {
  return (
    <div className="workflow-summary-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
