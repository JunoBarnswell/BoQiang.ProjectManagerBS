import SettingsIcon from '@mui/icons-material/Settings';
import { Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Switch, TextField, Typography } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../../core/query/useApiQuery';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { PermissionMuiButton } from '../../../../../shared/auth/PermissionMuiButton';
import { useMessage } from '../../../../../shared/feedback/useMessage';
import { managementApi } from '../../../api/management.api';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceUpsertRequest } from '../../../types/shared.types';
import { createResourceDraft } from '../common/sourcePageUtils';

export function FlowiseSsoConfigNativePage() {
  const message = useMessage();
  const { translate } = useI18n();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [draft, setDraft] = useState<FlowiseResourceUpsertRequest>(() => createResourceDraft({ resourceKey: 'sso', displayName: 'SSO Config', definitionJson: '{"enabled":false,"provider":"","settingsJson":"{}"}' }));
  const query = useApiQuery({ queryKey: ['flowise-source-sso-config'], queryFn: ({ signal }) => managementApi.sso.get(signal) });
  const config = query.data?.data ?? null;
  const saveMutation = useApiMutation({
    mutationFn: (request: FlowiseResourceUpsertRequest) => config?.id ? managementApi.sso.update(config.id, request) : managementApi.sso.create(request),
    onSuccess: async () => { setDialogOpen(false); message.success(translate(flowiseI18nKeys.source.ssoConfig.saveSuccess)); await query.refetch(); }
  });

  useEffect(() => {
    if (dialogOpen) {
      setDraft(createResourceDraft({
        definitionJson: JSON.stringify({ enabled: config?.enabled ?? false, provider: config?.provider ?? '', settingsJson: config?.settingsJson ?? '{}' }, null, 2),
        displayName: translate(flowiseI18nKeys.pages.ssoConfig),
        metadataJson: '{}',
        resourceKey: 'sso-config',
        status: config?.enabled ? 'Enabled' : 'Disabled'
      }));
    }
  }, [config, dialogOpen, translate]);

  return (
    <section className="flowise-source-page">
      <header className="flowise-source-view-header">
        <div><h1>{translate(flowiseI18nKeys.pages.ssoConfig)}</h1><p>{translate('flowise.native.ssoConfig.description')}</p></div>
        <PermissionMuiButton code={flowisePermissions.ssoManage} startIcon={<SettingsIcon />} variant="contained" onClick={() => setDialogOpen(true)}>
          {translate(flowiseI18nKeys.source.ssoConfig.configure)}
        </PermissionMuiButton>
      </header>
      <Card className="flowise-source-card">
        <CardContent>
          <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
            <Switch checked={config?.enabled ?? false} disabled />
            <div>
              <Typography variant="h6">{config?.provider || translate(flowiseI18nKeys.source.ssoConfig.noProvider)}</Typography>
              <Typography color="text.secondary">{translate(flowiseI18nKeys.source.ssoConfig.currentConfiguration)}</Typography>
            </div>
            <Chip label={config?.enabled ? translate(flowiseI18nKeys.status.enabled) : translate(flowiseI18nKeys.status.disabled)} color={config?.enabled ? 'success' : 'default'} size="small" />
          </Stack>
          <pre className="flowise-source-json-preview">{config?.settingsJson || '{}'}</pre>
        </CardContent>
      </Card>
      <Dialog fullWidth maxWidth="md" open={dialogOpen} onClose={() => setDialogOpen(false)}>
        <DialogTitle>{translate(flowiseI18nKeys.source.ssoConfig.configure)}</DialogTitle>
        <DialogContent><Stack spacing={2} sx={{ pt: 1 }}><TextField multiline label={translate(flowiseI18nKeys.source.ssoConfig.configureJson)} minRows={10} value={draft.definitionJson ?? '{}'} onChange={(event) => setDraft({ ...draft, definitionJson: event.target.value })} /></Stack></DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{translate(flowiseI18nKeys.common.cancel)}</Button>
          <PermissionMuiButton code={flowisePermissions.ssoManage} disabled={saveMutation.isPending} variant="contained" onClick={() => saveMutation.mutate(draft)}>
            {translate(flowiseI18nKeys.common.save)}
          </PermissionMuiButton>
        </DialogActions>
      </Dialog>
    </section>
  );
}
