import { translateCurrentLocale, useI18n } from '../../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import type { KnowledgeGraphEdgeView, KnowledgeGraphNodeView } from '../types';

import { KnowledgeGraphEmptyState, KnowledgeGraphStatusBadge } from './KnowledgeGraphStateViews';

interface KnowledgeGraphDetailsPanelProps {
  selectedEdge: KnowledgeGraphEdgeView | null;
  selectedNode: KnowledgeGraphNodeView | null;
  onDeleteEdge: (edgeId: string) => void;
  onDeleteNode: (nodeId: string) => void;
  onEditEdge: (edgeId: string) => void;
  onEditNode: (nodeId: string) => void;
}

export function KnowledgeGraphDetailsPanel({
  onDeleteEdge,
  onDeleteNode,
  onEditEdge,
  onEditNode,
  selectedEdge,
  selectedNode
}: KnowledgeGraphDetailsPanelProps) {
  const { translate } = useI18n();

  if (selectedNode) {
    return (
      <section className="kg-side-section">
        <header className="kg-section-header kg-section-header--compact">
          <div>
            <h2>{selectedNode.label}</h2>
            <span>{selectedNode.nodeCode}</span>
          </div>
          <KnowledgeGraphStatusBadge status={selectedNode.status} />
        </header>
        <div className="kg-detail-actions">
          <PermissionButton className="ghost-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onEditNode(selectedNode.id)}>
            {translate('common.edit')}
          </PermissionButton>
          <PermissionButton className="danger-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onDeleteNode(selectedNode.id)}>
            {translate('common.delete')}
          </PermissionButton>
        </div>
        <dl className="kg-kv">
          <DetailItem label={translate('kg.details.node.type')} value={selectedNode.nodeType} />
          <DetailItem label={translate('kg.details.node.source')} value={selectedNode.sourceName || selectedNode.sourceId || '-'} />
          <DetailItem label={translate('kg.details.node.weight')} value={String(selectedNode.weight)} />
          <DetailItem label={translate('kg.details.node.degree')} value={String(selectedNode.degree)} />
          <DetailItem label={translate('kg.details.node.position')} value={`${Math.round(selectedNode.position.x)}, ${Math.round(selectedNode.position.y)}`} />
        </dl>
        <TextBlock title={translate('kg.details.description')} value={selectedNode.description} />
        <TagList tags={selectedNode.tags} />
        <MetadataBlock metadata={selectedNode.metadata} />
      </section>
    );
  }

  if (selectedEdge) {
    return (
      <section className="kg-side-section">
        <header className="kg-section-header kg-section-header--compact">
          <div>
            <h2>{selectedEdge.label}</h2>
            <span>{selectedEdge.relationCode}</span>
          </div>
          <KnowledgeGraphStatusBadge status={selectedEdge.status} />
        </header>
        <div className="kg-detail-actions">
          <PermissionButton className="ghost-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onEditEdge(selectedEdge.id)}>
            {translate('common.edit')}
          </PermissionButton>
          <PermissionButton className="danger-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onDeleteEdge(selectedEdge.id)}>
            {translate('common.delete')}
          </PermissionButton>
        </div>
        <dl className="kg-kv">
          <DetailItem label={translate('kg.details.edge.relationType')} value={selectedEdge.relationType} />
          <DetailItem label={translate('kg.details.edge.source')} value={selectedEdge.sourceLabel} />
          <DetailItem label={translate('kg.details.edge.target')} value={selectedEdge.targetLabel} />
          <DetailItem label={translate('kg.details.edge.weight')} value={String(selectedEdge.weight)} />
        </dl>
        <TextBlock title={translate('kg.details.description')} value={selectedEdge.description} />
        <MetadataBlock metadata={selectedEdge.metadata} />
      </section>
    );
  }

  return (
    <section className="kg-side-section kg-side-section--empty">
      <KnowledgeGraphEmptyState
        description={translate('kg.details.empty.description')}
        title={translate('kg.details.empty.title')}
      />
    </section>
  );
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value || '-'}</dd>
    </div>
  );
}

function TextBlock({ title, value }: { title: string; value: string }) {
  return (
    <div className="kg-text-block">
      <strong>{title}</strong>
      <p>{value || '-'}</p>
    </div>
  );
}

function TagList({ tags }: { tags: string[] }) {
  if (tags.length === 0) {
    return null;
  }

  return (
    <div className="kg-tag-list">
      <strong><AppIcon name="flag" />{translateCurrentLocale('kg.details.tags')}</strong>
      <div>
        {tags.map((tag) => <span key={tag}>{tag}</span>)}
      </div>
    </div>
  );
}

function MetadataBlock({ metadata }: { metadata: Record<string, unknown> }) {
  return (
    <details className="kg-json" open={Object.keys(metadata).length > 0}>
      <summary><AppIcon name="braces" />{translateCurrentLocale('kg.details.metadata')}</summary>
      <pre>{JSON.stringify(metadata, null, 2)}</pre>
    </details>
  );
}
