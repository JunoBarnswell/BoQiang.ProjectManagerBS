import { Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Select, Stack, TextField } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseDatasetListItemDto, FlowiseDatasetSaveRequest } from '../../../types/evaluation.types';

interface AddEditDatasetDialogProps {
  item?: FlowiseDatasetListItemDto | null;
  open: boolean;
  saving?: boolean;
  onClose: () => void;
  onSubmit: (draft: FlowiseDatasetSaveRequest) => void;
}

function splitCsv(value: string): string[] {
  return value.split(',').map((item) => item.trim()).filter(Boolean);
}

function joinList(values: readonly string[] | undefined): string {
  return values?.join(', ') ?? '';
}

function createDraft(overrides: Partial<FlowiseDatasetSaveRequest> = {}): FlowiseDatasetSaveRequest {
  return {
    advancedMetadataJson: '{}',
    category: '',
    datasetKey: '',
    description: '',
    name: '',
    schema: { advancedSchemaJson: '{}', expectedOutputColumns: [], inputColumns: [] },
    status: 'Enabled',
    workspaceId: '',
    ...overrides
  };
}

function toDraft(item: FlowiseDatasetListItemDto): FlowiseDatasetSaveRequest {
  return createDraft({
    advancedMetadataJson: item.advancedMetadataJson,
    category: item.category ?? '',
    datasetKey: item.datasetKey,
    description: item.description ?? '',
    name: item.name,
    schema: {
      advancedSchemaJson: item.schema.advancedSchemaJson,
      expectedOutputColumns: item.schema.expectedOutputColumns ?? [],
      inputColumns: item.schema.inputColumns ?? []
    },
    status: item.status,
    workspaceId: item.workspaceId ?? ''
  });
}

export function AddEditDatasetDialog({ item, open, saving, onClose, onSubmit }: AddEditDatasetDialogProps) {
  const { translate } = useI18n();
  const [draft, setDraft] = useState<FlowiseDatasetSaveRequest>(() => createDraft());

  useEffect(() => {
    if (open) {
      setDraft(item ? toDraft(item) : createDraft());
    }
  }, [item, open]);

  return (
    <Dialog fullWidth maxWidth="sm" open={open} onClose={onClose}>
      <DialogTitle>{item ? 'Edit Dataset' : 'Add Dataset'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField required label={translate(flowiseI18nKeys.fields.name)} value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
          <TextField required label={translate(flowiseI18nKeys.fields.key)} value={draft.datasetKey} onChange={(event) => setDraft({ ...draft, datasetKey: event.target.value })} />
          <TextField label="Category" value={draft.category ?? ''} onChange={(event) => setDraft({ ...draft, category: event.target.value })} />
          <TextField label="Input Columns" value={joinList(draft.schema.inputColumns)} onChange={(event) => setDraft({ ...draft, schema: { ...draft.schema, inputColumns: splitCsv(event.target.value) } })} />
          <TextField label="Expected Output Columns" value={joinList(draft.schema.expectedOutputColumns)} onChange={(event) => setDraft({ ...draft, schema: { ...draft.schema, expectedOutputColumns: splitCsv(event.target.value) } })} />
          <TextField multiline label="Advanced Schema JSON" minRows={3} value={draft.schema.advancedSchemaJson} onChange={(event) => setDraft({ ...draft, schema: { ...draft.schema, advancedSchemaJson: event.target.value } })} />
          <TextField multiline label="Advanced Metadata JSON" minRows={3} value={draft.advancedMetadataJson ?? '{}'} onChange={(event) => setDraft({ ...draft, advancedMetadataJson: event.target.value })} />
          <TextField multiline label={translate(flowiseI18nKeys.fields.description)} minRows={2} value={draft.description ?? ''} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
          <FormControl>
            <InputLabel id="dataset-status-label">{translate(flowiseI18nKeys.fields.status)}</InputLabel>
            <Select label={translate(flowiseI18nKeys.fields.status)} labelId="dataset-status-label" value={draft.status ?? 'Enabled'} onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
              <MenuItem value="Enabled">{translate(flowiseI18nKeys.status.enabled)}</MenuItem>
              <MenuItem value="Disabled">{translate(flowiseI18nKeys.status.disabled)}</MenuItem>
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={saving || !draft.name.trim() || !draft.datasetKey.trim()} variant="contained" onClick={() => onSubmit(draft)}>{item ? translate(flowiseI18nKeys.common.save) : translate(flowiseI18nKeys.common.create)}</Button>
      </DialogActions>
    </Dialog>
  );
}
