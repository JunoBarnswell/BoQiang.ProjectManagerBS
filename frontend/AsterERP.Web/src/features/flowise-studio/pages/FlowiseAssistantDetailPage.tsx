import { useParams } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { flowiseAssistantsApi } from '../api/assistants.api';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { MainCard } from '../native/ui-component/cards/MainCard';
import type { FlowiseAssistantDefinitionDto } from '../types/assistant.types';

function joinValues(values: readonly string[] | undefined): string {
  return values && values.length > 0 ? values.join(', ') : '-';
}

function formatOptional(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

function formatDefinition(definition: FlowiseAssistantDefinitionDto | undefined): Array<{ label: string; value: string }> {
  return [
    { label: 'Model', value: formatOptional(definition?.model) },
    { label: 'Response Format', value: formatOptional(definition?.responseFormat) },
    { label: 'Temperature', value: formatOptional(definition?.temperature) },
    { label: 'Top P', value: formatOptional(definition?.topP) },
    { label: 'Tools', value: joinValues(definition?.tools) },
    { label: 'File IDs', value: joinValues(definition?.fileIds) }
  ];
}

export function FlowiseAssistantDetailPage() {
  const { id = '' } = useParams();
  const { translate } = useI18n();
  const assistantQuery = useApiQuery({
    enabled: Boolean(id),
    queryKey: ['flowise', 'assistant', id],
    queryFn: ({ signal }) => flowiseAssistantsApi.get(id, signal)
  });
  const assistant = assistantQuery.data?.data;
  const definitionRows = formatDefinition(assistant?.definition);

  return (
    <MainCard description={assistant?.description ?? ''} title={assistant?.name ?? translate(flowiseI18nKeys.pages.assistants)}>
      <div className="flowise-detail-layout">
        <section className="flowise-detail-panel">
          <h3>{translate(flowiseI18nKeys.detail.configuration)}</h3>
          <dl className="flowise-detail-list">
            <div><dt>Key</dt><dd>{assistant?.assistantKey ?? '-'}</dd></div>
            <div><dt>Type</dt><dd>{assistant?.assistantType ?? '-'}</dd></div>
            <div><dt>Status</dt><dd>{assistant?.status ?? '-'}</dd></div>
            <div><dt>Workspace</dt><dd>{assistant?.workspaceName ?? assistant?.workspaceId ?? '-'}</dd></div>
            {definitionRows.map((row) => (
              <div key={row.label}><dt>{row.label}</dt><dd>{row.value}</dd></div>
            ))}
          </dl>
        </section>
        <section className="flowise-detail-panel">
          <h3>Instructions</h3>
          <p className="flowise-detail-text">{assistant?.definition.instructions || '-'}</p>
        </section>
        <section className="flowise-detail-panel">
          <h3>Advanced Metadata</h3>
          <pre>{assistant?.advancedMetadataJson ?? '{}'}</pre>
        </section>
      </div>
    </MainCard>
  );
}
