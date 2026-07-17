import DatabaseIcon from '@mui/icons-material/Storage';
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Stack, Switch, Typography } from '@mui/material';
import { useEffect, useState } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import { FileUpload } from '../../ui-component/file/File';

interface UploadCSVFileDialogProps {
  datasetName: string;
  open: boolean;
  saving?: boolean;
  onClose: () => void;
  onSubmit: (file: File, firstRowHeaders: boolean) => void;
}

export function UploadCSVFileDialog({ datasetName, open, saving, onClose, onSubmit }: UploadCSVFileDialogProps) {
  const { translate } = useI18n();
  const [firstRowHeaders, setFirstRowHeaders] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  useEffect(() => {
    if (!open) {
      setFirstRowHeaders(false);
      setSelectedFile(null);
    }
  }, [open]);

  const titleTemplate = translate(flowiseI18nKeys.source.datasets.uploadTitle);
  const title = titleTemplate.replace('{name}', datasetName);

  return (
    <Dialog fullWidth maxWidth="sm" open={open} onClose={onClose}>
      <DialogTitle>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
          <Box className="flowise-source-dialog-icon"><DatabaseIcon /></Box>
          <span>{title}</span>
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">{translate(flowiseI18nKeys.source.datasets.uploadFormat)}</Typography>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <FileUpload
              disabled={saving}
              label={selectedFile?.name ?? translate(flowiseI18nKeys.source.datasets.uploadCsv)}
              onFilesSelected={(files) => setSelectedFile(files?.item(0) ?? null)}
            />
          </Stack>
          <FormControlLabel
            control={<Switch checked={firstRowHeaders} onChange={(event) => setFirstRowHeaders(event.target.checked)} />}
            label={translate(flowiseI18nKeys.source.datasets.firstRowHeaders)}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
        <Button disabled={saving || !selectedFile} variant="contained" onClick={() => selectedFile && onSubmit(selectedFile, firstRowHeaders)}>
          {translate(flowiseI18nKeys.actions.upload)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
