import type { ReactNode } from 'react';

import { formatMessage } from '../../../../core/i18n/formatMessage';
import { useI18n } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';

interface KnowledgeGraphEmptyStateProps {
  action?: ReactNode;
  description: string;
  title: string;
}

interface KnowledgeGraphErrorStateProps {
  error: unknown;
  onRetry?: () => void;
}

interface KnowledgeGraphTruncatedBannerProps {
  nodeTotal: number;
  edgeTotal: number;
  reason?: string | null;
}

export function KnowledgeGraphLoadingState({ label }: { label?: string }) {
  const { translate } = useI18n();
  const resolvedLabel = label ?? translate('kg.state.loading');

  return (
    <div className="kg-state kg-state--loading">
      <AppIcon className="kg-spin" name="refresh" />
      <span>{resolvedLabel}</span>
    </div>
  );
}

export function KnowledgeGraphEmptyState({ action, description, title }: KnowledgeGraphEmptyStateProps) {
  return (
    <div className="kg-state kg-state--empty">
      <AppIcon name="database" />
      <strong>{title}</strong>
      <span>{description}</span>
      {action ? <div className="kg-state__action">{action}</div> : null}
    </div>
  );
}

export function KnowledgeGraphErrorState({ error, onRetry }: KnowledgeGraphErrorStateProps) {
  const { translate } = useI18n();

  return (
    <div className="kg-state kg-state--error">
      <AppIcon name="warning-circle" />
      <strong>{translate('kg.state.loadFailed')}</strong>
      <span>{getErrorMessage(error, translate('kg.state.requestFailed'))}</span>
      {onRetry ? (
        <button className="ghost-button" type="button" onClick={onRetry}>
          <AppIcon name="refresh" />
          {translate('common.retry')}
        </button>
      ) : null}
    </div>
  );
}

export function KnowledgeGraphTruncatedBanner({ edgeTotal, nodeTotal, reason }: KnowledgeGraphTruncatedBannerProps) {
  const { translate } = useI18n();
  const baseText = formatMessage(translate('kg.state.truncated'), { edgeTotal, nodeTotal });

  return (
    <div className="kg-truncated" role="status">
      <AppIcon name="warning-circle" />
      <span>
        {baseText}
        {reason ? ` ${formatMessage(translate('kg.state.truncatedReason'), { reason })}` : ''}
      </span>
    </div>
  );
}

export function KnowledgeGraphStatusBadge({ status }: { status: string }) {
  const { translate } = useI18n();
  const normalized = status.toLowerCase();
  const tone = normalized.includes('fail') || normalized.includes('disabled') || normalized.includes('error')
    ? 'danger'
    : normalized.includes('running') || normalized.includes('pending')
      ? 'warning'
      : normalized.includes('success') || normalized.includes('enabled') || normalized.includes('active')
        ? 'success'
        : 'neutral';
  const label = resolveStatusLabel(translate, status);

  return <span className={`kg-status kg-status--${tone}`}>{label}</span>;
}

function resolveStatusLabel(translate: (key: string) => string, status: string): string {
  const normalized = status.trim().toLowerCase();

  if (!normalized) {
    return translate('common.empty');
  }

  if (
    normalized.includes('success')
    || normalized.includes('healthy')
    || normalized.includes('enabled')
    || normalized.includes('active')
    || normalized.includes('passed')
    || normalized.includes('succeeded')
    || normalized === 'ok'
  ) {
    return translate('ai.knowledgeGraph.health.healthy');
  }

  if (
    normalized.includes('warning')
    || normalized.includes('pending')
    || normalized.includes('running')
    || normalized.includes('processing')
    || normalized.includes('queued')
  ) {
    return translate('ai.knowledgeGraph.health.warning');
  }

  if (normalized.includes('failed') || normalized.includes('failure') || normalized.includes('error')) {
    return translate('status.error.title');
  }

  if (normalized.includes('disabled') || normalized.includes('blocked')) {
    return translate('ai.skCapabilities.status.blocked');
  }

  return translate('ai.toolExecutions.status.unknown');
}
