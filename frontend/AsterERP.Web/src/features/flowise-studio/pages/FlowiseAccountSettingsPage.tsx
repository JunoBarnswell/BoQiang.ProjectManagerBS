import { Stack, TextField } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../shared/auth/permissionCodes';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { flowiseStudioApi } from '../api/flowiseStudio.api';
import type { FlowiseAccountSettingsDto } from '../flowiseStudio.types';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { MainCard } from '../native/ui-component/cards/MainCard';

const emptyAccount: FlowiseAccountSettingsDto = {
  displayName: '',
  email: '',
  preferencesJson: '{}'
};

export function FlowiseAccountSettingsPage() {
  const { translate } = useI18n();
  const message = useMessage();
  const [form, setForm] = useState<FlowiseAccountSettingsDto>(emptyAccount);
  const accountQuery = useApiQuery({
    queryKey: ['flowise', 'account'],
    queryFn: ({ signal }) => flowiseStudioApi.account.get(signal)
  });

  const updateMutation = useApiMutation({
    mutationFn: flowiseStudioApi.account.update,
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: (response) => {
      setForm(response.data);
      message.success(translate(flowiseI18nKeys.common.save));
    }
  });

  useEffect(() => {
    if (accountQuery.data?.data) {
      setForm(accountQuery.data.data);
    }
  }, [accountQuery.data]);

  return (
    <MainCard
      actions={
        <PermissionButton className="btn-primary" code={flowisePermissions.accountEdit} disabled={updateMutation.isPending} onClick={() => updateMutation.mutate(form)} type="button">
          {translate(flowiseI18nKeys.actions.save)}
        </PermissionButton>
      }
      description={translate(flowiseI18nKeys.pages.account)}
      title={translate(flowiseI18nKeys.pages.account)}
    >
      <Stack className="flowise-settings-panel" spacing={2}>
        <TextField
          fullWidth
          label={translate(flowiseI18nKeys.fields.displayName)}
          value={form.displayName}
          onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
        />
        <TextField
          fullWidth
          label={translate(flowiseI18nKeys.fields.email)}
          type="email"
          value={form.email ?? ''}
          onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
        />
        <TextField
          fullWidth
          multiline
          label={translate(flowiseI18nKeys.fields.preferencesJson)}
          minRows={10}
          value={form.preferencesJson}
          onChange={(event) => setForm((current) => ({ ...current, preferencesJson: event.target.value }))}
        />
      </Stack>
    </MainCard>
  );
}
