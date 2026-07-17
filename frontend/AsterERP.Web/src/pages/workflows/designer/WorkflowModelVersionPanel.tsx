import type { WorkflowModelDetailDto, WorkflowProcessDefinitionDto } from '../../../api/workflow/workflows.api';
import { useI18n } from '../../../core/i18n/I18nProvider';

interface WorkflowModelVersionPanelProps {
  definitions: WorkflowProcessDefinitionDto[];
  model?: WorkflowModelDetailDto;
  publishing: boolean;
  validationErrors: string[];
  onPublish: () => void;
  onRollback: (definition: WorkflowProcessDefinitionDto) => void;
}

export function WorkflowModelVersionPanel({
  definitions,
  model,
  onPublish,
  publishing,
  validationErrors,
  onRollback
}: WorkflowModelVersionPanelProps) {
  const { translate } = useI18n();

  return (
    <div className="workflow-designer-panel">
      <div className="workflow-panel-header">
        <div>
          <div className="workflow-panel-title">{translate('workflow.modelDesigner.version.title')}</div>
          <div className="workflow-panel-subtitle">{model?.modelKey ?? '-'}</div>
        </div>
        <button className="workflow-primary-action" disabled={publishing} type="button" onClick={onPublish}>
          {publishing ? translate('workflow.modelDesigner.version.publishing') : translate('page.workflowDesigner.action.publish')}
        </button>
      </div>

      <div className="workflow-version-list">
        {definitions.length === 0 ? (
          <div className="workflow-config-empty">{translate('workflow.modelDesigner.version.empty')}</div>
        ) : definitions.map((definition) => (
          <div key={definition.id} className="workflow-version-item">
            <div>
              <strong>v{definition.version}</strong>
              <span>{definition.id}</span>
            </div>
            <em className={definition.isSuspended ? 'workflow-status-danger' : 'workflow-status-success'}>
              {definition.isSuspended ? translate('workflow.modelDesigner.version.suspended') : translate('workflow.modelDesigner.version.active')}
            </em>
            <button className="rounded border border-gray-300 px-2 py-1 text-xs" type="button" onClick={() => onRollback(definition)}>
              {translate('workflow.modelDesigner.version.rollback')}
            </button>
          </div>
        ))}
      </div>

      {validationErrors.length > 0 ? (
        <div className="mt-4 border border-red-200 bg-red-50 text-red-700 rounded p-3 text-sm grid gap-1">
          {validationErrors.map((error) => <div key={error}>{error}</div>)}
        </div>
      ) : null}
    </div>
  );
}
