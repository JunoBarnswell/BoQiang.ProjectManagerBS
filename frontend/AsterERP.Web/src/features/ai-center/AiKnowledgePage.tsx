import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import { SearchForm } from '../../shared/forms/SearchForm';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { aiChatApi, type AiKnowledgeDocumentDto, type AiKnowledgeSourceDto } from './api/aiCenter.api';

import './styles/ai-center.css';

interface KnowledgeSearchState {
  keyword: string;
  sourceId: string;
}

const defaultSearch: KnowledgeSearchState = {
  keyword: '',
  sourceId: ''
};

export function AiKnowledgePage() {
  const { translate } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [searchDraft, setSearchDraft] = useState(defaultSearch);
  const [search, setSearch] = useState(defaultSearch);
  const sourcesQuery = useApiQuery({
    queryKey: ['ai', 'knowledge', 'sources', search.keyword],
    queryFn: ({ signal }) => aiChatApi.knowledge.sources({ keyword: search.keyword, pageIndex: 1, pageSize: 100 }, signal)
  });

  const sourceOptions = useMemo(
    () => [
      { label: translate('ai.knowledge.option.all'), value: '' },
      ...((sourcesQuery.data?.data.items ?? []).map((item) => ({ label: item.sourceName, value: item.id })))
    ],
    [sourcesQuery.data?.data.items, translate]
  );

  const fields = useMemo<FormFieldConfig<KnowledgeSearchState>[]>(
    () => [
      { label: translate('ai.knowledge.field.keyword'), name: 'keyword', type: 'text' },
      { label: translate('ai.knowledge.field.sourceId'), name: 'sourceId', options: sourceOptions, type: 'select' }
    ],
    [sourceOptions, translate]
  );

  const documentsQuery = useApiQuery({
    queryKey: ['ai', 'knowledge', 'documents', search],
    queryFn: ({ signal }) =>
      aiChatApi.knowledge.documents(
        {
          keyword: search.keyword,
          pageIndex: 1,
          pageSize: 100,
          sourceId: search.sourceId || null
        },
        signal
      )
  });

  const sourceColumns = useMemo<DataTableColumn<AiKnowledgeSourceDto>[]>(
    () => [
      { key: 'sourceCode', title: translate('ai.knowledge.column.sourceCode'), render: (row) => row.sourceCode },
      { key: 'sourceName', title: translate('ai.knowledge.column.sourceName'), render: (row) => row.sourceName },
      { key: 'sourceType', title: translate('ai.knowledge.column.sourceType'), render: (row) => row.sourceType },
      { key: 'status', title: translate('ai.knowledge.column.status'), render: (row) => row.status },
      { key: 'createdTime', title: translate('ai.knowledge.column.createdTime'), render: (row) => new Date(row.createdTime).toLocaleString() }
    ],
    [translate]
  );

  const documentColumns = useMemo<DataTableColumn<AiKnowledgeDocumentDto>[]>(
    () => [
      { key: 'documentName', title: translate('ai.knowledge.column.documentName'), render: (row) => row.documentName },
      { key: 'contentType', title: translate('ai.knowledge.column.contentType'), render: (row) => row.contentType },
      { key: 'indexStatus', title: translate('ai.knowledge.column.indexStatus'), render: (row) => row.indexStatus },
      { key: 'chunkCount', title: translate('ai.knowledge.column.chunkCount'), render: (row) => row.chunkCount },
      { key: 'createdTime', title: translate('ai.knowledge.column.createdTime'), render: (row) => new Date(row.createdTime).toLocaleString() }
    ],
    [translate]
  );

  return (
    <CrudPage
      className="ai-chat-page"
      description={translate('ai.knowledge.description')}
      eyebrow={translate('ai.eyebrow')}
      searchArea={
        <SearchForm
          fields={fields}
          loading={sourcesQuery.isFetching || documentsQuery.isFetching}
          onReset={() => {
            setSearchDraft(defaultSearch);
            setSearch(defaultSearch);
          }}
          onSubmit={setSearch}
          onValueChange={setSearchDraft}
          value={searchDraft}
        />
      }
      title={translate('ai.capability.knowledge')}
    >
      <div className="ai-workflow-section">
        <h2>{translate('ai.knowledge.section.graph')}</h2>
        <div className="ai-empty-panel">
          <PermissionButton
            className="ghost-button"
            code="ai:knowledge:graph:view"
            fallback="hide"
            type="button"
            onClick={() => {
              const next = new URLSearchParams(searchParams);
              next.set('tab', 'knowledge-graph');
              setSearchParams(next, { replace: true });
            }}
          >
            {translate('ai.knowledge.action.openGraph')}
          </PermissionButton>
        </div>
      </div>
      <div className="ai-workflow-section">
        <h2>{translate('ai.knowledge.section.sources')}</h2>
        <DataTable
          className="ai-admin-grid"
          columns={sourceColumns}
          loading={sourcesQuery.isFetching}
          rowKey={(row) => row.id}
          rows={sourcesQuery.data?.data.items ?? []}
        />
      </div>
      <div className="ai-workflow-section">
        <h2>{translate('ai.knowledge.section.documents')}</h2>
        <DataTable
          className="ai-admin-grid"
          columns={documentColumns}
          loading={documentsQuery.isFetching}
          rowKey={(row) => row.id}
          rows={documentsQuery.data?.data.items ?? []}
        />
      </div>
      <div className="ai-empty-panel">
        {translate('ai.knowledge.ragUnavailable')}
      </div>
    </CrudPage>
  );
}
