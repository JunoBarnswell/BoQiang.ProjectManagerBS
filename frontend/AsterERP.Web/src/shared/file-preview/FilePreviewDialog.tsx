import { lazy, Suspense } from 'react';

import type { SystemFileRecordDto } from '../../api/system/files.types';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { ResponsiveModal } from '../responsive/ResponsiveModal';

import { getFileExtension } from './filePreviewUtils';
const GenericFilePreviewSurface = lazy(() => import('./GenericFilePreviewSurface').then((module) => ({ default: module.GenericFilePreviewSurface })));
const PptxPreviewSurface = lazy(() => import('./PptxPreviewSurface').then((module) => ({ default: module.PptxPreviewSurface })));

interface FilePreviewDialogProps {
  error?: string | null;
  file?: SystemFileRecordDto | null;
  loading?: boolean;
  open: boolean;
  previewFile?: File | null;
  onClose: () => void;
}

export function FilePreviewDialog({
  error,
  file,
  loading = false,
  open,
  previewFile,
  onClose
}: FilePreviewDialogProps) {
  const { translate } = useI18n();
  const viewerType = previewFile ? getFileExtension(previewFile.name) || file?.extension || previewFile.type : undefined;
  const usePptxPreviewSurface = viewerType === 'pptx';

  return (
    <ResponsiveModal
      mode="modal"
      open={open}
      title={file?.fileName ? formatMessage(translate('filePreview.titleWithName'), { name: file.fileName }) : translate('filePreview.title')}
      onClose={onClose}
    >
      <div className="flex min-h-[60vh] min-h-0 flex-col">
        {loading ? (
          <div className="flex min-h-[360px] items-center justify-center text-sm text-gray-500">{translate('filePreview.loading')}</div>
        ) : error ? (
          <div className="flex min-h-[360px] items-center justify-center rounded border border-rose-200 bg-rose-50 px-6 text-sm text-rose-700">
            {error}
          </div>
        ) : previewFile ? (
          <div className="min-h-[360px] max-h-[min(72vh,760px)] flex-1 overflow-auto rounded border border-gray-200 bg-white">
            {usePptxPreviewSurface ? (
              <Suspense fallback={<PreviewLoading label={translate('filePreview.loading')} />}>
                <PptxPreviewSurface file={previewFile} />
              </Suspense>
            ) : (
              <Suspense fallback={<PreviewLoading label={translate('filePreview.loading')} />}>
                <GenericFilePreviewSurface className="min-h-[360px] w-full" file={previewFile} loadingLabel={translate('filePreview.loading')} type={viewerType} />
              </Suspense>
            )}
          </div>
        ) : (
          <div className="flex min-h-[360px] items-center justify-center text-sm text-gray-500">{translate('filePreview.empty')}</div>
        )}
      </div>
    </ResponsiveModal>
  );
}

function PreviewLoading({ label }: { label: string }) {
  return <div className="flex min-h-[360px] items-center justify-center text-sm text-gray-500">{label}</div>;
}
