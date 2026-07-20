import { lazy, Suspense, useEffect, useState } from 'react';
import { createPortal } from 'react-dom';

import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { ResponsiveModal } from '../responsive/ResponsiveModal';

import { getFileExtension } from './filePreviewUtils';
import './filePreview.css';

const GenericFilePreviewSurface = lazy(() => import('./GenericFilePreviewSurface').then((module) => ({ default: module.GenericFilePreviewSurface })));
const PptxPreviewSurface = lazy(() => import('./PptxPreviewSurface').then((module) => ({ default: module.PptxPreviewSurface })));

interface FilePreviewDialogProps {
  error?: string | null;
  closeOnEscape?: boolean;
  file?: { extension?: string; fileName: string } | null;
  loading?: boolean;
  open: boolean;
  previewFile?: File | null;
  onClose: () => void;
}

export function FilePreviewDialog({
  error,
  closeOnEscape = true,
  file,
  loading = false,
  open,
  previewFile,
  onClose
}: FilePreviewDialogProps) {
  const { translate } = useI18n();
  const [fullscreen, setFullscreen] = useState(false);
  const viewerType = previewFile ? getFileExtension(previewFile.name) || file?.extension || previewFile.type : undefined;
  const usePptxPreviewSurface = viewerType === 'pptx';
  const title = file?.fileName
    ? formatMessage(translate('filePreview.titleWithName'), { name: file.fileName })
    : translate('filePreview.title');

  useEffect(() => {
    if (!open) {
      setFullscreen(false);
    }
  }, [open]);

  const dialog = (
    <ResponsiveModal
      bodyClassName="file-preview-modal__body"
      className="file-preview-modal"
      closeOnEscape={closeOnEscape}
      maxWidth={fullscreen ? undefined : 1200}
      mode={fullscreen ? 'fullscreen' : 'modal'}
      open={open}
      title={title}
      onClose={onClose}
    >
      <div className="file-preview-modal__toolbar">
        <button
          className="pm-workbench-command"
          type="button"
          onClick={() => setFullscreen((value) => !value)}
        >
          {fullscreen ? translate('filePreview.exitFullscreen') : translate('filePreview.enterFullscreen')}
        </button>
      </div>
      <div className="file-preview-modal__content">
        {loading ? (
          <PreviewState label={translate('filePreview.loading')} />
        ) : error ? (
          <PreviewState className="file-preview-modal__state--error" label={error} />
        ) : previewFile ? (
          usePptxPreviewSurface ? (
            <Suspense fallback={<PreviewState label={translate('filePreview.loading')} />}>
              <PptxPreviewSurface file={previewFile} />
            </Suspense>
          ) : (
            <Suspense fallback={<PreviewState label={translate('filePreview.loading')} />}>
              <GenericFilePreviewSurface
                className="file-preview-modal__viewer"
                file={previewFile}
                loadingLabel={translate('filePreview.loading')}
                type={viewerType}
              />
            </Suspense>
          )
        ) : (
          <PreviewState className="file-preview-modal__state--muted" label={translate('filePreview.empty')} />
        )}
      </div>
    </ResponsiveModal>
  );

  if (!open || typeof document === 'undefined') {
    return null;
  }

  return createPortal(dialog, document.body);
}

function PreviewState({ className, label }: { className?: string; label: string }) {
  return <div className={`file-preview-modal__state${className ? ` ${className}` : ''}`}>{label}</div>;
}
