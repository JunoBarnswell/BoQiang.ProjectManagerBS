import type { ExpressionValue } from '../api/runtime/expression-value.latest';
import type { PrintLaunchRequest } from '../features/print-center/types';

export interface RuntimeDesignerBindings extends Record<string, unknown> {
  data?: unknown;
}

export interface RuntimeDesignerElement {
  children: string[];
  bindings?: RuntimeDesignerBindings;
  events?: RuntimeActionFlow[];
  id: string;
  layout?: Record<string, unknown>;
  name: string;
  parentId?: string | null;
  permission?: { code?: string | null; visibleWhen?: ExpressionValue | string | null };
  props?: Record<string, unknown>;
  style?: Record<string, unknown>;
  type: string;
  validation?: Array<Record<string, unknown>>;
}

export interface RuntimeDesignerDocument {
  apiBindings?: Array<Record<string, unknown>>;
  elements: Record<string, RuntimeDesignerElement>;
  modals: Array<{ id: string; name: string; rootElementId: string; type: string }>;
  pageMicroflows?: Array<Record<string, unknown>>;
  pages: Array<{ id: string; name: string; rootElementId: string }>;
  runtimeContext?: Record<string, unknown>;
  variables?: Array<Record<string, unknown>>;
}

export interface RuntimeActionFlow {
  condition?: ExpressionValue | string | null;
  errorPolicy?: 'continue' | 'stop';
  id: string;
  name: string;
  permissionCode?: ExpressionValue | string | null;
  steps: Array<{ config?: Record<string, unknown>; id: string; type: string }>;
  trigger: string;
}

export interface RuntimeContext {
  closeModal: () => void;
  clearComponentValues?: (elementIds?: string[]) => void;
  componentValues?: Record<string, unknown>;
  document: RuntimeDesignerDocument;
  formValues: Record<string, unknown>;
  navigate: (path: string) => void;
  openPrint: (request: PrintLaunchRequest) => void;
  openModal: (modalId: string, row?: Record<string, unknown> | null, pageInputs?: Record<string, unknown>) => void;
  openPageInvocation: (invocation: Record<string, unknown>, row?: Record<string, unknown> | null, pageInputs?: Record<string, unknown>) => void;
  refreshModel: () => Promise<void>;
  refreshVersion: number;
  mergeVariables: (values: Record<string, unknown>) => void;
  setComponentValue?: (elementId: string, value: unknown) => void;
  setFormValue: (field: string, value: unknown) => void;
  setFormValues: (values: Record<string, unknown>) => void;
  setVariable: (key: string, value: unknown) => void;
  setVariablePath: (path: string, value: unknown) => void;
  signal?: AbortSignal;
  variables: Record<string, unknown>;
  scopes?: Record<string, Record<string, unknown>>;
}

export interface RuntimeResourceBinding {
  provider?: string | null;
  resourceId: string;
  resourceType?: string | null;
  modelCode?: string | null;
  field?: string | null;
  pageCode?: string | null;
  previewPageId?: string | null;
}

export const RUNTIME_SCOPE_NAMES = ['page', 'component', 'form', 'row', 'action', 'modal', 'variables', 'system'] as const;
export type RuntimeScopeName = typeof RUNTIME_SCOPE_NAMES[number];
