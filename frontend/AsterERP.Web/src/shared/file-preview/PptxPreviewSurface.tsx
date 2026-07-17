import { PptxViewer, RECOMMENDED_ZIP_LIMITS } from '@file-viewer/pptx';
import { createPptxWorker } from '@file-viewer/pptx/worker';
import { useEffect, useRef, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';

interface PptxPreviewSurfaceProps {
  file: File;
}

function formatPptxError(error: unknown, fallbackMessage: string) {
  if (error instanceof Error) {
    return error.message || fallbackMessage;
  }

  if (typeof error === 'string') {
    return error || fallbackMessage;
  }

  if (error && typeof error === 'object') {
    const record = error as Record<string, unknown>;
    const message = record.message ?? record.error ?? record.reason ?? record.type;
    if (typeof message === 'string' && message.trim()) {
      return message;
    }

    try {
      const serialized = JSON.stringify(record);
      if (serialized && serialized !== '{}') {
        return serialized;
      }
    } catch {
      return String(error);
    }
  }

  return fallbackMessage;
}

export function PptxPreviewSurface({ file }: PptxPreviewSurfaceProps) {
  const { translate } = useI18n();
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const loadingLabel = translate('filePreview.pptx.loading');
  const failedTitle = translate('filePreview.pptx.previewFailed');
  const failedFallback = translate('filePreview.pptx.parseFailed');

  useEffect(() => {
    let disposed = false;
    let viewer: PptxViewer | null = null;

    const render = async () => {
      setError(null);
      setLoading(true);

      try {
        const surface = surfaceRef.current;
        if (!surface) {
          return;
        }

        const buffer = await file.arrayBuffer();
        if (disposed) {
          return;
        }

        viewer = await PptxViewer.open(buffer, surface, {
          fitMode: 'contain',
          lazyMedia: true,
          lazySlides: true,
          listOptions: {
            batchSize: 4,
            initialSlides: 3,
            overscanViewport: 1.5,
            windowed: true
          },
          onError: (pptxError) => {
            if (!disposed) {
              setError(formatPptxError(pptxError, failedTitle));
              setLoading(false);
            }
          },
          onRenderComplete: () => {
            if (!disposed) {
              setLoading(false);
            }
          },
          onSlideError: (_slideIndex, slideError) => {
            console.warn('[pptx-slide]', slideError);
          },
          onSlideRendered: () => {
            if (!disposed) {
              setLoading(false);
            }
          },
          onWarning: (warning) => {
            console.warn('[pptx-preview]', warning);
          },
          workerFactory: () => createPptxWorker(),
          zipLimits: RECOMMENDED_ZIP_LIMITS
        });

        if (!disposed) {
          setLoading(false);
        }
      } catch (renderError) {
        if (!disposed) {
          setError(formatPptxError(renderError, failedFallback));
          setLoading(false);
        }
      }
    };

    void render();

    return () => {
      disposed = true;
      viewer?.destroy();
    };
  }, [failedFallback, failedTitle, file]);

  return (
    <div className="relative min-h-[360px] bg-[#eef3f8]">
      {loading ? (
        <div className="absolute left-1/2 top-4 z-10 -translate-x-1/2 rounded-full border border-slate-200 bg-white px-4 py-2 text-sm text-slate-600 shadow">
          {loadingLabel}
        </div>
      ) : null}
      {error ? (
        <div className="mx-auto mt-12 w-[min(680px,calc(100%-32px))] rounded border border-rose-200 bg-rose-50 p-6 text-sm text-rose-700">
          <strong className="mb-2 block text-base">{failedTitle}</strong>
          <span>{error}</span>
        </div>
      ) : null}
      <div ref={surfaceRef} className="min-h-[360px]" />
    </div>
  );
}
