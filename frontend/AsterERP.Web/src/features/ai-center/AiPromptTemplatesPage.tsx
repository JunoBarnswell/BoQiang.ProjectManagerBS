import { useMemo } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import type { CrudApi } from '../../shared/components/crud-page/useCrudResource';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { AiAdminCrudPage, type AiAdminSearchState } from './AiAdminCrudPage';
import { aiChatApi, type AiPromptTemplateDto, type AiPromptTemplateUpsertRequest } from './api/aiCenter.api';

const defaultFormState: AiPromptTemplateUpsertRequest = {
  category: 'general',
  isEnabled: true,
  sortOrder: 0,
  systemPrompt: '',
  templateCode: '',
  templateName: '',
  userPromptTemplate: '',
  variablesJson: ''
};

export function AiPromptTemplatesPage() {
  const { translate } = useI18n();
  const fields = useMemo<FormFieldConfig<AiPromptTemplateUpsertRequest>[]>(
    () => [
      { label: translate('ai.promptTemplates.field.templateCode'), name: 'templateCode', required: true, span: 1, type: 'text' },
      { label: translate('ai.promptTemplates.field.templateName'), name: 'templateName', required: true, span: 1, type: 'text' },
      { label: translate('ai.promptTemplates.field.category'), name: 'category', required: true, span: 1, type: 'text' },
      { label: translate('common.enabled'), name: 'isEnabled', span: 1, type: 'switch' },
      { label: translate('ai.promptTemplates.field.systemPrompt'), name: 'systemPrompt', required: true, rows: 8, span: 2, type: 'textarea' },
      { label: translate('ai.promptTemplates.field.userPromptTemplate'), name: 'userPromptTemplate', rows: 5, span: 2, type: 'textarea' },
      { label: translate('ai.promptTemplates.field.variablesJson'), name: 'variablesJson', rows: 5, span: 2, type: 'textarea' },
      { label: translate('ai.promptTemplates.field.sortOrder'), name: 'sortOrder', span: 1, type: 'number' }
    ],
    [translate]
  );

  const columns = useMemo<DataTableColumn<AiPromptTemplateDto>[]>(
    () => [
      { key: 'templateCode', responsivePriority: 95, title: translate('ai.promptTemplates.column.templateCode'), width: '170px' },
      { key: 'templateName', responsivePriority: 100, title: translate('ai.promptTemplates.column.templateName') },
      { key: 'category', responsivePriority: 75, title: translate('ai.promptTemplates.column.category'), width: '120px' },
      { key: 'sortOrder', hideBelow: 'lg', responsivePriority: 45, title: translate('ai.promptTemplates.column.sortOrder'), width: '80px' },
      {
        key: 'isEnabled',
        responsivePriority: 85,
        title: translate('ai.promptTemplates.column.status'),
        width: '100px',
        render: (row) => renderAiStatusPill(row.isEnabled, translate)
      }
    ],
    [translate]
  );

  const mutationApi = useMemo<Omit<CrudApi<AiPromptTemplateDto, AiPromptTemplateUpsertRequest, AiPromptTemplateUpsertRequest, AiAdminSearchState>, 'list'>>(
    () => ({
      create: aiChatApi.prompts.create,
      delete: aiChatApi.prompts.delete,
      update: aiChatApi.prompts.update
    }),
    []
  );

  return (
    <AiAdminCrudPage
      columns={columns}
      createPermission="ai:prompt:edit"
      defaultFormState={defaultFormState}
      description={translate('ai.promptTemplates.description')}
      editPermission="ai:prompt:edit"
      fields={fields}
      itemName={translate('ai.promptTemplates.itemName')}
      list={aiChatApi.prompts.list}
      mapToFormState={(item) => ({
        category: item.category,
        isEnabled: item.isEnabled,
        sortOrder: item.sortOrder,
        systemPrompt: item.systemPrompt,
        templateCode: item.templateCode,
        templateName: item.templateName,
        userPromptTemplate: item.userPromptTemplate ?? '',
        variablesJson: item.variablesJson ?? ''
      })}
      mutationApi={mutationApi}
      title={translate('ai.promptTemplates.title')}
    />
  );
}

function renderAiStatusPill(enabled: boolean, translate: (key: string) => string) {
  return (
    <span className={`ai-status-badge${enabled ? ' ai-status-badge--success' : ''}`}>
      {enabled ? translate('common.enabled') : translate('common.disabled')}
    </span>
  );
}
