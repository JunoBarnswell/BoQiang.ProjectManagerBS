import { useMemo } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import type { CrudApi } from '../../shared/components/crud-page/useCrudResource';
import { useMessage } from '../../shared/feedback/useMessage';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import { AiAdminCrudPage, type AiAdminSearchState } from './AiAdminCrudPage';
import { aiChatApi, type AiProviderDto, type AiProviderUpsertRequest } from './api/aiCenter.api';

const defaultFormState: AiProviderUpsertRequest = {
  apiKey: '',
  baseUrl: '',
  extraParametersJson: '',
  isEnabled: true,
  protocolType: 'DeepSeek',
  providerCode: '',
  providerName: '',
  timeoutSeconds: 120
};

export function AiModelProvidersPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const fields = useMemo<FormFieldConfig<AiProviderUpsertRequest>[]>(
    () => [
      { label: translate('ai.modelProviders.field.providerCode'), name: 'providerCode', required: true, span: 1, type: 'text' },
      { label: translate('ai.modelProviders.field.providerName'), name: 'providerName', required: true, span: 1, type: 'text' },
      {
        label: translate('ai.modelProviders.field.protocolType'),
        name: 'protocolType',
        options: [
          { label: translate('ai.modelProviders.option.deepSeek'), value: 'DeepSeek' },
          { label: translate('ai.modelProviders.option.glm'), value: 'GLM' },
          { label: translate('ai.modelProviders.option.zhipu'), value: 'Zhipu' },
          { label: translate('ai.modelProviders.option.openAiCompatible'), value: 'OpenAiCompatible' }
        ],
        required: true,
        span: 1,
        type: 'select'
      },
      { label: translate('ai.modelProviders.field.baseUrl'), name: 'baseUrl', required: true, span: 2, type: 'text' },
      { helpText: translate('ai.modelProviders.help.apiKey'), label: translate('ai.modelProviders.field.apiKey'), name: 'apiKey', span: 2, type: 'text' },
      { label: translate('ai.modelProviders.field.timeoutSeconds'), name: 'timeoutSeconds', required: true, span: 1, type: 'number' },
      { label: translate('common.enabled'), name: 'isEnabled', span: 1, type: 'switch' },
      { label: translate('ai.modelProviders.field.extraParametersJson'), name: 'extraParametersJson', rows: 6, span: 2, type: 'textarea' }
    ],
    [translate]
  );

  const columns = useMemo<DataTableColumn<AiProviderDto>[]>(
    () => [
      { key: 'providerCode', responsivePriority: 95, title: translate('ai.modelProviders.column.providerCode'), width: '150px' },
      { key: 'providerName', responsivePriority: 100, title: translate('ai.modelProviders.column.providerName') },
      { key: 'protocolType', responsivePriority: 80, title: translate('ai.modelProviders.column.protocolType'), width: '150px' },
      { key: 'baseUrl', hideBelow: 'xl', responsivePriority: 50, title: translate('ai.modelProviders.column.baseUrl') },
      { key: 'apiKeyMask', responsivePriority: 70, title: translate('ai.modelProviders.column.apiKeyMask'), width: '130px' },
      {
        key: 'isEnabled',
        responsivePriority: 90,
        title: translate('ai.modelProviders.column.status'),
        width: '110px',
        render: (row) => renderAiStatusPill(row.isEnabled, translate)
      }
    ],
    [translate]
  );

  const mutationApi = useMemo<Omit<CrudApi<AiProviderDto, AiProviderUpsertRequest, AiProviderUpsertRequest, AiAdminSearchState>, 'list'>>(
    () => ({
      create: aiChatApi.providers.create,
      delete: aiChatApi.providers.delete,
      update: aiChatApi.providers.update
    }),
    []
  );

  return (
    <AiAdminCrudPage
      columns={columns}
      createPermission="ai:model:edit"
      defaultFormState={defaultFormState}
      description={translate('ai.modelProviders.description')}
      editPermission="ai:model:edit"
      extraRowActions={(row) => (
        <PermissionButton
          className="ghost-button"
          code="ai:model:edit"
          fallback="disable"
          type="button"
          onClick={async () => {
            try {
              await aiChatApi.providers.test(row.id);
              message.success(translate('ai.modelProviders.success.test'));
            } catch (error) {
              message.error(getErrorMessage(error, translate('ai.modelProviders.error.test')));
            }
          }}
        >
          {translate('ai.modelProviders.actions.test')}
        </PermissionButton>
      )}
      fields={fields}
      itemName={translate('ai.modelProviders.itemName')}
      list={aiChatApi.providers.list}
      mapToFormState={(item) => ({
        apiKey: '',
        baseUrl: item.baseUrl,
        extraParametersJson: item.extraParametersJson ?? '',
        isEnabled: item.isEnabled,
        protocolType: item.protocolType,
        providerCode: item.providerCode,
        providerName: item.providerName,
        timeoutSeconds: item.timeoutSeconds
      })}
      mutationApi={mutationApi}
      title={translate('ai.modelProviders.title')}
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
