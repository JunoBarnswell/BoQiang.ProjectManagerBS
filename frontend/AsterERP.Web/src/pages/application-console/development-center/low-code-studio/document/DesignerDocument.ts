import type { LayoutProtocol as CanonicalLayoutProtocol } from '../layout/LayoutProtocol';
import type { ResponsiveOverrideMap } from '../responsive/responsiveModel';

import type { PropertyValue } from './PropertyValue';

export type { LayoutProtocol } from '../layout/LayoutProtocol';
export type DesignerLayoutProtocol = CanonicalLayoutProtocol;

export interface DesignerNodeLayout {
  /** Canonical persisted layout protocol sections. Earlier flat fields remain readable during migration. */
  container?: CanonicalLayoutProtocol['container'];
  constraints?: Record<string, unknown>;
  display?: string;
  height?: string | number | null;
  layoutProtocol?: CanonicalLayoutProtocol;
  placement?: CanonicalLayoutProtocol['placement'];
  protocol?: CanonicalLayoutProtocol;
  size?: CanonicalLayoutProtocol['size'];
  width?: string | number | null;
  [key: string]: unknown;
}

export interface DesignerDocumentNode {
  bindings?: DesignerNodeBindings;
  children: string[];
  events: Record<string, unknown>[];
  id: string;
  layout: DesignerNodeLayout;
  locked?: boolean;
  name?: string;
  parentId: string | null;
  permission?: Record<string, unknown>;
  props: Record<string, PropertyValue>;
  responsiveOverrides?: ResponsiveOverrideMap;
  style?: Record<string, unknown>;
  validation?: Record<string, unknown>[];
  type: string;
}

/** The single binding namespace used by the designer and runtime. */
export interface DesignerNodeBindings extends Record<string, unknown> {
  data?: Record<string, unknown>;
}

export interface DesignerDocument {
  actions: Record<string, unknown>[];
  apiBindings: Record<string, unknown>[];
  dataSources: Record<string, unknown>[];
  documentId: string;
  /** Computed by DesignerDocumentStore; never participates in its own hash. */
  documentHash?: string;
  elements: Record<string, DesignerDocumentNode>;
  metadata: Record<string, unknown>;
  modals: Array<{ id: string; name: string; rootElementId: string; type: string }>;
  pageMicroflows?: Record<string, unknown>[];
  pageParameters: Record<string, unknown>[];
  pages: Array<{ id: string; name: string; rootElementId: string }>;
  /** Formal page presentation metadata persisted in the latest document. */
  pageType?: string;
  permissions: Record<string, unknown>;
  runtimeContext: Record<string, unknown>;
  revision: number;
  styleTokens: Record<string, unknown>;
  variables: Array<Record<string, unknown>>;
  workflowBindings: Record<string, unknown>[];
}
