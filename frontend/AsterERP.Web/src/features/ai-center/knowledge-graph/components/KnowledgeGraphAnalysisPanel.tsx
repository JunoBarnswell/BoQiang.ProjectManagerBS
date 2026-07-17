import type { ReactNode } from 'react';

import { formatMessage } from '../../../../core/i18n/formatMessage';
import { useI18n } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import type {
  KnowledgeGraphImpactDraft,
  KnowledgeGraphImpactView,
  KnowledgeGraphOption,
  KnowledgeGraphPathDraft,
  KnowledgeGraphPathView
} from '../types';

interface KnowledgeGraphAnalysisPanelProps {
  impactDraft: KnowledgeGraphImpactDraft;
  impactResults: KnowledgeGraphImpactView[];
  impactRunning: boolean;
  nodeOptions: KnowledgeGraphOption[];
  pathDraft: KnowledgeGraphPathDraft;
  pathResults: KnowledgeGraphPathView[];
  pathRunning: boolean;
  relationTypeOptions: KnowledgeGraphOption[];
  onImpactDraftChange: (patch: Partial<KnowledgeGraphImpactDraft>) => void;
  onPathDraftChange: (patch: Partial<KnowledgeGraphPathDraft>) => void;
  onRunImpact: () => void;
  onRunPath: () => void;
}

export function KnowledgeGraphAnalysisPanel({
  impactDraft,
  impactResults,
  impactRunning,
  nodeOptions,
  onImpactDraftChange,
  onPathDraftChange,
  onRunImpact,
  onRunPath,
  pathDraft,
  pathResults,
  pathRunning,
  relationTypeOptions
}: KnowledgeGraphAnalysisPanelProps) {
  const { translate } = useI18n();

  return (
    <section className="kg-side-section kg-analysis">
      <header className="kg-section-header kg-section-header--compact">
        <div>
          <h2>{translate('kg.analysis.title')}</h2>
          <span>{translate('kg.analysis.description')}</span>
        </div>
      </header>

      <div className="kg-analysis-card">
        <h3><AppIcon name="git-branch" />{translate('kg.analysis.path.title')}</h3>
        <label className="kg-field">
          <span>{translate('kg.analysis.path.source')}</span>
          <select value={pathDraft.sourceNodeId} onChange={(event) => onPathDraftChange({ sourceNodeId: event.target.value })}>
            <option value="">{translate('common.select')}</option>
            {nodeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </label>
        <label className="kg-field">
          <span>{translate('kg.analysis.path.target')}</span>
          <select value={pathDraft.targetNodeId} onChange={(event) => onPathDraftChange({ targetNodeId: event.target.value })}>
            <option value="">{translate('common.select')}</option>
            {nodeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </label>
        <div className="kg-two-fields">
          <label className="kg-field">
            <span>{translate('kg.analysis.path.relationType')}</span>
            <select value={pathDraft.relationType} onChange={(event) => onPathDraftChange({ relationType: event.target.value })}>
              <option value="">{translate('ai.search.statusAll')}</option>
              {relationTypeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </label>
          <label className="kg-field">
            <span>{translate('kg.analysis.path.maxDepth')}</span>
            <input min={1} max={8} type="number" value={pathDraft.maxDepth} onChange={(event) => onPathDraftChange({ maxDepth: Number(event.target.value) })} />
          </label>
        </div>
        <button className="primary-button" disabled={pathRunning} type="button" onClick={onRunPath}>
          <AppIcon name="search" />
          {translate('kg.analysis.path.run')}
        </button>
        <ResultList emptyText={translate('kg.analysis.path.empty')} isEmpty={pathResults.length === 0}>
          {pathResults.map((item) => (
            <article className="kg-analysis-result" key={item.id}>
              <strong>{item.summary}</strong>
              <span>{formatMessage(translate('kg.analysis.path.summary'), { edgeCount: item.edges.length, nodeCount: item.nodes.length, score: item.score })}</span>
              <small>{item.riskLevel}</small>
            </article>
          ))}
        </ResultList>
      </div>

      <div className="kg-analysis-card">
        <h3><AppIcon name="target" />{translate('kg.analysis.impact.title')}</h3>
        <label className="kg-field">
          <span>{translate('kg.analysis.impact.node')}</span>
          <select value={impactDraft.nodeId} onChange={(event) => onImpactDraftChange({ nodeId: event.target.value })}>
            <option value="">{translate('common.select')}</option>
            {nodeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </label>
        <div className="kg-two-fields">
          <label className="kg-field">
            <span>{translate('kg.analysis.impact.direction')}</span>
            <select value={impactDraft.direction} onChange={(event) => onImpactDraftChange({ direction: event.target.value as KnowledgeGraphImpactDraft['direction'] })}>
              <option value="both">{translate('kg.analysis.direction.both')}</option>
              <option value="outgoing">{translate('kg.analysis.direction.outgoing')}</option>
              <option value="incoming">{translate('kg.analysis.direction.incoming')}</option>
            </select>
          </label>
          <label className="kg-field">
            <span>{translate('kg.analysis.impact.maxDepth')}</span>
            <input min={1} max={8} type="number" value={impactDraft.maxDepth} onChange={(event) => onImpactDraftChange({ maxDepth: Number(event.target.value) })} />
          </label>
        </div>
        <label className="kg-field">
          <span>{translate('kg.analysis.impact.relationType')}</span>
          <select value={impactDraft.relationType} onChange={(event) => onImpactDraftChange({ relationType: event.target.value })}>
            <option value="">{translate('ai.search.statusAll')}</option>
            {relationTypeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </label>
        <button className="primary-button" disabled={impactRunning} type="button" onClick={onRunImpact}>
          <AppIcon name="activity" />
          {translate('kg.analysis.impact.run')}
        </button>
        <ResultList emptyText={translate('kg.analysis.impact.empty')} isEmpty={impactResults.length === 0}>
          {impactResults.map((item) => (
            <article className="kg-analysis-result" key={item.id}>
              <strong>{item.summary}</strong>
              <span>{formatMessage(translate('kg.analysis.impact.summary'), { affectedEdges: item.affectedEdges.length, affectedNodes: item.affectedNodes.length, blastRadius: item.blastRadius })}</span>
              {item.recommendation ? <p>{item.recommendation}</p> : null}
              <small>{item.riskLevel}</small>
            </article>
          ))}
        </ResultList>
      </div>
    </section>
  );
}

function ResultList({ children, emptyText, isEmpty }: { children: ReactNode; emptyText: string; isEmpty: boolean }) {
  return <div className="kg-analysis-results">{isEmpty ? <span className="kg-muted">{emptyText}</span> : children}</div>;
}
