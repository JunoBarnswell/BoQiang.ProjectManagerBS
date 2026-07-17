import type { FileRenderHandler, FileViewerRenderedInstance, FileViewerRendererPlugin } from '@file-viewer/core';
import FileViewer, { type FileViewerProps } from '@file-viewer/react';
import { useEffect, useMemo, useState } from 'react';

interface GenericFilePreviewSurfaceProps {
  className?: string;
  file: File;
  loadingLabel: string;
  type?: string;
}

export function GenericFilePreviewSurface({ className, file, loadingLabel, type }: GenericFilePreviewSurfaceProps) {
  const rendererKind = useMemo(() => resolvePreviewRendererKind(type || file.name), [file.name, type]);
  const [renderers, setRenderers] = useState<PreviewRenderer[] | null>(null);

  useEffect(() => {
    let isDisposed = false;

    setRenderers(null);
    void loadPreviewRenderers(rendererKind).then((nextRenderers) => {
      if (!isDisposed) {
        setRenderers(nextRenderers);
      }
    });

    return () => {
      isDisposed = true;
    };
  }, [rendererKind]);

  if (!renderers) {
    return <div className="flex min-h-[360px] items-center justify-center text-sm text-gray-500">{loadingLabel}</div>;
  }

  return (
    <FileViewer
      className={className}
      file={file}
      name={file.name}
      size={file.size}
      type={type}
      options={{
        renderers: toViewerRendererInput(renderers),
        rendererMode: 'replace',
        theme: 'light',
        toolbar: { position: 'bottom-right' }
      }}
    />
  );
}

type PreviewRenderer = FileViewerRendererPlugin<FileRenderHandler<FileViewerRenderedInstance, HTMLDivElement>>;
type ViewerRendererInput = NonNullable<FileViewerProps['options']>['renderers'];
type PreviewRendererKind = 'archive' | 'cad' | 'fallback' | 'image' | 'media' | 'pdf' | 'spreadsheet' | 'text' | 'word';

function toViewerRendererInput(renderers: PreviewRenderer[]): ViewerRendererInput {
  return renderers as unknown as ViewerRendererInput;
}

async function loadPreviewRenderers(kind: PreviewRendererKind): Promise<PreviewRenderer[]> {
  switch (kind) {
    case 'archive': {
      const module = await import('@file-viewer/renderer-archive');
      return [module.archiveRenderer];
    }
    case 'cad': {
      const module = await import('@file-viewer/renderer-cad');
      return [module.cadRenderer];
    }
    case 'image': {
      const module = await import('@file-viewer/renderer-image');
      return [module.imageRenderer];
    }
    case 'media': {
      const module = await import('@file-viewer/renderer-media');
      return [module.mediaRenderer];
    }
    case 'pdf': {
      const module = await import('@file-viewer/renderer-pdf');
      return [module.pdfRenderer];
    }
    case 'spreadsheet': {
      const module = await import('@file-viewer/renderer-spreadsheet');
      return [module.spreadsheetRenderer];
    }
    case 'word': {
      const module = await import('@file-viewer/renderer-word');
      return [module.wordRenderer];
    }
    case 'fallback': {
      const [textModule, imageModule, pdfModule] = await Promise.all([
        import('@file-viewer/renderer-text'),
        import('@file-viewer/renderer-image'),
        import('@file-viewer/renderer-pdf')
      ]);
      return [textModule.textRenderer, imageModule.imageRenderer, pdfModule.pdfRenderer];
    }
    case 'text':
    default: {
      const module = await import('@file-viewer/renderer-text');
      return [module.textRenderer];
    }
  }
}

function resolvePreviewRendererKind(typeOrName: string | undefined): PreviewRendererKind {
  const extension = normalizePreviewExtension(typeOrName);
  if (extension && archiveExtensions.has(extension)) {
    return 'archive';
  }

  if (extension && cadExtensions.has(extension)) {
    return 'cad';
  }

  if (extension && imageExtensions.has(extension)) {
    return 'image';
  }

  if (extension && mediaExtensions.has(extension)) {
    return 'media';
  }

  if (extension === 'pdf') {
    return 'pdf';
  }

  if (extension && spreadsheetExtensions.has(extension)) {
    return 'spreadsheet';
  }

  if (extension && wordExtensions.has(extension)) {
    return 'word';
  }

  if (extension && textExtensions.has(extension)) {
    return 'text';
  }

  return 'fallback';
}

function normalizePreviewExtension(typeOrName: string | undefined): string {
  const value = typeOrName?.trim().toLowerCase() ?? '';
  if (!value) {
    return '';
  }

  if (value.includes('/')) {
    return mimeExtensionMap[value] ?? '';
  }

  return value.startsWith('.') ? value.slice(1) : value.split('.').pop() ?? '';
}

const archiveExtensions = new Set(['7z', 'bz2', 'gz', 'rar', 'tar', 'tgz', 'xz', 'zip']);
const cadExtensions = new Set(['dwf', 'dwg', 'dxf']);
const imageExtensions = new Set(['apng', 'avif', 'bmp', 'gif', 'heic', 'ico', 'jpeg', 'jpg', 'png', 'svg', 'webp']);
const mediaExtensions = new Set(['aac', 'avi', 'flac', 'm4a', 'm4v', 'mkv', 'mov', 'mp3', 'mp4', 'ogg', 'wav', 'webm']);
const spreadsheetExtensions = new Set(['csv', 'ods', 'xls', 'xlsx']);
const textExtensions = new Set(['css', 'html', 'js', 'json', 'log', 'md', 'sql', 'ts', 'txt', 'xml', 'yaml', 'yml']);
const wordExtensions = new Set(['doc', 'docx', 'odt', 'rtf']);
const mimeExtensionMap: Record<string, string> = {
  'application/msword': 'doc',
  'application/pdf': 'pdf',
  'application/rtf': 'rtf',
  'application/vnd.ms-excel': 'xls',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': 'xlsx',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'docx',
  'audio/mpeg': 'mp3',
  'image/jpeg': 'jpg',
  'image/png': 'png',
  'image/svg+xml': 'svg',
  'text/csv': 'csv',
  'text/markdown': 'md',
  'text/plain': 'txt',
  'video/mp4': 'mp4'
};
