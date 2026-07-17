import { FileCode2, GitBranch } from 'lucide-react';

import type {
  AiWorkflowDraftArtifactDto,
  AiWorkflowSimulationReportDto,
  AiWorkflowValidationReportDto
} from '.././api/aiCenter.api';
import { useI18n } from '../../../core/i18n/I18nProvider';

import { WorkflowBindingProposalPanel } from './WorkflowBindingProposalPanel';
import { WorkflowSimulationPanel } from './WorkflowSimulationPanel';
import { formatWorkflowTime, parseJsonValue } from './workflowUtils';
import { WorkflowValidationPanel } from './WorkflowValidationPanel';

interface WorkflowDraftPreviewCardProps {
  artifact: AiWorkflowDraftArtifactDto;
  simulation?: AiWorkflowSimulationReportDto | null;
  validation?: AiWorkflowValidationReportDto | null;
}

interface DraftDsl {
  edges?: Array<{ condition?: string | null; id: string; name?: string; sourceId: string; targetId: string }>;
  nodes?: Array<{ id: string; name: string; type: string }>;
}

export function WorkflowDraftPreviewCard({
  artifact,
  simulation,
  validation
}: WorkflowDraftPreviewCardProps) {
  const { translate } = useI18n();
  const draft = parseJsonValue<DraftDsl>(artifact.draftDslJson, {});
  const nodes = draft.nodes ?? [];
  const edges = draft.edges ?? [];

  return (
    <article className="ai-workflow-draft-card">
      <header>
        <div>
          <strong>{artifact.workflowName}</strong>
          <span>{artifact.workflowKey} / {artifact.businessType}</span>
        </div>
        <small>{artifact.status}</small>
      </header>
      <div className="ai-workflow-draft-meta">
        <span>{nodes.length} {translate('ai.workflowSupport.draftPreview.nodes')}</span>
        <span>{edges.length} {translate('ai.workflowSupport.draftPreview.edges')}</span>
        <span>{formatWorkflowTime(artifact.updatedTime ?? artifact.createdTime)}</span>
      </div>
      <div className="ai-workflow-node-list">
        {nodes.map((node) => (
          <span key={node.id}>{node.name}<small>{node.type}</small></span>
        ))}
      </div>
      <details className="ai-workflow-json">
        <summary>
          <FileCode2 size={14} />
          {translate('ai.workflowSupport.draftPreview.bpmnXml')}
        </summary>
        <pre>{artifact.bpmnXml ?? translate('ai.workflowSupport.draftPreview.notGenerated')}</pre>
      </details>
      <details className="ai-workflow-json">
        <summary>
          <GitBranch size={14} />
          {translate('ai.workflowSupport.draftPreview.businessCanvas')}
        </summary>
        <pre>{artifact.businessCanvasJson ? JSON.stringify(parseJsonValue(artifact.businessCanvasJson, {}), null, 2) : translate('ai.workflowSupport.draftPreview.notGenerated')}</pre>
      </details>
      <WorkflowValidationPanel report={validation} />
      <WorkflowSimulationPanel report={simulation} />
      <WorkflowBindingProposalPanel artifact={artifact} />
    </article>
  );
}
