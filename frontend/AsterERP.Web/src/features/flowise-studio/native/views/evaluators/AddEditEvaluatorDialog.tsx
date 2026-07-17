import TipsAndUpdatesIcon from '@mui/icons-material/TipsAndUpdates';
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Select, Stack, TextField } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseEvaluatorListItemDto, FlowiseEvaluatorSaveRequest } from '../../../types/evaluation.types';

import { SamplePromptDialog } from './SamplePromptDialog';

interface AddEditEvaluatorDialogProps {
  item?: FlowiseEvaluatorListItemDto | null;
  open: boolean;
  saving?: boolean;
  onClose: () => void;
  onSubmit: (draft: FlowiseEvaluatorSaveRequest) => void;
}

function createDraft(overrides: Partial<FlowiseEvaluatorSaveRequest> = {}): FlowiseEvaluatorSaveRequest {
  return {
    advancedMetadataJson: '{}',
    definition: { advancedConfigJson: '{}', gradingMode: '', model: '', promptTemplate: '', provider: '' },
    description: '',
    evaluatorKey: '',
    evaluatorType: 'llm',
    name: '',
    status: 'Enabled',
    workspaceId: '',
    ...overrides
  };
}

function toDraft(item: FlowiseEvaluatorListItemDto): FlowiseEvaluatorSaveRequest {
  return createDraft({
    advancedMetadataJson: item.advancedMetadataJson,
    definition: {
      advancedConfigJson: item.definition.advancedConfigJson,
      gradingMode: item.definition.gradingMode ?? '',
      model: item.definition.model ?? '',
      promptTemplate: item.definition.promptTemplate ?? '',
      provider: item.definition.provider ?? ''
    },
    description: item.description ?? '',
    evaluatorKey: item.evaluatorKey,
    evaluatorType: item.evaluatorType ?? '',
    name: item.name,
    status: item.status,
    workspaceId: item.workspaceId ?? ''
  });
}

export function AddEditEvaluatorDialog({ item, open, saving, onClose, onSubmit }: AddEditEvaluatorDialogProps) {
  const { translate } = useI18n();
  const [draft, setDraft] = useState<FlowiseEvaluatorSaveRequest>(() => createDraft());
  const [samplePromptOpen, setSamplePromptOpen] = useState(false);

  useEffect(() => {
    if (open) {
      setDraft(item ? toDraft(item) : createDraft());
    }
  }, [item, open]);

  const applySamplePrompt = (promptTemplate: string) => {
    setDraft({
      ...draft,
      evaluatorType: draft.evaluatorType || 'llm',
      definition: { ...draft.definition, promptTemplate }
    });
    setSamplePromptOpen(false);
  };

  return (
    <>
      <Dialog fullWidth maxWidth="md" open={open} onClose={onClose}>
        <DialogTitle>{item ? 'Edit Evaluator' : translate(flowiseI18nKeys.source.evaluators.addEvaluator)}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField required label={translate(flowiseI18nKeys.fields.name)} value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
            <TextField required label={translate(flowiseI18nKeys.fields.key)} value={draft.evaluatorKey} onChange={(event) => setDraft({ ...draft, evaluatorKey: event.target.value })} />
            <TextField label="Evaluator Type" value={draft.evaluatorType ?? ''} onChange={(event) => setDraft({ ...draft, evaluatorType: event.target.value })} />
            <TextField label="Provider" value={draft.definition.provider ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, provider: event.target.value } })} />
            <TextField label="Model" value={draft.definition.model ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, model: event.target.value } })} />
            <TextField label="Grading Mode" value={draft.definition.gradingMode ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, gradingMode: event.target.value } })} />
            <Stack spacing={1} sx={{ alignItems: 'flex-start' }}>
              <TextField fullWidth multiline label="Prompt Template" minRows={6} value={draft.definition.promptTemplate ?? ''} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, promptTemplate: event.target.value } })} />
              <Button startIcon={<TipsAndUpdatesIcon />} variant="outlined" onClick={() => setSamplePromptOpen(true)}>
                {translate(flowiseI18nKeys.source.evaluators.samplePrompt)}
              </Button>
            </Stack>
            <TextField multiline label="Advanced Config JSON" minRows={3} value={draft.definition.advancedConfigJson} onChange={(event) => setDraft({ ...draft, definition: { ...draft.definition, advancedConfigJson: event.target.value } })} />
            <TextField multiline label="Advanced Metadata JSON" minRows={3} value={draft.advancedMetadataJson ?? '{}'} onChange={(event) => setDraft({ ...draft, advancedMetadataJson: event.target.value })} />
            <TextField multiline label={translate(flowiseI18nKeys.fields.description)} minRows={2} value={draft.description ?? ''} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
            <FormControl>
              <InputLabel id="evaluator-status-label">{translate(flowiseI18nKeys.fields.status)}</InputLabel>
              <Select label={translate(flowiseI18nKeys.fields.status)} labelId="evaluator-status-label" value={draft.status ?? 'Enabled'} onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
                <MenuItem value="Enabled">{translate(flowiseI18nKeys.status.enabled)}</MenuItem>
                <MenuItem value="Disabled">{translate(flowiseI18nKeys.status.disabled)}</MenuItem>
              </Select>
            </FormControl>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
          <Button disabled={saving || !draft.name.trim() || !draft.evaluatorKey.trim()} variant="contained" onClick={() => onSubmit(draft)}>{item ? translate(flowiseI18nKeys.common.save) : translate(flowiseI18nKeys.common.create)}</Button>
        </DialogActions>
      </Dialog>
      <SamplePromptDialog open={samplePromptOpen} onClose={() => setSamplePromptOpen(false)} onSelect={applySamplePrompt} />
    </>
  );
}
