import { useMemo, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { AiStatusBadge } from '../../shared/components/ai-chat/AiStatusBadge';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { SearchForm } from '../../shared/forms/SearchForm';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import type { AiRunListItemDto } from './api/aiCenter.api';
import { aiObservabilityApi } from './api/observability.api';

import './styles/ai-center.css';

interface RunLogSearchState {
  endedAt: string;
  mode: string;
  startedAt: string;
  status: string;
  userId: string;
}

const defaultSearch: RunLogSearchState = {
  endedAt: '',
  mode: '',
  startedAt: '',
  status: '',
  userId: ''
};

export function AiRunLogsPage() {
  const { locale, translate } = useI18n();
  const [searchDraft, setSearchDraft] = useState(defaultSearch);
  const [search, setSearch] = useState(defaultSearch);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const fields = useMemo<FormFieldConfig<RunLogSearchState>[]>(
    () => [
      { label: translate('ai.runLogs.field.startedAt'), name: 'startedAt', type: 'datetime-local' },
      { label: translate('ai.runLogs.field.endedAt'), name: 'endedAt', type: 'datetime-local' },
      { label: translate('ai.runLogs.field.userId'), name: 'userId', type: 'text' },
      {
        label: translate('ai.runLogs.field.mode'),
        name: 'mode',
        options: [
          { label: translate('ai.runLogs.option.all'), value: '' },
          { label: translate('ai.runLogs.option.ask'), value: 'Ask' },
          { label: translate('ai.runLogs.option.plan'), value: 'Plan' },
          { label: translate('ai.runLogs.option.agent'), value: 'Agent' }
        ],
        type: 'select'
      },
      {
        label: translate('ai.runLogs.field.status'),
        name: 'status',
        options: [
          { label: translate('ai.runLogs.option.all'), value: '' },
          { label: translate('ai.runLogs.option.running'), value: 'Running' },
          { label: translate('ai.runLogs.option.succeeded'), value: 'Succeeded' },
          { label: translate('ai.runLogs.option.failed'), value: 'Failed' },
          { label: translate('ai.runLogs.option.cancelled'), value: 'Cancelled' }
        ],
        type: 'select'
      }
    ],
    [translate]
  );

  const query = useApiQuery({
    keepPreviousData: true,
    queryKey: ['ai', 'runs', search, pageIndex, pageSize],
    queryFn: ({ signal }) =>
      aiObservabilityApi.runs(
        {
          endedAt: toApiDate(search.endedAt),
          mode: search.mode,
          pageIndex,
          pageSize,
          startedAt: toApiDate(search.startedAt),
          status: search.status,
          userId: search.userId
        },
        signal
      )
  });

  const columns = useMemo<DataTableColumn<AiRunListItemDto>[]>(
    () => [
      { key: 'id', hideBelow: 'xl', responsivePriority: 35, title: translate('ai.runLogs.column.id'), width: '220px' },
      { key: 'conversationId', hideBelow: 'lg', responsivePriority: 45, title: translate('ai.runLogs.column.conversationId'), width: '220px' },
      { key: 'mode', responsivePriority: 95, title: translate('ai.runLogs.column.mode'), width: '100px' },
      {
        key: 'status',
        responsivePriority: 85,
        title: translate('ai.runLogs.column.status'),
        width: '100px',
        render: (row) => <AiStatusBadge status={row.status} />
      },
      { key: 'totalTokens', responsivePriority: 75, title: translate('ai.runLogs.column.totalTokens'), width: '100px' },
      {
        key: 'startedAt',
        responsivePriority: 80,
        title: translate('ai.runLogs.column.startedAt'),
        width: '150px',
        render: (row) => formatTime(row.startedAt, locale)
      },
      { key: 'errorMessage', hideBelow: 'lg', responsivePriority: 50, title: translate('ai.runLogs.column.errorMessage') }
    ],
    [locale, translate]
  );

  const rows = query.data?.data.items ?? [];
  const total = query.data?.data.total ?? 0;

  return (
    <CrudPage
      className="ai-chat-page"
      description={translate('ai.runLogs.description')}
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
      title={translate('ai.runLogs.title')}
    >
      <DataTable
        className="ai-admin-grid"
        columns={columns}
        fitScreen
        loading={query.isFetching}
        onPageChange={setPageIndex}
        onPageSizeChange={setPageSize}
        pagination={{ current: pageIndex, pageSize, total }}
        rowKey={(row) => row.id}
        rows={rows}
      />
    </CrudPage>
  );
}

function toApiDate(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}

function formatTime(value?: string | null, locale = 'zh-CN'): string {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(locale, {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit'
  }).format(new Date(value));
}
