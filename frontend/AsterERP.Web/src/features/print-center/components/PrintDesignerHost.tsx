import {
  createElement,
  forwardRef,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState
} from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import type { PrintDesignerCrudContext, PrintVariableNodeDto } from '../types';
import { createPrintDesignerCrudEndpoints, createPrintDesignerCrudFetcher } from '../utils/printRuntimeDesignerClient';

import { loadPrintDesignerElement } from './PrintDesignerElementLoader';


type PrintDesignerElementWithExtras = HTMLElement & {
  getTemplateData: () => Record<string, unknown> | null;
  loadTemplateData: (data: Record<string, unknown>) => boolean;
  setAvailableVariables?: (variables: PrintVariableNodeDto[]) => void;
  setBranding: (payload?: Record<string, unknown>) => void;
  setCrudEndpoints: (endpoints: Record<string, unknown>, options?: Record<string, unknown>) => void;
  setCrudMode: (mode: 'local' | 'remote') => void;
  setLanguage: (language: string) => void;
  setPrintDefaults: (payload?: Record<string, unknown>) => void;
  setTestData: (data: Record<string, unknown>, options?: { merge?: boolean }) => Promise<void>;
  setVariables: (data: Record<string, unknown>, options?: { merge?: boolean }) => Promise<void>;
};

export interface PrintDesignerHostRef {
  getTemplateData: () => Record<string, unknown> | null;
}

interface PrintDesignerHostProps {
  availableVariables: PrintVariableNodeDto[];
  crudContext?: PrintDesignerCrudContext;
  onTemplateLoadStatus?: (status: { attempts: number; loaded: boolean }) => void;
  templateData?: Record<string, unknown> | null;
  testData?: Record<string, unknown> | null;
  variables?: Record<string, unknown> | null;
}

function stableStringify(value: unknown): string {
  return JSON.stringify(value ?? null);
}

async function waitForDesignerFrame() {
  await new Promise((resolve) => requestAnimationFrame(() => resolve(undefined)));
}

export const PrintDesignerHost = forwardRef<PrintDesignerHostRef, PrintDesignerHostProps>(function PrintDesignerHost(
  { availableVariables, crudContext, onTemplateLoadStatus, templateData, testData, variables },
  ref
) {
  const { locale, translate } = useI18n();
  const [loaded, setLoaded] = useState(false);
  const elementRef = useRef<PrintDesignerElementWithExtras | null>(null);
  const lastTemplateKeyRef = useRef('');
  const lastTestDataKeyRef = useRef('');
  const lastVariablesKeyRef = useRef('');
  const lastAvailableVariablesKeyRef = useRef('');
  const crudKey = useMemo(() => stableStringify(crudContext), [crudContext]);

  useImperativeHandle(ref, () => ({
    getTemplateData: () => elementRef.current?.getTemplateData() ?? null
  }), []);

  useEffect(() => {
    let cancelled = false;
    void loadPrintDesignerElement()
      .then(() => customElements.whenDefined('print-designer'))
      .then(() => {
        if (!cancelled) {
          setLoaded(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!loaded || !elementRef.current) {
      return;
    }

    const element = elementRef.current;
    element.setLanguage(locale === 'zh-CN' ? 'zh' : 'en');
    element.setPrintDefaults({ mode: 'browser' });
    element.setBranding({ showLogo: false, showTitle: true, title: translate('print.runtime.brandingTitle') });
    if (crudContext) {
      element.setCrudEndpoints(
        createPrintDesignerCrudEndpoints(crudContext),
        { fetcher: createPrintDesignerCrudFetcher() }
      );
      element.setCrudMode(templateData ? 'local' : 'remote');
    } else {
      element.setCrudMode('local');
    }
  }, [crudKey, crudContext, loaded, locale, templateData, translate]);

  useEffect(() => {
    if (!loaded || !elementRef.current) {
      return;
    }

    const currentKey = stableStringify(availableVariables);
    if (currentKey !== lastAvailableVariablesKeyRef.current) {
      elementRef.current.setAvailableVariables?.(availableVariables);
      lastAvailableVariablesKeyRef.current = currentKey;
    }
  }, [availableVariables, loaded]);

  useEffect(() => {
    if (!loaded || !elementRef.current || !templateData) {
      return;
    }

    const currentKey = stableStringify(templateData);
    if (currentKey !== lastTemplateKeyRef.current) {
      let disposed = false;
      const loadTemplate = async () => {
        for (let attempt = 0; attempt < 120 && !disposed; attempt += 1) {
          const loadedTemplate = elementRef.current?.loadTemplateData(templateData);
          if (loadedTemplate) {
            lastTemplateKeyRef.current = currentKey;
            onTemplateLoadStatus?.({ attempts: attempt + 1, loaded: true });
            return;
          }

          await waitForDesignerFrame();
        }

        if (!disposed) {
          onTemplateLoadStatus?.({ attempts: 120, loaded: false });
        }
      };

      void loadTemplate();

      return () => {
        disposed = true;
      };
    }
  }, [loaded, onTemplateLoadStatus, templateData]);

  useEffect(() => {
    if (!loaded || !elementRef.current || !testData) {
      return;
    }

    const currentKey = stableStringify(testData);
    if (currentKey !== lastTestDataKeyRef.current) {
      void elementRef.current.setTestData(testData, { merge: false });
      lastTestDataKeyRef.current = currentKey;
    }
  }, [loaded, testData]);

  useEffect(() => {
    if (!loaded || !elementRef.current || !variables) {
      return;
    }

    const currentKey = stableStringify(variables);
    if (currentKey !== lastVariablesKeyRef.current) {
      void elementRef.current.setVariables(variables, { merge: false });
      lastVariablesKeyRef.current = currentKey;
    }
  }, [loaded, variables]);

  if (!loaded) {
    return (
      <div className="flex h-full min-h-[320px] items-center justify-center rounded-lg border border-dashed border-gray-300 bg-gray-50 text-gray-500">
        <div className="flex items-center gap-2 text-sm">
          <AppIcon className="animate-spin text-base" name="spinner-gap" />
          {translate('print.designer.loadingHost')}
        </div>
      </div>
    );
  }

  return createElement('print-designer', { ref: elementRef, className: 'block h-full w-full' });
});
