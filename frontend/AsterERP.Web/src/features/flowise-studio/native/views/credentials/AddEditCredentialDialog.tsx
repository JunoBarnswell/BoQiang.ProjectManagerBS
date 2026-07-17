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
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest } from '../../../types/shared.types';
import { createResourceDraft, toResourceDraft } from '../common/sourcePageUtils';

interface AddEditCredentialDialogProps {
  component?: FlowiseResourceDto | null;
  item?: FlowiseResourceDto | null;
  open: boolean;
  saving?: boolean;
  onClose: () => void;
  onSubmit: (draft: FlowiseResourceUpsertRequest) => void;
}

export function AddEditCredentialDialog({ component, item, open, saving, onClose, onSubmit }: AddEditCredentialDialogProps) {
  const { translate } = useI18n();
  const [draft, setDraft] = useState<FlowiseResourceUpsertRequest>(() => createResourceDraft());

  useEffect(() => {
    if (!open) {
      return;
    }
    if (item) {
      setDraft(toResourceDraft(item));
      return;
    }
    setDraft(createResourceDraft({
      category: component?.resourceKey ?? '',
      definitionJson: '{}',
      displayName: component?.displayName ? `${component.displayName} Credential` : '',
      metadataJson: component ? JSON.stringify({ credentialName: component.resourceKey }) : '{}',
      resourceKey: component?.resourceKey ? `${component.resourceKey}-${Date.now()}` : ''
    }));
  }, [component, item, open]);

  return (
    <Dialog fullWidth maxWidth="md" open={open} onClose={onClose}>
      <DialogTitle>{item ? 'Edit Credential' : 'Add Credential'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField required label={translate(flowiseI18nKeys.fields.name)} value={draft.displayName} onChange={(event) => setDraft({ ...draft, displayName: event.target.value })} />
          <TextField required label={translate(flowiseI18nKeys.fields.key)} value={draft.resourceKey} onChange={(event) => setDraft({ ...draft, resourceKey: event.target.value })} />
          <TextField label={translate(flowiseI18nKeys.fields.type)} value={draft.category ?? ''} onChange={(event) => setDraft({ ...draft, category: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.fields.description)} minRows={2} value={draft.description ?? ''} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
          <TextField multiline label="Credential Config JSON" minRows={5} value={draft.definitionJson ?? '{}'} onChange={(event) => setDraft({ ...draft, definitionJson: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.fields.metadataJson)} minRows={3} value={draft.metadataJson ?? '{}'} onChange={(event) => setDraft({ ...draft, metadataJson: event.target.value })} />
          <TextField label={translate(flowiseI18nKeys.fields.secret)} type="password" value={draft.secretValue ?? ''} onChange={(event) => setDraft({ ...draft, secretValue: event.target.value })} />
          <FormControl>
            <InputLabel id="credential-status-label">{translate(flowiseI18nKeys.fields.status)}</InputLabel>
            <Select label={translate(flowiseI18nKeys.fields.status)} labelId="credential-status-label" value={draft.status ?? 'Enabled'} onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
              <MenuItem value="Enabled">{translate(flowiseI18nKeys.status.enabled)}</MenuItem>
              <MenuItem value="Disabled">{translate(flowiseI18nKeys.status.disabled)}</MenuItem>
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={saving || !draft.displayName.trim() || !draft.resourceKey.trim()} variant="contained" onClick={() => onSubmit(draft)}>
          {item ? translate(flowiseI18nKeys.common.save) : translate(flowiseI18nKeys.common.create)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
