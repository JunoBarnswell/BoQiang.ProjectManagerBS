import { useMemo } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { AiStatusBadge } from '../../shared/components/ai-chat/AiStatusBadge';
import type { CrudApi } from '../../shared/components/crud-page/useCrudResource';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { AiAdminCrudPage, type AiAdminSearchState } from './AiAdminCrudPage';
import { aiChatApi, type AiModelConfigDto, type AiModelConfigUpsertRequest } from './api/aiCenter.api';

const defaultFormState: AiModelConfigUpsertRequest = {
  defaultTemperature: 0.7,
  defaultTopP: 0.95,
  displayName: '',
  isEnabled: true,
  maxContextTokens: 64000,
  maxOutputTokens: 8192,
  maxParallelRuns: 3,
  modelCode: '',
  providerId: '',
  reasoningEffort: 'high',
  sortOrder: 0,
  thinkingEnabledDefault: true,
  toolStreamEnabledDefault: false
};

export function AiModelConfigsPage() {
  const { translate } = useI18n();
  const providersQuery = useApiQuery({
    queryKey: ['ai', 'providers', 'options'],
    queryFn: ({ signal }) => aiChatApi.providers.options(signal)
  });

  const providerOptions = useMemo(() => providersQuery.data?.data ?? [], [providersQuery.data?.data]);
  const fields = useMemo<FormFieldConfig<AiModelConfigUpsertRequest>[]>(
    () => [
      {
        label: translate('ai.modelConfigs.field.providerId'),
        name: 'providerId',
        options: providerOptions.map((item) => ({ label: item.providerName, value: item.id })),
        required: true,
        span: 2,
        type: 'select'
      },
      { label: translate('ai.modelConfigs.field.modelCode'), name: 'modelCode', required: true, span: 1, type: 'text' },
      { label: translate('ai.modelConfigs.field.displayName'), name: 'displayName', required: true, span: 1, type: 'text' },
      { label: translate('ai.modelConfigs.field.maxContextTokens'), name: 'maxContextTokens', required: true, span: 1, type: 'number' },
      { label: translate('ai.modelConfigs.field.maxOutputTokens'), name: 'maxOutputTokens', required: true, span: 1, type: 'number' },
      { label: translate('ai.modelConfigs.field.defaultTemperature'), name: 'defaultTemperature', span: 1, type: 'number' },
      { label: translate('ai.modelConfigs.field.defaultTopP'), name: 'defaultTopP', span: 1, type: 'number' },
      {
        label: translate('ai.modelConfigs.field.reasoningEffort'),
        name: 'reasoningEffort',
        options: [
          { label: translate('ai.modelConfigs.reasoningEffort.low'), value: 'low' },
          { label: translate('ai.modelConfigs.reasoningEffort.medium'), value: 'medium' },
          { label: translate('ai.modelConfigs.reasoningEffort.high'), value: 'high' },
          { label: translate('ai.modelConfigs.reasoningEffort.max'), value: 'max' }
        ],
        span: 1,
        type: 'select'
      },
      { label: translate('ai.modelConfigs.field.maxParallelRuns'), name: 'maxParallelRuns', required: true, span: 1, type: 'number' },
      { label: translate('ai.modelConfigs.field.thinkingEnabledDefault'), name: 'thinkingEnabledDefault', span: 1, type: 'switch' },
      { label: translate('ai.modelConfigs.field.toolStreamEnabledDefault'), name: 'toolStreamEnabledDefault', span: 1, type: 'switch' },
      { label: translate('common.enabled'), name: 'isEnabled', span: 1, type: 'switch' },
      { label: translate('ai.modelConfigs.field.sortOrder'), name: 'sortOrder', span: 1, type: 'number' }
    ],
    [providerOptions, translate]
  );

  const columns = useMemo<DataTableColumn<AiModelConfigDto>[]>(
    () => [
      { key: 'displayName', responsivePriority: 100, title: translate('ai.modelConfigs.column.displayName') },
      { key: 'modelCode', responsivePriority: 95, title: translate('ai.modelConfigs.column.modelCode'), width: '190px' },
      { key: 'providerName', responsivePriority: 90, title: translate('ai.modelConfigs.column.providerName'), width: '150px' },
      { key: 'maxContextTokens', hideBelow: 'lg', responsivePriority: 70, title: translate('ai.modelConfigs.column.maxContextTokens'), width: '110px' },
      { key: 'reasoningEffort', hideBelow: 'xl', responsivePriority: 45, title: translate('ai.modelConfigs.column.reasoningEffort'), width: '100px' },
      {
        key: 'thinkingEnabledDefault',
        hideBelow: 'lg',
        responsivePriority: 65,
        title: translate('ai.modelConfigs.column.thinkingEnabledDefault'),
        width: '90px',
        render: (row) => (row.thinkingEnabledDefault ? translate('ai.modelConfigs.thinking.enabled') : translate('ai.modelConfigs.thinking.disabled'))
      },
      {
        key: 'isEnabled',
        responsivePriority: 85,
        title: translate('ai.modelConfigs.column.status'),
        width: '100px',
        render: (row) => <AiStatusBadge status={row.isEnabled ? 'Enabled' : 'Disabled'} />
      }
    ],
    [translate]
  );

  const mutationApi = useMemo<Omit<CrudApi<AiModelConfigDto, AiModelConfigUpsertRequest, AiModelConfigUpsertRequest, AiAdminSearchState>, 'list'>>(
    () => ({
      create: aiChatApi.models.create,
      delete: aiChatApi.models.delete,
      update: aiChatApi.models.update
    }),
    []
  );

  return (
    <AiAdminCrudPage
      columns={columns}
      createPermission="ai:model:add"
      defaultFormState={defaultFormState}
      description={translate('ai.modelConfigs.description')}
      editPermission="ai:model:edit"
      fields={fields}
      itemName={translate('ai.modelConfigs.itemName')}
      list={aiChatApi.models.list}
      mapToFormState={(item) => ({
        defaultTemperature: item.defaultTemperature,
        defaultTopP: item.defaultTopP,
        displayName: item.displayName,
        isEnabled: item.isEnabled,
        maxContextTokens: item.maxContextTokens,
        maxOutputTokens: item.maxOutputTokens,
        maxParallelRuns: item.maxParallelRuns,
        modelCode: item.modelCode,
        providerId: item.providerId,
        reasoningEffort: item.reasoningEffort ?? 'high',
        sortOrder: item.sortOrder,
        thinkingEnabledDefault: item.thinkingEnabledDefault,
        toolStreamEnabledDefault: item.toolStreamEnabledDefault
      })}
      mutationApi={mutationApi}
      title={translate('ai.capability.models')}
    />
  );
}
