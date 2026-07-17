import SearchIcon from '@mui/icons-material/Search';
import { Button, Stack, TextField } from '@mui/material';
import { useState } from 'react';
import { useParams } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { documentStoresApi } from '../api/documentStores.api';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { MainCard } from '../native/ui-component/cards/MainCard';
import { FlowListTable, type FlowListTableColumn } from '../native/ui-component/table/FlowListTable';
import type { FlowiseDocumentStoreChunkDto, FlowiseDocumentStoreFileDto } from '../types/documentStore.types';

export function FlowiseDocumentStoreDetailPage() {
  const { storeId = '' } = useParams();
  const { translate } = useI18n();
  const message = useMessage();
  const [query, setQuery] = useState('');
  const detailQuery = useApiQuery({
    enabled: Boolean(storeId),
    queryKey: ['flowise', 'document-store', storeId],
    queryFn: ({ signal }) => documentStoresApi.get(storeId, signal)
  });
  const filesQuery = useApiQuery({
    enabled: Boolean(storeId),
    queryKey: ['flowise', 'document-store', storeId, 'files'],
    queryFn: ({ signal }) => documentStoresApi.files(storeId, signal)
  });
  const chunksQuery = useApiQuery({
    enabled: Boolean(storeId),
    queryKey: ['flowise', 'document-store', storeId, 'chunks'],
    queryFn: ({ signal }) => documentStoresApi.chunks(storeId, undefined, signal)
  });
  const vectorConfigQuery = useApiQuery({
    enabled: Boolean(storeId),
    queryKey: ['flowise', 'document-store', storeId, 'vector-config'],
    queryFn: ({ signal }) => documentStoresApi.vectorConfig(storeId, signal)
  });
  const queryMutation = useApiMutation({
    mutationFn: () => documentStoresApi.query({ query, storeId }),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.vectorQueryFailed)))
  });

  const fileColumns: FlowListTableColumn<FlowiseDocumentStoreFileDto>[] = [
    { key: 'fileName', title: translate(flowiseI18nKeys.fields.file), width: '260px' },
    { key: 'loaderType', title: translate(flowiseI18nKeys.fields.loader), width: '160px' },
    { key: 'status', title: translate(flowiseI18nKeys.fields.status), width: '120px' },
    { key: 'fileSize', title: translate(flowiseI18nKeys.fields.size), width: '120px', render: (item) => `${Math.round(item.fileSize / 1024)} KB` }
  ];
  const chunkColumns: FlowListTableColumn<FlowiseDocumentStoreChunkDto>[] = [
    { key: 'chunkIndex', title: translate(flowiseI18nKeys.fields.chunkIndex), width: '70px' },
    { key: 'content', title: translate(flowiseI18nKeys.fields.content), width: '420px', render: (item) => <span className="flowise-cell-ellipsis">{item.content}</span> },
    { key: 'tokenCount', title: translate(flowiseI18nKeys.fields.tokens), width: '100px' }
  ];
  const vectorConfig = vectorConfigQuery.data?.data;
  const queryChunks = queryMutation.data?.data.chunks ?? [];

  return (
    <MainCard
      description={detailQuery.data?.data.description ?? ''}
      title={detailQuery.data?.data.name ?? translate(flowiseI18nKeys.detail.documentStore)}
    >
      <div className="flowise-detail-layout">
        <section className="flowise-detail-panel">
          <h3>{translate(flowiseI18nKeys.detail.loaderFiles)}</h3>
          <FlowListTable columns={fileColumns} emptyText={translate(flowiseI18nKeys.messages.noLoaderFiles)} getRowKey={(item) => item.id} loading={filesQuery.isLoading} rows={filesQuery.data?.data ?? []} />
        </section>
        <section className="flowise-detail-panel">
          <h3>{translate(flowiseI18nKeys.detail.chunks)}</h3>
          <FlowListTable columns={chunkColumns} emptyText={translate(flowiseI18nKeys.messages.noChunks)} getRowKey={(item) => item.id} loading={chunksQuery.isLoading} rows={chunksQuery.data?.data ?? []} />
        </section>
        <section className="flowise-detail-panel">
          <h3>{translate(flowiseI18nKeys.detail.vectorStore)}</h3>
          <dl className="flowise-detail-list">
            <div><dt>Vector Provider</dt><dd>{vectorConfig?.vectorProvider ?? '-'}</dd></div>
            <div><dt>Embedding Provider</dt><dd>{vectorConfig?.embeddingProvider ?? '-'}</dd></div>
            <div><dt>Record Manager</dt><dd>{vectorConfig?.recordManagerProvider ?? '-'}</dd></div>
          </dl>
          <pre>{vectorConfig?.vectorStoreConfigJson ?? '{}'}</pre>
          <Stack className="flowise-searchbar" direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
            <TextField
              fullWidth
              placeholder={translate(flowiseI18nKeys.search.vectorStore)}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
            <Button
              disabled={!query.trim() || queryMutation.isPending}
              startIcon={<SearchIcon />}
              type="button"
              variant="outlined"
              onClick={() => queryMutation.mutate()}
            >
              {translate(flowiseI18nKeys.actions.query)}
            </Button>
          </Stack>
          <FlowListTable
            columns={chunkColumns}
            emptyText={translate(flowiseI18nKeys.messages.noChunks)}
            getRowKey={(item) => item.id}
            loading={queryMutation.isPending}
            rows={queryChunks}
          />
        </section>
      </div>
    </MainCard>
  );
}
