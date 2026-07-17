import { useEffect, useMemo, useState } from 'react';

import type { WorkflowBindingUpsertRequest, WorkflowProcessDefinitionDto } from '../../../api/workflow/workflows.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { useMessage } from '../../../shared/feedback/useMessage';

import type { WorkflowBusinessDesign } from './workflowBusinessModel';

interface WorkflowModelBindingPanelProps {
  appCode?: string | null;
  definitions: WorkflowProcessDefinitionDto[];
  formContext: WorkflowBusinessDesign['formContext'];
  modelId: string;
  modelKey: string;
  modelName: string;
  saving: boolean;
  tenantId?: string | null;
  onSave: (request: WorkflowBindingUpsertRequest) => Promise<unknown>;
}

export function WorkflowModelBindingPanel({
  appCode,
  definitions,
  formContext,
  modelId,
  modelKey,
  modelName,
  onSave,
  saving,
  tenantId
}: WorkflowModelBindingPanelProps) {
  const { translate } = useI18n();
  const message = useMessage();
  const [selectedDefinitionId, setSelectedDefinitionId] = useState('');
  const [titleTemplate, setTitleTemplate] = useState(`${modelName} - \${businessKey}`);
  const [enabled, setEnabled] = useState(true);
  const releasedDefinitions = useMemo(
    () => definitions.filter((item) => item.key === modelKey && !item.isSuspended),
    [definitions, modelKey]
  );
  const selectedDefinition = releasedDefinitions.find((item) => item.id === selectedDefinitionId) ?? releasedDefinitions[0];

  useEffect(() => {
    if (!selectedDefinitionId && releasedDefinitions[0]?.id) {
      setSelectedDefinitionId(releasedDefinitions[0].id);
    }
  }, [releasedDefinitions, selectedDefinitionId]);

  const saveBinding = async () => {
    if (!tenantId || !appCode) {
      message.error(translate('workflow.modelDesigner.binding.missingWorkspace'));
      return;
    }
    if (!formContext) {
      message.error(translate('workflow.modelDesigner.binding.missingForm'));
      return;
    }
    if (!selectedDefinition?.id || !selectedDefinition.key) {
      message.error(translate('workflow.modelDesigner.binding.missingDefinition'));
      return;
    }

    await onSave({
      appCode,
      businessType: formContext.businessType,
      detailRoute: formContext.routePath ?? null,
      formResourceCode: formContext.resourceCode,
      isEnabled: enabled,
      keyField: formContext.keyField,
      menuCode: formContext.menuCode,
      modelCode: formContext.modelCode,
      modelId,
      modelKey,
      pageCode: formContext.pageCode,
      processDefinitionId: selectedDefinition.id,
      processDefinitionKey: selectedDefinition.key,
      remark: null,
      startFormJson: null,
      tenantId,
      titleTemplate
    });
  };

  return (
    <div className="workflow-designer-panel">
      <div className="workflow-panel-title">{translate('workflow.modelDesigner.binding.title')}</div>
      <div className="workflow-panel-subtitle">{translate('workflow.modelDesigner.binding.subtitle')}</div>

      <div className="workflow-property-grid mt-3">
        <label className="workflow-property-field">
          <span>{translate('workflow.modelDesigner.binding.formResource')}</span>
          <input readOnly value={formContext ? `${formContext.resourceName} / ${formContext.modelCode}` : translate('workflow.modelDesigner.binding.noForm')} />
        </label>
        <label className="workflow-property-field">
          <span>{translate('workflow.modelDesigner.binding.version')}</span>
          <select value={selectedDefinition?.id ?? ''} onChange={(event) => setSelectedDefinitionId(event.target.value)}>
            <option value="">{translate('workflow.modelDesigner.binding.noDefinition')}</option>
            {releasedDefinitions.map((definition) => (
              <option key={definition.id} value={definition.id}>
                v{definition.version} / {definition.id}
              </option>
            ))}
          </select>
        </label>
        <label className="workflow-property-field">
          <span>{translate('workflow.modelDesigner.binding.titleTemplate')}</span>
          <input value={titleTemplate} onChange={(event) => setTitleTemplate(event.target.value)} />
        </label>
        <label className="workflow-inline-check">
          <input checked={enabled} type="checkbox" onChange={(event) => setEnabled(event.target.checked)} />
          <span>{translate('workflow.modelDesigner.binding.enabled')}</span>
        </label>
        <button className="workflow-primary-action" disabled={saving} type="button" onClick={() => void saveBinding()}>
          {saving ? translate('workflow.modelDesigner.binding.saving') : translate('workflow.modelDesigner.binding.save')}
        </button>
      </div>
    </div>
  );
}
