import { useMemo } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { AiStatusBadge } from '../../shared/components/ai-chat/AiStatusBadge';
import type { CrudApi } from '../../shared/components/crud-page/useCrudResource';
import type { FormFieldConfig } from '../../shared/forms/formTypes';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { AiAdminCrudPage, type AiAdminSearchState } from './AiAdminCrudPage';
import { aiChatApi, type AiAgentProfileDto, type AiAgentProfileUpsertRequest } from './api/aiCenter.api';

const defaultFormState: AiAgentProfileUpsertRequest = {
  agentCode: '',
  agentName: '',
  isCoordinator: false,
  isEnabled: true,
  modelConfigId: '',
  promptTemplateId: '',
  rolePrompt: '',
  sortOrder: 0,
  allowedFunctionsJson: ''
};

export function AiAgentProfilesPage() {
  const { translate } = useI18n();
  const modelsQuery = useApiQuery({
    queryKey: ['ai', 'models', 'options'],
    queryFn: ({ signal }) => aiChatApi.models.options(signal)
  });
  const promptsQuery = useApiQuery({
    queryKey: ['ai', 'prompts', 'options'],
    queryFn: ({ signal }) => aiChatApi.prompts.options(signal)
  });

  const modelOptions = useMemo(() => modelsQuery.data?.data ?? [], [modelsQuery.data?.data]);
  const promptOptions = useMemo(() => promptsQuery.data?.data ?? [], [promptsQuery.data?.data]);

  const fields = useMemo<FormFieldConfig<AiAgentProfileUpsertRequest>[]>(
    () => [
      { label: translate('ai.agentProfiles.field.agentCode'), name: 'agentCode', required: true, span: 1, type: 'text' },
      { label: translate('ai.agentProfiles.field.agentName'), name: 'agentName', required: true, span: 1, type: 'text' },
      {
        label: translate('ai.agentProfiles.field.modelConfigId'),
        name: 'modelConfigId',
        options: [{ label: translate('ai.agentProfiles.option.followConversationModel'), value: '' }, ...modelOptions.map((item) => ({ label: item.displayName, value: item.id }))],
        span: 1,
        type: 'select'
      },
      {
        label: translate('ai.agentProfiles.field.promptTemplateId'),
        name: 'promptTemplateId',
        options: [{ label: translate('ai.agentProfiles.option.followConversationTemplate'), value: '' }, ...promptOptions.map((item) => ({ label: item.templateName, value: item.id }))],
        span: 1,
        type: 'select'
      },
      { label: translate('ai.agentProfiles.field.isCoordinator'), name: 'isCoordinator', span: 1, type: 'switch' },
      { label: translate('common.enabled'), name: 'isEnabled', span: 1, type: 'switch' },
      { label: translate('ai.agentProfiles.field.rolePrompt'), name: 'rolePrompt', required: true, rows: 8, span: 2, type: 'textarea' },
      { label: translate('ai.agentProfiles.field.allowedFunctionsJson'), name: 'allowedFunctionsJson', rows: 6, span: 2, type: 'textarea' },
      { label: translate('ai.agentProfiles.field.sortOrder'), name: 'sortOrder', span: 1, type: 'number' }
    ],
    [modelOptions, promptOptions, translate]
  );

  const columns = useMemo<DataTableColumn<AiAgentProfileDto>[]>(
    () => [
      { key: 'agentCode', responsivePriority: 95, title: translate('ai.agentProfiles.column.agentCode'), width: '160px' },
      { key: 'agentName', responsivePriority: 100, title: translate('ai.agentProfiles.column.agentName') },
      {
        key: 'isCoordinator',
        responsivePriority: 80,
        title: translate('ai.agentProfiles.column.isCoordinator'),
        width: '100px',
        render: (row) => (row.isCoordinator ? translate('common.yes') : translate('common.no'))
      },
      { key: 'sortOrder', hideBelow: 'lg', responsivePriority: 45, title: translate('ai.agentProfiles.column.sortOrder'), width: '80px' },
      {
        key: 'isEnabled',
        responsivePriority: 85,
        title: translate('ai.agentProfiles.column.status'),
        width: '100px',
        render: (row) => <AiStatusBadge status={row.isEnabled ? 'Enabled' : 'Disabled'} />
      }
    ],
    [translate]
  );

  const mutationApi = useMemo<Omit<CrudApi<AiAgentProfileDto, AiAgentProfileUpsertRequest, AiAgentProfileUpsertRequest, AiAdminSearchState>, 'list'>>(
    () => ({
      create: aiChatApi.agents.create,
      delete: aiChatApi.agents.delete,
      update: aiChatApi.agents.update
    }),
    []
  );

  return (
    <AiAdminCrudPage
      columns={columns}
      createPermission="ai:prompt:edit"
      defaultFormState={defaultFormState}
      description={translate('ai.agentProfiles.description')}
      editPermission="ai:prompt:edit"
      fields={fields}
      itemName={translate('ai.agentProfiles.itemName')}
      list={aiChatApi.agents.list}
      mapToFormState={(item) => ({
        agentCode: item.agentCode,
        agentName: item.agentName,
        isCoordinator: item.isCoordinator,
        isEnabled: item.isEnabled,
        modelConfigId: item.modelConfigId ?? '',
        promptTemplateId: item.promptTemplateId ?? '',
        rolePrompt: item.rolePrompt,
        sortOrder: item.sortOrder,
        allowedFunctionsJson: item.allowedFunctionsJson ?? ''
      })}
      mutationApi={mutationApi}
      title={translate('ai.agentProfiles.title')}
    />
  );
}
