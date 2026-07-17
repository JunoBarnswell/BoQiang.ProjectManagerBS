import { Button, Dialog, DialogContent, DialogTitle, IconButton } from '@mui/material';
import type { ReactNode } from 'react';


import { AppIcon } from '../../../../../shared/icons/AppIcon';

interface ChatExpandDialogProps {
  children: ReactNode;
  clearText: string;
  open: boolean;
  title: string;
  validationText?: string;
  onClear: () => void;
  onClose: () => void;
  onValidate?: () => void;
}

export function ChatExpandDialog({
  children,
  clearText,
  open,
  title,
  validationText,
  onClear,
  onClose,
  onValidate
}: ChatExpandDialogProps) {
  return (
    <Dialog
      fullWidth
      aria-describedby="flowise-chat-expand-description"
      aria-labelledby="flowise-chat-expand-title"
      className="flowise-chat-expand-dialog"
      maxWidth="md"
      open={open}
      onClose={onClose}
    >
      <DialogTitle className="flowise-chat-expand-dialog__title" id="flowise-chat-expand-title">
        <span>{title}</span>
        <div>
          <Button color="error" size="small" startIcon={<AppIcon name="trash" />} variant="outlined" onClick={onClear}>
            {clearText}
          </Button>
          {onValidate && validationText ? (
            <Button size="small" startIcon={<AppIcon name="check-circle" />} variant="outlined" onClick={onValidate}>
              {validationText}
            </Button>
          ) : null}
          <IconButton size="small" onClick={onClose}>
            <AppIcon name="x" />
          </IconButton>
        </div>
      </DialogTitle>
      <DialogContent className="flowise-chat-expand-dialog__content" id="flowise-chat-expand-description">
        {children}
      </DialogContent>
    </Dialog>
  );
}
