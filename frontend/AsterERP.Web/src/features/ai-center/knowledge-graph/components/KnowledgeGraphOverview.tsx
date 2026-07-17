import { useI18n } from '../../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import type { KnowledgeGraphOverviewView } from '../types';
import { formatDateTime } from '../utils/knowledgeGraphFormatters';

interface KnowledgeGraphOverviewProps {
  overview?: KnowledgeGraphOverviewView;
  loading?: boolean;
  onRefresh: () => void;
  onRebuild: () => void;
  rebuilding?: boolean;
}

export function KnowledgeGraphOverview({
  loading,
  onRebuild,
  onRefresh,
  overview,
  rebuilding
}: KnowledgeGraphOverviewProps) {
  const { translate } = useI18n();
  const metrics = overview?.metrics ?? [];

  return (
    <section className="kg-overview">
      <header className="kg-section-header">
        <div>
          <h2>{translate('kg.overview.title')}</h2>
          <span>{overview?.summary ?? translate('kg.overview.summary')}</span>
        </div>
        <div className="kg-action-row">
          <button className="ghost-button" disabled={loading} type="button" onClick={onRefresh}>
            <AppIcon name="refresh" />
            {translate('common.refresh')}
          </button>
          <PermissionButton className="primary-button" code="ai:knowledge:graph:reindex" disabled={rebuilding} fallback="disable" iconStart={false} type="button" onClick={onRebuild}>
            <AppIcon name="database" />
            {translate('kg.overview.rebuild')}
          </PermissionButton>
        </div>
      </header>

      <div className="kg-metric-grid">
        {metrics.map((metric) => (
          <article className={`kg-metric kg-metric--${metric.tone}`} key={metric.key}>
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
          </article>
        ))}
      </div>

      <footer className="kg-overview-footer">
        <span>
          <AppIcon name="shield" />
          {overview?.healthStatus ?? translate('kg.overview.unknown')}
        </span>
        <span>
          <AppIcon name="clock" />
          {formatDateTime(overview?.lastUpdatedAt)}
        </span>
      </footer>
    </section>
  );
}
