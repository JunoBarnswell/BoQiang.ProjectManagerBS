import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import OutlinedInput from '@mui/material/OutlinedInput';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';

interface SaveChatflowDialogProps {
  initialName: string;
  open: boolean;
  saving: boolean;
  title: string;
  onClose: () => void;
  onConfirm: (name: string) => void;
}

export function SaveChatflowDialog({ initialName, open, saving, title, onClose, onConfirm }: SaveChatflowDialogProps) {
  const { translate } = useI18n();
  const [name, setName] = useState(initialName);
  const isReadyToSave = Boolean(name.trim());

  useEffect(() => {
    if (open) {
      setName(initialName);
    }
  }, [initialName, open]);

  return (
    <Dialog
      aria-describedby="flowise-save-chatflow-description"
      aria-labelledby="flowise-save-chatflow-title"
      disableRestoreFocus
      fullWidth
      maxWidth="xs"
      open={open}
      onClose={onClose}
    >
      <DialogTitle id="flowise-save-chatflow-title" sx={{ fontSize: '1rem' }}>
        {title}
      </DialogTitle>
      <DialogContent id="flowise-save-chatflow-description">
        <OutlinedInput
          autoFocus
          fullWidth
          id="chatflow-name"
          placeholder={translate(flowiseI18nKeys.editor.newChatflowPlaceholder)}
          sx={{ mt: 1 }}
          type="text"
          value={name}
          onChange={(event) => setName(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && isReadyToSave && !saving) {
              onConfirm(name.trim());
            }
          }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={!isReadyToSave || saving} variant="contained" onClick={() => onConfirm(name.trim())}>
          {translate(flowiseI18nKeys.actions.rename)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
