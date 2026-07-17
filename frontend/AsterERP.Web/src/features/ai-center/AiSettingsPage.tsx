import { useQueryClient } from '@tanstack/react-query';
import { Archive, Bell, Bot, DatabaseBackup, Download, Save, Upload } from 'lucide-react';
import type { ReactNode } from 'react';
import { useEffect, useMemo, useState } from 'react';

import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../shared/feedback/useMessage';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import type { AiSettingsDto } from './api/aiCenter.api';
import { aiCapabilityApi } from './api/capability.api';
import { aiSettingsApi } from './api/settings.api';

import './styles/ai-center.css';

const defaultSettings: AiSettingsDto = {
  cleanupBatchSize: 500,
  defaultAgentProfileId: '',
  defaultModelConfigId: '',
  defaultPromptTemplateId: '',
  defaultProviderId: '',
  logRetentionDays: 180,
  notificationSettingsJson: '{}'
};

export function AiSettingsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const queryClient = useQueryClient();
  const [settings, setSettings] = useState(defaultSettings);
  const [exchangeJson, setExchangeJson] = useState('');
  const [cleanupDays, setCleanupDays] = useState(180);
  const [saving, setSaving] = useState(false);

  const settingsQuery = useApiQuery({
    queryKey: ['ai', 'settings'],
    queryFn: ({ signal }) => aiSettingsApi.get(signal)
  });
  const providersQuery = useApiQuery({
    queryKey: ['ai', 'providers', 'options'],
    queryFn: ({ signal }) => aiCapabilityApi.providers.options(signal)
  });
  const modelsQuery = useApiQuery({
    queryKey: ['ai', 'models', 'options'],
    queryFn: ({ signal }) => aiCapabilityApi.models.options(signal)
  });
  const agentsQuery = useApiQuery({
    queryKey: ['ai', 'agents', 'options'],
    queryFn: ({ signal }) => aiCapabilityApi.agents.options(signal)
  });
  const promptsQuery = useApiQuery({
    queryKey: ['ai', 'prompts', 'options'],
    queryFn: ({ signal }) => aiCapabilityApi.prompts.options(signal)
  });

  useEffect(() => {
    if (settingsQuery.data?.data) {
      setSettings(settingsQuery.data.data);
      setCleanupDays(settingsQuery.data.data.logRetentionDays);
    }
  }, [settingsQuery.data?.data]);

  const modelOptions = useMemo(() => modelsQuery.data?.data ?? [], [modelsQuery.data?.data]);
  const providerOptions = useMemo(() => providersQuery.data?.data ?? [], [providersQuery.data?.data]);
  const agentOptions = useMemo(() => agentsQuery.data?.data ?? [], [agentsQuery.data?.data]);
  const promptOptions = useMemo(() => promptsQuery.data?.data ?? [], [promptsQuery.data?.data]);
  const isOptionLoading = providersQuery.isFetching || modelsQuery.isFetching || agentsQuery.isFetching || promptsQuery.isFetching;
  const notificationJsonValid = useMemo(() => isValidJson(settings.notificationSettingsJson), [settings.notificationSettingsJson]);
  const exchangeJsonValid = useMemo(() => !exchangeJson.trim() || isValidJson(exchangeJson), [exchangeJson]);
  const defaultSummaries = [
    {
      key: 'provider',
      label: translate('ai.settings.field.defaultProviderId'),
      value: providerOptions.find((item) => item.id === settings.defaultProviderId)?.providerName ?? translate('ai.settings.option.unassigned')
    },
    {
      key: 'model',
      label: translate('ai.settings.field.defaultModelConfigId'),
      value: modelOptions.find((item) => item.id === settings.defaultModelConfigId)?.displayName ?? translate('ai.settings.option.unassigned')
    },
    {
      key: 'agent',
      label: translate('ai.settings.field.defaultAgentProfileId'),
      value: agentOptions.find((item) => item.id === settings.defaultAgentProfileId)?.agentName ?? translate('ai.settings.option.unassigned')
    },
    {
      key: 'prompt',
      label: translate('ai.settings.field.defaultPromptTemplateId'),
      value: promptOptions.find((item) => item.id === settings.defaultPromptTemplateId)?.templateName ?? translate('ai.settings.option.unassigned')
    }
  ];

  const handleSave = async () => {
    if (!notificationJsonValid) {
      message.error(translate('ai.settings.error.invalidNotificationJson'));
      return;
    }

    try {
      setSaving(true);
      const response = await aiSettingsApi.update(settings);
      setSettings(response.data);
      await queryClient.invalidateQueries({ queryKey: ['ai', 'settings'] });
      message.success(translate('ai.settings.success.save'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.settings.error.save')));
    } finally {
      setSaving(false);
    }
  };

  const handleExport = async () => {
    try {
      const response = await aiSettingsApi.export();
      setExchangeJson(JSON.stringify(response.data, null, 2));
      message.success(translate('ai.settings.success.export'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.settings.error.export')));
    }
  };

  const handleImport = async () => {
    if (!exchangeJson.trim() || !exchangeJsonValid) {
      message.error(translate('ai.settings.error.invalidExchangeJson'));
      return;
    }

    try {
      setSaving(true);
      const payload = JSON.parse(exchangeJson);
      const response = await aiSettingsApi.import(payload);
      await queryClient.invalidateQueries({ queryKey: ['ai', 'settings'] });
      message.success(
        formatMessage(translate('ai.settings.success.import'), {
          agentProfilesImported: response.data.agentProfilesImported,
          promptTemplatesImported: response.data.promptTemplatesImported,
          settingsUpdated: response.data.settingsUpdated
        })
      );
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.settings.error.import')));
    } finally {
      setSaving(false);
    }
  };

  const handleCleanup = async () => {
    try {
      setSaving(true);
      const response = await aiSettingsApi.cleanup({ batchSize: settings.cleanupBatchSize, retentionDays: cleanupDays });
      message.success(
        formatMessage(translate('ai.settings.success.cleanup'), {
          indexTasksDeleted: response.data.indexTasksDeleted,
          toolExecutionsDeleted: response.data.toolExecutionsDeleted,
          usageLogsDeleted: response.data.usageLogsDeleted
        })
      );
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.settings.error.cleanup')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <CrudPage
      actions={
        <PermissionButton className="primary-button" code="ai:settings:edit" disabled={saving || !notificationJsonValid} type="button" onClick={() => void handleSave()}>
          <Save aria-hidden="true" size={15} />
          {translate('ai.settings.actions.saveSettings')}
        </PermissionButton>
      }
      className="ai-chat-page"
      description={translate('ai.settings.description')}
      eyebrow={translate('ai.eyebrow')}
      title={translate('ai.settings.title')}
    >
      <div className="ai-settings-layout">
        <section className="ai-settings-card ai-settings-card--wide">
          <SettingsSectionHeader description={translate('ai.settings.section.defaultsDescription')} icon={<Bot aria-hidden="true" size={18} />} title={translate('ai.settings.section.defaults')} />
          <div className="ai-settings-summary-grid">
            {defaultSummaries.map((item) => (
              <div key={item.key} className="ai-settings-summary-item">
                <span>{item.label}</span>
                <strong>{item.value}</strong>
              </div>
            ))}
          </div>
          <div className="ai-settings-form-grid">
            <label className="ai-field">
              <span>{translate('ai.settings.field.defaultProviderId')}</span>
              <select disabled={saving || isOptionLoading} value={settings.defaultProviderId ?? ''} onChange={(event) => setSettings((current) => ({ ...current, defaultProviderId: event.target.value }))}>
                <option value="">{translate('ai.settings.option.unassigned')}</option>
                {providerOptions.map((provider) => (
                  <option key={provider.id} value={provider.id}>
                    {provider.providerName}
                  </option>
                ))}
              </select>
              <em>{formatMessage(translate('ai.settings.meta.availableCount'), { count: providerOptions.length })}</em>
            </label>
            <label className="ai-field">
              <span>{translate('ai.settings.field.defaultModelConfigId')}</span>
              <select disabled={saving || isOptionLoading} value={settings.defaultModelConfigId ?? ''} onChange={(event) => setSettings((current) => ({ ...current, defaultModelConfigId: event.target.value }))}>
                <option value="">{translate('ai.settings.option.unassigned')}</option>
                {modelOptions.map((model) => (
                  <option key={model.id} value={model.id}>
                    {model.displayName}
                  </option>
                ))}
              </select>
              <em>{formatMessage(translate('ai.settings.meta.availableCount'), { count: modelOptions.length })}</em>
            </label>
            <label className="ai-field">
              <span>{translate('ai.settings.field.defaultAgentProfileId')}</span>
              <select disabled={saving || isOptionLoading} value={settings.defaultAgentProfileId ?? ''} onChange={(event) => setSettings((current) => ({ ...current, defaultAgentProfileId: event.target.value }))}>
                <option value="">{translate('ai.settings.option.unassigned')}</option>
                {agentOptions.map((agent) => (
                  <option key={agent.id} value={agent.id}>
                    {agent.agentName}
                  </option>
                ))}
              </select>
              <em>{formatMessage(translate('ai.settings.meta.availableCount'), { count: agentOptions.length })}</em>
            </label>
            <label className="ai-field">
              <span>{translate('ai.settings.field.defaultPromptTemplateId')}</span>
              <select disabled={saving || isOptionLoading} value={settings.defaultPromptTemplateId ?? ''} onChange={(event) => setSettings((current) => ({ ...current, defaultPromptTemplateId: event.target.value }))}>
                <option value="">{translate('ai.settings.option.unassigned')}</option>
                {promptOptions.map((prompt) => (
                  <option key={prompt.id} value={prompt.id}>
                    {prompt.templateName}
                  </option>
                ))}
              </select>
              <em>{formatMessage(translate('ai.settings.meta.availableCount'), { count: promptOptions.length })}</em>
            </label>
          </div>
        </section>

        <section className="ai-settings-card">
          <SettingsSectionHeader description={translate('ai.settings.section.governanceDescription')} icon={<Archive aria-hidden="true" size={18} />} title={translate('ai.settings.section.governance')} />
          <div className="ai-settings-form-grid ai-settings-form-grid--compact">
            <label className="ai-field">
              <span>{translate('ai.settings.field.logRetentionDays')}</span>
              <input disabled={saving} min={1} type="number" value={settings.logRetentionDays} onChange={(event) => setSettings((current) => ({ ...current, logRetentionDays: Number(event.target.value) }))} />
            </label>
            <label className="ai-field">
              <span>{translate('ai.settings.field.cleanupBatchSize')}</span>
              <input disabled={saving} min={1} type="number" value={settings.cleanupBatchSize} onChange={(event) => setSettings((current) => ({ ...current, cleanupBatchSize: Number(event.target.value) }))} />
            </label>
            <label className="ai-field">
              <span>{translate('ai.settings.field.cleanupRetentionDays')}</span>
              <input disabled={saving} min={1} type="number" value={cleanupDays} onChange={(event) => setCleanupDays(Number(event.target.value))} />
              <em>{translate('ai.settings.help.cleanupRetentionDays')}</em>
            </label>
            <PermissionButton className="danger-button ai-settings-card-action" code="ai:settings:edit" disabled={saving} type="button" onClick={() => void handleCleanup()}>
              <Archive aria-hidden="true" size={15} />
              {translate('ai.settings.actions.cleanup')}
            </PermissionButton>
          </div>
        </section>

        <section className="ai-settings-card">
          <SettingsSectionHeader description={translate('ai.settings.section.notificationDescription')} icon={<Bell aria-hidden="true" size={18} />} title={translate('ai.settings.section.notification')} />
          <label className={`ai-field ai-settings-json-field${notificationJsonValid ? '' : ' ai-field--error'}`}>
            <span>{translate('ai.settings.field.notificationSettingsJson')}</span>
            <textarea aria-invalid={!notificationJsonValid} disabled={saving} value={settings.notificationSettingsJson} onChange={(event) => setSettings((current) => ({ ...current, notificationSettingsJson: event.target.value }))} />
            <em>{notificationJsonValid ? translate('ai.settings.meta.jsonValid') : translate('ai.settings.meta.jsonInvalid')}</em>
          </label>
        </section>

        <section className="ai-settings-card ai-settings-card--wide">
          <SettingsSectionHeader description={translate('ai.settings.section.exchangeDescription')} icon={<DatabaseBackup aria-hidden="true" size={18} />} title={translate('ai.settings.section.exchange')} />
          <div className="ai-settings-exchange-toolbar">
            <button className="ghost-button" disabled={saving} type="button" onClick={() => void handleExport()}>
              <Download aria-hidden="true" size={15} />
              {translate('ai.settings.actions.export')}
            </button>
            <PermissionButton className="primary-button" code="ai:settings:edit" disabled={saving || !exchangeJson.trim() || !exchangeJsonValid} type="button" onClick={() => void handleImport()}>
              <Upload aria-hidden="true" size={15} />
              {translate('ai.settings.actions.import')}
            </PermissionButton>
            <span className={`ai-settings-json-state${exchangeJsonValid ? '' : ' ai-settings-json-state--error'}`}>
              {exchangeJsonValid ? translate('ai.settings.meta.jsonReady') : translate('ai.settings.meta.jsonInvalid')}
            </span>
          </div>
          <textarea className="ai-settings-json" placeholder={translate('ai.settings.placeholder.exchangeJson')} value={exchangeJson} onChange={(event) => setExchangeJson(event.target.value)} />
        </section>
      </div>
    </CrudPage>
  );
}

function SettingsSectionHeader({ description, icon, title }: { description: string; icon: ReactNode; title: string }) {
  return (
    <header className="ai-settings-section-header">
      <span className="ai-settings-section-icon">{icon}</span>
      <div>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
    </header>
  );
}

function isValidJson(value: string) {
  if (!value.trim()) {
    return false;
  }

  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
}
