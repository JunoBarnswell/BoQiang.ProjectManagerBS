import { Link2 } from 'lucide-react';

import type { AiWorkflowDraftArtifactDto } from '.././api/aiCenter.api';
import { useI18n } from '../../../core/i18n/I18nProvider';

import { parseJsonValue, stringifyCompact } from './workflowUtils';

export function WorkflowBindingProposalPanel({ artifact }: { artifact: AiWorkflowDraftArtifactDto }) {
  const { translate } = useI18n();
  const binding = parseJsonValue<Record<string, unknown> | null>(artifact.bindingProposalJson, null);
  const permissions = parseJsonValue<unknown[] | null>(artifact.formPermissionProposalJson, null);
  const actions = parseJsonValue<unknown[] | null>(artifact.actionMappingProposalJson, null);

  return (
    <div className="ai-workflow-panel">
      <header>
        <Link2 size={15} />
        <strong>{translate('ai.workflowSupport.bindingProposal.title')}</strong>
      </header>
      {binding ? <KeyValueList value={binding} /> : <div className="ai-empty-state">{translate('ai.workflowSupport.bindingProposal.empty')}</div>}
      {permissions ? <JsonPreview title={translate('ai.workflowSupport.bindingProposal.formPermissions')} value={permissions} /> : null}
      {actions ? <JsonPreview title={translate('ai.workflowSupport.bindingProposal.actionMapping')} value={actions} /> : null}
    </div>
  );
}

function KeyValueList({ value }: { value: Record<string, unknown> }) {
  return (
    <dl className="ai-workflow-kv">
      {Object.entries(value).map(([key, item]) => (
        <div key={key}>
          <dt>{key}</dt>
          <dd>{stringifyCompact(item)}</dd>
        </div>
      ))}
    </dl>
  );
}

function JsonPreview({ title, value }: { title: string; value: unknown }) {
  return (
    <details className="ai-workflow-json">
      <summary>{title}</summary>
      <pre>{JSON.stringify(value, null, 2)}</pre>
    </details>
  );
}
