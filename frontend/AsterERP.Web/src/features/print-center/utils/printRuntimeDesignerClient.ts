import { appEnv } from '../../../core/config/env';
import { getAccessToken } from '../../../core/http/tokenStorage';
import { getStoredWorkspace } from '../../../core/http/workspaceStorage';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { getCurrentLocale, translateCurrentLocale } from '../../../core/i18n/I18nProvider';
import { saveBlob } from '../../../shared/file-preview/filePreviewUtils';
import { loadPrintDesignerElement } from '../components/PrintDesignerElementLoader';
import type { PrintDesignerCrudContext, PrintRuntimeResolveResponse, PrintVariableNodeDto } from '../types';

import { expandPrintRuntimeVariables } from './printRuntimeVariables';

type PrintDesignerElementWithExtras = HTMLElement & {
  export: (request: Record<string, unknown>) => Promise<Blob | void>;
  getPreviewHtml: () => Promise<string>;
  loadTemplateData: (data: Record<string, unknown>) => boolean;
  print: (request?: Record<string, unknown>) => Promise<void>;
  setAvailableVariables?: (variables: PrintVariableNodeDto[]) => void;
  setBranding: (payload?: Record<string, unknown>) => void;
  setCrudEndpoints: (endpoints: Record<string, unknown>, options?: Record<string, unknown>) => void;
  setCrudMode: (mode: 'local' | 'remote') => void;
  setLanguage: (language: string) => void;
  setPrintDefaults: (payload?: Record<string, unknown>) => void;
  setTestData: (data: Record<string, unknown>, options?: { merge?: boolean }) => Promise<void>;
  setVariables: (data: Record<string, unknown>, options?: { merge?: boolean }) => Promise<void>;
};

function getDesignerLanguage(): 'zh' | 'en' {
  return getCurrentLocale() === 'zh-CN' ? 'zh' : 'en';
}

function buildApiUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  const baseUrl = appEnv.apiBaseUrl.replace(/\/+$/, '');
  const requestPath = path.startsWith('/') ? path : `/${path}`;
  return `${baseUrl}${requestPath}`;
}

export function createPrintDesignerCrudFetcher() {
  return async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const headers = new Headers(init?.headers);
    if (!headers.has('Content-Type') && init?.body) {
      headers.set('Content-Type', 'application/json');
    }

    const token = getAccessToken();
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }

    const workspace = getStoredWorkspace();
    if (workspace) {
      headers.set('X-Tenant-Id', workspace.tenantId);
      headers.set('X-App-Code', workspace.appCode);
    }

    const target = typeof input === 'string' ? input : input.toString();
    const response = await fetch(buildApiUrl(target), { ...init, headers });
    const contentType = response.headers.get('Content-Type') ?? '';
    if (!contentType.includes('application/json')) {
      if (!response.ok) {
        throw new Error(formatMessage(translateCurrentLocale('print.runtime.fetchFailed'), { status: response.status }));
      }

      return response;
    }

    const payload = await response.clone().json().catch(() => null);
    if (!response.ok) {
      throw new Error(payload?.message ?? formatMessage(translateCurrentLocale('print.runtime.fetchFailed'), { status: response.status }));
    }

    if (payload && typeof payload === 'object' && 'code' in payload && 'data' in payload) {
      return new Response(JSON.stringify(payload.data), {
        status: response.status,
        statusText: response.statusText,
        headers: {
          'Content-Type': 'application/json'
        }
      });
    }

    return response;
  };
}

export function createPrintDesignerCrudEndpoints(context: PrintDesignerCrudContext) {
  const query = `menuCode=${encodeURIComponent(context.menuCode)}&scene=${encodeURIComponent(context.scene)}`;
  return {
    customElements: {
      delete: '/system/print-center/designer/custom-elements/{id}',
      get: '/system/print-center/designer/custom-elements/{id}',
      list: '/system/print-center/designer/custom-elements',
      upsert: '/system/print-center/designer/custom-elements'
    },
    templates: {
      delete: '/system/print-center/designer/templates/{id}',
      get: '/system/print-center/designer/templates/{id}',
      list: `/system/print-center/designer/templates?${query}`,
      upsert: `/system/print-center/designer/templates?${query}`
    }
  };
}

async function createEphemeralDesigner(payload: Pick<PrintRuntimeResolveResponse, 'availableVariables' | 'data' | 'testData' | 'variables'>) {
  await loadPrintDesignerElement();
  await customElements.whenDefined('print-designer');

  const element = document.createElement('print-designer') as PrintDesignerElementWithExtras;
  element.style.position = 'fixed';
  element.style.left = '-10000px';
  element.style.top = '-10000px';
  element.style.width = '1200px';
  element.style.height = '900px';
  document.body.appendChild(element);
  await new Promise((resolve) => requestAnimationFrame(() => resolve(undefined)));

  element.setLanguage(getDesignerLanguage());
  element.setPrintDefaults({ mode: 'browser' });
  element.setBranding({ showLogo: false, showTitle: true, title: translateCurrentLocale('print.runtime.brandingTitle') });
  element.setCrudMode('local');
  if (payload.availableVariables.length > 0) {
    element.setAvailableVariables?.(payload.availableVariables);
  }

  if (payload.data) {
    element.loadTemplateData(payload.data);
  }

  if (payload.testData) {
    await element.setTestData(expandPrintRuntimeVariables(payload.testData), { merge: false });
  }

  if (payload.variables) {
    await element.setVariables(expandPrintRuntimeVariables(payload.variables), { merge: false });
  }

  return element;
}

async function withEphemeralDesigner<T>(
  payload: Pick<PrintRuntimeResolveResponse, 'availableVariables' | 'data' | 'testData' | 'variables'>,
  action: (element: PrintDesignerElementWithExtras) => Promise<T>
): Promise<T> {
  const element = await createEphemeralDesigner(payload);
  try {
    return await action(element);
  } finally {
    element.remove();
  }
}

export async function previewPrintRuntime(payload: PrintRuntimeResolveResponse): Promise<void> {
  const html = await withEphemeralDesigner(payload, async (element) => element.getPreviewHtml());
  const popup = window.open('', '_blank', 'noopener,noreferrer');
  if (!popup) {
    throw new Error(translateCurrentLocale('print.runtime.previewBlocked'));
  }

  popup.document.open();
  popup.document.write(html);
  popup.document.close();
}

export async function printPrintRuntime(payload: PrintRuntimeResolveResponse): Promise<void> {
  await withEphemeralDesigner(payload, async (element) => {
    await element.print({ mode: 'browser' });
  });
}

export async function exportPrintRuntimePdf(payload: PrintRuntimeResolveResponse): Promise<void> {
  const result = await withEphemeralDesigner(payload, async (element) => element.export({ type: 'pdf' }));
  if (result instanceof Blob) {
    saveBlob(result, `${payload.suggestedFileName || payload.templateCode || 'print'}.pdf`);
  }
}
