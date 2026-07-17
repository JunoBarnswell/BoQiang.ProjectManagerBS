import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../shared/feedback/useMessage';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import type { AiSecuritySettingsDto } from './api/aiCenter.api';
import { aiSecurityApi } from './api/security.api';

import './styles/ai-center.css';

const defaultSettings: AiSecuritySettingsDto = {
  allowReasoningDisplay: true,
  maxContextMessages: 40,
  maxInputCharacters: 16000,
  maxParallelAgents: 3,
  multiAgentFailurePolicy: 'SkipFailed',
  requireToolConfirmation: true
};

export function AiSecuritySettingsPage() {
  const { translate } = useI18n();
  const queryClient = useQueryClient();
  const message = useMessage();
  const [settings, setSettings] = useState(defaultSettings);
  const [saving, setSaving] = useState(false);

  const query = useApiQuery({
    queryKey: ['ai', 'security'],
    queryFn: ({ signal }) => aiSecurityApi.policy(signal)
  });

  useEffect(() => {
    if (query.data?.data) {
      setSettings(query.data.data);
    }
  }, [query.data?.data]);

  const handleSave = async () => {
    try {
      setSaving(true);
      const response = await aiSecurityApi.updatePolicy(settings);
      setSettings(response.data);
      await queryClient.invalidateQueries({ queryKey: ['ai', 'security'] });
      message.success(translate('ai.securitySettings.success.save'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('ai.securitySettings.error.save')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <CrudPage
      actions={
        <PermissionButton className="primary-button" code="ai:security:edit" disabled={saving} type="button" onClick={() => void handleSave()}>
          {saving ? translate('common.loading') : translate('ai.securitySettings.actions.saveSettings')}
        </PermissionButton>
      }
      className="ai-chat-page"
      description={translate('ai.securitySettings.description')}
      eyebrow={translate('ai.eyebrow')}
      title={translate('ai.securitySettings.title')}
    >
      <div className="ai-security-form">
        <label className="ai-field">
          <span>{translate('ai.securitySettings.field.maxParallelAgents')}</span>
          <input
            min={1}
            type="number"
            value={settings.maxParallelAgents}
            onChange={(event) => setSettings((current) => ({ ...current, maxParallelAgents: Number(event.target.value) }))}
          />
        </label>
        <label className="ai-field">
          <span>{translate('ai.securitySettings.field.maxInputCharacters')}</span>
          <input
            min={1}
            type="number"
            value={settings.maxInputCharacters}
            onChange={(event) => setSettings((current) => ({ ...current, maxInputCharacters: Number(event.target.value) }))}
          />
        </label>
        <label className="ai-field">
          <span>{translate('ai.securitySettings.field.maxContextMessages')}</span>
          <input
            min={1}
            type="number"
            value={settings.maxContextMessages}
            onChange={(event) => setSettings((current) => ({ ...current, maxContextMessages: Number(event.target.value) }))}
          />
        </label>
        <label className="ai-field">
          <span>{translate('ai.securitySettings.field.multiAgentFailurePolicy')}</span>
          <select
            value={settings.multiAgentFailurePolicy}
            onChange={(event) => setSettings((current) => ({ ...current, multiAgentFailurePolicy: event.target.value }))}
          >
            <option value="SkipFailed">{translate('ai.securitySettings.option.skipFailed')}</option>
            <option value="FailAll">{translate('ai.securitySettings.option.failAll')}</option>
          </select>
        </label>
        <div className="ai-two-columns">
          <label>
            <span>{translate('ai.securitySettings.field.requireToolConfirmation')}</span>
            <input
              checked={settings.requireToolConfirmation}
              type="checkbox"
              onChange={(event) => setSettings((current) => ({ ...current, requireToolConfirmation: event.target.checked }))}
            />
          </label>
          <label>
            <span>{translate('ai.securitySettings.field.allowReasoningDisplay')}</span>
            <input
              checked={settings.allowReasoningDisplay}
              type="checkbox"
              onChange={(event) => setSettings((current) => ({ ...current, allowReasoningDisplay: event.target.checked }))}
            />
          </label>
        </div>
        <div className="ai-security-actions">
          <button className="ghost-button" disabled={query.isFetching} type="button" onClick={() => void query.refetch()}>
            {translate('common.refresh')}
          </button>
          <PermissionButton className="primary-button" code="ai:security:edit" disabled={saving} type="button" onClick={() => void handleSave()}>
            {translate('common.save')}
          </PermissionButton>
        </div>
      </div>
    </CrudPage>
  );
}
