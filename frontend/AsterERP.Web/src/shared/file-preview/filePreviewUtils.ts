import type { SystemFileRecordDto } from '../../api/system/files.types';
import { translateCurrentLocale } from '../../core/i18n/I18nProvider';

const sizeUnits = ['B', 'KB', 'MB', 'GB', 'TB'] as const;

export function getFileExtension(fileName?: string | null): string {
  const value = fileName?.trim() ?? '';
  const dotIndex = value.lastIndexOf('.');
  return dotIndex >= 0 && dotIndex < value.length - 1 ? value.slice(dotIndex + 1).toLowerCase() : '';
}

export function formatFileSize(size?: number | null): string {
  if (!Number.isFinite(size ?? Number.NaN) || (size ?? 0) < 0) {
    return '-';
  }

  let outputSize = size ?? 0;
  let unitIndex = 0;
  while (outputSize >= 1024 && unitIndex < sizeUnits.length - 1) {
    outputSize /= 1024;
    unitIndex += 1;
  }

  const fractionDigits = outputSize >= 10 || unitIndex === 0 ? 0 : 1;
  return `${outputSize.toFixed(fractionDigits)} ${sizeUnits[unitIndex]}`;
}

export function getPreviewStatusLabel(file: Pick<SystemFileRecordDto, 'previewPipeline' | 'previewSupported'>): string {
  return file.previewSupported ? file.previewPipeline || translateCurrentLocale('filePreview.supported') : translateCurrentLocale('filePreview.unsupported');
}

export function createPreviewFile(file: SystemFileRecordDto, blob: Blob, responseFileName?: string): File {
  const fileName = file.fileName || responseFileName || 'preview.bin';
  const contentType = blob.type || file.contentType || 'application/octet-stream';
  return new File([blob], fileName, { type: contentType });
}

export function saveBlob(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = fileName || 'download.bin';
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
}
