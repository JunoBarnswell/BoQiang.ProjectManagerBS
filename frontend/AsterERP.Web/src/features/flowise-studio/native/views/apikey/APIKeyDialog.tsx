import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField
} from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowisePermissions } from '../../../../../shared/auth/permissionCodes';
import { PermissionMuiButton } from '../../../../../shared/auth/PermissionMuiButton';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest } from '../../../types/shared.types';
import { createResourceDraft, toResourceDraft } from '../common/sourcePageUtils';

interface APIKeyDialogProps {
  item?: FlowiseResourceDto | null;
  open: boolean;
  saving?: boolean;
  onClose: () => void;
  onSubmit: (draft: FlowiseResourceUpsertRequest) => void;
}

export function APIKeyDialog({ item, open, saving, onClose, onSubmit }: APIKeyDialogProps) {
  const { translate } = useI18n();
  const [draft, setDraft] = useState<FlowiseResourceUpsertRequest>(() => createResourceDraft({ metadataJson: '{"permissions":[],"chatFlows":[]}' }));

  useEffect(() => {
    if (open) {
      setDraft(item ? toResourceDraft(item) : createResourceDraft({ metadataJson: '{"permissions":[],"chatFlows":[]}' }));
    }
  }, [item, open]);

  return (
    <Dialog fullWidth maxWidth="sm" open={open} onClose={onClose}>
      <DialogTitle>{item ? translate('flowise.native.apiKeys.editTitle') : translate('flowise.native.apiKeys.createTitle')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField
            required
            label={translate(flowiseI18nKeys.fields.name)}
            value={draft.displayName}
            onChange={(event) => setDraft({ ...draft, displayName: event.target.value })}
          />
          <TextField
            required
            label={translate(flowiseI18nKeys.fields.key)}
            value={draft.resourceKey}
            onChange={(event) => setDraft({ ...draft, resourceKey: event.target.value })}
          />
          <TextField
            label={translate(flowiseI18nKeys.fields.secret)}
            helperText="Leave empty to auto-generate a Flowise API key."
            type="password"
            value={draft.secretValue ?? ''}
            onChange={(event) => setDraft({ ...draft, secretValue: event.target.value })}
          />
          <TextField
            multiline
            label={translate(flowiseI18nKeys.fields.description)}
            minRows={2}
            value={draft.description ?? ''}
            onChange={(event) => setDraft({ ...draft, description: event.target.value })}
          />
          <TextField
            multiline
            label={translate(flowiseI18nKeys.fields.metadataJson)}
            minRows={4}
            value={draft.metadataJson ?? '{}'}
            onChange={(event) => setDraft({ ...draft, metadataJson: event.target.value })}
          />
          <FormControl>
            <InputLabel id="api-key-status-label">{translate(flowiseI18nKeys.fields.status)}</InputLabel>
            <Select
              label={translate(flowiseI18nKeys.fields.status)}
              labelId="api-key-status-label"
              value={draft.status ?? 'Enabled'}
              onChange={(event) => setDraft({ ...draft, status: event.target.value })}
            >
              <MenuItem value="Enabled">{translate(flowiseI18nKeys.status.enabled)}</MenuItem>
              <MenuItem value="Disabled">{translate(flowiseI18nKeys.status.disabled)}</MenuItem>
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <PermissionMuiButton code={flowisePermissions.apiKeysEdit} disabled={saving || !draft.displayName.trim() || !draft.resourceKey.trim()} variant="contained" onClick={() => onSubmit(draft)}>
          {item ? translate(flowiseI18nKeys.common.save) : translate(flowiseI18nKeys.common.create)}
        </PermissionMuiButton>
      </DialogActions>
    </Dialog>
  );
}
