import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  Switch
} from '@mui/material';
import { useEffect, useState } from 'react';


import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';

interface ChatFeedbackDialogProps {
  enabled: boolean;
  open: boolean;
  saving: boolean;
  title: string;
  onClose: () => void;
  onConfirm: (enabled: boolean) => void;
}

export function ChatFeedbackDialog({ enabled, open, saving, title, onClose, onConfirm }: ChatFeedbackDialogProps) {
  const { translate } = useI18n();
  const [draftEnabled, setDraftEnabled] = useState(enabled);

  useEffect(() => {
    if (open) {
      setDraftEnabled(enabled);
    }
  }, [enabled, open]);

  return (
    <Dialog
      fullWidth
      maxWidth="sm"
      open={open}
      aria-describedby="flowise-chat-feedback-description"
      aria-labelledby="flowise-chat-feedback-title"
      onClose={onClose}
    >
      <DialogTitle id="flowise-chat-feedback-title" sx={{ fontSize: '1rem' }}>
        {title}
      </DialogTitle>
      <DialogContent id="flowise-chat-feedback-description">
        <Stack direction="column" spacing={2} sx={{ pt: 1, width: '100%' }}>
          <FormControlLabel
            control={(
              <Switch
                checked={draftEnabled}
                color="primary"
                onChange={(event) => setDraftEnabled(event.target.checked)}
              />
            )}
            label={translate(flowiseI18nKeys.messages.enableChatFeedback)}
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button color="inherit" type="button" onClick={onClose}>
          {translate(flowiseI18nKeys.common.cancel)}
        </Button>
        <Box sx={{ flex: 1 }} />
        <Button disabled={saving} type="button" variant="contained" onClick={() => onConfirm(draftEnabled)}>
          {translate(flowiseI18nKeys.common.save)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
