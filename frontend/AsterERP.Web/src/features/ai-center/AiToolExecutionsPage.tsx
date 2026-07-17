import { useMemo, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { SearchForm } from '../../shared/forms/SearchForm';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import type { AiToolInvocationDto } from './api/aiCenter.api';
import { aiObservabilityApi } from './api/observability.api';

import './styles/ai-center.css';

interface ToolExecutionSearchState {
  endedAt: string;
  runId: string;
  startedAt: string;
  status: string;
  toolCode: string;
}

const defaultSearch: ToolExecutionSearchState = {
  endedAt: '',
  runId: '',
  startedAt: '',
  status: '',
  toolCode: ''
};

export function AiToolExecutionsPage() {
  const { locale, translate } = useI18n();
  const [searchDraft, setSearchDraft] = useState(defaultSearch);
  const [search, setSearch] = useState(defaultSearch);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const fields = useMemo<FormFieldConfig<ToolExecutionSearchState>[]>(
    () => [
      { label: translate('ai.toolExecutions.field.startedAt'), name: 'startedAt', type: 'datetime-local' },
      { label: translate('ai.toolExecutions.field.endedAt'), name: 'endedAt', type: 'datetime-local' },
      { label: translate('ai.toolExecutions.field.runId'), name: 'runId', type: 'text' },
      { label: translate('ai.toolExecutions.field.toolCode'), name: 'toolCode', type: 'text' },
      {
        label: translate('ai.toolExecutions.field.status'),
        name: 'status',
        options: [
          { label: translate('ai.toolExecutions.option.all'), value: '' },
          { label: translate('ai.toolExecutions.option.succeeded'), value: 'Succeeded' },
          { label: translate('ai.toolExecutions.option.failed'), value: 'Failed' },
          { label: translate('ai.toolExecutions.option.pendingConfirmation'), value: 'PendingConfirmation' }
        ],
        type: 'select'
      }
    ],
    [translate]
  );

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['ai', 'tool-executions', search, pageIndex, pageSize],
    queryFn: ({ signal }) =>
      aiObservabilityApi.toolExecutions(
        {
          endedAt: toApiDate(search.endedAt),
          pageIndex,
          pageSize,
          runId: search.runId,
          startedAt: toApiDate(search.startedAt),
          status: search.status,
          toolCode: search.toolCode
        },
        signal
      )
  });

  const columns = useMemo<DataTableColumn<AiToolInvocationDto>[]>(
    () => [
      { key: 'toolCode', responsivePriority: 100, title: translate('ai.toolExecutions.column.toolCode'), width: '180px' },
      { key: 'toolName', responsivePriority: 90, title: translate('ai.toolExecutions.column.toolName'), width: '180px' },
      { key: 'status', responsivePriority: 85, title: translate('ai.toolExecutions.column.status'), width: '120px', render: (row) => renderExecutionStatusPill(row.status, translate) },
      { key: 'durationMs', responsivePriority: 70, title: translate('ai.toolExecutions.column.durationMs'), width: '100px' },
      { key: 'runId', hideBelow: 'lg', responsivePriority: 55, title: translate('ai.toolExecutions.column.runId'), width: '220px' },
      { key: 'traceId', hideBelow: 'xl', responsivePriority: 45, title: translate('ai.toolExecutions.column.traceId'), width: '220px' },
      { key: 'createdTime', responsivePriority: 75, title: translate('ai.toolExecutions.column.createdTime'), width: '150px', render: (row) => formatTime(row.createdTime, locale) },
      { key: 'errorMessage', hideBelow: 'lg', responsivePriority: 50, title: translate('ai.toolExecutions.column.errorMessage') }
    ],
    [locale, translate]
  );

  return (
    <CrudPage
      className="ai-chat-page"
      description={translate('ai.toolExecutions.description')}
      eyebrow={translate('ai.eyebrow')}
      searchArea={
        <SearchForm
          fields={fields}
          loading={query.isFetching}
          onReset={() => {
            setSearchDraft(defaultSearch);
            setSearch(defaultSearch);
            setPageIndex(1);
          }}
          onSubmit={(value) => {
            setSearch(value);
            setPageIndex(1);
          }}
          onValueChange={setSearchDraft}
          value={searchDraft}
        />
      }
      title={translate('ai.toolExecutions.title')}
    >
      <DataTable
        className="ai-admin-grid"
        columns={columns}
        fitScreen
        loading={query.isFetching}
        onPageChange={setPageIndex}
        onPageSizeChange={setPageSize}
        pagination={{ current: pageIndex, pageSize, total: query.data?.data.total ?? 0 }}
        rowKey={(row) => row.id}
        rows={query.data?.data.items ?? []}
      />
    </CrudPage>
  );
}

function toApiDate(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}

function formatTime(value: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit'
  }).format(new Date(value));
}

function renderExecutionStatusPill(status: string, translate: (key: string) => string) {
  const label =
    status === 'Succeeded'
      ? translate('ai.toolExecutions.status.succeeded')
      : status === 'Failed'
        ? translate('ai.toolExecutions.status.failed')
        : status === 'PendingConfirmation'
          ? translate('ai.toolExecutions.status.pendingConfirmation')
          : translate('ai.toolExecutions.status.unknown');
  const className =
    status === 'Succeeded'
      ? 'ai-status-badge ai-status-badge--success'
      : status === 'Failed'
        ? 'ai-status-badge ai-status-badge--danger'
        : status === 'PendingConfirmation'
          ? 'ai-status-badge ai-status-badge--running'
          : 'ai-status-badge';
  return <span className={className}>{label}</span>;
}
