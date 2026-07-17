import { RefreshCw, Workflow } from 'lucide-react';

import type {
  AiWorkflowOverviewDto
} from '.././api/aiCenter.api';
import { useI18n } from '../../../core/i18n/I18nProvider';

import { WorkflowDraftPreviewCard } from './WorkflowDraftPreviewCard';

interface WorkflowToolInspectorSectionProps {
  loading?: boolean;
  overview?: AiWorkflowOverviewDto | null;
  onRefresh: () => void;
}

export function WorkflowToolInspectorSection({
  loading,
  overview,
  onRefresh
}: WorkflowToolInspectorSectionProps) {
  const { translate } = useI18n();
  const drafts = overview?.draftArtifacts ?? [];
  const latestDraft = drafts[0] ?? null;
  const validation = latestDraft
    ? overview?.validationReports.find((item) => item.draftArtifactId === latestDraft.id)
    : null;
  const simulation = latestDraft
    ? overview?.simulationReports.find((item) => item.draftArtifactId === latestDraft.id)
    : null;

  return (
    <section className="ai-workflow-section">
      <header className="ai-workflow-section__header">
        <h3>
          <Workflow size={16} />
          {translate('ai.workflowSupport.toolInspector.title')}
        </h3>
        <button className="icon-button" title={translate('ai.workflowSupport.toolInspector.refresh')} type="button" onClick={onRefresh}>
          <RefreshCw size={14} />
        </button>
      </header>
      <div className="ai-workflow-summary">
        <article><strong>{drafts.length}</strong><span>{translate('ai.workflowSupport.toolInspector.drafts')}</span></article>
        <article><strong>{overview?.validationReports.length ?? 0}</strong><span>{translate('ai.workflowSupport.toolInspector.validation')}</span></article>
        <article><strong>{overview?.simulationReports.length ?? 0}</strong><span>{translate('ai.workflowSupport.toolInspector.simulation')}</span></article>
      </div>
      {latestDraft ? (
        <WorkflowDraftPreviewCard
          artifact={latestDraft}
          simulation={simulation}
          validation={validation}
        />
      ) : (
        <div className="ai-empty-state">{loading ? translate('ai.workflowSupport.toolInspector.loading') : translate('ai.workflowSupport.toolInspector.empty')}</div>
      )}
    </section>
  );
}
