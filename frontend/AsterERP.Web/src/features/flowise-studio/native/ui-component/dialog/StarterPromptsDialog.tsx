
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import TipsAndUpdatesIcon from '@mui/icons-material/TipsAndUpdates';
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  InputAdornment,
  List,
  OutlinedInput,
  Stack,
  Typography
} from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';

interface StarterPromptsDialogProps {
  initialPrompts: string[];
  open: boolean;
  saving: boolean;
  title: string;
  onClose: () => void;
  onConfirm: (prompts: string[]) => void;
}

export function StarterPromptsDialog({ initialPrompts, open, saving, title, onClose, onConfirm }: StarterPromptsDialogProps) {
  const { translate } = useI18n();
  const [promptRows, setPromptRows] = useState<string[]>(normalizePrompts(initialPrompts));

  useEffect(() => {
    if (open) {
      setPromptRows(normalizePrompts(initialPrompts));
    }
  }, [initialPrompts, open]);

  const updatePrompt = (index: number, value: string) => {
    setPromptRows((current) => current.map((prompt, promptIndex) => (promptIndex === index ? value : prompt)));
  };

  const addPrompt = () => {
    setPromptRows((current) => [...current, '']);
  };

  const removePrompt = (index: number) => {
    setPromptRows((current) => {
      const next = current.filter((_, promptIndex) => promptIndex !== index);
      return next.length > 0 ? next : [''];
    });
  };

  return (
    <Dialog
      fullWidth
      maxWidth="sm"
      open={open}
      aria-describedby="flowise-starter-prompts-description"
      aria-labelledby="flowise-starter-prompts-title"
      onClose={onClose}
    >
      <DialogTitle id="flowise-starter-prompts-title" sx={{ fontSize: '1rem' }}>
        {title}
      </DialogTitle>
      <DialogContent id="flowise-starter-prompts-description">
        <Stack direction="column" spacing={2} sx={{ pt: 1, width: '100%' }}>
          <Box
            sx={{
              alignItems: 'center',
              bgcolor: 'rgba(34, 197, 94, 0.08)',
              border: '1px solid',
              borderColor: 'rgba(34, 197, 94, 0.2)',
              borderRadius: '8px',
              display: 'flex',
              gap: 1.25,
              px: 1.75,
              py: 1.25
            }}
          >
            <TipsAndUpdatesIcon sx={{ color: '#16a34a', flexShrink: 0, fontSize: 20 }} />
            <Typography color="text.secondary" sx={{ fontSize: '0.8125rem', lineHeight: 1.5 }}>
              {translate(flowiseI18nKeys.messages.starterPromptsHelp)}
            </Typography>
          </Box>
          <Stack direction="column" spacing={1}>
            <Typography>{translate(flowiseI18nKeys.detail.starterPrompts)}</Typography>
            <List disablePadding sx={{ width: '100%' }}>
              {promptRows.map((prompt, index) => (
                <Box key={`${index}-${promptRows.length}`} sx={{ display: 'flex', gap: 1, mb: 1, width: '100%' }}>
                  <OutlinedInput
                    fullWidth
                    name="prompt"
                    size="small"
                    type="text"
                    value={prompt}
                    endAdornment={(
                      <InputAdornment position="end">
                        {promptRows.length > 1 ? (
                          <IconButton
                            color="error"
                            disabled={promptRows.length === 1}
                            edge="end"
                            size="small"
                            sx={{ height: 30, width: 30 }}
                            aria-label={translate(flowiseI18nKeys.actions.remove)}
                            onClick={() => removePrompt(index)}
                          >
                            <DeleteIcon fontSize="small" />
                          </IconButton>
                        ) : null}
                      </InputAdornment>
                    )}
                    onChange={(event) => updatePrompt(index, event.target.value)}
                  />
                  {index === promptRows.length - 1 ? (
                    <IconButton color="primary" aria-label={translate(flowiseI18nKeys.actions.addNew)} onClick={addPrompt}>
                      <AddIcon />
                    </IconButton>
                  ) : (
                    <Box sx={{ width: 40 }} />
                  )}
                </Box>
              ))}
            </List>
            <Typography color="text.secondary" variant="caption">
              {translate(flowiseI18nKeys.messages.onePromptPerLine)}
            </Typography>
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button color="inherit" type="button" onClick={onClose}>
          {translate(flowiseI18nKeys.common.cancel)}
        </Button>
        <Button disabled={saving} type="button" variant="contained" onClick={() => onConfirm(splitRows(promptRows))}>
          {translate(flowiseI18nKeys.common.save)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function normalizePrompts(prompts: string[]) {
  return prompts.length > 0 ? prompts : [''];
}

function splitRows(rows: string[]) {
  return rows.flatMap((value) => value.split(/\r?\n/)).map((item) => item.trim()).filter(Boolean);
}
