import { Dialog, DialogContent, DialogTitle, Stack, Typography } from '@mui/material';

interface HowToUseVariablesDialogProps {
  open: boolean;
  onClose: () => void;
}

export function HowToUseVariablesDialog({ open, onClose }: HowToUseVariablesDialogProps) {
  return (
    <Dialog fullWidth maxWidth="sm" open={open} onClose={onClose}>
      <DialogTitle>How to use Variables</DialogTitle>
      <DialogContent>
        <Stack spacing={1.5}>
          <Typography>Reference a variable in prompts, tools, and runtime configuration with double braces.</Typography>
          <Typography component="code" className="flowise-source-code-line">{'{{variableName}}'}</Typography>
          <Typography>Secret variables stay masked in the list. Use reveal only when the current user has secret reveal permission.</Typography>
        </Stack>
      </DialogContent>
    </Dialog>
  );
}
