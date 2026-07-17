import { Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Select, Stack, TextField } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseEvaluationListItemDto, FlowiseEvaluationSaveRequest } from '../../../types/evaluation.types';

interface CreateEvaluationDialogProps {
  item?: FlowiseEvaluationListItemDto | null;
  open: boolean;
  saving?: boolean;
  onClose: () => void;
  onSubmit: (draft: FlowiseEvaluationSaveRequest) => void;
}

function createDraft(overrides: Partial<FlowiseEvaluationSaveRequest> = {}): FlowiseEvaluationSaveRequest {
  return {
    advancedMetadataJson: '{}',
    category: '',
    definition: { datasetId: '', evaluatorId: '', model: '', runConfigJson: '{}', targetFlowId: '' },
    description: '',
    evaluationKey: '',
    name: '',
    status: 'Draft',
    workspaceId: '',
    ...overrides
  };
}

function toDraft(item: FlowiseEvaluationListItemDto): FlowiseEvaluationSaveRequest {
  return createDraft({
    advancedMetadataJson: item.advancedMetadataJson,
    category: item.category ?? '',
    definition: {
      datasetId: item.definition.datasetId,
      evaluatorId: item.definition.evaluatorId,
      model: item.definition.model ?? '',
      runConfigJson: item.definition.runConfigJson,
      targetFlowId: item.definition.targetFlowId
    },
    description: item.description ?? '',
    evaluationKey: item.evaluationKey,
    name: item.name,
    status: item.status,
    workspaceId: item.workspaceId ?? ''
  });
}

export function CreateEvaluationDialog({ item, open, saving, onClose, onSubmit }: CreateEvaluationDialogProps) {
  const { translate } = useI18n();
  const [draft, setDraft] = useState<FlowiseEvaluationSaveRequest>(() => createDraft());

  useEffect(() => {
    if (open) {
      setDraft(item ? toDraft(item) : createDraft());
    }
  }, [item, open]);

  return (
    <Dialog fullWidth maxWidth="md" open={open} onClose={onClose}>
      <DialogTitle>{item ? 'Edit Evaluation' : 'Create Evaluation'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField required label={translate(flowiseI18nKeys.fields.name)} value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
          <TextField required label={translate(flowiseI18nKeys.fields.key)} value={draft.evaluationKey} onChange={(event) => setDraft({ ...draft, evaluationKey: event.target.value })} />
          <TextField label="Category" value={draft.category ?? ''} onChange={(event) => setDraft({ ...draft, category: event.target.value })} />
          <TextField required label="Dataset ID" value={draft.definition.datasetId} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, datasetId: event.target.value } })} />
          <TextField required label="Evaluator ID" value={draft.definition.evaluatorId} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, evaluatorId: event.target.value } })} />
          <TextField required label="Target Flow ID" value={draft.definition.targetFlowId} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, targetFlowId: event.target.value } })} />
          <TextField label="Model" value={draft.definition.model ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, model: event.target.value } })} />
          <TextField multiline label="Run Config JSON" minRows={3} value={draft.definition.runConfigJson} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, runConfigJson: event.target.value } })} />
          <TextField multiline label="Advanced Metadata JSON" minRows={3} value={draft.advancedMetadataJson ?? '{}'} onChange={(event) => setDraft({ ...draft, advancedMetadataJson: event.target.value })} />
          <FormControl>
            <InputLabel id="evaluation-status-label">{translate(flowiseI18nKeys.fields.status)}</InputLabel>
            <Select label={translate(flowiseI18nKeys.fields.status)} labelId="evaluation-status-label" value={draft.status ?? 'Draft'} onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
              <MenuItem value="Draft">{translate(flowiseI18nKeys.status.draft)}</MenuItem>
              <MenuItem value="Running">{translate(flowiseI18nKeys.status.running)}</MenuItem>
              <MenuItem value="Completed">{translate(flowiseI18nKeys.status.completed)}</MenuItem>
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={saving || !draft.name.trim() || !draft.evaluationKey.trim() || !draft.definition.datasetId.trim() || !draft.definition.evaluatorId.trim() || !draft.definition.targetFlowId.trim()} variant="contained" onClick={() => onSubmit(draft)}>{item ? translate(flowiseI18nKeys.common.save) : translate(flowiseI18nKeys.common.create)}</Button>
      </DialogActions>
    </Dialog>
  );
}
