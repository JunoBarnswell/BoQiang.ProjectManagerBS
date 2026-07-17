import { Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Select, Stack, TextField } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseDocumentStoreListItemDto, FlowiseDocumentStoreSaveRequest } from '../../../types/documentStore.types';

interface AddDocStoreDialogProps {
  item?: FlowiseDocumentStoreListItemDto | null;
  open: boolean;
  saving?: boolean;
  onClose: () => void;
  onSubmit: (draft: FlowiseDocumentStoreSaveRequest) => void;
}

function createDraft(overrides: Partial<FlowiseDocumentStoreSaveRequest> = {}): FlowiseDocumentStoreSaveRequest {
  return {
    advancedMetadataJson: '{}',
    category: '',
    description: '',
    loaderConfig: {
      advancedConfigJson: '{}',
      chunkOverlap: null,
      chunkSize: null,
      loaderType: '',
      sourceType: ''
    },
    name: '',
    status: 'Enabled',
    storeKey: '',
    workspaceId: '',
    ...overrides
  };
}

function toDraft(item: FlowiseDocumentStoreListItemDto): FlowiseDocumentStoreSaveRequest {
  return createDraft({
    advancedMetadataJson: item.advancedMetadataJson,
    category: item.category ?? '',
    description: item.description ?? '',
    loaderConfig: {
      advancedConfigJson: item.loaderConfig.advancedConfigJson ?? '{}',
      chunkOverlap: item.loaderConfig.chunkOverlap ?? null,
      chunkSize: item.loaderConfig.chunkSize ?? null,
      loaderType: item.loaderConfig.loaderType ?? '',
      sourceType: item.loaderConfig.sourceType ?? ''
    },
    name: item.name,
    status: item.status,
    storeKey: item.storeKey,
    workspaceId: item.workspaceId ?? ''
  });
}

function nullableNumber(value: string): number | null {
  return value === '' ? null : Number(value);
}

export function AddDocStoreDialog({ item, open, saving, onClose, onSubmit }: AddDocStoreDialogProps) {
  const { translate } = useI18n();
  const [draft, setDraft] = useState<FlowiseDocumentStoreSaveRequest>(() => createDraft());

  useEffect(() => {
    if (open) {
      setDraft(item ? toDraft(item) : createDraft());
    }
  }, [item, open]);

  return (
    <Dialog fullWidth maxWidth="md" open={open} onClose={onClose}>
      <DialogTitle>{item ? 'Edit Document Store' : 'Add Document Store'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField required label={translate(flowiseI18nKeys.fields.name)} value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
          <TextField required label={translate(flowiseI18nKeys.fields.key)} value={draft.storeKey} onChange={(event) => setDraft({ ...draft, storeKey: event.target.value })} />
          <TextField label="Category" value={draft.category ?? ''} onChange={(event) => setDraft({ ...draft, category: event.target.value })} />
          <TextField label="Loader Type" value={draft.loaderConfig.loaderType ?? ''} onChange={(event) => setDraft({ ...draft, loaderConfig: { ...draft.loaderConfig, loaderType: event.target.value } })} />
          <TextField label="Source Type" value={draft.loaderConfig.sourceType ?? ''} onChange={(event) => setDraft({ ...draft, loaderConfig: { ...draft.loaderConfig, sourceType: event.target.value } })} />
          <TextField label="Chunk Size" type="number" value={draft.loaderConfig.chunkSize ?? ''} onChange={(event) => setDraft({ ...draft, loaderConfig: { ...draft.loaderConfig, chunkSize: nullableNumber(event.target.value) } })} />
          <TextField label="Chunk Overlap" type="number" value={draft.loaderConfig.chunkOverlap ?? ''} onChange={(event) => setDraft({ ...draft, loaderConfig: { ...draft.loaderConfig, chunkOverlap: nullableNumber(event.target.value) } })} />
          <TextField multiline label="Advanced Loader Config JSON" minRows={3} value={draft.loaderConfig.advancedConfigJson} onChange={(event) => setDraft({ ...draft, loaderConfig: { ...draft.loaderConfig, advancedConfigJson: event.target.value } })} />
          <TextField multiline label="Advanced Metadata JSON" minRows={3} value={draft.advancedMetadataJson ?? '{}'} onChange={(event) => setDraft({ ...draft, advancedMetadataJson: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.fields.description)} minRows={2} value={draft.description ?? ''} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
          <FormControl>
            <InputLabel id="docstore-status-label">{translate(flowiseI18nKeys.fields.status)}</InputLabel>
            <Select label={translate(flowiseI18nKeys.fields.status)} labelId="docstore-status-label" value={draft.status ?? 'Enabled'} onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
              <MenuItem value="Enabled">{translate(flowiseI18nKeys.status.enabled)}</MenuItem>
              <MenuItem value="Disabled">{translate(flowiseI18nKeys.status.disabled)}</MenuItem>
              <MenuItem value="Draft">{translate(flowiseI18nKeys.status.draft)}</MenuItem>
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={saving || !draft.name.trim() || !draft.storeKey.trim()} variant="contained" onClick={() => onSubmit(draft)}>
          {item ? translate(flowiseI18nKeys.common.save) : translate(flowiseI18nKeys.common.create)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
