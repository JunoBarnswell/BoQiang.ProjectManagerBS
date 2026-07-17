import type { ReactNode } from 'react';

import type { RuntimeExpressionScope } from '../shared/runtime/runtimeExpression';

import type { RuntimeActionFlow, RuntimeContext, RuntimeDesignerElement } from './RuntimeTypes';

export interface RuntimeComponentRenderContext {
  action?: RuntimeActionFlow;
  bindings?: Record<string, unknown>;
  changeAction?: RuntimeActionFlow;
  children: ReactNode[];
  componentType: string;
  disabled: boolean;
  element: RuntimeDesignerElement;
  executeAction: (action: RuntimeActionFlow, runtime: RuntimeContext) => Promise<RuntimeContext>;
  onChange: (value: unknown, changeAction?: RuntimeActionFlow) => void;
  layout: Record<string, unknown>;
  loading: boolean;
  permission?: { code?: string | null; visibleWhen?: unknown };
  props: Record<string, unknown>;
  readOnly: boolean;
  runtime: RuntimeContext;
  scope: RuntimeExpressionScope;
  style: Record<string, unknown>;
  title: string;
  value: unknown;
  visible: boolean;
}

export type RuntimeComponentRenderer = (context: RuntimeComponentRenderContext) => ReactNode;
