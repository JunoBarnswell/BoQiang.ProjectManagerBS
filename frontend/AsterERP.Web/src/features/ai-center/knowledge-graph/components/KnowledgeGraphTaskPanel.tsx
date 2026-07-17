import { useMemo } from 'react';

import { useI18n } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import { DataTable } from '../../../../shared/table/DataTable';
import type { DataTableColumn } from '../../../../shared/table/tableTypes';
import type { KnowledgeGraphTaskView } from '../types';
import { formatDateTime, formatPercent } from '../utils/knowledgeGraphFormatters';

import { KnowledgeGraphErrorState, KnowledgeGraphStatusBadge } from './KnowledgeGraphStateViews';

interface KnowledgeGraphTaskPanelProps {
  error: unknown;
  loading: boolean;
  tasks: KnowledgeGraphTaskView[];
  onRefresh: () => void;
}

export function KnowledgeGraphTaskPanel({ error, loading, onRefresh, tasks }: KnowledgeGraphTaskPanelProps) {
  const { translate } = useI18n();
  const taskColumns = useMemo<DataTableColumn<KnowledgeGraphTaskView>[]>(
    () => [
      {
        key: 'taskName',
        responsivePriority: 100,
        title: translate('kg.taskPanel.task'),
        width: '180px',
        render: (row) => (
          <div className="kg-task-name">
            <strong>{row.taskName}</strong>
            <small>{row.taskCode}</small>
          </div>
        )
      },
      { key: 'taskType', responsivePriority: 80, title: translate('kg.taskPanel.type'), width: '110px' },
      {
        key: 'status',
        responsivePriority: 90,
        title: translate('kg.taskPanel.status'),
        width: '110px',
        render: (row) => <KnowledgeGraphStatusBadge status={row.status} />
      },
      {
        key: 'progressPercent',
        responsivePriority: 75,
        title: translate('kg.taskPanel.progress'),
        width: '120px',
        render: (row) => (
          <div className="kg-task-progress">
            <span><i style={{ width: formatPercent(row.progressPercent) }} /></span>
            <b>{formatPercent(row.progressPercent)}</b>
          </div>
        )
      },
      { key: 'createdTime', hideBelow: 'xl', title: translate('kg.taskPanel.createdTime'), width: '160px', render: (row) => formatDateTime(row.createdTime) },
      { key: 'completedAt', hideBelow: 'xl', title: translate('kg.taskPanel.completedAt'), width: '160px', render: (row) => formatDateTime(row.completedAt) },
      { key: 'errorMessage', hideBelow: 'lg', title: translate('kg.taskPanel.errorMessage'), render: (row) => row.errorMessage || '-' }
    ],
    [translate]
  );

  return (
    <section className="kg-side-section kg-task-panel">
      <header className="kg-section-header kg-section-header--compact">
        <div>
          <h2>{translate('kg.taskPanel.title')}</h2>
          <span>{translate('kg.taskPanel.description')}</span>
        </div>
        <button className="ghost-button" disabled={loading} type="button" onClick={onRefresh}>
          <AppIcon name="refresh" />
          {translate('common.refresh')}
        </button>
      </header>
      {error ? (
        <KnowledgeGraphErrorState error={error} onRetry={onRefresh} />
      ) : (
        <DataTable
          className="kg-data-table"
          columns={taskColumns}
          emptyText={translate('kg.taskPanel.empty')}
          fitScreen
          loading={loading}
          rowKey={(row) => row.id}
          rows={tasks}
        />
      )}
    </section>
  );
}
