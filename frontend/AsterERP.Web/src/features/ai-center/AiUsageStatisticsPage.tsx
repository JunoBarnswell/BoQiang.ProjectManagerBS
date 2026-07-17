import { useMemo, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { SearchForm } from '../../shared/forms/SearchForm';

import { aiCapabilityApi } from './api/capability.api';
import { aiObservabilityApi } from './api/observability.api';

import './styles/ai-center.css';

interface UsageSearchState {
  endedAt: string;
  modelCode: string;
  providerCode: string;
  startedAt: string;
  userId: string;
}

const defaultSearch: UsageSearchState = {
  endedAt: '',
  modelCode: '',
  providerCode: '',
  startedAt: '',
  userId: ''
};

export function AiUsageStatisticsPage() {
  const { locale, translate } = useI18n();
  const [searchDraft, setSearchDraft] = useState(defaultSearch);
  const [search, setSearch] = useState(defaultSearch);

  const providersQuery = useApiQuery({
    queryKey: ['ai', 'providers', 'options'],
    queryFn: ({ signal }) => aiCapabilityApi.providers.options(signal)
  });
  const modelsQuery = useApiQuery({
    queryKey: ['ai', 'models', 'options'],
    queryFn: ({ signal }) => aiCapabilityApi.models.options(signal)
  });

  const fields = useMemo<FormFieldConfig<UsageSearchState>[]>(
    () => [
      { label: translate('ai.usageStatistics.field.startedAt'), name: 'startedAt', type: 'datetime-local' },
      { label: translate('ai.usageStatistics.field.endedAt'), name: 'endedAt', type: 'datetime-local' },
      {
        label: translate('ai.usageStatistics.field.providerCode'),
        name: 'providerCode',
        options: [
          { label: translate('ai.usageStatistics.option.all'), value: '' },
          ...(providersQuery.data?.data ?? []).map((item) => ({ label: item.providerName, value: item.providerCode }))
        ],
        type: 'select'
      },
      {
        label: translate('ai.usageStatistics.field.modelCode'),
        name: 'modelCode',
        options: [
          { label: translate('ai.usageStatistics.option.all'), value: '' },
          ...(modelsQuery.data?.data ?? []).map((item) => ({ label: item.displayName, value: item.modelCode }))
        ],
        type: 'select'
      },
      { label: translate('ai.usageStatistics.field.userId'), name: 'userId', type: 'text' }
    ],
    [modelsQuery.data?.data, providersQuery.data?.data, translate]
  );

  const usageQuery = useApiQuery({
    queryKey: ['ai', 'usage', search],
    queryFn: ({ signal }) =>
      aiObservabilityApi.summary(
        {
          endedAt: toApiDate(search.endedAt),
          modelCode: search.modelCode,
          providerCode: search.providerCode,
          startedAt: toApiDate(search.startedAt),
          userId: search.userId
        },
        signal
      )
  });

  const usage = usageQuery.data?.data;

  return (
    <CrudPage
      className="ai-chat-page"
      description={translate('ai.usageStatistics.description')}
      eyebrow={translate('ai.eyebrow')}
      searchArea={
        <SearchForm
          fields={fields}
          loading={usageQuery.isFetching}
          onReset={() => {
            setSearchDraft(defaultSearch);
            setSearch(defaultSearch);
          }}
          onSubmit={setSearch}
          onValueChange={setSearchDraft}
          value={searchDraft}
        />
      }
      title={translate('ai.usageStatistics.title')}
    >
      <div className="ai-usage-summary">
        <UsageCard label={translate('ai.usageStatistics.card.requestCount')} locale={locale} value={usage?.requestCount ?? 0} />
        <UsageCard label={translate('ai.usageStatistics.card.successCount')} locale={locale} value={usage?.successCount ?? 0} />
        <UsageCard label={translate('ai.usageStatistics.card.failedCount')} locale={locale} value={usage?.failedCount ?? 0} />
        <UsageCard label={translate('ai.usageStatistics.card.totalTokens')} locale={locale} value={usage?.totalTokens ?? 0} />
        <UsageCard label={translate('ai.usageStatistics.card.promptTokens')} locale={locale} value={usage?.promptTokens ?? 0} />
        <UsageCard label={translate('ai.usageStatistics.card.completionTokens')} locale={locale} value={usage?.completionTokens ?? 0} />
        <UsageCard label={translate('ai.usageStatistics.card.reasoningTokens')} locale={locale} value={usage?.reasoningTokens ?? 0} />
        <UsageCard label={translate('ai.usageStatistics.card.costAmount')} locale={locale} value={usage?.costAmount ?? 0} />
      </div>
    </CrudPage>
  );
}

function UsageCard({ label, locale, value }: { label: string; locale: string; value: number }) {
  return (
    <article>
      <span>{label}</span>
      <strong>{new Intl.NumberFormat(locale).format(value)}</strong>
    </article>
  );
}

function toApiDate(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}
