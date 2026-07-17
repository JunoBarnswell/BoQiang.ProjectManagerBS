import { Dialog, DialogContent, DialogTitle, IconButton } from '@mui/material';

import { AppIcon } from '../../../../../shared/icons/AppIcon';
import type { FlowiseSourceDocumentDto } from '../../../types/prediction.types';

interface SourceDocDialogProps {
  documents: FlowiseSourceDocumentDto[];
  open: boolean;
  title: string;
  onClose: () => void;
}

export function SourceDocDialog({ documents, open, title, onClose }: SourceDocDialogProps) {
  return (
    <Dialog fullWidth maxWidth="sm" open={open} onClose={onClose}>
      <DialogTitle className="flowise-source-doc-dialog__title">
        <span>{title}</span>
        <IconButton size="small" onClick={onClose}>
          <AppIcon name="x" />
        </IconButton>
      </DialogTitle>
      <DialogContent>
        <div className="flowise-source-doc-dialog__content">
          {documents.map((document, index) => (
            <section key={`${document.sourceId ?? 'source'}-${index}`}>
              {document.content ? <p>{document.content}</p> : null}
              <pre>{formatSourceDocument(document)}</pre>
            </section>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function formatSourceDocument(document: FlowiseSourceDocumentDto): string {
  return JSON.stringify(
    {
      content: document.content,
      metadata: parseJson(document.metadataJson),
      score: document.score,
      sourceId: document.sourceId
    },
    null,
    2
  );
}

function parseJson(value: string): unknown {
  try {
    return value ? JSON.parse(value) : {};
  } catch {
    return value;
  }
}
