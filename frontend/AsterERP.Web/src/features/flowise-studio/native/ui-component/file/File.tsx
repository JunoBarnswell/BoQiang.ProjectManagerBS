import { Button } from '@mui/material';
import { useRef } from 'react';


import { AppIcon } from '../../../../../shared/icons/AppIcon';

interface FileUploadProps {
  disabled?: boolean;
  label: string;
  multiple?: boolean;
  onFilesSelected: (files: FileList | null) => void;
}

export function FileUpload({ disabled = false, label, multiple = false, onFilesSelected }: FileUploadProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);

  return (
    <>
      <input
        ref={inputRef}
        multiple={multiple}
        className="flowise-chat-file-input"
        type="file"
        onChange={(event) => {
          onFilesSelected(event.currentTarget.files);
          event.currentTarget.value = '';
        }}
      />
      <Button disabled={disabled} startIcon={<AppIcon name="upload" />} variant="outlined" onClick={() => inputRef.current?.click()}>
        {label}
      </Button>
    </>
  );
}
