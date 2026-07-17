import { useMemo, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { SearchForm } from '../../shared/forms/SearchForm';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import type { AiFailureSummaryDto } from './api/aiCenter.api';
import { aiObservabilityApi } from './api/observability.api';

import './styles/ai-center.css';

interface FailureSearchState {
  endedAt: string;
  startedAt: string;
}

const defaultSearch: FailureSearchState = {
  endedAt: '',
  startedAt: ''
};

export function AiFailureAnalysisPage() {
  const { translate } = useI18n();
  const [searchDraft, setSearchDraft] = useState(defaultSearch);
  const [search, setSearch] = useState(defaultSearch);

  const fields = useMemo<FormFieldConfig<FailureSearchState>[]>(
    () => [
      { label: translate('ai.failureAnalysis.field.startedAt'), name: 'startedAt', type: 'datetime-local' },
      { label: translate('ai.failureAnalysis.field.endedAt'), name: 'endedAt', type: 'datetime-local' }
    ],
    [translate]
  );

  const query = useApiQuery({
    queryKey: ['ai', 'failures', search],
    queryFn: ({ signal }) =>
      aiObservabilityApi.failures(
        {
          endedAt: toApiDate(search.endedAt),
          startedAt: toApiDate(search.startedAt)
        },
        signal
      )
  });

  const columns = useMemo<DataTableColumn<AiFailureSummaryDto>[]>(
    () => [
      { key: 'errorCode', responsivePriority: 100, title: translate('ai.failureAnalysis.column.errorCode'), width: '180px' },
      { key: 'count', responsivePriority: 90, title: translate('ai.failureAnalysis.column.count'), width: '100px' },
      { key: 'errorMessage', responsivePriority: 80, title: translate('ai.failureAnalysis.column.errorMessage') }
    ],
    [translate]
  );

  return (
    <CrudPage
      className="ai-chat-page"
      description={translate('ai.failureAnalysis.description')}
      eyebrow={translate('ai.eyebrow')}
      searchArea={
        <SearchForm
          fields={fields}
          loading={query.isFetching}
          onReset={() => {
            setSearchDraft(defaultSearch);
            setSearch(defaultSearch);
          }}
          onSubmit={setSearch}
          onValueChange={setSearchDraft}
          value={searchDraft}
        />
      }
      title={translate('ai.failureAnalysis.title')}
    >
      <DataTable className="ai-admin-grid" columns={columns} fitScreen loading={query.isFetching} rowKey={(row) => row.errorCode} rows={query.data?.data ?? []} />
    </CrudPage>
  );
}

function toApiDate(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}
